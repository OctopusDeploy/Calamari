using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.IdentityManagement;
using Amazon.S3;
using Amazon.SecurityToken;

namespace Calamari.Aws.Util
{
    public interface IAmazonClientFactory
    {
        Task<IAmazonS3> GetS3Client();

        Task<IAmazonIdentityManagementService> GetIdentityManagementClient();
        
        Task<IAmazonSecurityTokenService> GetSecurityTokenClient();

        Task<IAmazonCloudFormation> GetCloudFormationClient();
    }
}