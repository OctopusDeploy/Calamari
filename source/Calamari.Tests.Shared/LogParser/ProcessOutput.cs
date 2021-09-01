using System;

namespace Calamari.Tests.Shared.LogParser
{
    public class ProcessOutput
    {
        public ProcessOutput(ProcessOutputSource source, string text)
            : this(source, text, DateTimeOffset.UtcNow)
        {
        }

        public ProcessOutput(ProcessOutputSource source, string text, DateTimeOffset occurred)
        {
            this.Source = source;
            this.Text = text;
            this.Occurred = occurred;
        }

        public ProcessOutputSource Source { get; }

        public DateTimeOffset Occurred { get; }

        public string Text { get; }
    }
}