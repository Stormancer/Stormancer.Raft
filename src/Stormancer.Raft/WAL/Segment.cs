using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Raft.WAL
{
    internal struct SegmentHeader
    {
        public SegmentHeader()
        {

        }

        public int Version { get; set; } = 0;
        public ulong FirstEntryId { get; set; } = 1;



        public bool TryRead(ref ReadOnlySpan<byte> buffer, out SegmentHeader header, out int length)
        {
            var version = BinaryPrimitives.ReadInt32BigEndian(buffer);
            length = 8;
            if (buffer.Length < 8)
            {
                header = default;
                return false;
            }


            var firstEntryId = BinaryPrimitives.ReadUInt64BigEndian(buffer);

            header = new SegmentHeader { FirstEntryId = firstEntryId, Version = version };
            return true;
        }

        public int Length => 8;

        public void Write(ref Span<byte> buffer)
        {
            BinaryPrimitives.WriteInt32BigEndian(buffer, Version);
            BinaryPrimitives.WriteUInt64BigEndian(buffer.Slice(4), FirstEntryId);
        }
    }
}
