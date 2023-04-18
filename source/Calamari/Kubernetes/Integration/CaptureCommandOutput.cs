using System.Collections.Generic;
using Calamari.Common.Plumbing.Commands;

namespace Calamari.Kubernetes.Integration
{
    public interface ICommandOutput
    {
        public IReadOnlyList<Message> Messages { get; }
    }
    public class CaptureCommandOutput : ICommandInvocationOutputSink, ICommandOutput
    {
        private readonly List<Message> messages = new();
        public IReadOnlyList<Message> Messages => messages;
        public void WriteInfo(string line)
        {
            messages.Add(new Message(Level.Info, line));
        }

        public void WriteError(string line)
        {
            messages.Add(new Message(Level.Error, line));
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
