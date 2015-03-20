using System;

namespace Calamari
{
    public class CalamariLogger
    {
        public static void SetOctopusVariable(string name, object value)
        {
            Console.WriteLine("##octopus[setVariable name=\"" + name + "\" value=\"" + value + "\"]");
        }

        public static void Verbose(string message)
        {
            Console.WriteLine("##octopus[stdout-verbose]");
            Console.WriteLine(message);
            Console.WriteLine("##octopus[stdout-default]");
        }

        public static void VerboseFormat(string messageFormat, params object[] args)
        {
            Verbose(String.Format(messageFormat, args));
        }

        public static void Error(string message)
        {
            Console.WriteLine("##octopus[stdout-error]");
            Console.WriteLine(message);
            Console.WriteLine("##octopus[stdout-default]");
        }

        public static void ErrorFormat(string messageFormat, params object[] args)
        {
            Error(String.Format(messageFormat, args));
        }
    }
}