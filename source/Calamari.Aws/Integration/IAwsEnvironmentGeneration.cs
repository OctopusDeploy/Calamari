using System.Collections.Specialized;
using System.Net;
using Amazon;
using Amazon.Runtime;

namespace Calamari.Aws.Integration
{
    /// <summary>
    /// Defines a service that generates AWS environment variables and credentials objects.
    /// </summary>
    public interface IAwsEnvironmentGeneration
    {
        /// <summary>
        /// A key value mapping that defines the environment variables required to run AWS scripts.
        /// This is useful when running external scripts.
        /// </summary>
        StringDictionary EnvironmentVars { get; }
        /// <summary>
        /// A AWS credentials object that includes the information required to run AWS SDK requests.
        /// This is useful when interacting with AWS directly via the SDK.
        /// </summary>
        AWSCredentials AwsCredentials { get; }
        /// <summary>
        /// The region to use
        /// </summary>
        RegionEndpoint AwsRegion { get; }
        
        int ProxyPort { get; }
        ICredentials ProxyCredentials { get; }
        string ProxyHost { get; }       
    }
}