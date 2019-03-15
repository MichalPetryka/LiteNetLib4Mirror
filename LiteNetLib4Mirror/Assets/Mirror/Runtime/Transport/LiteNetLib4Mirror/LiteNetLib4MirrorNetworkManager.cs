using UnityEngine;

namespace Mirror.LiteNetLib4Mirror
{
	[RequireComponent(typeof(LiteNetLib4MirrorTransport))]
	public class LiteNetLib4MirrorNetworkManager : NetworkManager
	{
		/// <summary>
		/// Singleton of the modified NetworkManager
		/// </summary>
		// ReSharper disable once InconsistentNaming
		public static new LiteNetLib4MirrorNetworkManager singleton;

		public override void Awake()
		{
			LiteNetLib4MirrorTransport.Singleton.Initialize();
			base.Awake();
			singleton = this;
			NetworkManager.singleton = this;
		}

		/// <summary>
		/// Start client with ip and port
		/// </summary>
		/// <param name="ip">IP to connect</param>
		/// <param name="port">Port</param>
		public NetworkClient StartClient(string ip, ushort port)
		{
			networkAddress = ip;
			maxConnections = 2;
			LiteNetLib4MirrorTransport.Singleton.clientAddress = ip;
			LiteNetLib4MirrorTransport.Singleton.port = port;
			LiteNetLib4MirrorTransport.Singleton.maxConnections = 2;
			maxConnections = 2;
			return StartClient();
		}

#if DISABLE_IPV6
		/// <summary>
		/// Start Host with provided bind address, port and connection limit
		/// </summary>
		/// <param name="serverIPv4BindAddress">IPv4 bind address</param>
		/// <param name="port">Port</param>
		/// <param name="maxPlayers">Connection limit</param>
#else
		/// <summary>
		/// Start Host with provided bind addresses, port and connection limit
		/// </summary>
		/// <param name="serverIPv4BindAddress">IPv4 bind address</param>
		/// <param name="serverIPv6BindAddress">IPv6 bind address</param>
		/// <param name="port">Port</param>
		/// <param name="maxPlayers">Connection limit</param>
#endif
#if DISABLE_IPV6
		public NetworkClient StartHost(string serverIPv4BindAddress, ushort port, ushort maxPlayers)
#else
		public NetworkClient StartHost(string serverIPv4BindAddress, string serverIPv6BindAddress, ushort port, ushort maxPlayers)
#endif
		{
			networkAddress = serverIPv4BindAddress;
			maxConnections = maxPlayers;
			LiteNetLib4MirrorTransport.Singleton.clientAddress = serverIPv4BindAddress;
			LiteNetLib4MirrorTransport.Singleton.serverIPv4BindAddress = serverIPv4BindAddress;
#if !DISABLE_IPV6
			LiteNetLib4MirrorTransport.Singleton.serverIPv6BindAddress = serverIPv6BindAddress;
#endif
			LiteNetLib4MirrorTransport.Singleton.port = port;
			LiteNetLib4MirrorTransport.Singleton.maxConnections = maxPlayers;
			maxConnections = maxPlayers;
			return StartHost();
		}

#if DISABLE_IPV6
		/// <summary>
		/// Start Server with provided bind address, port and connection limit
		/// </summary>
		/// <param name="serverIPv4BindAddress">IPv4 bind address</param>
		/// <param name="port">Port</param>
		/// <param name="maxPlayers">Connection limit</param>
#else
		/// <summary>
		/// Start Server with provided bind addresses, port and connection limit
		/// </summary>
		/// <param name="serverIPv4BindAddress">IPv4 bind address</param>
		/// <param name="serverIPv6BindAddress">IPv6 bind address</param>
		/// <param name="port">Port</param>
		/// <param name="maxPlayers">Connection limit</param>
#endif
#if DISABLE_IPV6
		public bool StartServer(string serverIPv4BindAddress, ushort port, ushort maxPlayers)
#else
		public bool StartServer(string serverIPv4BindAddress, string serverIPv6BindAddress, ushort port, ushort maxPlayers)
#endif
		{
			networkAddress = serverIPv4BindAddress;
			maxConnections = maxPlayers;
			LiteNetLib4MirrorTransport.Singleton.clientAddress = serverIPv4BindAddress;
			LiteNetLib4MirrorTransport.Singleton.serverIPv4BindAddress = serverIPv4BindAddress;
#if !DISABLE_IPV6
			LiteNetLib4MirrorTransport.Singleton.serverIPv6BindAddress = serverIPv6BindAddress;
#endif
			LiteNetLib4MirrorTransport.Singleton.port = port;
			LiteNetLib4MirrorTransport.Singleton.maxConnections = maxPlayers;
			maxConnections = maxPlayers;
			return StartServer();
		}

		/// <summary>
		/// Start Host with local bind adresses, port and connection limit
		/// </summary>
		/// <param name="port">Port</param>
		/// <param name="maxPlayers">Connection limit</param>
		public NetworkClient StartHost(ushort port, ushort maxPlayers)
		{
			networkAddress = "127.0.0.1";
			maxConnections = maxPlayers;
			LiteNetLib4MirrorTransport.Singleton.clientAddress = "127.0.0.1";
			LiteNetLib4MirrorTransport.Singleton.serverIPv4BindAddress = "0.0.0.0";
#if !DISABLE_IPV6
			LiteNetLib4MirrorTransport.Singleton.serverIPv6BindAddress = "::";
#endif
			LiteNetLib4MirrorTransport.Singleton.port = port;
			LiteNetLib4MirrorTransport.Singleton.maxConnections = maxPlayers;
			maxConnections = maxPlayers;
			return StartHost();
		}

		/// <summary>
		/// Start Server with local bind addresses, port and connection limit
		/// </summary>
		/// <param name="port">Port</param>
		/// <param name="maxPlayers">Connection limit</param>
		public bool StartServer(ushort port, ushort maxPlayers)
		{
			networkAddress = "127.0.0.1";
			maxConnections = maxPlayers;
			LiteNetLib4MirrorTransport.Singleton.serverIPv4BindAddress = "0.0.0.0";
#if !DISABLE_IPV6
			LiteNetLib4MirrorTransport.Singleton.serverIPv6BindAddress = "::";
#endif
			LiteNetLib4MirrorTransport.Singleton.port = port;
			LiteNetLib4MirrorTransport.Singleton.maxConnections = maxPlayers;
			maxConnections = maxPlayers;
			return StartServer();
		}
	}
}
