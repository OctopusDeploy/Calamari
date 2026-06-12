using System;
using System.Text;

namespace Calamari.AiAgent
{
    /// <summary>
    /// Buffers streamed text chunks and invokes a callback for each complete line.
    /// Call <see cref="Append"/> as chunks arrive, and <see cref="Flush"/> when the
    /// stream ends to emit any remaining partial line.
    /// </summary>
    public class LineBuffer
    {
        readonly Action<string> onLine;
        readonly StringBuilder buffer = new();

        public LineBuffer(Action<string> onLine)
        {
            this.onLine = onLine;
        }

        public void Append(string text)
        {
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '\n')
                {
                    onLine(buffer.ToString());
                    buffer.Clear();
                }
                else if (c != '\r')
                {
                    buffer.Append(c);
                }
            }
        }

        public void Flush()
        {
            if (buffer.Length > 0)
            {
                onLine(buffer.ToString());
                buffer.Clear();
            }
        }
    }
}
