using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

namespace Calamari.Common.Plumbing.Extensions
{
    public static class EncodingDetectingFileReader
    {
        public static readonly ReadOnlyCollection<Encoding> DefaultEncodingsToTry;

        static EncodingDetectingFileReader()
        {
#if NETSTANDARD
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // Required to use code pages in .NET Standard
#endif
            DefaultEncodingsToTry = new List<Encoding>
            {
                new UTF8Encoding(false, true),
                Encoding.GetEncoding("windows-1252",
                                     EncoderFallback.ExceptionFallback /* Detect problems if re-used for output */,
                                     DecoderFallback.ReplacementFallback)
            }.AsReadOnly();
        }

        public static (string text, Encoding encoding) ReadToEnd(string path)
        {
            return ReadToEnd(path, DefaultEncodingsToTry.ToArray());
        }

        public static (string text, Encoding encoding) ReadToEnd(string path, params Encoding[] encodingsToTry)
        {
            if (encodingsToTry.Length < 1)
                throw new Exception("No encodings specified.");

            if (encodingsToTry.Take(encodingsToTry.Length - 1)
                              .FirstOrDefault(DecoderDoesNotRaiseErrorsForUnsupportedCharacters) is { } e)
                throw new Exception($"The supplied encoding '{e}' does not raise errors for unsupported characters, so the subsequent "
                                    + "encoder will never be used. Please set DecoderFallback to ExceptionFallback or use Unicode.");

            var bytes = File.ReadAllBytes(path);

            Exception lastException = null;
            foreach (var encoding in encodingsToTry)
                try
                {
                    using (var stream = new MemoryStream(bytes))
                    using (var reader = new StreamReader(stream, encoding))
                    {
                        return (reader.ReadToEnd(), reader.CurrentEncoding);
                    }
                }
                catch (DecoderFallbackException ex)
                {
                    lastException = ex;
                }

            throw new Exception("Unable to decode file contents with the specified encodings.", lastException);
        }

        public static bool DecoderDoesNotRaiseErrorsForUnsupportedCharacters(Encoding encoding)
        {
            return encoding.DecoderFallback != DecoderFallback.ExceptionFallback
                   && !encoding.WebName.StartsWith("utf-")
                   && !encoding.WebName.StartsWith("unicode")
                   && !encoding.WebName.StartsWith("ucs-");
        }
    }
}