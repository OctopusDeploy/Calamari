using System;

namespace Calamari.Exceptions
{
    /// <summary>
    /// Represents an error downloading a maven artifact
    /// </summary>
    public class MavenDownloadException :  Exception
    {
        public MavenDownloadException()
        {
        }

        public MavenDownloadException(string message)
            : base(message)
        {
        }

        public MavenDownloadException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}