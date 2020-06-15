namespace Sashimi.AzureCloudService
{
    static class CertificateExpectations
    {
        const string OctopusAzureCertificateName = "Octopus Deploy";
        const string OctopusAzureCertificateFullName = "cn=" + OctopusAzureCertificateName;

        public static string BuildOctopusAzureCertificateFullName(string azureAccountName)
        {
            return $"{OctopusAzureCertificateFullName} - {azureAccountName}";
        }
    }
}