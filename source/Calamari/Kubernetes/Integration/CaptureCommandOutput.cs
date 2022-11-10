using System.Collections.Generic;
using Calamari.Common.Plumbing.Commands;

namespace Calamari.Kubernetes.Integration
{
    public class CaptureCommandOutput : ICommandInvocationOutputSink
    {
        public List<Message> Messages { get; } = new List<Message>();
        public void WriteInfo(string line)
        {
            Messages.Add(new Message(Level.Info, line));
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
        Info,
        Error
    }
}
