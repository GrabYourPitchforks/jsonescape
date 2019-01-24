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
        private readonly bool _replaceInvalidSequences;

        protected Utf8JavaScriptEncoder(bool replaceInvalidSequences)
        {
            _replaceInvalidSequences = replaceInvalidSequences;
        }

        public int MaxOutputBytesPerRune => 12;

        public virtual OperationStatus Encode(ReadOnlySpan<byte> source, Span<byte> destination, out int numBytesConsumed, out int numBytesWritten, bool isFinalChunk = true)
        {
            int tempNumBytesConsumed = 0;
            int tempNumBytesWritten = 0;

            while (!source.IsEmpty)
            {
                // First, run over the source buffer looking for the index of the first byte to encode.
                // We'll memcpy as a single chunk all bytes that don't need to be encoded. To ensure the
                // memcpy succeeds, and to ensure we don't inadvertently split a multi-byte UTF-8 subsequence,
                // we need to truncate the source buffer temporarily.

                {
                    ReadOnlySpan<byte> sourceToScan = source;
                    if (sourceToScan.Length > destination.Length)
                    {
                        sourceToScan = sourceToScan.Slice(0, destination.Length);
                    }

                    int numBytesToMemcpy = GetIndexOfFirstByteToEncode(sourceToScan);
                    if (numBytesToMemcpy < 0)
                    {
                        numBytesToMemcpy = sourceToScan.Length;
                    }

                    sourceToScan.CopyTo(destination);
                    source = source.Slice(numBytesToMemcpy);
                    destination = destination.Slice(numBytesToMemcpy);

                    tempNumBytesConsumed += numBytesToMemcpy;
                    tempNumBytesWritten += numBytesToMemcpy;
                }

                // Quick check - did we consume all bytes from the source buffer?

                if (source.IsEmpty)
                {
                    break;
                }

                // Decode the first scalar value from the input buffer. If it's an error, we'll fix it up later.

                OperationStatus runeDecodeStatus = MissingRuneApis.DecodeFirstRuneFromUtf8(source, out Rune decodedRune, out int runeLengthInBytes);
                if (runeDecodeStatus != OperationStatus.Done)
                {
                    Debug.Assert(runeDecodeStatus == OperationStatus.NeedMoreData || runeDecodeStatus == OperationStatus.InvalidData);

                    // If the data was incomplete (but valid), return that same status to our caller
                    // only if the caller told us to expect more data.

                    if (!isFinalChunk && runeDecodeStatus == OperationStatus.NeedMoreData)
                    {
                        goto NeedsMoreData;
                    }

                    // We'll treat this as an error, which means we either fix it up on-the-fly (as U+FFFD)
                    // or we return failure to our callers.

                    if (!_replaceInvalidSequences)
                    {
                        goto InvalidData;
                    }

                    decodedRune = Rune.ReplacementChar;
                }

                // We've obtained the specific scalar value we need to escape; escape it now.

                int escapedBytesWrittenThisIteration = EscapeRune(decodedRune, destination);
                if (escapedBytesWrittenThisIteration < 0)
                {
                    goto DestinationTooSmall;
                }

                // When we bump the number of bytes consumed, we want to use the length returned by the "decode first rune"
                // method, not Rune.Utf8SequenceLength. The reason is that when we see invalid data and replace it with
                // U+FFFD in the output, calling Rune.Utf8SequenceLength on U+FFFD will always return 3 bytes, but the amount
                // of invalid data we're skipping from the source buffer might have been of a different length.

                tempNumBytesConsumed += runeLengthInBytes;
                source = source.Slice(runeLengthInBytes);

                tempNumBytesWritten += escapedBytesWrittenThisIteration;
                destination = destination.Slice(escapedBytesWrittenThisIteration);
            }

            // If we fell out of the loop, we're done!

            OperationStatus retVal = OperationStatus.Done;

        ReturnCommon:
            numBytesConsumed = tempNumBytesConsumed;
            numBytesWritten = tempNumBytesWritten;
            return retVal;

        InvalidData:
            retVal = OperationStatus.InvalidData;
            goto ReturnCommon;

        DestinationTooSmall:
            retVal = OperationStatus.DestinationTooSmall;
            goto ReturnCommon;

        NeedsMoreData:
            retVal = OperationStatus.NeedMoreData;
            goto ReturnCommon;
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
