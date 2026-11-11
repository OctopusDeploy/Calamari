using System;
using System.Text;

namespace Calamari.Integration.Tomcat
{
    /// <summary>
    /// The parameters needed to talk to a Tomcat Manager instance and build its "/text/..." API URLs,
    /// matching the URL-building logic in the existing com.octopus.calamari.tomcat.TomcatOptions
    /// (Octopus.Dependencies.Java).
    /// </summary>
    public class TomcatManagerOptions
    {
        public string Controller { get; }
        public string User { get; }
        public string Password { get; }
        public string Application { get; }
        public string Name { get; }
        public string Tag { get; }
        public string Version { get; }

        public TomcatManagerOptions(string controller, string user, string password, string application, string name, string tag, string version)
        {
            Controller = string.IsNullOrWhiteSpace(controller) ? "http://localhost:8080" : controller.Trim();
            User = user ?? "";
            Password = password ?? "";
            Application = (application ?? "").Trim();
            Name = (name ?? "").Trim();
            Tag = (tag ?? "").Trim();
            Version = (version ?? "").Trim();
        }

        /// <summary>
        /// The path Tomcat's Manager app should deploy the application under. Derived from the
        /// application filename's "context##version" naming convention when no explicit name is set.
        /// </summary>
        public string? UrlPath
        {
            get
            {
                if (Name == "/")
                    return "";

                if (!string.IsNullOrWhiteSpace(Name))
                    return Name.TrimStart('/');

                if (!string.IsNullOrWhiteSpace(Application))
                {
                    var baseName = System.IO.Path.GetFileNameWithoutExtension(Application);
                    return baseName.Split(new[] { "##" }, StringSplitOptions.None)[0].Replace("#", "/");
                }

                return null;
            }
        }

        string? UrlVersion
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Version))
                    return Version;

                if (Application.Contains("##"))
                {
                    var baseName = System.IO.Path.GetFileNameWithoutExtension(Application);
                    var parts = baseName.Split(new[] { "##" }, StringSplitOptions.None);
                    return parts.Length > 1 ? parts[1] : null;
                }

                return null;
            }
        }

        public string BuildUrl(string action, bool includeUpdateFlag = false)
        {
            var builder = new StringBuilder($"{Controller}/text/{action}?");

            if (includeUpdateFlag)
                builder.Append("update=true&");

            var version = UrlVersion;
            if (!string.IsNullOrWhiteSpace(version))
                builder.Append($"version={Uri.EscapeDataString(version)}&");

            var path = UrlPath;
            if (path != null)
                builder.Append($"path=/{Uri.EscapeDataString(path)}&");

            if (!string.IsNullOrWhiteSpace(Tag))
                builder.Append($"tag={Uri.EscapeDataString(Tag)}");

            return builder.ToString().TrimEnd('&', '?');
        }

        public string DeployUrl => BuildUrl("deploy", includeUpdateFlag: true);
        public string RedeployUrl => BuildUrl("deploy");
        public string UndeployUrl => BuildUrl("undeploy");
        public string StartUrl => BuildUrl("start");
        public string StopUrl => BuildUrl("stop");
        public string ListUrl => $"{Controller}/text/list";

        public string AuthorizationHeaderValue => "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{User}:{Password}"));
    }
}
