using System;
using System.Collections.Generic;
using System.Globalization;

namespace Calamari.Integration.Processes
{
    public class CommandLine
    {
        readonly string executable;
        string action;
        readonly List<string> arguments = new List<string>();
        
        public static CommandLine Execute(string executable)
        {
            return new CommandLine(executable);
        }

        public CommandLine(string executable)
        {
            if (executable == null) throw new ArgumentNullException("executable");
            this.executable = executable;
        }

        public CommandLine Action(string actionName)
        {
            if (actionName == null) throw new ArgumentNullException("actionName");
            if (action != null) throw new InvalidOperationException("Action is already set");
            action = Normalize(actionName);
            return this;
        }

        public CommandLine Argument(string argument)
        {
            arguments.Add(Escape(argument));
            return this;
        }

        public CommandLine Flag(string flagName)
        {
            arguments.Add(MakeFlag(flagName));
            return this;
        }

        string MakeFlag(string flagName)
        {
            return "-" + Normalize(flagName);
        }

        public CommandLine PositionalArgument(object argValue)
        {
            arguments.Add(MakePositionalArg(argValue));
            return this;
        }

        public CommandLine Argument(string argName, object argValue)
        {
            arguments.Add(MakeArg(argName, argValue));
            return this;
        }

        static string MakePositionalArg(object argValue)
        {
            var sval = "";
            var f = argValue as IFormattable;
            if (f != null)
                sval = f.ToString(null, CultureInfo.InvariantCulture);
            else if (argValue != null)
                sval = argValue.ToString();

            return Escape(sval);
        }
        static string MakeArg(string argName, object argValue)
        {
            var sval = "";
            var f = argValue as IFormattable;
            if (f != null)
                sval = f.ToString(null, CultureInfo.InvariantCulture);
            else if (argValue != null)
                sval = argValue.ToString();

            return string.Format("-{0} {1}", Normalize(argName), Escape(sval));
        }

        public static string Escape(string argValue)
        {
            if (argValue == null) throw new ArgumentNullException("argValue");

            // Though it isn't aesthetically pleasing, we always return a double-quoted
            // value.

            var last = argValue.Length - 1;
            var preq = true;
            while (last >= 0)
            {
                // Escape backslashes only when they're butted up against the
                // end of the value, or an embedded double quote

                var cur = argValue[last];
                if (cur == '\\' && preq)
                {
                    argValue = argValue.Insert(last, "\\");
                }
                else if (cur == '"')
                {
                    preq = true;
                }
                else
                {
                    preq = false;
                }
                last -= 1;
            }

            // Double-quotes are always escaped.
            return "\"" + argValue.Replace("\"", "\\\"") + "\"";
        }

        static string Normalize(string s)
        {
            if (s == null) throw new ArgumentNullException("s");
            return s.Trim();
        }

        public CommandLineInvocation Build()
        {
            var argLine = new List<string>();
            if (action != null)
                argLine.Add(action);
            argLine.AddRange(arguments);

            return new CommandLineInvocation(executable, string.Join(" ", argLine));
        }
    }
}