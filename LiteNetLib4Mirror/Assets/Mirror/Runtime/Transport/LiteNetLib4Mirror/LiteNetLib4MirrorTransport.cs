using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LiteNetLib;
using LiteNetLib.Utils;
using LiteNetLib4Mirror.Open.Nat;
using UnityEngine;
using UnityEngine.Events;

namespace Mirror.LiteNetLib4Mirror
{
	[Serializable] public class UnityEventError : UnityEvent<SocketError> { }
	[Serializable] public class UnityEventIntError : UnityEvent<int, SocketError> { }
	[Serializable] public class UnityEventIpEndpointString : UnityEvent<IPEndPoint, string> { }
	[RequireComponent(typeof(NetworkManager))]
	public class LiteNetLib4MirrorTransport : Transport, ISegmentTransport
	{
		public static LiteNetLib4MirrorTransport Singleton;
		public const string TransportVersion = "1.1.0";

#if UNITY_EDITOR
		[Header("Connection settings")]
#endif
		public string clientAddress = "127.0.0.1";
#if UNITY_EDITOR
		[Rename("Server IPv4 Bind Address")]
#endif
		public string serverIPv4BindAddress = "0.0.0.0";
#if !DISABLE_IPV6
#if UNITY_EDITOR
		[Rename("Server IPv6 Bind Address")]
#endif
		public string serverIPv6BindAddress = "::";
#endif
		public ushort port = 7777;
#if UNITY_EDITOR
		[Rename("Use UPnP")]
#endif
		public bool useUpnP = true;
		public ushort maxConnections = 20;

#if UNITY_EDITOR
		[ArrayRename("Channel")]
#endif
		public DeliveryMethod[] channels =
		{
			DeliveryMethod.ReliableOrdered,
			DeliveryMethod.Unreliable,
			DeliveryMethod.Sequenced,
			DeliveryMethod.ReliableSequenced,
			DeliveryMethod.ReliableUnordered
		};

#if UNITY_EDITOR
		[Header("Connection additional auth code (optional)")]
#endif
		public string authCode;

		/// <summary>Library logic update (and send) period in milliseconds</summary>
#if UNITY_EDITOR
		[Header("LiteNetLib settings")]
		[Tooltip("Library logic update (and send) period in milliseconds")]
#endif
		public int updateTime = 15;
		/// <summary>Interval for latency detection and checking connection</summary>
#if UNITY_EDITOR
		[Tooltip("Interval for latency detection and checking connection")]
#endif
		public int pingInterval = 1000;
		/// <summary>If client or server doesn't receive any packet from remote peer during this time then connection will be closed (including library internal keepalive packets)</summary>
#if UNITY_EDITOR
		[Tooltip("If client or server doesn't receive any packet from remote peer during this time then connection will be closed (including library internal keepalive packets)")]
#endif
		public int disconnectTimeout = 5000;
		/// <summary>Simulate packet loss by dropping random amout of packets. (Works only in DEBUG mode)</summary>
#if UNITY_EDITOR
		[Tooltip("Simulate packet loss by dropping random amout of packets. (Works only in DEBUG mode)")]
#endif
		public bool simulatePacketLoss;
		/// <summary>Simulate latency by holding packets for random time. (Works only in DEBUG mode)</summary>
#if UNITY_EDITOR
		[Tooltip("Simulate latency by holding packets for random time. (Works only in DEBUG mode)")]
#endif
		public bool simulateLatency;
		/// <summary>Chance of packet loss when simulation enabled. Value in percents.</summary>
#if UNITY_EDITOR
		[Tooltip("Chance of packet loss when simulation enabled. Value in percents.")]
		[Range(0, 100)]
#endif
		public int simulationPacketLossChance = 10;
		/// <summary>Minimum simulated latency</summary>
#if UNITY_EDITOR
		[Tooltip("Minimum simulated latency")]
#endif
		public int simulationMinLatency = 30;
		/// <summary>Maximum simulated latency</summary>
#if UNITY_EDITOR
		[Tooltip("Maximum simulated latency")]
#endif
		public int simulationMaxLatency = 100;
		/// <summary>Allows receive DiscoveryRequests</summary>
#if UNITY_EDITOR
		[Tooltip("Allows receive DiscoveryRequests")]
#endif
		public bool discoveryEnabled;
		/// <summary>Delay betwen connection attempts</summary>
#if UNITY_EDITOR
		[Tooltip("Delay betwen connection attempts")]
#endif
		public int reconnectDelay = 500;
		/// <summary>Maximum connection attempts before client stops and call disconnect event.</summary>
#if UNITY_EDITOR
		[Tooltip("Maximum connection attempts before client stops and call disconnect event.")]
#endif
		public int maxConnectAttempts = 10;

#if UNITY_EDITOR
		[Header("Custom events")]
#endif
		public UnityEventError onClientSocketError;
		public UnityEventIntError onServerSocketError;
		public UnityEventIpEndpointString onClientDiscoveryResponse;

		public static States State { get; private set; } = States.NonInitialized;
		public static SocketError LastError { get; private set; }
		public static SocketError LastDisconnectError { get; private set; }
		public static DisconnectReason LastDisconnectReason { get; private set; }

		public static NetManager Host { get; private set; }
		public static bool UpnpFailed { get; private set; }

		private static readonly Dictionary<int, NetPeer> Peers = new Dictionary<int, NetPeer>();
		private static readonly NetDataWriter DataWriter = new NetDataWriter();
		private static string _code;
		private static bool _update;
		private static ushort _lastForwardedPort;
		private static string _lastDiscoveryMessage;

		public enum States
		{
			NonInitialized,
			Idle,
			Client,
			Server
		}

#region Unity Functions
		private void Awake()
		{
			if (Singleton == null)
			{
				Singleton = this;
				State = States.Idle;
			}
		}

		private void LateUpdate()
		{
			if (_update)
			{
				Host.PollEvents();
			}
		}

		private void OnDestroy()
		{
			StopInternal();
			if (_lastForwardedPort != 0)
			{
				NatDiscoverer.ReleaseAll();
				_lastForwardedPort = 0;
			}
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
			return ClientSendInternal(channels[channelId], data, 0, data.Length);
		}

		public override void ClientDisconnect()
		{
			if (!ServerActiveInternal())
			{
				StopInternal();
			}
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
			return ServerSendInternal(connectionId, channels[channelId], data, 0, data.Length);
		}

		public override bool ServerDisconnect(int connectionId)
		{
			return connectionId == 0 || ServerDisconnectInternal(connectionId);
		}

		public override void ServerStop()
		{
			StopInternal();
		}

		public override string ServerGetClientAddress(int connectionId)
		{
			return ServerGetClientAddressInteral(connectionId);
		}

		public override void Shutdown()
		{
			StopInternal();
		}

		public override int GetMaxPacketSize(int channelId = Channels.DefaultReliable)
		{
			return GetMaxPacketSizeInternal(channels[channelId]);
		}
#endregion

#region ISegmentTransport
		public bool ClientSend(int channelId, ArraySegment<byte> data)
		{
			return ClientSendInternal(channels[channelId], data.Array, data.Offset, data.Count);
		}

		public bool ServerSend(int connectionId, int channelId, ArraySegment<byte> data)
		{
			return ServerSendInternal(connectionId, channels[channelId], data.Array, data.Offset, data.Count);
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
			if (!IPAddress.TryParse(address, out IPAddress ipAddress)) ipAddress = address == "localhost" ? IPAddress.Parse("127.0.0.1") : Dns.GetHostAddresses(address)[0];

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
		public static void ForwardPort(NetworkProtocolType networkProtocolType = NetworkProtocolType.Udp)
		{
			ForwardPortInternalAsync(Singleton.port, networkProtocolType);
		}

		public static void ForwardPort(ushort port, NetworkProtocolType networkProtocolType = NetworkProtocolType.Udp)
		{
			ForwardPortInternalAsync(port, networkProtocolType);
		}
#pragma warning restore 4014

		private static async Task ForwardPortInternalAsync(ushort port, NetworkProtocolType networkProtocolType = NetworkProtocolType.Udp)
		{
			try
			{
				if (_lastForwardedPort == port || UpnpFailed) return;
				if (_lastForwardedPort != 0)
				{
					NatDiscoverer.ReleaseAll();
				}
				NatDiscoverer discoverer = new NatDiscoverer();
				NatDevice device;
				using (CancellationTokenSource cts = new CancellationTokenSource(10000))
				{
					device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts).ConfigureAwait(false);
				}

				await device.CreatePortMapAsync(new Mapping(networkProtocolType, port, port, "LiteNetLib4Mirror UPnP")).ConfigureAwait(false);
				_lastForwardedPort = port;
				Debug.Log("Port forwarded successfully!");
			}
			catch
			{
				Debug.LogWarning("UPnP failed!");
				UpnpFailed = true;
			}
		}
		#endregion

		public virtual string GenerateCode()
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(Concatenate(Application.productName, Application.companyName, Application.unityVersion, TransportVersion, Singleton.authCode)));
		}

		public static void SendDiscoveryRequest(string text)
		{
			if (Singleton.discoveryEnabled)
			{
				if (_lastDiscoveryMessage != text)
				{
					_lastDiscoveryMessage = text;
					DataWriter.Reset();
					DataWriter.Put(text);
				}
				Host?.SendDiscoveryRequest(DataWriter, Singleton.port);
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
				case States.Client:
					return $"LiteNetLib4Mirror Client Connected to {Singleton.clientAddress}:{Singleton.port}";
				case States.Server:
#if DISABLE_IPV6
					return $"LiteNetLib4Mirror Server active at IPv4:{Singleton.serverIPv4BindAddress} Port:{Singleton.port}";
#else
					return $"LiteNetLib4Mirror Server active at IPv4:{Singleton.serverIPv4BindAddress} IPv6:{Singleton.serverIPv6BindAddress} Port:{Singleton.port}";
#endif
				default:
					return "Invalid state!";
			}
		}

		private static void SetParameters()
		{
			Host.UpdateTime = Singleton.updateTime;
			Host.PingInterval = Singleton.pingInterval;
			Host.DisconnectTimeout = Singleton.disconnectTimeout;
			Host.SimulatePacketLoss = Singleton.simulatePacketLoss;
			Host.SimulateLatency = Singleton.simulateLatency;
			Host.SimulationPacketLossChance = Singleton.simulationPacketLossChance;
			Host.SimulationMinLatency = Singleton.simulationMinLatency;
			Host.SimulationMaxLatency = Singleton.simulationMaxLatency;
			Host.DiscoveryEnabled = Singleton.discoveryEnabled;
			Host.ReconnectDelay = Singleton.reconnectDelay;
			Host.MaxConnectAttempts = Singleton.maxConnectAttempts;
		}

		private static bool ClientConnectedInternal()
		{
			return State == States.Client;
		}

		private static void ClientConnectInternal(string code)
		{
			try
			{
				EventBasedNetListener listener = new EventBasedNetListener();
				Host = new NetManager(listener);
				listener.NetworkReceiveEvent += ClientOnNetworkReceiveEvent;
				listener.NetworkErrorEvent += ClientOnNetworkErrorEvent;
				listener.PeerConnectedEvent += ClientOnPeerConnectedEvent;
				listener.PeerDisconnectedEvent += ClientOnPeerDisconnectedEvent;
				if (Singleton.discoveryEnabled)
				{
					listener.NetworkReceiveUnconnectedEvent += ClientOnNetworkReceiveUnconnectedEvent;
				}

				SetParameters();

				Host.Start();
				Host.Connect(new IPEndPoint(Parse(Singleton.clientAddress), Singleton.port), code);

				_update = true;
				State = States.Client;
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

		private static bool ClientSendInternal(DeliveryMethod method, byte[] data, int start, int length)
		{
			try
			{
				Host.FirstPeer.Send(data, start, length, method);
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static bool ServerActiveInternal()
		{
			return State == States.Server;
		}

		private static void ServerStartInternal(string code)
		{
			try
			{
				_code = code;
				EventBasedNetListener listener = new EventBasedNetListener();
				Host = new NetManager(listener);
				listener.ConnectionRequestEvent += ServerOnConnectionRequestEvent;
				listener.PeerDisconnectedEvent += ServerOnPeerDisconnectedEvent;
				listener.NetworkErrorEvent += ServerOnNetworkErrorEvent;
				listener.NetworkReceiveEvent += ServerOnNetworkReceiveEvent;
				listener.PeerConnectedEvent += ServerOnPeerConnectedEvent;
				if (Singleton.discoveryEnabled)
				{
					listener.NetworkReceiveUnconnectedEvent += ServerOnNetworkReceiveUnconnectedEvent;
				}

				SetParameters();
				if (Singleton.useUpnP)
				{
					ForwardPort();
				}
#if DISABLE_IPV6
				Host.Start(Parse(Singleton.serverIPv4BindAddress), Parse("::1"), Singleton.port);
#else
				Host.Start(Parse(Singleton.serverIPv4BindAddress), Parse(Singleton.serverIPv6BindAddress), Singleton.port);
#endif
				_update = true;
				State = States.Server;
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
			if (messagetype == UnconnectedMessageType.DiscoveryRequest && Singleton.ProcessDiscoveryRequest(remoteendpoint, reader.GetString(), out string response))
			{
				if (_lastDiscoveryMessage != response)
				{
					_lastDiscoveryMessage = response;
					DataWriter.Reset();
					DataWriter.Put(response);
				}
				Host.SendDiscoveryResponse(DataWriter, remoteendpoint);
			}
			reader.Recycle();
		}

		private static void ServerOnPeerConnectedEvent(NetPeer peer)
		{
			Peers.Add(peer.Id + 1, peer);
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
			for (NetPeer peer = Host.FirstPeer; peer != null; peer = peer.NextPeer)
			{
				if (peer.EndPoint.ToString() == endpoint.ToString())
				{
					Singleton.OnServerError.Invoke(peer.Id + 1, new SocketException((int) socketerror));
					Singleton.onServerSocketError.Invoke(peer.Id + 1, socketerror);
					return;
				}
			}
		}

		private static void ServerOnPeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectinfo)
		{
			LastDisconnectError = disconnectinfo.SocketErrorCode;
			LastDisconnectReason = disconnectinfo.Reason;
			Singleton.OnServerDisconnected.Invoke(peer.Id + 1);
			Peers.Remove(peer.Id + 1);
		}

		private static void ServerOnConnectionRequestEvent(ConnectionRequest request)
		{
			if (Host.PeersCount >= Singleton.maxConnections)
			{
				request.Reject();
			}
			else if (request.AcceptIfKey(_code) == null)
			{
				Debug.LogWarning("Client tried to join with an invalid auth code! Current code:" + _code);
			}
		}

		private static bool ServerSendInternal(int connectionId, DeliveryMethod method, byte[] data, int start, int length)
		{
			try
			{
				Peers[connectionId].Send(data, start, length, method);
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
				Peers[connectionId].Disconnect();
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static void StopInternal()
		{
			if (Host != null)
			{
				Peers.Clear();
				Host.Flush();
				Host.Stop();
				Host = null;
				_update = false;
				State = States.Idle;
			}
		}

		private static string ServerGetClientAddressInteral(int connectionId)
		{
			return Peers[connectionId].EndPoint.Address.ToString();
		}

		private static int GetMaxPacketSizeInternal(DeliveryMethod channel)
		{
			int mtu = Host?.FirstPeer?.Mtu ?? NetConstants.MaxPacketSize;
			switch (channel)
			{
				case DeliveryMethod.ReliableOrdered:
				case DeliveryMethod.ReliableUnordered:
					return ushort.MaxValue * (mtu - NetConstants.FragmentHeaderSize);
				case DeliveryMethod.ReliableSequenced:
				case DeliveryMethod.Sequenced:
					return mtu - NetConstants.SequencedHeaderSize;
				default:
					return mtu - NetConstants.HeaderSize;
			}
		}
	}
}
