namespace Calamari.Integration.Proxies
{
    public abstract class ProxySettings
    {
        public abstract T Accept<T>(IProxySettingsVisitor<T> visitor);
    }
    
    public class BypassProxySettings : ProxySettings
    {
        public override T Accept<T>(IProxySettingsVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    public class UseSystemProxySettings : ProxySettings
    {
        public string Username { get; }
        public string Password { get; }

        public UseSystemProxySettings(string username, string password)
        {
            Username = username;
            Password = password;
        }

        public override T Accept<T>(IProxySettingsVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    public class UseCustomProxySettings : ProxySettings
    {
        public string Host { get; }
        public int Port { get; }
        public string Username { get; }
        public string Password { get; }

        public UseCustomProxySettings(string host, int port, string username, string password)
        {
            Host = host;
            Port = port;
            Username = username;
            Password = password;
        }

        public override T Accept<T>(IProxySettingsVisitor<T> visitor)
        {
            return visitor.Visit(this);
        }
    }

    public interface IProxySettingsVisitor<T>
    {
        T Visit(BypassProxySettings proxySettings);
        T Visit(UseSystemProxySettings proxySettings);
        T Visit(UseCustomProxySettings proxySettings);
    }
}