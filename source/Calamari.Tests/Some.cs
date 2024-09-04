using System;
using System.Threading;

namespace Calamari.Tests
{
    public static class Some
    {
        static int next;

        public static string String() => "S__" + Int();

        public static int Int() => Interlocked.Increment(ref next);
    }
}