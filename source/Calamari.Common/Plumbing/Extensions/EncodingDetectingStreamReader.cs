using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Calamari.Common.Plumbing.Extensions
{
    public static class EncodingDetectingFileReader
    {
        static EncodingDetectingFileReader()
        {
#if NETSTANDARD
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); // Required to use code pages in .NET Standard
#endif
        }

        public static (string text, Encoding encoding) ReadToEnd(string path)
        {
            return ReadToEnd(path,
                             new UTF8Encoding(false, true),
                             Encoding.GetEncoding(1252));
        }

        public static (string text, Encoding encoding) ReadToEnd(string path, params Encoding[] encodingsToTry)
        {
            if (encodingsToTry.Length < 1)
                throw new Exception("No encodings specified.");

            if (encodingsToTry.Take(encodingsToTry.Length - 1)
                              .Any(encoding => encoding.DecoderFallback != DecoderFallback.ExceptionFallback))
            {
                throw new Exception("Encodings prior to the last must have exception fallback enabled.");
            }

            Exception lastException = null;
            foreach (var encoding in encodingsToTry)
            {
                try
                {
                    using (var reader = new StreamReader(path, encoding))
                        return (reader.ReadToEnd(), reader.CurrentEncoding);
                }
                catch (DecoderFallbackException ex)
                {
                    lastException = ex;
                }
            }

            Debug.Assert(lastException != null, nameof(lastException) + " != null");
            throw lastException;
        }
    }
}