namespace Calamari.Integration.Nginx
{
    public class NixNginxServer : NginxServer
    {
        protected override string GetConfigRootDirectory()
        {
            return "/etc/nginx/conf.d";
        }

        protected override string GetSslCertRootDirectory()
        {
            return "/etc/ssl";
        }
    }
}
