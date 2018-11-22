namespace Calamari.Integration.Nginx
{
    public class WindowsNginxServer : NginxServer
    {
        public override string GetConfigRootDirectory()
        {
            throw new System.NotImplementedException();
        }

        public override string GetSslRootDirectory()
        {
            throw new System.NotImplementedException();
        }
    }
}
