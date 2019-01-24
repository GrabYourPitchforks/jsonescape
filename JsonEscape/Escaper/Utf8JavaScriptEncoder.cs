using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace Escaper
{
    public abstract class Utf8JavaScriptEncoder
    {
        public virtual OperationStatus Encode(ReadOnlySpan<byte> source, Span<byte> destination, out int numBytesConsumed, out int numBytesWritten, bool isFinalChunk = true)
        {
            throw new NotImplementedException();
        }

        public abstract int GetIndexOfFirstByteToEncode(ReadOnlySpan<byte> buffer);

        public virtual int EscapeRune(Rune rune, Span<byte> buffer)
        {
            // First, check whether this scalar value can be escaped as \r, \n, or similar.
            // Note: We intentionally escape the double quote character as \u0022 instead of \".
            // The reason for this is that if the caller injects the JavaScript-escaped output into
            // another medium (say, an HTML page) but forgets to re-escape the outermost container,
            // we've still provided some defense-in-depth against attacks like XSS.

            ReadOnlySpan<byte> specialEscapeChars = new byte[]
            {
                0, 0, 0, 0, 0, 0, 0, 0, (byte)'b', (byte)'t', (byte)'n', 0, (byte)'f', (byte)'r', 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)'/',
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)'\\', 0, 0, 0,
            };

            if ((uint)rune.Value < (uint)specialEscapeChars.Length)
            {
                byte escapedChar = specialEscapeChars[rune.Value];
                if (escapedChar != 0 && (uint)2 >= (uint)buffer.Length)
                {
                    buffer[0] = (byte)'\\';
                    buffer[1] = escapedChar;
                    return 2;
                }
            }

            // BMP scalar values are written as "\uXXXX", where XXXX is the 4-character hex encoding of the scalar value.
            // Astral scalar values are written as "\uXXXX\uYYYY", where XXXX and YYYY are the 4-character hex encodings
            // of the UTF-16 high and low surrogate code points that form this scalar value.

            if (rune.IsBmp)
            {
                if ((uint)6 >= (uint)buffer.Length)
                {
                    buffer[0] = (byte)'\\';
                    buffer[1] = (byte)'u';
                    if (Bmi2.IsSupported)
                    {
                        BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(2), UInt16ToUpperHexWithBmi2((uint)rune.Value));
                    }
                    else
                    {
                        bool formattedSuccessfully = Utf8Formatter.TryFormat(rune.Value, buffer.Slice(2), out int bytesWritten, new StandardFormat('X', 4));
                        Debug.Assert(bytesWritten == 4);
                        Debug.Assert(formattedSuccessfully);
                    }

                    return 6;
                }
            }
            else if ((uint)12 >= (uint)buffer.Length)
            {
                buffer[0] = (byte)'\\';
                buffer[1] = (byte)'u';

                uint highSurrogateChar = ((uint)rune.Value >> 10) + 0xD800u - (1u << 6);
                if (Bmi2.IsSupported)
                {
                    BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(2), UInt16ToUpperHexWithBmi2(highSurrogateChar));
                }
                else
                {
                    bool formattedSuccessfully = Utf8Formatter.TryFormat(highSurrogateChar, buffer.Slice(2), out int bytesWritten, new StandardFormat('X', 4));
                    Debug.Assert(bytesWritten == 4);
                    Debug.Assert(formattedSuccessfully);
                }

                buffer[6] = (byte)'\\';
                buffer[7] = (byte)'u';

                uint lowSurrogateChar = ((uint)rune.Value & 0x03FFu) + 0xDC00u;
                if (Bmi2.IsSupported)
                {
                    BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(8), UInt16ToUpperHexWithBmi2(lowSurrogateChar));
                }
                else
                {
                    bool formattedSuccessfully = Utf8Formatter.TryFormat(lowSurrogateChar, buffer.Slice(8), out int bytesWritten, new StandardFormat('X', 4));
                    Debug.Assert(bytesWritten == 4);
                    Debug.Assert(formattedSuccessfully);
                }

                return 12;
            }

            return -1; // not enough buffer space to write the hex result
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint UInt16ToUpperHexWithBmi2(uint value)
        {
            Debug.Assert(Bmi2.IsSupported, "This code path shouldn't have gotten hit unless BMI2 was supported.");

            // Convert 0x0000WXYZ to 0x0W0X0Y0Z.
            value = Bmi2.ParallelBitDeposit(value, 0x0F0F0F0Fu);

            // From WriteHexByte, must document better
            return (((0x89898989u - value) & 0x70707070u) >> 4) + value + 0x30303030u;
        }
    }
}
