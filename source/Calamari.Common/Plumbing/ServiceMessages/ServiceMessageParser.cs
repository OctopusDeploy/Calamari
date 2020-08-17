using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Calamari.Common.Plumbing.ServiceMessages
{
    public class ServiceMessageParser
    {
        readonly Action<ServiceMessage> serviceMessage;
        readonly StringBuilder buffer = new StringBuilder();
        State state = State.Default;

        public ServiceMessageParser(Action<ServiceMessage> serviceMessage)
        {
            this.serviceMessage = serviceMessage;
        }

        public void Parse(string line)
        {
            foreach (var c in line)
                switch (state)
                {
                    case State.Default:
                        if (c == '#')
                        {
                            state = State.PossibleMessage;
                            buffer.Append(c);
                        }

                        break;

                    case State.PossibleMessage:
                        buffer.Append(c);
                        var progress = buffer.ToString();
                        if ("##octopus" == progress)
                        {
                            state = State.InMessage;
                            buffer.Clear();
                        }
                        else if (!"##octopus".StartsWith(progress))
                        {
                            state = State.Default;
                            buffer.Clear();
                        }

                        break;

                    case State.InMessage:
                        if (c == ']')
                        {
                            ProcessMessage(buffer.ToString());
                            state = State.Default;
                            buffer.Clear();
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

        void ProcessMessage(string message)
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
                // Ignore
            }
        }

        enum State
        {
            Default,
            PossibleMessage,
            InMessage
        }
    }
}