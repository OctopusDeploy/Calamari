using System;

namespace Calamari.Testing.LogParser
{
    public class ProcessOutput
    {
        readonly ProcessOutputSource source;
        readonly string text;
        readonly DateTimeOffset occurred;

        public ProcessOutput(ProcessOutputSource source, string text)
            : this(source, text, DateTimeOffset.UtcNow)
        {
        }

        public ProcessOutput(ProcessOutputSource source, string text, DateTimeOffset occurred)
        {
            this.source = source;
            this.text = text;
            this.occurred = occurred;
        }

        public ProcessOutputSource Source
        {
            get
            {
                return this.source;
            }
        }

        public DateTimeOffset Occurred
        {
            get
            {
                return this.occurred;
            }
        }

        public string Text
        {
            get
            {
                return this.text;
            }
        }
    }
}