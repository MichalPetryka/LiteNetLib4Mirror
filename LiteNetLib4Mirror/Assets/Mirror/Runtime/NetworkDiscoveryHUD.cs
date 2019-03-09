using System.Collections;
using System.ComponentModel;
using System.Net;
using UnityEngine;

namespace Mirror.LiteNetLib4Mirror
{
	[RequireComponent(typeof(NetworkManagerHUD))]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public class NetworkDiscoveryHUD : MonoBehaviour
	{
		[SerializeField] private float discoveryInterval = 1f;
		private NetworkManager _manager;
		private NetworkManagerHUD _managerHud;
		private bool _noDiscovering = true;
		private bool _existsDiscovery = false;

		void Awake()
		{
			_manager = GetComponent<NetworkManager>();
			_managerHud = GetComponent<NetworkManagerHUD>();
			if (LiteNetLib4MirrorDiscovery.Singleton != null)
			{
				_existsDiscovery = true;
			}
			else
			{
				Debug.LogWarning("NetworkDiscoveryHUD could not find a LiteNetLib4MirrorDiscovery. The NetworkDiscoveryHUD requires a LiteNetLib4MirrorDiscovery.");
			}
		}

		void OnGUI()
		{
			if (!_managerHud.showGUI || !_existsDiscovery)
			{
				_noDiscovering = true;
				return;
			}

			GUILayout.BeginArea(new Rect(10 + _managerHud.offsetX + 215 + 10, 40 + _managerHud.offsetY, 215, 9999));
			if (!_manager.IsClientConnected() && !NetworkServer.active)
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
					GUILayout.Label($"LocalPort: {LiteNetLib4MirrorCore.Host.LocalPort}");
					if (GUILayout.Button("Stop Discovery"))
					{
						_noDiscovering = true;
						LiteNetLib4MirrorDiscovery.Stop();
					}
				}
			}

			GUILayout.EndArea();
		}

		IEnumerator StartDiscovery()
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

		void OnClientDiscoveryResponse(IPEndPoint endpoint, string text)
		{
			var ip = endpoint.Address.ToString();
			var port = (ushort)endpoint.Port;
			(_manager as LiteNetLib4MirrorNetworkManager)?.StartClient(ip, port);
			_noDiscovering = true;
		}
	}
}
