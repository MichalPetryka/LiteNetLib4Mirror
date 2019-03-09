using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using UnityEngine;

namespace Mirror.LiteNetLib4Mirror
{
	public static class LiteNetLib4MirrorServer
	{
		internal static readonly Dictionary<int, NetPeer> Peers = new Dictionary<int, NetPeer>();
		public static string Code { get; internal set; }

		internal static bool ServerActiveInternal()
		{
			return LiteNetLib4MirrorCore.State == LiteNetLib4MirrorCore.States.Server;
		}

		internal static void ServerStartInternal(string code)
		{
			try
			{
				Code = code;
				EventBasedNetListener listener = new EventBasedNetListener();
				LiteNetLib4MirrorCore.Host = new NetManager(listener);
				listener.ConnectionRequestEvent += ServerOnConnectionRequestEvent;
				listener.PeerDisconnectedEvent += ServerOnPeerDisconnectedEvent;
				listener.NetworkErrorEvent += ServerOnNetworkErrorEvent;
				listener.NetworkReceiveEvent += ServerOnNetworkReceiveEvent;
				listener.PeerConnectedEvent += ServerOnPeerConnectedEvent;
				if (LiteNetLib4MirrorDiscovery.Singleton != null)
				{
					listener.NetworkReceiveUnconnectedEvent += LiteNetLib4MirrorDiscovery.OnDiscoveryRequest;
				}

				LiteNetLib4MirrorCore.SetParameters(true);
				if (LiteNetLib4MirrorTransport.Singleton.useUpnP)
				{
					LiteNetLib4MirrorUtils.ForwardPort();
				}
#if DISABLE_IPV6
				LiteNetLib4MirrorCore.Host.Start(LiteNetLib4MirrorUtils.Parse(LiteNetLib4MirrorTransport.Singleton.serverIPv4BindAddress), LiteNetLib4MirrorUtils.Parse("::"), LiteNetLib4MirrorTransport.Singleton.port);
#else
				LiteNetLib4MirrorCore.Host.Start(LiteNetLib4MirrorUtils.Parse(LiteNetLib4MirrorTransport.Singleton.serverIPv4BindAddress), LiteNetLib4MirrorUtils.Parse(LiteNetLib4MirrorTransport.Singleton.serverIPv6BindAddress), LiteNetLib4MirrorTransport.Singleton.port);
#endif
				LiteNetLib4MirrorTransport.Polling = true;
				LiteNetLib4MirrorCore.State = LiteNetLib4MirrorCore.States.Server;
			}
			catch (Exception ex)
			{
				LiteNetLib4MirrorCore.State = LiteNetLib4MirrorCore.States.Idle;
				LiteNetLib4MirrorUtils.LogException(ex);
			}
		}

		internal static void ServerOnPeerConnectedEvent(NetPeer peer)
		{
			Peers.Add(peer.Id + 1, peer);
			LiteNetLib4MirrorTransport.Singleton.OnServerConnected.Invoke(peer.Id + 1);
		}

		internal static void ServerOnNetworkReceiveEvent(NetPeer peer, NetPacketReader reader, DeliveryMethod deliverymethod)
		{
			LiteNetLib4MirrorTransport.Singleton.OnServerDataReceived.Invoke(peer.Id + 1, reader.GetRemainingBytes());
			reader.Recycle();
		}

		internal static void ServerOnNetworkErrorEvent(IPEndPoint endpoint, SocketError socketerror)
		{
			LiteNetLib4MirrorCore.LastError = socketerror;
			for (NetPeer peer = LiteNetLib4MirrorCore.Host.FirstPeer; peer != null; peer = peer.NextPeer)
			{
				if (peer.EndPoint.ToString() == endpoint.ToString())
				{
					LiteNetLib4MirrorTransport.Singleton.OnServerError.Invoke(peer.Id + 1, new SocketException((int)socketerror));
					LiteNetLib4MirrorTransport.Singleton.onServerSocketError.Invoke(peer.Id + 1, socketerror);
					return;
				}
			}
		}

		internal static void ServerOnPeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectinfo)
		{
			LiteNetLib4MirrorCore.LastDisconnectError = disconnectinfo.SocketErrorCode;
			LiteNetLib4MirrorCore.LastDisconnectReason = disconnectinfo.Reason;
			LiteNetLib4MirrorTransport.Singleton.OnServerDisconnected.Invoke(peer.Id + 1);
			Peers.Remove(peer.Id + 1);
		}

		internal static void ServerOnConnectionRequestEvent(ConnectionRequest request)
		{
			LiteNetLib4MirrorTransport.Singleton.ProcessConnectionRequest(request);
		}

		internal static bool ServerSendInternal(int connectionId, DeliveryMethod method, byte[] data, int start, int length, byte channelNumber)
		{
			try
			{
				Peers[connectionId].Send(data, start, length, channelNumber, method);
				return true;
			}
			catch
			{
				return false;
			}
		}

		internal static bool ServerDisconnectInternal(int connectionId)
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

		internal static string ServerGetClientAddressInteral(int connectionId)
		{
			return Peers[connectionId].EndPoint.Address.ToString();
		}
	}
}
