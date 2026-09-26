using System;
using BenchmarkDotNet.Running;

namespace Calamari.Benchmarks
{
    public static class Program
    {
        public static void Main(string[] args)
            => BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}
