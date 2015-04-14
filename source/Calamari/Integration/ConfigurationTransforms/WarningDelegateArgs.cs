namespace Calamari.Integration.ConfigurationTransforms
{
    public delegate void LogDelegate(object sender, WarningDelegateArgs args);

    public class WarningDelegateArgs
    {
        public string Message { get; set; }

        public WarningDelegateArgs(string message)
        {
            Message = message;
        }
    }
}