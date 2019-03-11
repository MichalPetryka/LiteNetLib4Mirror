using System.Collections;
using System.ComponentModel;
using System.Net;
using UnityEngine;

namespace Mirror.LiteNetLib4Mirror
{
	[RequireComponent(typeof(NetworkManager))]
	[RequireComponent(typeof(NetworkManagerHUD))]
	[RequireComponent(typeof(LiteNetLib4MirrorTransport))]
	[RequireComponent(typeof(LiteNetLib4MirrorDiscovery))]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public class NetworkDiscoveryHUD : MonoBehaviour
	{
		[SerializeField] private float discoveryInterval = 1f;
		private NetworkManager _manager;
		private NetworkManagerHUD _managerHud;
		private bool _noDiscovering = true;

		private void Awake()
		{
			_manager = GetComponent<NetworkManager>();
			_managerHud = GetComponent<NetworkManagerHUD>();
		}

		private void OnGUI()
		{
			if (!_managerHud.showGUI)
			{
				_noDiscovering = true;
				return;
			}

			bool noConnection = (_manager.client == null || _manager.client.connection == null ||
			                     _manager.client.connection.connectionId == -1);

			GUILayout.BeginArea(new Rect(10 + _managerHud.offsetX + 215 + 10, 40 + _managerHud.offsetY, 215, 9999));
			if (!_manager.IsClientConnected() && !NetworkServer.active)
			{
				if (noConnection)
				{
					if (_noDiscovering)
					{
						if (GUILayout.Button("Start Discovery"))
						{
							StartCoroutine(StartDiscovery());
						}
					} else
					{
						GUILayout.Label("Discovering..");
						GUILayout.Label($"LocalPort: {LiteNetLib4MirrorCore.Host.LocalPort}");
						if (GUILayout.Button("Stop Discovery"))
						{
							_noDiscovering = true;
						}
					}
				}
				else
				{
					_noDiscovering = true;
				}
			}
			else
			{
				_noDiscovering = true;
			}

			GUILayout.EndArea();
		}

		private IEnumerator StartDiscovery()
		{
			_noDiscovering = false;

			LiteNetLib4MirrorDiscovery.SeekerInitialize();
			LiteNetLib4MirrorDiscovery.Singleton.onDiscoveryResponse.AddListener(OnClientDiscoveryResponse);
			while (!_noDiscovering)
			{
				LiteNetLib4MirrorDiscovery.SendDiscoveryRequest(string.Empty);
				yield return new WaitForSeconds(discoveryInterval);
			}

			LiteNetLib4MirrorDiscovery.Singleton.onDiscoveryResponse.RemoveListener(OnClientDiscoveryResponse);
			if (!NetworkClient.active && !NetworkServer.active)
			{
				LiteNetLib4MirrorDiscovery.Stop();
			}
		}

		private void OnClientDiscoveryResponse(IPEndPoint endpoint, string text)
		{
			LiteNetLib4MirrorDiscovery.Stop();

			string ip = endpoint.Address.ToString();
			ushort port = (ushort)endpoint.Port;

			_manager.networkAddress = ip;
			_manager.maxConnections = 2;
			LiteNetLib4MirrorTransport.Singleton.clientAddress = ip;
			LiteNetLib4MirrorTransport.Singleton.port = port;
			LiteNetLib4MirrorTransport.Singleton.maxConnections = 2;
			_manager.StartClient();
			_noDiscovering = true;
		}
	}
}
