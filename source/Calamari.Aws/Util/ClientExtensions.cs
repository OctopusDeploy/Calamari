using Amazon.S3;
using Calamari.Aws.Integration;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Aws.Util
{
    public static class ClientExtensions
    {
        public static TConfig AsClientConfig<TConfig>(this IAwsEnvironmentGeneration environment)
            where TConfig : AmazonS3Config, new()
        {
            return new TConfig().Tee(x =>
            {
                x.RegionEndpoint = environment.AwsRegion;
                x.ProxyPort = environment.ProxyPort;
                x.ProxyCredentials = environment.ProxyCredentials;
                x.ProxyHost = environment.ProxyHost;
            });
        }}

    public static class ClientHelpers
    {
        public static AmazonS3Client CreateS3Client(IAwsEnvironmentGeneration environment)
        {
            return new AmazonS3Client(environment.AwsCredentials, environment.AsClientConfig<AmazonS3Config>());
        }
    }
}
