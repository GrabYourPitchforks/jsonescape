using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Escaper
{
    public static class MissingRuneApis
    {
        public static OperationStatus DecodeFirstRuneFromUtf8(ReadOnlySpan<byte> buffer, out Rune rune, out int numElementsConsumed)
        {
            throw new NotImplementedException();
        }
    }
}
