namespace Calamari.Tests
{
    class TestProgramWrapper
    {
        //This is a shell around Calamari.exe so we can use it in .net core testing, since in .net core when we reference the
        //Calamari project we only get the dll, not the exe
        static int Main(string[] args)
            => Program.Main(args);
    }
}