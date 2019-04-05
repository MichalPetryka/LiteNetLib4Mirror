using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LiteNetLib.Utils;
using LiteNetLib4Mirror.Open.Nat;
using UnityEngine;
using UnityEngine.Events;

namespace Mirror.LiteNetLib4Mirror
{
	[Serializable] public class UnityEventError : UnityEvent<SocketError> { }
	[Serializable] public class UnityEventIntError : UnityEvent<int, SocketError> { }
	[Serializable] public class UnityEventIpEndpointString : UnityEvent<IPEndPoint, string> { }
	public static class LiteNetLib4MirrorUtils
	{
		internal static ushort LastForwardedPort;
		public static bool UpnpFailed { get; private set; }

		public static void LogException(Exception exception)
		{
			Debug.LogException(exception);
		}

		public static string ToBase64(string text)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
		}

		public static string FromBase64(string text)
		{
			return Encoding.UTF8.GetString(Convert.FromBase64String(text));
		}

		public static NetDataWriter ReusePut(NetDataWriter writer, string text, ref string lastText)
		{
			if (text != lastText)
			{
				lastText = text;
				writer.Reset();
				writer.Put(ToBase64(text));
			}

			return writer;
		}

		public static NetDataWriter ReusePutDiscovery(NetDataWriter writer, string text, ref string lastText)
		{
			if (text != lastText)
			{
				string application = Application.productName;
				lastText = application + text;
				writer.Reset();
				writer.Put(application);
				writer.Put(ToBase64(text));
			}

			return writer;
		}

		public static string Concatenate(params string[] array)
		{
			StringBuilder sb = new StringBuilder();
			for (int index = 0; index < array.Length; index++) sb.Append(array[index]);

			return sb.ToString();
		}

		public static IPAddress Parse(string address)
		{
			switch (address)
			{
				case "0.0.0.0":
					return IPAddress.Any;
				case "0:0:0:0:0:0:0:0":
				case "::":
					return IPAddress.IPv6Any;
				case "localhost":
				case "127.0.0.1":
					return IPAddress.Loopback;
				case "0:0:0:0:0:0:0:1":
				case "::1":
					return IPAddress.IPv6Loopback;
			}

			return IPAddress.TryParse(address, out IPAddress ipAddress) ? ipAddress : Dns.GetHostAddresses(address)[0];
		}

		public static IPEndPoint Parse(string address, ushort port)
		{
			return new IPEndPoint(Parse(address), port);
		}

		/// <summary>
		/// Utility function for getting first free port in range (as a bonus, should work if unity doesn't shit itself)
		/// </summary>
		/// <param name="ports">Available ports</param>
		/// <returns>First free port in range</returns>
		public static ushort GetFirstFreePort(params ushort[] ports)
		{
			if (ports == null || ports.Length == 0) throw new Exception("No ports provided");
			ushort freeport = ports.Except(Array.ConvertAll(IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners(), p => (ushort)p.Port)).FirstOrDefault();
			if (freeport == 0) throw new Exception("No free port!");
			return freeport;
		}

#pragma warning disable 4014
		public static void ForwardPort(NetworkProtocolType networkProtocolType = NetworkProtocolType.Udp, int milisecondsDelay = 10000)
		{
			ForwardPortInternalAsync(LiteNetLib4MirrorTransport.Singleton.port, milisecondsDelay, networkProtocolType);
		}

		public static void ForwardPort(ushort port, NetworkProtocolType networkProtocolType = NetworkProtocolType.Udp, int milisecondsDelay = 10000)
		{
			ForwardPortInternalAsync(port, milisecondsDelay, networkProtocolType);
		}
#pragma warning restore 4014

		private static async Task ForwardPortInternalAsync(ushort port, int milisecondsDelay, NetworkProtocolType networkProtocolType = NetworkProtocolType.Udp)
		{
			try
			{
				if (LastForwardedPort == port || UpnpFailed) return;
				if (LastForwardedPort != 0)
				{
					NatDiscoverer.ReleaseAll();
				}
				NatDiscoverer discoverer = new NatDiscoverer();
				NatDevice device;
				using (CancellationTokenSource cts = new CancellationTokenSource(milisecondsDelay))
				{
					device = await discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts).ConfigureAwait(false);
				}

				await device.CreatePortMapAsync(new Mapping(networkProtocolType, port, port, "LiteNetLib4Mirror UPnP")).ConfigureAwait(false);
				LastForwardedPort = port;
				Debug.Log("Port forwarded successfully!");
			}
			catch
			{
				Debug.LogWarning("UPnP failed!");
				UpnpFailed = true;
			}
		}
	}
}
