using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Calamari.Common.Plumbing.ServiceMessages;

namespace Calamari.Testing.LogParser
{
    public class ServiceMessageParser
    {
        readonly Action<ProcessOutputSource, string> output;
        readonly Action<ServiceMessage> serviceMessage;
        readonly StringBuilder buffer = new StringBuilder();
        State state = State.Default;
        ProcessOutputSource lastSource;

        public ServiceMessageParser(Action<ProcessOutputSource, string> output, Action<ServiceMessage> serviceMessage)
        {
            this.output = output;
            this.serviceMessage = serviceMessage;
        }

        public void Append(ProcessOutputSource source, string line)
        {
            if (lastSource != source)
                Finish();

            lastSource = source;

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];

                switch (state)
                {
                    case State.Default:
                        if (c == '\r')
                        {
                        }
                        else if (c == '\n')
                        {
                            Flush(output);
                        }
                        else if (c == '#')
                        {
                            state = State.PossibleMessage;
                            buffer.Append(c);
                        }
                        else
                        {
                            buffer.Append(c);
                        }
                        break;

                    case State.PossibleMessage:
                        buffer.Append(c);
                        var progress = buffer.ToString();
                        if (ServiceMessage.ServiceMessageLabel == progress)
                        {
                            state = State.InMessage;
                            buffer.Clear();
                        }
                        else if (!ServiceMessage.ServiceMessageLabel.StartsWith(progress))
                        {
                            state = State.Default;
                        }
                        break;

                    case State.InMessage:
                        if (c == ']')
                        {
                            Flush(ProcessMessage);
                            state = State.Default;
                        }
                        else
                        {
                            buffer.Append(c);
                        }
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public void Finish()
        {
            if (buffer.Length > 0)
            {
                Flush(output);
            }
        }

        void ProcessMessage(ProcessOutputSource source, string message)
        {
            try
            {
                message = message.Trim().TrimStart('[').Replace("\r", "").Replace("\n", "");

                var element = XElement.Parse("<" + message + "/>");
                var name = element.Name.LocalName;
                var values = element.Attributes().ToDictionary(s => s.Name.LocalName, s => Encoding.UTF8.GetString(Convert.FromBase64String(s.Value)), StringComparer.OrdinalIgnoreCase);
                serviceMessage(new ServiceMessage(name, values));
            }
            catch
            {
                serviceMessage(new ServiceMessage("stdout-warning", null));
                output(source, $"Could not parse '{ServiceMessage.ServiceMessageLabel}[{message}]'");
                serviceMessage(new ServiceMessage("stdout-default", null));
            }
        }

        void Flush(Action<ProcessOutputSource, string> to)
        {
            var result = buffer.ToString();
            buffer.Clear();

            if (result.Length > 0)
                to(lastSource, result);
        }

        enum State
        {
            Default,
            PossibleMessage,
            InMessage
        }
    }
}