namespace Calamari.Integration.Nginx
{
    public class WindowsNginxServer : NginxServer
    {
        protected override string GetConfigRootDirectory()
        {
            return "";
        }

        protected override string GetSslCertRootDirectory()
        {
            return "";
        }
    }
}
