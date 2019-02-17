using LiteNetLib;
using LiteNetLib.Utils;
using Open.Nat;
using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace Mirror.LiteNetLib
{
	[Serializable] public class UnityEventError : UnityEvent<SocketError> { }
	[Serializable] public class UnityEventIntError : UnityEvent<int, SocketError> { }
	[Serializable] public class UnityEventIpEndpointString : UnityEvent<IPEndPoint, string> { }
	[RequireComponent(typeof(LiteNetLib4MirrorNetworkManager))]
	public class LiteNetLib4MirrorTransport : Transport
	{
		public static LiteNetLib4MirrorTransport Singleton;
		public const string TransportVersion = "1.0.1";

#if UNITY_EDITOR
		[Header("Connection settings")]
#endif
		public string clientAddress = "127.0.0.1";
#if UNITY_EDITOR
		[Rename("Server IPv4 Bind Address")]
#endif
		public string serverIPv4BindAddress = "127.0.0.1";
#if !DISABLE_IPV6
#if UNITY_EDITOR
		[Rename("Server IPv6 Bind Address")]
#endif
		public string serverIPv6BindAddress = "::1";
#endif
		public ushort port = 7777;
#if UNITY_EDITOR
		[Rename("Use UPnP")]
#endif
		public bool useUpnP = true;
		public ushort maxConnections = 20;

#if UNITY_EDITOR
		[Header("Connection additional auth code (optional)")]
#endif
		public string authCode;

#if UNITY_EDITOR
		[Header("Channel types")]
		[ArrayRename("Channel")]
#endif
		public DeliveryMethod[] packetSendMethods =
		{
			DeliveryMethod.ReliableOrdered,
			DeliveryMethod.Sequenced
		};

		/// Enable NAT punch messages
#if UNITY_EDITOR
		[Header("LiteNetLib settings")]
#endif
		public bool natPunchEnabled;
		/// Library logic update (and send) period in milliseconds
		public int updateTime = 15;
		/// Interval for latency detection and checking connection
		public int pingInterval = 1000;
		/// If client or server doesn't receive any packet from remote peer during this time then connection will be closed (including library internal keepalive packets)
		public int disconnectTimeout = 5000;
		/// Simulate packet loss by dropping random amout of packets. (Works only in DEBUG mode)
		public bool simulatePacketLoss;
		/// Simulate latency by holding packets for random time. (Works only in DEBUG mode)
		public bool simulateLatency;
		/// Chance of packet loss when simulation enabled. Value in percents.
		[Range(0, 100)]
		public int simulationPacketLossChance = 10;
		/// Minimum simulated latency
		public int simulationMinLatency = 30;
		/// Maximum simulated latency
		public int simulationMaxLatency = 100;
		/// Allows receive DiscoveryRequests
		public bool discoveryEnabled;
		/// Merge small packets into one before sending to reduce outgoing packets count. (May increase a bit outgoing data size)
		public bool mergeEnabled;
		/// Delay betwen connection attempts
		public int reconnectDelay = 500;
		/// Maximum connection attempts before client stops and call disconnect event.
		public int maxConnectAttempts = 10;

		public enum States
		{
			NonInitialized,
			Idle,
			ClientConnecting,
			ClientConnected,
			ServerStarting,
			ServerActive
		}

		private static string _code;
		private static bool _update;
		private static bool _awaked;
		private static bool _forwarded;

		private static NetManager _host;

		public UnityEventError onClientSocketError;
		public UnityEventIntError onServerSocketError;
		public UnityEventIpEndpointString onClientDiscoveryResponse;

		public static States State { get; private set; } = States.NonInitialized;

		public static SocketError LastError { get; private set; }
		public static SocketError LastDisconnectError { get; private set; }
		public static DisconnectReason LastDisconnectReason { get; private set; }

#region Unity Functions
		private void Awake()
		{
			if (!_awaked)
			{
				Singleton = this;
				State = States.Idle;
				_awaked = true;
			}
		}

		private void LateUpdate()
		{
			if (_update) _host.PollEvents();
		}

		private void OnDestroy()
		{
			ShutdownInternal();
		}
#endregion

#region Transport Overrides
		public override bool ClientConnected()
		{
			return ClientConnectedInternal();
		}

		public override void ClientConnect(string address)
		{
			clientAddress = address;
			ClientConnectInternal(GenerateCode());
		}

		public override bool ClientSend(int channelId, byte[] data)
		{
			return ClientSendInternal(packetSendMethods[channelId], data);
		}

		public override void ClientDisconnect()
		{
			StopInternal();
		}

		public override bool ServerActive()
		{
			return ServerActiveInternal();
		}

		public override void ServerStart()
		{
			ServerStartInternal(GenerateCode());
		}

		public override bool ServerSend(int connectionId, int channelId, byte[] data)
		{
			return ServerSendInternal(connectionId, packetSendMethods[channelId], data);
		}

		public override bool ServerDisconnect(int connectionId)
		{
			return ServerDisconnectInternal(connectionId);
		}

		public override void ServerStop()
		{
			StopInternal();
		}

		public override bool GetConnectionInfo(int connectionId, out string address)
		{
			return GetConnectionInfoInteral(connectionId, out address);
		}

		public override void Shutdown()
		{
			ShutdownInternal();
		}

		public override int GetMaxPacketSize(int channelId = Channels.DefaultReliable)
		{
			return GetMaxPacketSizeInternal(packetSendMethods[channelId]);
		}
		#endregion

#region Dissonance ArraySegment
		public bool ClientSend(int channelId, ArraySegment<byte> data)
		{
			return ClientSendInternal(packetSendMethods[channelId], data);
		}

		public bool ServerSend(int connectionId, int channelId, ArraySegment<byte> data)
		{
			return ServerSendInternal(connectionId, packetSendMethods[channelId], data);
		}
#endregion

#region Utils
		private static void LogException(Exception exception)
		{
			Debug.LogException(exception);
		}

		private static string Concatenate(params string[] array)
		{
			StringBuilder sb = new StringBuilder();
			for (int index = 0; index < array.Length; index++) sb.Append(array[index]);

			return sb.ToString();
		}

		private static IPAddress Parse(string address)
		{
			IPAddress ipAddress;
			if (!IPAddress.TryParse(address, out ipAddress)) ipAddress = address == "localhost" ? IPAddress.Parse("127.0.0.1") : Dns.GetHostAddresses(address)[0];

			return ipAddress;
		}

		/// <summary>
		/// Utility function for getting first free port in range (as a bonus, should work if unity doesn't shit itself)
		/// </summary>
		/// <param name="ports">Available ports</param>
		/// <returns>First free port in range</returns>
		public static int GetFirstFreePort(params ushort[] ports)
		{
			if (ports == null || ports.Length == 0) throw new Exception("No ports provided");
			ushort freeport = ports.Except(Array.ConvertAll(IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners(), p => (ushort)p.Port)).FirstOrDefault();
			if (freeport == 0) throw new Exception("No free port!");
			return freeport;
		}

#pragma warning disable 4014
		public static void ForwardPort()
		{
			ForwardPortInternalAsync(Singleton.port);
		}

		public static void ForwardPort(ushort port)
		{
			ForwardPortInternalAsync(port);
		}
#pragma warning restore 4014

		private static async Task ForwardPortInternalAsync(ushort port)
		{
			try
			{
				NatDiscoverer discoverer = new NatDiscoverer();
				NatDevice device;
				using (CancellationTokenSource cts = new CancellationTokenSource(10000))
				{
					device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts).ConfigureAwait(false);
				}

				await device.CreatePortMapAsync(new Mapping(Protocol.Udp, port, port, "LiteNetLib4Mirror UPnP")).ConfigureAwait(false);
				_forwarded = true;
				Debug.Log("Port forwarded successfully!");
			}
			catch
			{
				Debug.LogWarning("UPnP failed!");
			}
		}
		#endregion

		public virtual string GenerateCode()
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(Concatenate(Application.productName, Application.companyName, Application.unityVersion, TransportVersion, Singleton.authCode)));
		}

		public static void SendDiscoveryRequest(string text)
		{
			if (Singleton.discoveryEnabled && _host != null)
			{
				NetDataWriter nw = new NetDataWriter();
				nw.Put(text);
				_host.SendDiscoveryRequest(nw, Singleton.port);
			}
		}

		public override string ToString()
		{
			return ToStringInternal();
		}

		private static string ToStringInternal()
		{
			switch (State)
			{
				case States.NonInitialized:
					return "LiteNetLib4Mirror isn't initialized";
				case States.Idle:
					return "LiteNetLib4Mirror Transport idle";
				case States.ClientConnecting:
					return $"LiteNetLib4Mirror Client Connecting to {Singleton.clientAddress}:{Singleton.port}";
				case States.ClientConnected:
					return $"LiteNetLib4Mirror Client Connected to {Singleton.clientAddress}:{Singleton.port}";
				case States.ServerStarting:
#if DISABLE_IPV6
					return $"LiteNetLib4Mirror Server starting at IPv4:{Singleton.serverIPv4BindAddress} Port:{Singleton.port}";
#else
					return $"LiteNetLib4Mirror Server starting at IPv4:{Singleton.serverIPv4BindAddress} IPv6:{Singleton.serverIPv6BindAddress} Port:{Singleton.port}";
#endif
				case States.ServerActive:
#if DISABLE_IPV6
					return $"LiteNetLib4Mirror Server active at IPv4:{Singleton.serverIPv4BindAddress} Port:{Singleton.port}";
#else
					return $"LiteNetLib4Mirror Server active at IPv4:{Singleton.serverIPv4BindAddress} IPv6:{Singleton.serverIPv6BindAddress} Port:{Singleton.port}";
#endif
				default:
					return "Invalid state!";
			}
		}

		private static bool ClientConnectedInternal()
		{
			return State == States.ClientConnected;
		}

		private static void ClientConnectInternal(string code)
		{
			State = States.ClientConnecting;
			try
			{
				EventBasedNetListener listener = new EventBasedNetListener();
				_host = new NetManager(listener);
				listener.NetworkReceiveEvent += ClientOnNetworkReceiveEvent;
				listener.NetworkErrorEvent += ClientOnNetworkErrorEvent;
				listener.PeerConnectedEvent += ClientOnPeerConnectedEvent;
				listener.PeerDisconnectedEvent += ClientOnPeerDisconnectedEvent;
				if (Singleton.discoveryEnabled)
				{
					listener.NetworkReceiveUnconnectedEvent += ClientOnNetworkReceiveUnconnectedEvent;
				}
				
				_host.NatPunchEnabled = Singleton.natPunchEnabled;
				_host.UpdateTime = Singleton.updateTime;
				_host.PingInterval = Singleton.pingInterval;
				_host.DisconnectTimeout = Singleton.disconnectTimeout;
				_host.SimulatePacketLoss = Singleton.simulatePacketLoss;
				_host.SimulateLatency = Singleton.simulateLatency;
				_host.SimulationPacketLossChance = Singleton.simulationPacketLossChance;
				_host.SimulationMinLatency = Singleton.simulationMinLatency;
				_host.SimulationMaxLatency = Singleton.simulationMaxLatency;
				_host.DiscoveryEnabled = Singleton.discoveryEnabled;
				_host.MergeEnabled = Singleton.mergeEnabled;
				_host.ReconnectDelay = Singleton.reconnectDelay;
				_host.MaxConnectAttempts = Singleton.maxConnectAttempts;

				_host.Start();
				_host.Connect(new IPEndPoint(Parse(Singleton.clientAddress), Singleton.port), code);

				_update = true;
				State = States.ClientConnected;
			}
			catch (Exception ex)
			{
				State = States.Idle;
				LogException(ex);
			}
		}

		private static void ClientOnNetworkReceiveUnconnectedEvent(IPEndPoint remoteendpoint, NetPacketReader reader, UnconnectedMessageType messagetype)
		{
			if (messagetype == UnconnectedMessageType.DiscoveryResponse)
			{
				Singleton.onClientDiscoveryResponse.Invoke(remoteendpoint, reader.GetString());
			}
			reader.Recycle();
		}

		private static void ClientOnPeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectinfo)
		{
			LastDisconnectError = disconnectinfo.SocketErrorCode;
			LastDisconnectReason = disconnectinfo.Reason;
			Singleton.OnClientDisconnected.Invoke();
		}

		private static void ClientOnPeerConnectedEvent(NetPeer peer)
		{
			Singleton.OnClientConnected.Invoke();
		}

		private static void ClientOnNetworkReceiveEvent(NetPeer peer, NetPacketReader reader, DeliveryMethod deliverymethod)
		{
			Singleton.OnClientDataReceived.Invoke(reader.GetRemainingBytes());
			reader.Recycle();
		}

		private static void ClientOnNetworkErrorEvent(IPEndPoint endpoint, SocketError socketerror)
		{
			LastError = socketerror;
			Singleton.OnClientError.Invoke(new SocketException((int) socketerror));
			Singleton.onClientSocketError.Invoke(socketerror);
		}

		private static bool ClientSendInternal(DeliveryMethod method, byte[] data)
		{
			try
			{
				_host.FirstPeer.Send(data, method);
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static bool ClientSendInternal(DeliveryMethod method, ArraySegment<byte> data)
		{
			try
			{
				_host.FirstPeer.Send(data.Array, data.Offset, data.Count, method);
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static bool ServerActiveInternal()
		{
			return State == States.ServerActive;
		}

		private static void ServerStartInternal(string code)
		{
			State = States.ServerStarting;
			try
			{
				_code = code;
				EventBasedNetListener listener = new EventBasedNetListener();
				_host = new NetManager(listener);
				listener.ConnectionRequestEvent += ServerOnConnectionRequestEvent;
				listener.PeerDisconnectedEvent += ServerOnPeerDisconnectedEvent;
				listener.NetworkErrorEvent += ServerOnNetworkErrorEvent;
				listener.NetworkReceiveEvent += ServerOnNetworkReceiveEvent;
				listener.PeerConnectedEvent += ServerOnPeerConnectedEvent;
				if (Singleton.discoveryEnabled)
				{
					listener.NetworkReceiveUnconnectedEvent += ServerOnNetworkReceiveUnconnectedEvent;
				}

				_host.NatPunchEnabled = Singleton.natPunchEnabled;
				_host.UpdateTime = Singleton.updateTime;
				_host.PingInterval = Singleton.pingInterval;
				_host.DisconnectTimeout = Singleton.disconnectTimeout;
				_host.SimulatePacketLoss = Singleton.simulatePacketLoss;
				_host.SimulateLatency = Singleton.simulateLatency;
				_host.SimulationPacketLossChance = Singleton.simulationPacketLossChance;
				_host.SimulationMinLatency = Singleton.simulationMinLatency;
				_host.SimulationMaxLatency = Singleton.simulationMaxLatency;
				_host.DiscoveryEnabled = Singleton.discoveryEnabled;
				_host.MergeEnabled = Singleton.mergeEnabled;
				_host.ReconnectDelay = Singleton.reconnectDelay;
				_host.MaxConnectAttempts = Singleton.maxConnectAttempts;
				if (Singleton.useUpnP && !_forwarded)
				{
					ForwardPort();
				}
#if DISABLE_IPV6
				_host.Start(Parse(Singleton.serverIPv4BindAddress), Parse("::1"), Singleton.port);
#else
				_host.Start(Parse(Singleton.serverIPv4BindAddress), Parse(Singleton.serverIPv6BindAddress), Singleton.port);
#endif
				_update = true;
				State = States.ServerActive;
			}
			catch (Exception ex)
			{
				State = States.Idle;
				LogException(ex);
			}
		}

		public virtual bool ProcessDiscoveryRequest(IPEndPoint ipEndPoint, string text, out string response)
		{
			response = "LiteNetLib4Mirror Discovery accepted";
			return true;
		}

		private static void ServerOnNetworkReceiveUnconnectedEvent(IPEndPoint remoteendpoint, NetPacketReader reader, UnconnectedMessageType messagetype)
		{
			string response;
			if (messagetype == UnconnectedMessageType.DiscoveryRequest && Singleton.ProcessDiscoveryRequest(remoteendpoint, reader.GetString(), out response))
			{
				NetDataWriter nw = new NetDataWriter();
				nw.Put(response);
				_host.SendDiscoveryResponse(nw, remoteendpoint);
			}
			reader.Recycle();
		}

		private static void ServerOnPeerConnectedEvent(NetPeer peer)
		{
			Singleton.OnServerConnected.Invoke(peer.Id + 1);
		}

		private static void ServerOnNetworkReceiveEvent(NetPeer peer, NetPacketReader reader, DeliveryMethod deliverymethod)
		{
			Singleton.OnServerDataReceived.Invoke(peer.Id + 1, reader.GetRemainingBytes());
			reader.Recycle();
		}

		private static void ServerOnNetworkErrorEvent(IPEndPoint endpoint, SocketError socketerror)
		{
			LastError = socketerror;
			for (NetPeer peer = _host.ConnectedPeerList[0]; peer != null; peer = peer.NextPeer)
				if (peer.EndPoint.ToString() == endpoint.ToString())
				{
					Singleton.OnServerError.Invoke(peer.Id + 1, new SocketException((int) socketerror));
					Singleton.onServerSocketError.Invoke(peer.Id + 1, socketerror);
					return;
				}
		}

		private static void ServerOnPeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectinfo)
		{
			LastDisconnectError = disconnectinfo.SocketErrorCode;
			LastDisconnectReason = disconnectinfo.Reason;
			Singleton.OnServerDisconnected.Invoke(peer.Id + 1);
		}

		private static void ServerOnConnectionRequestEvent(ConnectionRequest request)
		{
			if (_host.PeersCount >= Singleton.maxConnections)
			{
				request.Reject();
			}
			else if (request.AcceptIfKey(_code) == null)
			{
				Debug.LogWarning("Client tried to join with an invalid auth code! Current code:" + _code);
			}
		}

		private static bool ServerSendInternal(int connectionId, DeliveryMethod method, byte[] data)
		{
			try
			{
				_host.ConnectedPeerList[connectionId - 1].Send(data, method);
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static bool ServerSendInternal(int connectionId, DeliveryMethod method, ArraySegment<byte> data)
		{
			try
			{
				_host.ConnectedPeerList[connectionId - 1].Send(data.Array, data.Offset, data.Count, method);
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static bool ServerDisconnectInternal(int connectionId)
		{
			try
			{
				_host.ConnectedPeerList[connectionId - 1].Disconnect();
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static void StopInternal()
		{
			_host.Stop();
			State = States.Idle;
		}

		private static bool GetConnectionInfoInteral(int connectionId, out string address)
		{
			if (_host.ConnectedPeerList.Count < connectionId)
			{
				address = _host.ConnectedPeerList[connectionId - 1].EndPoint.Address.ToString();
				return true;
			}

			address = "(invalid)";
			return false;
		}

		private static void ShutdownInternal()
		{
			if (ClientConnectedInternal() || ServerActiveInternal()) StopInternal();
			if (_forwarded)
			{
				NatDiscoverer.ReleaseAll();
			}
			Singleton = null;
			State = States.NonInitialized;
		}

		private static int GetMaxPacketSizeInternal(DeliveryMethod channel)
		{
			if (_host?.FirstPeer != null)
			{
				switch (channel)
				{
					case DeliveryMethod.ReliableOrdered:
					case DeliveryMethod.ReliableUnordered:
						return ushort.MaxValue * (_host.FirstPeer.Mtu - NetConstants.FragmentHeaderSize);
					case DeliveryMethod.ReliableSequenced:
					case DeliveryMethod.Sequenced:
						return _host.FirstPeer.Mtu - NetConstants.SequencedHeaderSize;
					default:
						return _host.FirstPeer.Mtu - NetConstants.HeaderSize;
				}
			}
			switch (channel)
			{
				case DeliveryMethod.ReliableOrdered:
				case DeliveryMethod.ReliableUnordered:
					return ushort.MaxValue * (NetConstants.MaxPacketSize - NetConstants.FragmentHeaderSize);
				case DeliveryMethod.ReliableSequenced:
				case DeliveryMethod.Sequenced:
					return NetConstants.MaxPacketSize - NetConstants.SequencedHeaderSize;
				default:
					return NetConstants.MaxPacketSize - NetConstants.HeaderSize;
			}
		}
	}
}