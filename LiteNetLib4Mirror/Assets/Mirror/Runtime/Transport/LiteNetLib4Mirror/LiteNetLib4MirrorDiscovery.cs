using System.Net;
using LiteNetLib;
using LiteNetLib.Utils;
using UnityEngine;

namespace Mirror.LiteNetLib4Mirror
{
	public class LiteNetLib4MirrorDiscovery : MonoBehaviour
	{
		public UnityEventIpEndpointString onDiscoveryResponse;
		private static readonly NetDataWriter DataWriter = new NetDataWriter();
		public static LiteNetLib4MirrorDiscovery Singleton { get; private set; }
		private static string _lastDiscoveryMessage;

		private void Awake()
		{
			if (Singleton == null)
			{
				GetComponent<LiteNetLib4MirrorTransport>()?.Initialize();
				Singleton = this;
			}
		}

		/// <summary>
		/// Override this in your code to decide about accepting requests.
		/// </summary>
		protected virtual bool ProcessDiscoveryRequest(IPEndPoint ipEndPoint, string text, out string response)
		{
			response = "LiteNetLib4Mirror Discovery accepted";
			return true;
		}

		public static void SeekerInitialize()
		{
			if (LiteNetLib4MirrorCore.State == LiteNetLib4MirrorCore.States.Idle)
			{
				EventBasedNetListener eventBasedNetListener = new EventBasedNetListener();
				LiteNetLib4MirrorCore.Host = new NetManager(eventBasedNetListener);
				eventBasedNetListener.NetworkReceiveUnconnectedEvent += OnDiscoveryResponse;
				LiteNetLib4MirrorCore.Host.DiscoveryEnabled = true;
				LiteNetLib4MirrorCore.Host.Start();
				LiteNetLib4MirrorCore.State = LiteNetLib4MirrorCore.States.Discovery;
				LiteNetLib4MirrorTransport.Polling = true;
			}
			else
			{
				Debug.LogWarning("LiteNetLib4Mirror is already running client or server!");
			}
		}

		public static void SendDiscoveryRequest(string text)
		{
			if (LiteNetLib4MirrorCore.State == LiteNetLib4MirrorCore.States.Discovery)
			{
				if (_lastDiscoveryMessage != text)
				{
					_lastDiscoveryMessage = text;
					DataWriter.Reset();
					DataWriter.Put(text);
				}

				LiteNetLib4MirrorCore.Host.SendDiscoveryRequest(DataWriter, LiteNetLib4MirrorTransport.Singleton.port);
			}
		}

		public static void Stop()
		{
			LiteNetLib4MirrorCore.StopInternal();
		}

		internal static void OnDiscoveryResponse(IPEndPoint remoteendpoint, NetPacketReader reader, UnconnectedMessageType messagetype)
		{
			if (messagetype == UnconnectedMessageType.DiscoveryResponse)
			{
				Singleton.onDiscoveryResponse.Invoke(remoteendpoint, reader.GetString());
			}
			reader.Recycle();
		}

		internal static void OnDiscoveryRequest(IPEndPoint remoteendpoint, NetPacketReader reader, UnconnectedMessageType messagetype)
		{
			if (messagetype == UnconnectedMessageType.DiscoveryRequest && Singleton.ProcessDiscoveryRequest(remoteendpoint, reader.GetString(), out string response))
			{
				if (_lastDiscoveryMessage != response)
				{
					_lastDiscoveryMessage = response;
					DataWriter.Reset();
					DataWriter.Put(response);
				}
				LiteNetLib4MirrorCore.Host.SendDiscoveryResponse(DataWriter, remoteendpoint);
			}
			reader.Recycle();
		}
	}
}
