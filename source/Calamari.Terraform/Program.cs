using System;
using Calamari.Commands.Support;
using Calamari.Common;
using Calamari.Plumbing;

namespace Calamari.Terraform
{
    public class Program : CalamariFlavourProgram
    {

        Program() : base(ConsoleLog.Instance)
        {
        }
        
        public static int Main(string[] args)
        {
            return new Program().Run(args);
        }
    }
}