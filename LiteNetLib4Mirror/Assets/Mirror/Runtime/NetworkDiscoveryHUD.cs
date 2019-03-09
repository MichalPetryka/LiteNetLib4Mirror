﻿using System.Collections;
using System.ComponentModel;
using System.Net;
using UnityEngine;

namespace Mirror.LiteNetLib4Mirror
{
	[RequireComponent(typeof(LiteNetLib4MirrorNetworkManager))]
	[RequireComponent(typeof(NetworkManagerHUD))]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public class NetworkDiscoveryHUD : MonoBehaviour
	{
		[SerializeField] private float discoveryInterval = 1f;
		private LiteNetLib4MirrorNetworkManager _manager;
		private NetworkManagerHUD _managerHud;
		private bool _noDiscovering = true;
		private bool _existsDiscovery = false;

		private void Awake()
		{
			_manager = GetComponent<LiteNetLib4MirrorNetworkManager>();
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

		private void OnGUI()
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
			var ip = endpoint.Address.ToString();
			var port = (ushort)endpoint.Port;
			_manager.StartClient(ip, port);
			_noDiscovering = true;
		}
	}
}