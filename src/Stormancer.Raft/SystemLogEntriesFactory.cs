using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Raft
{
    internal class NoOpRecord : IRecord<NoOpRecord>
    {
        public static bool TryRead(ReadOnlySequence<byte> buffer, [NotNullWhen(true)] out NoOpRecord? record, out int length)
        {
            throw new NotImplementedException();
        }

        public int GetLength()
        {
            return 0;
        }

        public bool TryWrite(ref Span<byte> buffer, out int length)
        {
            length = 0;
            return true;
        }
    }

    internal class SystemLogEntriesFactory : IIntegerRecordTypeLogEntryFactory
    {
        private static readonly (int Id, Type recordType)[] _recordTypes = [(1, typeof(NoOpRecord)), (2, typeof(ShardsConfigurationRecord))];
        public IEnumerable<(int Id, Type RecordType)> GetMetadata() => _recordTypes;

        public bool TryRead(ulong id, ulong term,int recordTypeId, ReadOnlySequence<byte> buffer, [NotNullWhen(true)] out LogEntry? entry, out int length)
        {
         
            switch(recordTypeId)
            {
                case 1:
                    entry = new LogEntry(id,term,new NoOpRecord());
                    length = 0;
                    return true;
                case 2:

                    if (ShardsConfigurationRecord.TryRead(buffer, out var config, out length))
                    {

                        entry = new LogEntry(id, term, config);
                        return true;
                    }
                    else
                    {
                        entry = null;
                        return false;
                    }
                default:
                    length = 0;
                    entry = null;
                    return false;
            }

        }
        public int GetLength(LogEntry entry)
        {
            return entry.Record.GetLength();
        }
        public bool TryWriteContent(ref Span<byte> buffer, LogEntry entry, out int length)
        {
            if (entry.Record is NoOpRecord)
            {
                length = 0;
                return true;
            }
            else if (entry.Record is ShardsConfigurationRecord)
            {
                entry.Record.TryWrite(ref buffer, out length);

                return true;

            }
            else
            {
                length = 0;
                return false;
            }
        }
    }
}
