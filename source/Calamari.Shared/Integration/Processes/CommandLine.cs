using System;
using System.Collections.Generic;
using System.Globalization;
using Calamari.Util;

namespace Calamari.Integration.Processes
{
    public class CommandLine
    {
        readonly string executable;
        readonly Func<string[], int> func;
        string action;
        readonly List<string> arguments = new List<string>();
        bool rawArgList;
        bool doubleDash;
        bool useDotnet;


        public CommandLine(Func<string[], int> func)
        {
            this.func = func;
            rawArgList = true;
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



        public CommandLine DoubleDash()
        {
            doubleDash = true;
            return this;
        }

        public CommandLine Flag(string flagName)
        {
            arguments.Add(MakeFlag(flagName));
            return this;
        }

        public CommandLine UseDotnet()
        {
            useDotnet = true;
            return this;
        }

        string MakeFlag(string flagName)
        {
            return GetDash() + Normalize(flagName);
        }

        string GetDash()
        {
            return doubleDash ? "--" : "-";
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

        string MakePositionalArg(object argValue)
        {
            var sval = "";
            var f = argValue as IFormattable;
            if (f != null)
                sval = f.ToString(null, CultureInfo.InvariantCulture);
            else if (argValue != null)
                sval = argValue.ToString();

            return Escape(sval);
        }

        string MakeArg(string argName, object argValue)
        {
            var sval = "";
            var f = argValue as IFormattable;
            if (f != null)
                sval = f.ToString(null, CultureInfo.InvariantCulture);
            else if (argValue != null)
                sval = argValue.ToString();

            return string.Format("{2}{0} {1}", Normalize(argName), Escape(sval), GetDash());
        }

        string Escape(string argValue)
        {
            if (argValue == null) throw new ArgumentNullException("argValue");
            if (rawArgList)
                return argValue;

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

#if WORKAROUND_FOR_EMPTY_STRING_BUG
            // linux under bash on netcore empty "" gets eaten, hand "\0"
            // which gets through as a null string
            if(argValue == "")
                argValue = "\0";
#endif
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
            var actualExe = executable;

            if (useDotnet)
            {
                argLine.Add(MakePositionalArg(executable));
                actualExe = "dotnet";
            }

            if (action != null)
                argLine.Add(action);
            argLine.AddRange(arguments);

            return new CommandLineInvocation(actualExe, string.Join(" ", argLine));
        }

        public LibraryCallInvocation BuildLibraryCall()
        {
            var argLine = new List<string>();
            if (action != null)
                argLine.Add(action);
            argLine.AddRange(arguments);
            return new LibraryCallInvocation(func, argLine.ToArray());
        }
    }
}