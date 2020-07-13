using System;

namespace Calamari.Common.Features.Processes
{
    public class LibraryCallInvocation
    {
        public LibraryCallInvocation(Func<string[], int> func, string[] v)
        {
            Executable = func;
            Arguments = v;
        }

        public Func<string[], int> Executable { get; }
        public string[] Arguments { get; }
    }
}