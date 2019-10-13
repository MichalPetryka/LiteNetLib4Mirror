using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public static class NetworkReaderPool
    {
        // reuse all readers, saves tons of memory allocations in hotpath
        static readonly Stack<NetworkReader> readerPool = new Stack<NetworkReader>();

        public static NetworkReader GetReader(byte[] data)
        {
            if (readerPool.Count != 0)
            {
                NetworkReader reader = readerPool.Pop();
                reader.pooled = false;
                // reset cached writer length and position
                reader.SetBuffer(data);
                return reader;
            }

            return new NetworkReader(data, true);
        }

        public static NetworkReader GetReader(ArraySegment<byte> data)
        {
            if (readerPool.Count != 0)
            {
                NetworkReader reader = readerPool.Pop();
                reader.pooled = false;
                // reset cached writer length and position
                reader.SetBuffer(data);
                return reader;
            }

            return new NetworkReader(data, true);
        }

        public static void Recycle(NetworkReader reader)
        {
            if (reader == null)
            {
                Debug.LogWarning("Recycling null readers is not allowed, please check your code!");
                return;
            }

            if (reader.reusable && !reader.pooled)
            {
                reader.pooled = true;
                reader.SetBuffer(default(ArraySegment<byte>));
                readerPool.Push(reader);
            }
        }
    }
}
