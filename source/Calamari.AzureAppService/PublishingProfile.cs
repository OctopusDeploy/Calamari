using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Calamari.AzureAppService
{
    class PublishingProfile
    {
        public string Site { get; set; }

        public string Password { get; set; }

        public string Username { get; set; }

        public string PublishUrl { get; set; }
        
        public string GetBasicAuthCredentials()
            => Convert.ToBase64String(Encoding.ASCII.GetBytes($"{Username}:{Password}"));
        
        public static async Task<PublishingProfile> ParseXml(Stream publishingProfileXmlStream)
        {
            using var streamReader = new StreamReader(publishingProfileXmlStream);
            var document = XDocument.Parse(await streamReader.ReadToEndAsync());

            var profile = (from el in document.Descendants("publishProfile")
                           where string.Compare(el.Attribute("publishMethod")?.Value,
                                                "MSDeploy",
                                                StringComparison.OrdinalIgnoreCase)
                                 == 0
                           select new PublishingProfile
                           {
                               PublishUrl = $"https://{el.Attribute("publishUrl")?.Value}",
                               Username = el.Attribute("userName")?.Value,
                               Password = el.Attribute("userPWD")?.Value,
                               Site = el.Attribute("msdeploySite")?.Value
                           }).FirstOrDefault();

            if (profile == null) throw new Exception("Failed to retrieve publishing profile.");

            return profile;
        }
    }
}
