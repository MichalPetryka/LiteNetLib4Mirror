using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    public static class NetworkWriterPool
    {
        static readonly Stack<NetworkWriter> pool = new Stack<NetworkWriter>();

        public static NetworkWriter GetWriter()
        {
            if (pool.Count != 0)
            {
                NetworkWriter writer = pool.Pop();
                // reset cached writer length and position
                writer.pooled = false;
                writer.SetLength(0);
                return writer;
            }

            return new NetworkWriter(true);
        }

        public static void Recycle(NetworkWriter writer)
        {
            if (writer == null)
            {
                Debug.LogWarning("Recycling null writers is not allowed, please check your code!");
                return;
            }
            if (writer.reusable && !writer.pooled)
            {
                writer.pooled = true;
                writer.validCache = false;
                pool.Push(writer);
            }
        }
    }
}
