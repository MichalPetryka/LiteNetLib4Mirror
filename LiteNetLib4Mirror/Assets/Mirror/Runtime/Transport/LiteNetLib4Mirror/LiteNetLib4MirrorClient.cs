using System;
using System.Net;
using System.Net.Sockets;
using LiteNetLib;

namespace Mirror.LiteNetLib4Mirror
{
	public static class LiteNetLib4MirrorClient
	{
		/// <summary>
		/// Use LiteNetLib4MirrorNetworkManager.DisconnectConnection to send the reason
		/// </summary>
		public static string LastDisconnectReason;
		internal static bool IsConnected()
		{
			return LiteNetLib4MirrorCore.State == LiteNetLib4MirrorCore.States.ClientConnected || LiteNetLib4MirrorCore.State == LiteNetLib4MirrorCore.States.ClientConnecting;
		}

		internal static void ConnectClient(string code)
		{
			try
			{
				if (LiteNetLib4MirrorCore.State == LiteNetLib4MirrorCore.States.Discovery)
				{
					LiteNetLib4MirrorCore.StopTransport();
				}
				EventBasedNetListener listener = new EventBasedNetListener();
				LiteNetLib4MirrorCore.Host = new NetManager(listener);
				listener.NetworkReceiveEvent += OnNetworkReceive;
				listener.NetworkErrorEvent += OnNetworkError;
				listener.PeerConnectedEvent += OnPeerConnected;
				listener.PeerDisconnectedEvent += OnPeerDisconnected;

				LiteNetLib4MirrorCore.SetOptions(false);

				LiteNetLib4MirrorCore.Host.Start();
				LiteNetLib4MirrorCore.Host.Connect(LiteNetLib4MirrorUtils.Parse(LiteNetLib4MirrorTransport.Singleton.clientAddress, LiteNetLib4MirrorTransport.Singleton.port), code);

				LiteNetLib4MirrorTransport.Polling = true;
				LiteNetLib4MirrorCore.State = LiteNetLib4MirrorCore.States.ClientConnecting;
			}
			catch (Exception ex)
			{
				LiteNetLib4MirrorCore.State = LiteNetLib4MirrorCore.States.Idle;
				LiteNetLib4MirrorUtils.LogException(ex);
			}
		}

		private static void OnPeerConnected(NetPeer peer)
		{
			LastDisconnectReason = null;
			LiteNetLib4MirrorCore.State = LiteNetLib4MirrorCore.States.ClientConnected;
			LiteNetLib4MirrorTransport.Singleton.OnClientConnected.Invoke();
		}

		private static void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectinfo)
		{
			if (disconnectinfo.AdditionalData.TryGetString(out string reason))
			{
				LastDisconnectReason = LiteNetLib4MirrorUtils.FromBase64(reason);
			}
			LiteNetLib4MirrorCore.State = LiteNetLib4MirrorCore.States.Idle;
			LiteNetLib4MirrorCore.LastDisconnectError = disconnectinfo.SocketErrorCode;
			LiteNetLib4MirrorCore.LastDisconnectReason = disconnectinfo.Reason;
			LiteNetLib4MirrorTransport.Singleton.OnClientDisconnected.Invoke();
			LiteNetLib4MirrorCore.StopTransport();
		}

		private static void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliverymethod)
		{
#if NONALLOC_RECEIVE
			LiteNetLib4MirrorTransport.Singleton.OnClientDataReceivedNonAlloc.Invoke(reader.GetRemainingBytesSegment());
#else
			LiteNetLib4MirrorTransport.Singleton.OnClientDataReceived.Invoke(reader.GetRemainingBytes());
#endif
			reader.Recycle();
		}

		private static void OnNetworkError(IPEndPoint endpoint, SocketError socketerror)
		{
			LiteNetLib4MirrorCore.LastError = socketerror;
			LiteNetLib4MirrorTransport.Singleton.OnClientError.Invoke(new SocketException((int)socketerror));
			LiteNetLib4MirrorTransport.Singleton.onClientSocketError.Invoke(socketerror);
		}

		internal static bool Send(DeliveryMethod method, byte[] data, int start, int length, byte channelNumber)
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
