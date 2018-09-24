namespace Calamari.Integration.Nginx
{
    public class WindowsNginxServer : NginxServer
    {
        protected override string GetConfigRootDirectory()
        {
            throw new System.NotImplementedException();
        }

        protected override string GetSslCertRootDirectory()
        {
            throw new System.NotImplementedException();
        }
    }
}
