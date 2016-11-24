using System.Collections.Generic;

namespace Calamari.Tests.Integration.Helpers
{
    public class InMemoryLog : ILog
    {
        public List<Message> Messages { get; } = new List<Message>();

        public void Verbose(string message)
        {
            Messages.Add(new Message(Level.Verbose, message, null));
        }

        public void VerboseFormat(string message, params object[] args)
        {
            Messages.Add(new Message(Level.Verbose, message, args));
        }

        public void Info(string message)
        {
            Messages.Add(new Message(Level.Info, message, null));
        }

        public void InfoFormat(string message, params object[] args)
        {
            Messages.Add(new Message(Level.Info, message, args));
        }

        public void Warn(string message)
        {
            Messages.Add(new Message(Level.Warn, message, null));
        }

        public void WarnFormat(string message, params object[] args)
        {
            Messages.Add(new Message(Level.Warn, message, args));
        }

        public void Error(string message)
        {
            Messages.Add(new Message(Level.Error, message, null));
        }

        public void ErrorFormat(string message, params object[] args)
        {
            Messages.Add(new Message(Level.Error, message, args));
        }


        public class Message
        {
            public Level Level { get; }
            public string MessageFormat { get; }
            public object[] Args { get; }
            public string FormattedMessage { get; }

            public Message(Level level, string message, object[] args)
            {
                Level = level;
                MessageFormat = message;
                Args = args;
                FormattedMessage = args == null ? message : string.Format(message, args);
            }

        }

        public enum Level
        {
            Verbose, Info, Warn, Error
        }

      
    }

}