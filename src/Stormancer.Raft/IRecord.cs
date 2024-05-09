using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Raft
{
    public interface IRecord
    {
        int GetLength();

        bool TryWrite(ref Span<byte> buffer,out int length);
    }
    public interface IRecord<T> : IRecord where T : IRecord<T>
    {


        static abstract bool TryRead(ReadOnlySequence<byte> buffer,[NotNullWhen(true)] out T? record, out int length);
    }
}
