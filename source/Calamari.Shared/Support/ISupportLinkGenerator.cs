namespace Calamari.Support
{
    /// <summary>
    /// Defines a service for generating messages that link to external support documentation.
    /// </summary>
    public interface ISupportLinkGenerator
    {
        /// <summary>
        /// Takes a base message and an error code and returns the full error message
        /// complete with a link to the external docs
        /// </summary>
        /// <param name="baseMessage">The text to accompany the error message</param>
        /// <param name="errorCode">The error message code</param>
        /// <returns></returns>
        string GenerateSupportMessage(string baseMessage, string errorCode);
    }
}