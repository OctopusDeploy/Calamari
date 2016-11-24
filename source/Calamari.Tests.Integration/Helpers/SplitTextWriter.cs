using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Calamari.Tests.Integration.Helpers
{
    public class SplitTextWriter : TextWriter
    {
        private readonly List<TextWriter> writers;

        public SplitTextWriter(params TextWriter[] writers)
        {
            if (!writers.Any())
                throw new InvalidOperationException("You must supply at least one TextWriter.");

            this.writers = writers.ToList();
        }

        public override Encoding Encoding
        {
            get
            {
                var encoding = writers[0].Encoding;
                for (var i = 1; i <= writers.Count; i++)
                {
                    if (writers[i].Encoding.Equals(encoding))
                    {
                        throw new InvalidOperationException(
                            "Multiple writers have different encodings so this SplitTextWriter does not represent a single Encoding type.");
                    }
                }
                return encoding;
            }
        }

#if net40
        public override void Close()
        {
            writers.ForEach(writer => writer.Close());
        }
#endif


        /// <devdoc>
        ///    <para>
        ///       Writes a string
        ///       to the text stream.
        ///    </para>
        /// </devdoc>
        public override void Write(string s)
        {
            writers.ForEach(writer => writer.Write(s));
        }

        /// <devdoc>
        ///    <para>
        ///       Writes the text representation of a Boolean value to the text stream.
        ///    </para>
        /// </devdoc>
        public override void Write(bool value)
        {
            writers.ForEach(writer => writer.Write(value));
        }

        /// <devdoc>
        ///    <para>
        ///       Writes a character to the text stream.
        ///    </para>
        /// </devdoc>
        public override void Write(char value)
        {
            writers.ForEach(writer => writer.Write(value));
        }

        /// <devdoc>
        ///    <para>
        ///       Writes a
        ///       character array to the text stream.
        ///    </para>
        /// </devdoc>
        public override void Write(char[] buffer)
        {
            writers.ForEach(writer => writer.Write(buffer));
        }

        /// <devdoc>
        ///    <para>
        ///       Writes a subarray
        ///       of characters to the text stream.
        ///    </para>
        /// </devdoc>
        public override void Write(char[] buffer, int index, int count)
        {
            writers.ForEach(writer => writer.Write(buffer, index, count));
        }

        /// <devdoc>
        ///    <para>
        ///       Writes the text representation of a Double to the text stream.
        ///    </para>
        /// </devdoc>
        public override void Write(double value)
        {
            writers.ForEach(writer => writer.Write(value));
        }

        /// <devdoc>
        ///    <para>
        ///       Writes the text representation of
        ///       a Single to the text
        ///       stream.
        ///    </para>
        /// </devdoc>
        public override void Write(float value)
        {
            writers.ForEach(writer => writer.Write(value));
        }

        /// <devdoc>
        ///    <para>
        ///       Writes the text representation of an integer to the text stream.
        ///    </para>
        /// </devdoc>
        public override void Write(int value)
        {
            writers.ForEach(writer => writer.Write(value));
        }

        /// <devdoc>
        ///    <para>
        ///       Writes the text representation of an 8-byte integer to the text stream.
        ///    </para>
        /// </devdoc>
        public override void Write(long value)
        {
            writers.ForEach(writer => writer.Write(value));
        }

        /// <devdoc>
        ///    <para>
        ///       Writes the text representation of an object
        ///       to the text stream.
        ///    </para>
        /// </devdoc>
        public override void Write(object value)
        {
            writers.ForEach(writer => writer.Write(value));
        }

        /// <devdoc>
        ///    <para>
        ///       Writes out a formatted string, using the same semantics as specified.
        ///    </para>
        /// </devdoc>
        public override void Write(string format, object arg0)
        {
            writers.ForEach(writer => writer.Write(format, arg0));
        }

        /// <devdoc>
        ///    <para>
        ///       Writes out a formatted string,
        ///       using the same semantics as specified.
        ///    </para>
        /// </devdoc>
        public override void Write(string format, object arg0, object arg1)
        {
            writers.ForEach(writer => writer.Write(format, arg0, arg1));
        }

        /// <devdoc>
        ///    <para>
        ///       Writes out a formatted string,
        ///       using the same semantics as specified.
        ///    </para>
        /// </devdoc>
        public override void Write(string format, params object[] arg)
        {
            writers.ForEach(writer => writer.Write(format, arg));
        }

        /// <devdoc>
        ///    <para>
        ///       Writes the specified
        ///       string to a line without tabs.
        ///    </para>
        /// </devdoc>
        public void WriteLineNoTabs(string s)
        {
            writers.ForEach(writer => writer.WriteLine(s));
        }

        /// <devdoc>
        ///    <para>
        ///       Writes the specified string followed by
        ///       a line terminator to the text stream.
        ///    </para>
        /// </devdoc>
        public override void WriteLine(string s)
        {
            writers.ForEach(writer => writer.WriteLine(s));
        }

        /// <devdoc>
        ///    <para>
        ///       Writes a line terminator.
        ///    </para>
        /// </devdoc>
        public override void WriteLine()
        {
            writers.ForEach(writer => writer.WriteLine());
        }

        /// <devdoc>
        ///    <para>
        ///       Writes the text representation of a Boolean followed by a line terminator to
        ///       the text stream.
        ///    </para>
        /// </devdoc>
        public override void WriteLine(bool value)
        {
            writers.ForEach(writer => writer.WriteLine(value));
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public override void WriteLine(char value)
        {
            writers.ForEach(writer => writer.WriteLine(value));
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public override void WriteLine(char[] buffer)
        {
            writers.ForEach(writer => writer.WriteLine(buffer));
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public override void WriteLine(char[] buffer, int index, int count)
        {
            writers.ForEach(writer => writer.WriteLine(buffer, index, count));
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public override void WriteLine(double value)
        {
            writers.ForEach(writer => writer.WriteLine(value));
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public override void WriteLine(float value)
        {
            writers.ForEach(writer => writer.WriteLine(value));
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public override void WriteLine(int value)
        {
            writers.ForEach(writer => writer.WriteLine(value));
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public override void WriteLine(long value)
        {
            writers.ForEach(writer => writer.WriteLine(value));
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public override void WriteLine(object value)
        {
            writers.ForEach(writer => writer.WriteLine(value));
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public override void WriteLine(string format, object arg0)
        {
            writers.ForEach(writer => writer.WriteLine(format, arg0));
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public override void WriteLine(string format, object arg0, object arg1)
        {
            writers.ForEach(writer => writer.WriteLine(format, arg0, arg1));
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public override void WriteLine(string format, params object[] arg)
        {
            writers.ForEach(writer => writer.WriteLine(format, arg));
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public override void WriteLine(UInt32 value)
        {
            writers.ForEach(writer => writer.WriteLine(value));
        }

        public override void WriteLine(ulong value)
        {
            writers.ForEach(writer => writer.WriteLine(value));
        }

        public override void WriteLine(string format, object arg0, object arg1, object arg2)
        {
            writers.ForEach(writer => writer.WriteLine(format, arg0, arg1, arg2));
        }

        public override void Write(string format, object arg0, object arg1, object arg2)
        {
            writers.ForEach(writer => writer.Write(format, arg0, arg1, arg2));
        }

        public override void Write(decimal value)
        {
            writers.ForEach(writer => writer.Write(value));
        }

        public override void WriteLine(decimal value)
        {
            writers.ForEach(writer => writer.WriteLine(value));
        }

        public override void Flush()
        {
            writers.ForEach(writer => writer.Flush());
        }
    }
}