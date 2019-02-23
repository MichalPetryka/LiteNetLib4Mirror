using System;

namespace Mirror.LiteNetLib4MirrorTransport
{
	public interface ISegmentTransport
	{
		bool ServerSend(int connectionId, int channelId, ArraySegment<byte> data);
		bool ClientSend(int channelId, ArraySegment<byte> data);
	}
}
