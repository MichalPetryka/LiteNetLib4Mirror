using LiteNetLib;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

namespace Mirror.LiteNetLib
{
	[Serializable] public class UnityEventError : UnityEvent<SocketError> { }
	[Serializable] public class UnityEventIntError : UnityEvent<int, SocketError> { }
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

		private static NetManager _host;

		public void Load()
		{
			if (Singleton != null)
				throw new Exception("Already initialized!");
			Singleton = this;
			State = States.Idle;
		}

		public UnityEventError onClientSocketError;
		public UnityEventIntError onServerSocketError;

		public static States State { get; private set; } = States.NonInitialized;

		public static SocketError LastError { get; private set; }
		public static SocketError LastDisconnectError { get; private set; }
		public static DisconnectReason LastDisconnectReason { get; private set; }

		public override bool ClientConnected()
		{
			return ClientConnectedInternal();
		}

		public override void ClientConnect(string address)
		{
			ClientConnectInternal();
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
			ServerStartInternal();
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

		private static string GenerateCode()
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(Concatenate(Application.productName, Application.companyName, Application.unityVersion, TransportVersion, Singleton.authCode)));
		}

		private void LateUpdate()
		{
			if (_update) _host.PollEvents();
		}

		private void OnDestroy()
		{
			ShutdownInternal();
		}

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

		public override string ToString()
		{
			return ToStringInternal();
		}

		private static string ToStringInternal()
		{
			switch (State)
			{
				case States.NonInitialized:
					return "LiteNetLib4Mirror is not initialized";
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

		private static void ClientConnectInternal()
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
				_host.Connect(new IPEndPoint(Parse(Singleton.clientAddress), Singleton.port), GenerateCode());

				_update = true;
				State = States.ClientConnected;
			}
			catch (Exception ex)
			{
				State = States.Idle;
				LogException(ex);
			}
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
				_host.ConnectedPeerList[0].Send(data, method);
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

		private static void ServerStartInternal()
		{
			State = States.ServerStarting;
			try
			{
				_code = GenerateCode();
				EventBasedNetListener listener = new EventBasedNetListener();
				_host = new NetManager(listener);
				listener.ConnectionRequestEvent += ServerOnConnectionRequestEvent;
				listener.PeerDisconnectedEvent += ServerOnPeerDisconnectedEvent;
				listener.NetworkErrorEvent += ServerOnNetworkErrorEvent;
				listener.NetworkReceiveEvent += ServerOnNetworkReceiveEvent;
				listener.PeerConnectedEvent += ServerOnPeerConnectedEvent;

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
			for (int i = 0; i < _host.ConnectedPeerList.Count; i++)
				if (_host.ConnectedPeerList[i].EndPoint.ToString() == endpoint.ToString())
				{
					Singleton.OnServerError.Invoke(i, new SocketException((int) socketerror));
					Singleton.onServerSocketError.Invoke(i, socketerror);
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
			if (_host.PeersCount < Singleton.maxConnections)
				if (request.AcceptIfKey(_code) == null)
					Debug.LogWarning("Client tried to join with an invalid auth code! Current code:" + _code);
			else
				request.Reject();
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

		private static bool ServerDisconnectInternal(int connectionId)
		{
			try
			{
				_host.ConnectedPeerList[connectionId].Disconnect();
				return true;
			}
			catch
			{
				return false;
			}
		}

		private static void StopInternal()
		{
			_host.DisconnectAll();
			_host.Stop();
			State = States.Idle;
		}

		private static bool GetConnectionInfoInteral(int connectionId, out string address)
		{
			if (_host.ConnectedPeerList.Count < connectionId)
			{
				address = _host.ConnectedPeerList[connectionId].EndPoint.Address.ToString();
				return true;
			}

			address = "(invalid)";
			return false;
		}

		private static void ShutdownInternal()
		{
			if (ClientConnectedInternal() || ServerActiveInternal()) StopInternal();
			Singleton = null;
			State = States.NonInitialized;
		}

		private static int GetMaxPacketSizeInternal(DeliveryMethod channel)
		{
			switch (channel)
			{
				case DeliveryMethod.ReliableOrdered:
				case DeliveryMethod.ReliableUnordered:
					return ushort.MaxValue * NetConstants.MaxPacketSize;
				default:
					return NetConstants.MaxPacketSize;
			}
		}
	}
}