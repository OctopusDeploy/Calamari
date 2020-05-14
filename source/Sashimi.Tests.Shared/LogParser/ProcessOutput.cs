using System;

namespace Sashimi.Tests.Shared.LogParser
{
    public class ProcessOutput
    {
        private readonly ProcessOutputSource source;
        private readonly string text;
        private readonly DateTimeOffset occurred;

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