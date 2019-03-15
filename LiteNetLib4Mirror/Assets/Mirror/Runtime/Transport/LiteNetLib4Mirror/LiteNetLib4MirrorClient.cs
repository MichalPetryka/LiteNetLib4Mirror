using System;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;

namespace Mirror.LiteNetLib4Mirror
{
	public static class LiteNetLib4MirrorClient
	{
		internal static bool ClientConnectedInternal()
		{
			return LiteNetLib4MirrorCore.State == LiteNetLib4MirrorCore.States.Client;
		}

		internal static void ClientConnectInternal(string code)
		{
			try
			{
				if (LiteNetLib4MirrorCore.State == LiteNetLib4MirrorCore.States.Discovery)
				{
					LiteNetLib4MirrorCore.StopInternal();
				}
				EventBasedNetListener listener = new EventBasedNetListener();
				LiteNetLib4MirrorCore.Host = new NetManager(listener);
				listener.NetworkReceiveEvent += ClientOnNetworkReceiveEvent;
				listener.NetworkErrorEvent += ClientOnNetworkErrorEvent;
				listener.PeerConnectedEvent += ClientOnPeerConnectedEvent;
				listener.PeerDisconnectedEvent += ClientOnPeerDisconnectedEvent;

				LiteNetLib4MirrorCore.SetParameters(false);

				LiteNetLib4MirrorCore.Host.Start();
				LiteNetLib4MirrorCore.Host.Connect(LiteNetLib4MirrorUtils.Parse(LiteNetLib4MirrorTransport.Singleton.clientAddress, LiteNetLib4MirrorTransport.Singleton.port), code);

				LiteNetLib4MirrorTransport.Polling = true;
				LiteNetLib4MirrorCore.State = LiteNetLib4MirrorCore.States.Client;
			}
			catch (Exception ex)
			{
				LiteNetLib4MirrorCore.State = LiteNetLib4MirrorCore.States.Idle;
				LiteNetLib4MirrorUtils.LogException(ex);
			}
		}

		private static void ClientOnPeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectinfo)
		{
			LiteNetLib4MirrorCore.LastDisconnectError = disconnectinfo.SocketErrorCode;
			LiteNetLib4MirrorCore.LastDisconnectReason = disconnectinfo.Reason;
			LiteNetLib4MirrorTransport.Singleton.OnClientDisconnected.Invoke();
			LiteNetLib4MirrorCore.StopInternal();
		}

		private static void ClientOnPeerConnectedEvent(NetPeer peer)
		{
			LiteNetLib4MirrorTransport.Singleton.OnClientConnected.Invoke();
		}

		private static void ClientOnNetworkReceiveEvent(NetPeer peer, NetPacketReader reader, DeliveryMethod deliverymethod)
		{
#if NONALLOC_RECEIVE
			LiteNetLib4MirrorTransport.Singleton.OnClientDataReceivedNonAlloc.Invoke(reader.GetRemainingBytesSegment());
#else
			LiteNetLib4MirrorTransport.Singleton.OnClientDataReceived.Invoke(reader.GetRemainingBytes());
#endif
			reader.Recycle();
		}

		private static void ClientOnNetworkErrorEvent(IPEndPoint endpoint, SocketError socketerror)
		{
			LiteNetLib4MirrorCore.LastError = socketerror;
			LiteNetLib4MirrorTransport.Singleton.OnClientError.Invoke(new SocketException((int)socketerror));
			LiteNetLib4MirrorTransport.Singleton.onClientSocketError.Invoke(socketerror);
		}

		internal static bool ClientSendInternal(DeliveryMethod method, byte[] data, int start, int length, byte channelNumber)
		{
			try
			{
				LiteNetLib4MirrorCore.Host.FirstPeer.Send(data, start, length, channelNumber, method);
				return true;
			}
			catch
			{
				return false;
			}
		}
	}
}
