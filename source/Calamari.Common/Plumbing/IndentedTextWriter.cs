//Taken from https://github.com/Microsoft/referencesource/blob/d925d870f3cb3f6acdb14e71522ece7054e2233b/System/compmod/system/codedom/compiler/IndentTextWriter.cs
// as this isn't available in .NET Core at this point.
//------------------------------------------------------------------------------
//The MIT License(MIT)

//Copyright(c) Microsoft Corporation

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal 
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is 
//furnished to do so, subject to the following conditions: 

//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software. 

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.                                                           
////------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace Calamari.Common.Plumbing
{
    /// <devdoc>
    ///     <para>Provides a text writer that can indent new lines by a tabString token.</para>
    /// </devdoc>
    public class IndentedTextWriter : TextWriter
    {
        /// <devdoc>
        ///     <para>[To be supplied.]</para>
        /// </devdoc>
        public const string DefaultTabString = "    ";

        int indentLevel;
        bool tabsPending;

        /// <devdoc>
        ///     <para>
        ///     Initializes a new instance of <see cref='System.CodeDom.Compiler.IndentedTextWriter' /> using the specified
        ///     text writer and default tab string.
        ///     </para>
        /// </devdoc>
        public IndentedTextWriter(TextWriter writer)
            : this(writer, DefaultTabString)
        {
        }

        /// <devdoc>
        ///     <para>
        ///     Initializes a new instance of <see cref='System.CodeDom.Compiler.IndentedTextWriter' /> using the specified
        ///     text writer and tab string.
        ///     </para>
        /// </devdoc>
        public IndentedTextWriter(TextWriter writer, string tabString)
            : base(CultureInfo.InvariantCulture)
        {
            InnerWriter = writer;
            TabString = tabString;
            indentLevel = 0;
            tabsPending = false;
        }

        /// <devdoc>
        ///     <para>[To be supplied.]</para>
        /// </devdoc>
        public override Encoding Encoding => InnerWriter.Encoding;

        /// <devdoc>
        ///     <para>
        ///     Gets or sets the new line character to use.
        ///     </para>
        /// </devdoc>
        public override string NewLine
        {
            get => InnerWriter.NewLine;

            set => InnerWriter.NewLine = value;
        }

        /// <devdoc>
        ///     <para>
        ///     Gets or sets the number of spaces to indent.
        ///     </para>
        /// </devdoc>
        public int Indent
        {
            get => indentLevel;
            set
            {
                Debug.Assert(value >= 0, "Bogus Indent... probably caused by mismatched Indent++ and Indent--");
                if (value < 0)
                    value = 0;
                indentLevel = value;
            }
        }

        /// <devdoc>
        ///     <para>
        ///     Gets or sets the TextWriter to use.
        ///     </para>
        /// </devdoc>
        public TextWriter InnerWriter { get; }

        internal string TabString { get; }

        /// <devdoc>
        ///     <para>
        ///     Closes the document being written to.
        ///     </para>
        /// </devdoc>
        public override void Close()
        {
            InnerWriter.Close();
        }

        /// <devdoc>
        ///     <para>[To be supplied.]</para>
        /// </devdoc>
        public override void Flush()
        {
            InnerWriter.Flush();
        }

        /// <devdoc>
        ///     <para>[To be supplied.]</para>
        /// </devdoc>
        protected virtual void OutputTabs()
        {
            if (tabsPending)
            {
                for (var i = 0; i < indentLevel; i++)
                    InnerWriter.Write(TabString);
                tabsPending = false;
            }
        }

        /// <devdoc>
        ///     <para>
        ///     Writes a string
        ///     to the text stream.
        ///     </para>
        /// </devdoc>
        public override void Write(string s)
        {
            OutputTabs();
            InnerWriter.Write(s);
        }

        /// <devdoc>
        ///     <para>
        ///     Writes the text representation of a Boolean value to the text stream.
        ///     </para>
        /// </devdoc>
        public override void Write(bool value)
        {
            OutputTabs();
            InnerWriter.Write(value);
        }

        /// <devdoc>
        ///     <para>
        ///     Writes a character to the text stream.
        ///     </para>
        /// </devdoc>
        public override void Write(char value)
        {
            OutputTabs();
            InnerWriter.Write(value);
        }

        /// <devdoc>
        ///     <para>
        ///     Writes a
        ///     character array to the text stream.
        ///     </para>
        /// </devdoc>
        public override void Write(char[] buffer)
        {
            OutputTabs();
            InnerWriter.Write(buffer);
        }

        /// <devdoc>
        ///     <para>
        ///     Writes a subarray
        ///     of characters to the text stream.
        ///     </para>
        /// </devdoc>
        public override void Write(char[] buffer, int index, int count)
        {
            OutputTabs();
            InnerWriter.Write(buffer, index, count);
        }

        /// <devdoc>
        ///     <para>
        ///     Writes the text representation of a Double to the text stream.
        ///     </para>
        /// </devdoc>
        public override void Write(double value)
        {
            OutputTabs();
            InnerWriter.Write(value);
        }

        /// <devdoc>
        ///     <para>
        ///     Writes the text representation of
        ///     a Single to the text
        ///     stream.
        ///     </para>
        /// </devdoc>
        public override void Write(float value)
        {
            OutputTabs();
            InnerWriter.Write(value);
        }

        /// <devdoc>
        ///     <para>
        ///     Writes the text representation of an integer to the text stream.
        ///     </para>
        /// </devdoc>
        public override void Write(int value)
        {
            OutputTabs();
            InnerWriter.Write(value);
        }

        /// <devdoc>
        ///     <para>
        ///     Writes the text representation of an 8-byte integer to the text stream.
        ///     </para>
        /// </devdoc>
        public override void Write(long value)
        {
            OutputTabs();
            InnerWriter.Write(value);
        }

        /// <devdoc>
        ///     <para>
        ///     Writes the text representation of an object
        ///     to the text stream.
        ///     </para>
        /// </devdoc>
        public override void Write(object value)
        {
            OutputTabs();
            InnerWriter.Write(value);
        }

        /// <devdoc>
        ///     <para>
        ///     Writes out a formatted string, using the same semantics as specified.
        ///     </para>
        /// </devdoc>
        public override void Write(string format, object arg0)
        {
            OutputTabs();
            InnerWriter.Write(format, arg0);
        }

        /// <devdoc>
        ///     <para>
        ///     Writes out a formatted string,
        ///     using the same semantics as specified.
        ///     </para>
        /// </devdoc>
        public override void Write(string format, object arg0, object arg1)
        {
            OutputTabs();
            InnerWriter.Write(format, arg0, arg1);
        }

        /// <devdoc>
        ///     <para>
        ///     Writes out a formatted string,
        ///     using the same semantics as specified.
        ///     </para>
        /// </devdoc>
        public override void Write(string format, params object[] arg)
        {
            OutputTabs();
            InnerWriter.Write(format, arg);
        }

        /// <devdoc>
        ///     <para>
        ///     Writes the specified
        ///     string to a line without tabs.
        ///     </para>
        /// </devdoc>
        public void WriteLineNoTabs(string s)
        {
            InnerWriter.WriteLine(s);
        }

        /// <devdoc>
        ///     <para>
        ///     Writes the specified string followed by
        ///     a line terminator to the text stream.
        ///     </para>
        /// </devdoc>
        public override void WriteLine(string s)
        {
            OutputTabs();
            InnerWriter.WriteLine(s);
            tabsPending = true;
        }

        /// <devdoc>
        ///     <para>
        ///     Writes a line terminator.
        ///     </para>
        /// </devdoc>
        public override void WriteLine()
        {
            OutputTabs();
            InnerWriter.WriteLine();
            tabsPending = true;
        }

        /// <devdoc>
        ///     <para>
        ///     Writes the text representation of a Boolean followed by a line terminator to
        ///     the text stream.
        ///     </para>
        /// </devdoc>
        public override void WriteLine(bool value)
        {
            OutputTabs();
            InnerWriter.WriteLine(value);
            tabsPending = true;
        }

        /// <devdoc>
        ///     <para>[To be supplied.]</para>
        /// </devdoc>
        public override void WriteLine(char value)
        {
            OutputTabs();
            InnerWriter.WriteLine(value);
            tabsPending = true;
        }

        /// <devdoc>
        ///     <para>[To be supplied.]</para>
        /// </devdoc>
        public override void WriteLine(char[] buffer)
        {
            OutputTabs();
            InnerWriter.WriteLine(buffer);
            tabsPending = true;
        }

        /// <devdoc>
        ///     <para>[To be supplied.]</para>
        /// </devdoc>
        public override void WriteLine(char[] buffer, int index, int count)
        {
            OutputTabs();
            InnerWriter.WriteLine(buffer, index, count);
            tabsPending = true;
        }

        /// <devdoc>
        ///     <para>[To be supplied.]</para>
        /// </devdoc>
        public override void WriteLine(double value)
        {
            OutputTabs();
            InnerWriter.WriteLine(value);
            tabsPending = true;
        }

        /// <devdoc>
        ///     <para>[To be supplied.]</para>
        /// </devdoc>
        public override void WriteLine(float value)
        {
            OutputTabs();
            InnerWriter.WriteLine(value);
            tabsPending = true;
        }

        /// <devdoc>
        ///     <para>[To be supplied.]</para>
        /// </devdoc>
        public override void WriteLine(int value)
        {
            OutputTabs();
            InnerWriter.WriteLine(value);
            tabsPending = true;
        }

        /// <devdoc>
        ///     <para>[To be supplied.]</para>
        /// </devdoc>
        public override void WriteLine(long value)
        {
            OutputTabs();
            InnerWriter.WriteLine(value);
            tabsPending = true;
        }

        /// <devdoc>
        ///     <para>[To be supplied.]</para>
        /// </devdoc>
        public override void WriteLine(object value)
        {
            OutputTabs();
            InnerWriter.WriteLine(value);
            tabsPending = true;
        }

        /// <devdoc>
        ///     <para>[To be supplied.]</para>
        /// </devdoc>
        public override void WriteLine(string format, object arg0)
        {
            OutputTabs();
            InnerWriter.WriteLine(format, arg0);
            tabsPending = true;
        }

        /// <devdoc>
        ///     <para>[To be supplied.]</para>
        /// </devdoc>
        public override void WriteLine(string format, object arg0, object arg1)
        {
            OutputTabs();
            InnerWriter.WriteLine(format, arg0, arg1);
            tabsPending = true;
        }

        /// <devdoc>
        ///     <para>[To be supplied.]</para>
        /// </devdoc>
        public override void WriteLine(string format, params object[] arg)
        {
            OutputTabs();
            InnerWriter.WriteLine(format, arg);
            tabsPending = true;
        }

        /// <devdoc>
        ///     <para>[To be supplied.]</para>
        /// </devdoc>
        public override void WriteLine(uint value)
        {
            OutputTabs();
            InnerWriter.WriteLine(value);
            tabsPending = true;
        }

        internal void InternalOutputTabs()
        {
            for (var i = 0; i < indentLevel; i++)
                InnerWriter.Write(TabString);
        }
    }

    class Indentation
    {
        readonly IndentedTextWriter writer;
        readonly int indent;
        string? s;

        internal Indentation(IndentedTextWriter writer, int indent)
        {
            this.writer = writer;
            this.indent = indent;
        }

        internal string IndentationString
        {
            get
            {
                if (s == null)
                {
                    var tabString = writer.TabString;
                    var sb = new StringBuilder(indent * tabString.Length);
                    for (var i = 0; i < indent; i++)
                        sb.Append(tabString);
                    s = sb.ToString();
                }

                return s;
            }
        }
    }
}