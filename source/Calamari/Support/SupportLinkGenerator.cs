using EnsureThat;

namespace Calamari.Support
{
    public class SupportLinkGenerator : ISupportLinkGenerator
    {
        private const string JavaErrorCodePrefix = "JAVA";
        /// <summary>
        /// The shortended link to the Java documentation
        /// </summary>
        private const string JavaSupportLink = "http://g.octopushq.com/JavaAppDeploy";
        
        public string GenerateSupportMessage(string baseMessage, string errorCode)
        {
            EnsureArg.IsNotNullOrWhiteSpace(baseMessage);
            EnsureArg.IsNotNullOrWhiteSpace(errorCode);

            if (errorCode.StartsWith(JavaErrorCodePrefix))
            {
                return $"{errorCode}: {baseMessage} {JavaSupportLink + "#" + errorCode.ToLower()}";
            }

            return $"{errorCode}: {baseMessage}";
        }
    }
}