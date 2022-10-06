namespace Calamari.Common.Plumbing.Logging
{
    public static class ExitStatus
    {
        public const int Success = 0;
        public const int CommandExceptionError = 1;
        public const int RecursiveDefinitionExceptionError = 101;
        public const int ReflectionTypeLoadExceptionError = 43;
        public const int OtherError = 100;
    }
}