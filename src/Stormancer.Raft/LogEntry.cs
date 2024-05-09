﻿using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Raft
{
    public enum ReplicatedLogEntryType
    {
        SystemClusterConfiguration,
        NoOp,
        Content
    }

    public class LogEntry
    {
        public LogEntry(ulong id, ulong term, IRecord record)
        {
            Id = id;
            Term = term;
            Record = record;
        }

        public ulong Id { get; }
        public ulong Term { get; }
        public IRecord Record { get; }


    }
    public interface ILogEntryReaderWriter
    {
        bool TryRead(ulong id, ulong term, ReadOnlySequence<byte> buffer, [NotNullWhen(true)] out LogEntry? entry, out int length);
        bool TryWriteContent(ref Span<byte> buffer, LogEntry entry, out int length);

        int GetContentLength(LogEntry entry);

    }
    public class IntegerRecordTypeLogEntryReaderWriter : ILogEntryReaderWriter
    {
        private readonly Dictionary<int, IIntegerRecordTypeLogEntryFactory> _idHandlers = new Dictionary<int, IIntegerRecordTypeLogEntryFactory>();
        private readonly Dictionary<Type, (IIntegerRecordTypeLogEntryFactory factory,int recordId)> _typeHandlers = new();

        public IntegerRecordTypeLogEntryReaderWriter(IEnumerable<IIntegerRecordTypeLogEntryFactory> factories)
        {
            Initialize(factories);
        }

        private void Initialize(IEnumerable<IIntegerRecordTypeLogEntryFactory> factories)
        {
            foreach(var factory in factories)
            {
                foreach(var (id,type) in factory.GetMetadata())
                {
                    _idHandlers.Add(id, factory);
                    _typeHandlers.Add(type,( factory,id));
                }
            }
        }

        public bool TryRead(ulong id, ulong term, ReadOnlySequence<byte> buffer, [NotNullWhen(true)] out LogEntry? entry, out int length)
        {
            if(buffer.Length < 8)
            {
                entry = null;
                length = 8;
                return false;
            }

            Span<byte> b = stackalloc byte[4];
            buffer.CopyTo(b);
            BinaryPrimitives.TryReadInt32BigEndian(b, out var recordTypeId);
            buffer.Slice(4, 4).CopyTo(b);
            BinaryPrimitives.TryReadInt32BigEndian(b, out var contentLength);
            length = contentLength + 8;
            if (_idHandlers.TryGetValue(recordTypeId, out var handler))
            {
                return handler.TryRead(id, term, recordTypeId, buffer.Slice(8), out entry, out _);
               
            }
            else
            {
                entry = null;
                return false;
            }


            
        }

        public bool TryWriteContent(ref Span<byte> buffer, LogEntry entry, out int length)
        {
            if(_typeHandlers.TryGetValue(entry.Record.GetType(),out var tuple))
            {
                var (factory, recordType) = tuple;
                BinaryPrimitives.TryWriteInt32BigEndian(buffer, recordType);
                length = GetContentLength(entry);
                BinaryPrimitives.TryWriteInt32BigEndian(buffer.Slice(4),length);

                var contentSpan = buffer.Slice(8);
                return factory.TryWriteContent(ref contentSpan, entry, out _);
            }
            else
            {
               
                length = 0;
                return false;
            }
        }

        public int GetContentLength(LogEntry entry)
        {
            return entry.Record.GetLength() + 8;
        }
    }

    public interface IIntegerRecordTypeLogEntryFactory
    {
        public IEnumerable<(int Id, Type RecordType)> GetMetadata();

        public bool TryRead(ulong id, ulong term, int recordType, ReadOnlySequence<byte> buffer, [NotNullWhen(true)] out LogEntry? entry, out int length);

        public bool TryWriteContent(ref Span<byte> buffer, LogEntry entry, out int length);


    }




    public interface IReplicatedLogEntry<T> where T : IReplicatedLogEntry<T>
    {

        int GetLength();
        bool TryWrite(Span<byte> buffer, out int length);


        ulong Id { get; }

        ulong Term { get; }

        ReplicatedLogEntryType Type { get; }



        /// <summary>
        /// If the entry is of type ClusterConfiguration, returns the stored <see cref="ShardsConfigurationRecord"/>
        /// </summary>
        /// <returns></returns>
        TContent? As<TContent>() where TContent : IRecord<TContent>;
        static abstract bool TryRead(ulong id, ulong term, ReadOnlySpan<byte> content, [NotNullWhen(true)] out T? value);


        static abstract T CreateSystem<TContent>(ulong id, ulong term, ReplicatedLogEntryType type, IRecord content);

    }

    public interface ISerializedEntry
    {
        ulong Id { get; }
        ulong Term { get; }

        TLogEntry ReadAs<TLogEntry>() where TLogEntry : IReplicatedLogEntry<TLogEntry>;
    }
}
