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
	// ReSharper disable once InconsistentNaming
	public class NetworkDiscoveryHUD : MonoBehaviour
	{
		[SerializeField] public float discoveryInterval = 1f;
		private NetworkManagerHUD _managerHud;
		private bool _noDiscovering = true;

		private void Awake()
		{
			_managerHud = GetComponent<NetworkManagerHUD>();
		}

		private void OnGUI()
		{
			if (!_managerHud.showGUI)
			{
				_noDiscovering = true;
				return;
			}

			GUILayout.BeginArea(new Rect(10 + _managerHud.offsetX + 215 + 10, 40 + _managerHud.offsetY, 215, 9999));
			if (!NetworkManager.singleton.IsClientConnected() && !NetworkServer.active)
			{
				if (_noDiscovering)
				{
					if (GUILayout.Button("Start Discovery"))
					{
						LiteNetLib4MirrorDiscovery.SeekerInitialize();
						StartCoroutine(StartDiscovery());
					}
				}
				else
				{
					GUILayout.Label("Discovering..");
					GUILayout.Label($"LocalPort: {LiteNetLib4MirrorTransport.Singleton.port}");
					if (GUILayout.Button("Stop Discovery"))
					{
						_noDiscovering = true;
						LiteNetLib4MirrorDiscovery.Stop();
					}
				}
			}

			GUILayout.EndArea();
		}

		private IEnumerator StartDiscovery()
		{
			_noDiscovering = false;

			LiteNetLib4MirrorDiscovery.Singleton.onDiscoveryResponse.AddListener(OnClientDiscoveryResponse);
			while (!_noDiscovering)
			{
				LiteNetLib4MirrorDiscovery.SendDiscoveryRequest(string.Empty);
				yield return new WaitForSeconds(discoveryInterval);
			}

			LiteNetLib4MirrorDiscovery.Singleton.onDiscoveryResponse.RemoveListener(OnClientDiscoveryResponse);
		}

		private void OnClientDiscoveryResponse(IPEndPoint endpoint, string text)
		{
			string ip = endpoint.Address.ToString();
			ushort port = (ushort)endpoint.Port;

			NetworkManager.singleton.networkAddress = ip;
			NetworkManager.singleton.maxConnections = 2;
			LiteNetLib4MirrorTransport.Singleton.clientAddress = ip;
			LiteNetLib4MirrorTransport.Singleton.port = port;
			LiteNetLib4MirrorTransport.Singleton.maxConnections = 2;
			NetworkManager.singleton.StartClient();
			_noDiscovering = true;
		}
	}
}
