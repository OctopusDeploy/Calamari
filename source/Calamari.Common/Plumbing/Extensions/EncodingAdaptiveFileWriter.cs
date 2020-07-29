using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Calamari.Common.Plumbing.Extensions
{
    public static class EncodingAdaptiveFileWriter
    {
        public static void Write(string path, Func<string> getText, params Encoding[] encodingsToTry)
        {
            if (encodingsToTry.Length < 1)
                throw new Exception("No encodings specified.");

            if (encodingsToTry.Take(encodingsToTry.Length - 1)
                              .FirstOrDefault(EncoderDoesNotRaiseErrorsForUnsupportedCharacters) is { } e)
                throw new Exception($"The supplied encoding '{e}' does not raise errors for unsupported characters, so the subsequent "
                                    + "encoder will never be used. Please set DecoderFallback to ExceptionFallback or use Unicode.");

            var text = getText();

            byte[] bytes = null;
            Exception lastException = null;
            foreach (var encoding in encodingsToTry)
                try
                {
                    bytes = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
                    break;
                }
                catch (EncoderFallbackException ex)
                {
                    lastException = ex;
                }

            if (bytes == null)
            {
                throw new Exception("Unable to encode text with the specified encodings.", lastException);
            }

            File.WriteAllBytes(path, bytes);
        }

        public static void Write(string path, Action<TextWriter> writeToWriter, params Encoding[] encodingsToTry)
        {
            Write(path,
                  () =>
                  {
                      using (var textWriter = new StringWriter())
                      {
                          writeToWriter(textWriter);
                          textWriter.Close();
                          return textWriter.ToString();
                      }
                  },
                  encodingsToTry);
        }

        public static bool EncoderDoesNotRaiseErrorsForUnsupportedCharacters(Encoding encoding)
        {
            return encoding.EncoderFallback != EncoderFallback.ExceptionFallback
                && !encoding.WebName.StartsWith("utf-")
                && !encoding.WebName.StartsWith("unicode")
                && !encoding.WebName.StartsWith("ucs-");
        }
    }
}