using System.IO;
using System.Threading.Tasks;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;

namespace Calamari.AzureAppService
{
    public class ZipPackageProvider : BaseZipPackageProvider
    {
        public override bool SupportsAsynchronousDeployment => true;
        public override string UploadUrlPath => @"/api/zipdeploy";
        public override string ContentType => "application/octet-stream";
        public override string AdditionalParameters => "?isAsync=true";
    }
}