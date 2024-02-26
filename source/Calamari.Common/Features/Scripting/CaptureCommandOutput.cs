using System.Collections.Generic;
using Calamari.Common.Plumbing.Commands;

namespace Calamari.Common.Features.Scripting
{
        public class CaptureCommandOutput : ICommandInvocationOutputSink
        {
            private List<Message> messages = new List<Message>();

            public List<Message> Messages => messages;

            public void WriteInfo(string line)
            {
                Messages.Add(new Message(Level.Verbose, line));
            }

            public void WriteError(string line)
            {
                Messages.Add(new Message(Level.Error, line));
            }
        }

        public class Message
        {
            public Level Level { get; }
            public string Text { get; }
            public Message(Level level, string text)
            {
                Level = level;
                Text = text;
            }
        }

        public enum Level
        {
            Verbose,
            Error
        }
}