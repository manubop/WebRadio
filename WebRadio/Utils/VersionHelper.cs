using System;
using System.Buffers.Binary;

namespace WebRadio.Utils
{
    internal sealed class VersionHelper
    {
        public static Version GetVersion(int v)
        {
            var dest = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(dest, v);
            return new Version(dest[0], dest[1], dest[2], dest[3]);
        }
    }
}
