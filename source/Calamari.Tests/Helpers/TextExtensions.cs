using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Calamari.Common.Plumbing.Extensions;

namespace Calamari.Tests.Helpers
{
    public static class TextExtensions
    {
        static readonly byte BytePeriod = Encoding.ASCII.GetBytes(".")[0];
        static readonly byte ByteCaret = Encoding.ASCII.GetBytes("^")[0];
        static readonly byte ByteQuestionMark = Encoding.ASCII.GetBytes("?")[0];

        public static string ToReadableHexDump(this IEnumerable<byte> byteSequence)
        {
            const int blockLength = 8;
            return byteSequence
                   .Select((byt, index) => (byt, index))
                   .GroupBy(pair => pair.index / blockLength,
                            pair => pair.byt,
                            (key, bytes) => bytes.ToArray())
                   .Select(bytes => BitConverter.ToString(bytes).Replace('-', ' ').PadRight(blockLength * 3 + 2)
                                    + bytes.ToPrintableAsciiString())
                   .Join(Environment.NewLine);
        }

        public static string ToPrintableAsciiString(this IEnumerable<byte> bytes)
        {
            return Encoding.ASCII.GetString(bytes.Select(byt => byt >= 0x20 && byt <= 0x7E
                                                             ? byt
                                                             : byt == 0x00
                                                                 ? BytePeriod
                                                                 : byt == 0x0D || byt == 0x0A
                                                                     ? ByteCaret
                                                                     : ByteQuestionMark)
                                                 .ToArray());
        }
    }
}