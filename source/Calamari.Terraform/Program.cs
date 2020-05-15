namespace Calamari.Terraform
{
    public class Program : CalamariFlavourProgram
    {
        public Program(ILog log) : base(log)
        {
        }
        
        public static int Main(string[] args)
        {
            return new Program(ConsoleLog.Instance).Run(args);
        }
    }
}