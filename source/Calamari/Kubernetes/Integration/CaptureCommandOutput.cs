using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Commands;

namespace Calamari.Kubernetes.Integration
{
    public interface ICommandOutput
    {
        Message[] Messages { get; }
        IEnumerable<string> InfoLogs { get; }
        string MergeInfoLogs();
    }

    public class CaptureCommandOutput : ICommandInvocationOutputSink, ICommandOutput
    {
        readonly List<Message> messages = new List<Message>();
        Message[] snapshot;

        public Message[] Messages => snapshot ??= messages.ToArray();

        public IEnumerable<string> InfoLogs
        {
            get
            {
                foreach (var message in Messages)
                {
                    if (message.Level == Level.Info)
                        yield return message.Text;
                }
            }
        }

        public string MergeInfoLogs() => string.Join(Environment.NewLine, InfoLogs);

        public void WriteInfo(string line)
        {
            Add(new Message(Level.Info, line));
        }

        public void WriteError(string line)
        {
            Add(new Message(Level.Error, line));
        }

        void Add(Message message)
        {
            messages.Add(message);
            snapshot = null;
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
