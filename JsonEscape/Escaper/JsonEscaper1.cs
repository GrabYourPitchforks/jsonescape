using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Escaper
{
    public static class JsonEscaper1
    {
        private const int SCALAR_INVALID = -1;
        private const int SCALAR_INCOMPLETE = -2;

        public static int GetIndexOfFirstByteToEncode(ReadOnlySpan<byte> data)
        {
            // nonzero = allowed, 0 = disallowed
            ReadOnlySpan<byte> allowList = new byte[256] {
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 00 .. 0F
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 10 .. 1F
                1, 1, 0, 1, 1, 1, 0, 0, 1, 1, 1, 0, 1, 1, 1, 1, // 20 .. 2F
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 0, 1, // 30 .. 3F
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // 40 .. 4F
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 1, 1, 1, // 50 .. 5F
                0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, // 60 .. 6F
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, // 70 .. 7F
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 80 .. 8F
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 90 .. 9F
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // A0 .. AF
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // B0 .. BF
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // C0 .. CF
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // D0 .. DF
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // E0 .. EF
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // F0 .. FF
            };

            int idx;
            for (idx = 0; idx < data.Length; idx++)
            {
                // The JIT *should* elide all bounds checking for the below
                // call site. 'idx' is known to be a valid index into 'data'
                // due to the for loop above, and 'data[idx]' is known to
                // be in the range 0..255 since it's typed as byte, which
                // means it should never be out of range of 'allowList'.
                // If the JIT doesn't elide all bounds checking then it's a
                // JIT bug.

                if (allowList[data[idx]] == 0)
                    goto Return;
            }

            idx = -1; // all characters allowed

        Return:
            return idx;
        }

        public static void EscapeNextSubseqeunce(ReadOnlySpan<byte> dataIn, Span<byte> dataOut, out int bytesConsumed, out int bytesWritten)
        {
            ReadOnlySpan<byte> replacementList = new byte[256]
            {
                0, 0, 0, 0, 0, 0, 0, 0, (byte)'b', (byte)'t', (byte)'n', 0, (byte)'f', (byte)'r', 0, 0, // 00 .. 0F
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 10 .. 1F
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 20 .. 2F
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 30 .. 3F
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 40 .. 4F
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)'\\', 0, 0, 0, // 50 .. 5F
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 60 .. 6F
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 70 .. 7F
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 80 .. 8F
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // 90 .. 9F
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // A0 .. AF
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // B0 .. BF
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // C0 .. CF
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // D0 .. DF
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // E0 .. EF
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, // F0 .. FF
            };

            byte firstByte = dataIn[0];
            byte replacementByte = replacementList[firstByte];

            if (replacementByte != 0)
            {
                // If this has an acceptable "\?" encoding, write it out now.

                Debug.Assert(firstByte <= 0x7F, "First byte should be ASCII.");
                dataOut[1] = replacementByte;
                dataOut[0] = (byte)'\\';
                bytesWritten = 2;
                bytesConsumed = 1;
                return;
            }

            if (firstByte <= 0x7F)
            {
                // If this is ASCII, then write out "\u00XX".

                dataOut[5] = (byte)NibbleToHexDigit((uint)firstByte & 0xF);
                dataOut[4] = (byte)NibbleToHexDigit((uint)firstByte >> 4);
                dataOut[0] = (byte)'\\';
                dataOut[1] = (byte)'u';
                dataOut[2] = (byte)'0';
                dataOut[3] = (byte)'0';

                bytesWritten = 6;
                bytesConsumed = 1;
                return;
            }

            uint firstByteMod = (uint)firstByte - 0xC2;

            if (firstByteMod <= 0xDF - 0xC2)
            {
                // This is presumably the start of a two-byte sequence, but need to confirm
                // that the next byte is a valid continuation byte.
                // [ 110yyyyy 10xxxxxx ] -> nibbles [ 0yyy yyxx xxxx ]

                if (dataIn.Length < 2)
                {
                    goto Error1; // second byte of two-byte sequence is missing
                }

                int secondByteSigned = (sbyte)dataIn[1];
                if (secondByteSigned > unchecked((sbyte)0xBF))
                {
                    goto Error1; // second byte of two-byte sequence is invalid
                }

                dataOut[5] = (byte)NibbleToHexDigit((uint)secondByteSigned & 0xF);
                dataOut[3] = (byte)NibbleToHexDigit(((uint)firstByte - 0xC0) >> 2);
            }

        Error1:
            bytesConsumed = 1;
            goto ErrorCommon;

        ErrorCommon:
            dataOut[5] = (byte)'D';
            dataOut[4] = (byte)'F';
            dataOut[3] = (byte)'F';
            dataOut[2] = (byte)'F';
            dataOut[1] = (byte)'u';
            dataOut[0] = (byte)'\\';
            bytesWritten = 6;
            return;
        }

        private static int GetNextScalarValueFromUtf8(ReadOnlySpan<byte> utf8Input, out int bytesConsumed)
        {
            if (utf8Input.IsEmpty)
            {
                goto Error;
            }

            uint firstByte = utf8Input[0];

            // First, check for ASCII.

            if (firstByte <= 0x7Fu)
            {
                bytesConsumed = 1;
                return (int)firstByte;
            }

            // Then, check for the 2-byte header.

            uint firstByteModified = firstByte - 0xC2u; // first byte cannot begin with 80..C1

            if ((byte)firstByteModified <= 0xDFu - 0xC2u)
            {
                // This is presumably the start of a 2-byte sequence, but need to confirm.
                // [ 110yyyyy 10xxxxxx ]

                if (utf8Input.Length < 2)
                {
                    goto Error;
                }

                int secondByteSigned = (sbyte)utf8Input[1];
                if (secondByteSigned > unchecked((sbyte)0xBF))
                {
                    goto Error;
                }

                bytesConsumed = 2;
                return (int)(firstByteModified << 6) + secondByteSigned + (0x02 << 6) + unchecked((sbyte)0x80);
            }

            if ((byte)firstByteModified <= 0xEFu - 0xC2u)
            {
                // This is presumably the start of a 3-byte sequence, but need to confirm.
                // [ 1110zzzz 10yyyyyy 10xxxxxx ]

                if (utf8Input.Length < 3)
                {
                    goto Error;
                }

                int secondByteSigned = (sbyte)utf8Input[1];
                if (secondByteSigned > unchecked((sbyte)0xBF))
                {
                    goto Error;
                }

                // perform overlong check now

                uint firstAndSecondBytes = (firstByteModified << 6) + (uint)secondByteSigned;
                if (firstAndSecondBytes < unchecked(((0xE0u - 0xC2u) << 6) + 0xFFFFFFA0u))
                {
                    goto Error;
                }

                // perform surrogate check now

                if (IsBetweenInclusive(firstAndSecondBytes, unchecked(((0xEDu - 0xC2u) << 6) + 0xFFFFFF9Fu), unchecked(((0xEDu - 0xC2u) << 6) + 0xFFFFFFBFu)))
                {
                    goto Error;
                }

                int thirdByteSigned = (sbyte)utf8Input[2];
                if (thirdByteSigned > unchecked((sbyte)0xBF))
                {
                    goto Error;
                }

                bytesConsumed = 3;
                return (int)(firstAndSecondBytes << 6) + thirdByteSigned + unchecked((0xC2 << 12) - (0xE0 << 12) - ((sbyte)0x80 << 6) - (sbyte)0x80);
            }

            if ((byte)firstByteModified <= 0xF4u - 0xC2u)
            {
                // This is presumably the start of a 4-byte sequence, but need to confirm.
                // [ 11110uuu 10uuzzzz 10yyyyyy 10xxxxxx ]

                if (utf8Input.Length < 4)
                {
                    goto Error;
                }

                int secondByteSigned = (sbyte)utf8Input[1];
                if (secondByteSigned > unchecked((sbyte)0xBF))
                {
                    goto Error;
                }

                // perform overlong and out-of-range check now

                uint firstAndSecondBytes = (firstByteModified << 6) + (uint)secondByteSigned;
                if (!IsBetweenInclusive(firstAndSecondBytes, unchecked(((0xF0u - 0xC2u) << 6) + 0xFFFFFF90u), unchecked(((0xF4u - 0xC2u) << 6) + 0xFFFFFF8Fu)))
                {
                    goto Error;
                }

                int thirdByteSigned = (sbyte)utf8Input[2];
                if (thirdByteSigned > unchecked((sbyte)0xBF))
                {
                    goto Error;
                }

                int fourthByteSigned = (sbyte)utf8Input[3];
                if (fourthByteSigned > unchecked((sbyte)0xBF))
                {
                    goto Error;
                }

                int thirdAndFourthBytes = (thirdByteSigned << 6) + fourthByteSigned;

                bytesConsumed = 4;
                return (int)(firstAndSecondBytes << 12) + thirdAndFourthBytes + unchecked((0xC2 << 18) - (0xF0 << 18) - ((sbyte)0x80 << 12) - ((sbyte)0x80 << 6) - (sbyte)0x80);
            }

        Error:
            return GetNextScalarValueFromUtf8_ErrorHandler(utf8Input, out bytesConsumed);
        }

        private static int GetNextScalarValueFromUtf8_ErrorHandler(ReadOnlySpan<byte> utf8Input, out int bytesConsumed)
        {
            // This is the error handling logic for when we can't read a scalar value from the input because the
            // input does not represent valid UTF-8. It's ok for this logic to be slow since this is an error handling
            // code path; the caller should be optimized for well-formed input.

            // There's a race condition here in that it's possible that the UTF-8 was ill-formed when the fast-path method
            // inspected the data, but between that method running and this error handling method running some other thread
            // may have updated the buffer to contain well-formed data. We're not too worried about detecting this situation
            // since our API contracts require the buffers to be stable, so we'll just return any arbitrary error.

            if (utf8Input.IsEmpty)
            {
                bytesConsumed = 0;
                return SCALAR_INCOMPLETE;
            }

            uint firstByte = utf8Input[0];

            if (firstByte <= 0xC1u)
            {
                // 80..C1 will never begin a valid UTF-8 sequence.

                bytesConsumed = 1;
                return SCALAR_INVALID;
            }

            if (firstByte <= 0xDFu)
            {
                // 2-byte sequence marker

                if (utf8Input.Length < 2)
                {
                    bytesConsumed = 1;
                    return SCALAR_INCOMPLETE;
                }

                // only alternative was that the second byte wasn't a valid continuation byte
                // that's a maximally invalid subsequence of length 1

                bytesConsumed = 1;
                return SCALAR_INVALID;
            }

            if (firstByte <= 0xEFu)
            {
                // 3-byte sequence marker

                if (utf8Input.Length < 2)
                {
                    bytesConsumed = 1;
                    return SCALAR_INCOMPLETE;
                }

                if (firstByte == 0xE0u)
                {
                    if (!IsBetweenInclusive(utf8Input[1], 0xA0u, 0xBFu))
                    {
                        // 3-byte sequence marker not followed by a valid continuation byte (or overlong)
                        // maximally invalid subsequence of length 1
                        bytesConsumed = 1;
                        return SCALAR_INVALID;
                    }
                }
                else if (IsBetweenInclusive(firstByte, 0xE1u, 0xECu))
                {
                    if (!IsBetweenInclusive(utf8Input[1], 0x80u, 0xBFu))
                    {
                        // 3-byte sequence marker not followed by a valid continuation byte
                        // maximally invalid subsequence of length 1
                        bytesConsumed = 1;
                        return SCALAR_INVALID;
                    }
                }
                else if (firstByte == 0xEDu)
                {
                    if (!IsBetweenInclusive(utf8Input[1], 0x80u, 0x9Fu))
                    {
                        // 3-byte sequence marker not followed by a valid continuation byte (or surrogate)
                        // maximally invalid subsequence of length 1
                        bytesConsumed = 1;
                        return SCALAR_INVALID;
                    }
                }
                else
                {
                    if (!IsBetweenInclusive(utf8Input[1], 0x80u, 0xBFu))
                    {
                        // 3-byte sequence marker not followed by a valid continuation byte
                        // maximally invalid subsequence of length 1
                        bytesConsumed = 1;
                        return SCALAR_INVALID;
                    }
                }

                // If we reached this point, the first two bytes are a valid subsequence.
                // Only need to check now that the third (final) byte exists and is a continuation byte.

                if (utf8Input.Length < 3)
                {
                    bytesConsumed = 2;
                    return SCALAR_INCOMPLETE;
                }

                // only alternative was that the third byte wasn't a valid continuation byte
                // that's a maximally invalid subsequence of length 2

                bytesConsumed = 2;
                return SCALAR_INVALID;
            }

            if (firstByte <= 0xF4u)
            {
                // 3-byte sequence marker

                if (utf8Input.Length < 2)
                {
                    bytesConsumed = 1;
                    return SCALAR_INCOMPLETE;
                }

                if (firstByte == 0xF0u)
                {
                    if (!IsBetweenInclusive(utf8Input[1], 0x90u, 0xBFu))
                    {
                        // 4-byte sequence marker not followed by a valid continuation byte (or overlong)
                        // maximally invalid subsequence of length 1
                        bytesConsumed = 1;
                        return SCALAR_INVALID;
                    }
                }
                else if (IsBetweenInclusive(firstByte, 0xF1u, 0xF3u))
                {
                    if (!IsBetweenInclusive(utf8Input[1], 0x80u, 0xBFu))
                    {
                        // 4-byte sequence marker not followed by a valid continuation byte
                        // maximally invalid subsequence of length 1
                        bytesConsumed = 1;
                        return SCALAR_INVALID;
                    }
                }
                else
                {
                    if (!IsBetweenInclusive(utf8Input[1], 0x80u, 0x8Fu))
                    {
                        // 4-byte sequence marker not followed by a valid continuation byte (or out-of-range)
                        // maximally invalid subsequence of length 1
                        bytesConsumed = 1;
                        return SCALAR_INVALID;
                    }
                }

                // If we reached this point, the first two bytes are a valid subsequence.
                // Only need to check now that the third and fourth bytes exists and are continuation bytes.

                if (utf8Input.Length < 3)
                {
                    bytesConsumed = 2;
                    return SCALAR_INCOMPLETE;
                }

                if (!IsBetweenInclusive(utf8Input[2], 0x80u, 0xBFu))
                {
                    // 4-byte sequence marker with two valid starting bytes and a non-continuation third byte
                    // maximally invalid subsequence of length 2
                    bytesConsumed = 2;
                    return SCALAR_INVALID;
                }

                // only alternative was that the fourth byte wasn't a valid continuation byte
                // that's a maximally invalid subsequence of length 3

                bytesConsumed = 3;
                return SCALAR_INVALID;
            }

            // If we got to this point, the first byte of the sequence was F5..FF, which is never valid UTF-8.

            bytesConsumed = 1;
            return SCALAR_INVALID;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint NibbleToHexDigit(uint value)
        {
            Debug.Assert(0 <= value && value <= 15, "Expected a nibble.");

            // branchless implementation below

            uint offset = (value - 10) >> 29; // = 0 if 'value' in [A..F]; = 7 if 'value' in [0..9]
            return value + 55 - offset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsBetweenInclusive(uint value, uint lower, uint upper)
        {
            Debug.Assert(lower <= upper);
            return (value - lower) <= (upper - lower);
        }
    }
}
