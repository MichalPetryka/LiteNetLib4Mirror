using Mirror;
using Mirror.LiteNetLib;
using System;
using System.Linq;
using System.Net.NetworkInformation;
using UnityEngine;

[RequireComponent(typeof(LiteNetLib4MirrorTransport))]
public class LiteNetLib4MirrorNetworkManager : NetworkManager
{
	/// <summary>
	/// Singleton of the modified NetworkManager
	/// </summary>
	public new static LiteNetLib4MirrorNetworkManager singleton;

	public virtual LiteNetLib4MirrorTransport Transport => LiteNetLib4MirrorTransport.Singleton;

	public override void Awake()
	{
		singleton = this;
		NetworkManager.singleton = this;
		GetComponent<LiteNetLib4MirrorTransport>().Load();
		transport = Transport;
		base.Awake();
	}
	/// <summary>
	/// Start client with ip and port
	/// </summary>
	/// <param name="ip">IP to connect</param>
	/// <param name="port">Port</param>
	/// <returns></returns>
	public NetworkClient StartClient(string ip, ushort port)
	{
		maxConnections = 2;
		Transport.clientAddress = ip;
		Transport.port = port;
		Transport.maxConnections = 2;
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
	/// <returns></returns>
	public NetworkClient StartHost(string serverIPv4BindAddress, ushort port, ushort maxPlayers)
#else
	/// <summary>
	/// Start Host with provided bind addresses, port and connection limit
	/// </summary>
	/// <param name="serverIPv4BindAddress">IPv4 bind address</param>
	/// <param name="serverIPv6BindAddress">IPv6 bind address</param>
	/// <param name="port">Port</param>
	/// <param name="maxPlayers">Connection limit</param>
	/// <returns></returns>
	public NetworkClient StartHost(string serverIPv4BindAddress, string serverIPv6BindAddress, ushort port, ushort maxPlayers)
#endif
	{
		maxConnections = maxPlayers;
		Transport.clientAddress = serverIPv4BindAddress;
		Transport.serverIPv4BindAddress = serverIPv4BindAddress;
#if !DISABLE_IPV6
		Transport.serverIPv6BindAddress = serverIPv6BindAddress;
#endif
		Transport.port = port;
		Transport.maxConnections = maxPlayers;
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
	/// <returns></returns>
	public NetworkClient StartHost(string serverIPv4BindAddress, ushort port, ushort maxPlayers)
	public bool StartServer(string serverIPv4BindAddress, ushort port, ushort maxPlayers)
#else
	/// <summary>
	/// Start Server with provided bind addresses, port and connection limit
	/// </summary>
	/// <param name="serverIPv4BindAddress">IPv4 bind address</param>
	/// <param name="serverIPv6BindAddress">IPv6 bind address</param>
	/// <param name="port">Port</param>
	/// <param name="maxPlayers">Connection limit</param>
	/// <returns></returns>
	public bool StartServer(string serverIPv4BindAddress, string serverIPv6BindAddress, ushort port, ushort maxPlayers)
#endif
	{
		maxConnections = maxPlayers;
		Transport.clientAddress = serverIPv4BindAddress;
		Transport.serverIPv4BindAddress = serverIPv4BindAddress;
#if !DISABLE_IPV6
		Transport.serverIPv6BindAddress = serverIPv6BindAddress;
#endif
		Transport.port = port;
		Transport.maxConnections = maxPlayers;
		maxConnections = maxPlayers;
		return StartServer();
	}

	/// <summary>
	/// Start Host with local bind adresses, port and connection limit
	/// </summary>
	/// <param name="port">Port</param>
	/// <param name="maxPlayers">Connection limit</param>
	/// <returns></returns>
	public NetworkClient StartHost(ushort port, ushort maxPlayers)
	{
		maxConnections = maxPlayers;
		Transport.clientAddress = "127.0.0.1";
		Transport.serverIPv4BindAddress = "127.0.0.1";
#if !DISABLE_IPV6
		Transport.serverIPv6BindAddress = "::1";
#endif
		Transport.port = port;
		Transport.maxConnections = maxPlayers;
		maxConnections = maxPlayers;
		return StartHost();
	}

	/// <summary>
	/// Start Server with local bind addresses, port and connection limit
	/// </summary>
	/// <param name="port">Port</param>
	/// <param name="maxPlayers">Connection limit</param>
	/// <returns></returns>
	public bool StartServer(ushort port, ushort maxPlayers)
	{
		maxConnections = maxPlayers;
		Transport.serverIPv4BindAddress = "127.0.0.1";
#if !DISABLE_IPV6
		Transport.serverIPv6BindAddress = "::1";
#endif
		Transport.port = port;
		Transport.maxConnections = maxPlayers;
		maxConnections = maxPlayers;
		return StartServer();
	}
	/// <summary>
	/// Utility function for getting first free port in range (as a bonus, should work if unity doesn't shit itself)
	/// </summary>
	/// <param name="ports">Available ports</param>
	/// <returns></returns>
	public int GetFirstFreePort(params ushort[] ports)
	{
		if (ports == null || ports.Length == 0) throw new Exception("No ports provided");
		ushort freeport = ports.Except(Array.ConvertAll(IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners(), p => (ushort)p.Port)).FirstOrDefault();
		if (freeport == 0) throw new Exception("No free port!");
		return freeport;
	}
}
