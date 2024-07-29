using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Commands;

namespace Calamari.Kubernetes.Integration
{
    public interface ICommandOutput
    {
        Message[] Messages { get; }
        IEnumerable<string> InfoLogs { get; }
    }
    public class CaptureCommandOutput : ICommandInvocationOutputSink, ICommandOutput
    {
        private readonly List<Message> messages = new List<Message>();
        public Message[] Messages => messages.ToArray();

        public IEnumerable<string> InfoLogs => Messages.Where(m => m.Level == Level.Info).Select(m => m.Text).ToArray();

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
