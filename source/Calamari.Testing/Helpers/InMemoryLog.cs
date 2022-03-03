using System;
using System.Collections.Generic;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Testing.Helpers
{
    public class InMemoryLog : AbstractLog
    {
        public List<Message> Messages { get; } = new List<Message>();
        public List<string> StandardOut { get; } = new List<string>();
        public List<string> StandardError  { get; }= new List<string>();

        protected override void StdOut(string message)
        {
            Console.WriteLine(message); // Write to console for the test output
            StandardOut.Add(message);
        }

        protected override void StdErr(string message)
        {
            Console.Error.WriteLine(message);
            StandardError.Add(message);
        }

        public override void Verbose(string message)
        {
            Messages.Add(new Message(Level.Verbose, message));
            base.Verbose(message);
        }

        public override void VerboseFormat(string messageFormat, params object[] args)
        {
            Messages.Add(new Message(Level.Verbose, messageFormat, args));
            base.VerboseFormat(messageFormat, args);
        }

        public override void Info(string message)
        {
            Messages.Add(new Message(Level.Info, message));
            base.Info(message);
        }

        public override void InfoFormat(string messageFormat, params object[] args)
        {
            Messages.Add(new Message(Level.Info, messageFormat, args));
            base.InfoFormat(messageFormat, args);
        }

        public override void Warn(string message)
        {
            Messages.Add(new Message(Level.Warn, message));
            base.Warn(message);
        }

        public override void WarnFormat(string messageFormat, params object[] args)
        {
            Messages.Add(new Message(Level.Warn, messageFormat, args));
            base.WarnFormat(messageFormat, args);
        }

        public override void Error(string message)
        {
            Messages.Add(new Message(Level.Error, message));
            base.Error(message);
        }

        public override void ErrorFormat(string messageFormat, params object[] args)
        {
            Messages.Add(new Message(Level.Error, messageFormat, args));
            base.ErrorFormat(messageFormat, args);
        }

        public class Message
        {
            public Level Level { get; }
            public string MessageFormat { get; }
            public object[] Args { get; }
            public string FormattedMessage { get; }

            public Message(Level level, string message, params object[] args)
            {
                Level = level;
                MessageFormat = message;
                Args = args;
                FormattedMessage = args == null || args.Length == 0 ? message : string.Format(message, args);
            }
        }

        public enum Level
        {
            Verbose,
            Info,
            Warn,
            Error
        }
    }
}