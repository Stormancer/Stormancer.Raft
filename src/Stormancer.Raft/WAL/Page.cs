using Microsoft.VisualBasic;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Raft.WAL
{
    internal struct PageHeader
    {
        public PageHeader() { }
        public int Version { get; set; } = 0;
        public byte Type { get; set; } = 0;


        public int Length => 5;

        public void Write(ref Span<byte> buffer)
        {
            BinaryPrimitives.WriteInt32BigEndian(buffer, Version);
            buffer[4] = Type;

        }

        public static bool TryRead(ref ReadOnlySpan<byte> buffer,out PageHeader header, out int read)
        {
            var version = BinaryPrimitives.ReadInt32BigEndian(buffer);
            var length = 5;
            read = length;
            if (buffer.Length < length)
            {
                header = default;
                return false;
            }
            else
            {
                var type  = buffer[4];
                header = new PageHeader() { Type = type, Version = version };
                return true;
            }
        }
    }
}
