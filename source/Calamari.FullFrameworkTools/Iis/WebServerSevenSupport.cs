using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Integration.Iis;
using Microsoft.Web.Administration;

namespace Calamari.FullFrameworkTools.Iis
{
    public class WebServerSevenSupport : WebServerSupport
    {
        const string Localhost = "localhost";

        public override void CreateWebSiteOrVirtualDirectory(string webSiteName, string virtualDirectoryPath, string webRootPath, int port)
        {
            var virtualParts = (virtualDirectoryPath ?? String.Empty).Split('/', '\\').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();

            Execute(serverManager =>
            {
                var existing = serverManager.Sites.FirstOrDefault(x => String.Equals(x.Name, webSiteName, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    existing = serverManager.Sites.Add(webSiteName, webRootPath, port);                    
                }

                if (virtualParts.Length > 0)
                {
                    var vd = existing.Applications.Single().VirtualDirectories.Add(virtualDirectoryPath, webRootPath);
                }
                
                serverManager.CommitChanges();
            });
        }

        public override string GetHomeDirectory(string webSiteName, string virtualDirectoryPath)
        {
            string? result = null;

            FindVirtualDirectory(webSiteName, virtualDirectoryPath, found =>
            {
                result = found.PhysicalPath;
            });

            if (result == null)
            {
                throw new Exception("The virtual directory does not exist.");
            }

            return result;
        }

        public override void DeleteWebSite(string webSiteName)
        {
            Execute(serverManager =>
            {
                var existing = serverManager.Sites.FirstOrDefault(x => String.Equals(x.Name, webSiteName, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    throw new Exception($"The site '{webSiteName}'  does not exist.");
                }

                existing.Delete();
                serverManager.CommitChanges();
            });
        }

        public void DeleteApplicationPool(string applicationPoolName)
        {
            Execute(serverManager =>
            {
                var existing = serverManager.ApplicationPools.FirstOrDefault(x => String.Equals(x.Name, applicationPoolName, StringComparison.OrdinalIgnoreCase));
                if (existing == null)
                {
                    throw new Exception($"The application pool '{applicationPoolName}' does not exist");
                }

                existing.Delete();
                serverManager.CommitChanges();
            });
        }

        public override bool ChangeHomeDirectory(string webSiteName, string? virtualDirectoryPath, string newWebRootPath)
        {
            var result = false;
            
            FindVirtualDirectory(webSiteName, virtualDirectoryPath, found =>
            {
                found.PhysicalPath = newWebRootPath;
                result = true;
            });
            
            return result;
        }

        void FindVirtualDirectory(string webSiteName, string? virtualDirectoryPath, Action<VirtualDirectory> found)
        {
            Execute(serverManager => 
            {
                var site = serverManager.Sites.FirstOrDefault(s => s.Name.ToLowerInvariant() == webSiteName.ToLowerInvariant());
                if (site == null) 
                    return;
                
                var virtuals = ListVirtualDirectories("", site);
                foreach (var vdir in virtuals)
                {
                    if (!string.Equals(Normalize(vdir.FullVirtualPath), Normalize(virtualDirectoryPath), StringComparison.OrdinalIgnoreCase)) 
                        continue;

                    found(vdir.VirtualDirectory);
                    serverManager.CommitChanges();
                }
            });
        }

        static string Normalize(string? fullVirtualPath)
        {
            if (fullVirtualPath == null)
                return string.Empty;

            return string.Join("/", fullVirtualPath.Split('/').Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        static IEnumerable<VirtualDirectoryNode> ListVirtualDirectories(string path, ConfigurationElement element)
        {
            var site = element as Site;
            if (site != null)
            {
                foreach (var child in site.Applications)
                    foreach (var item in ListVirtualDirectories("", child))
                        yield return item;
            }

            var app = element as Application;
            if (app != null)
            {
                foreach (var child in app.VirtualDirectories)
                    foreach (var item in ListVirtualDirectories(path + "/" + app.Path, child))
                        yield return item;
            }

            var vdir = element as VirtualDirectory;
            if (vdir != null)
            {
                yield return new VirtualDirectoryNode(path + "/" + vdir.Path, vdir);

                foreach (var child in vdir.ChildElements)
                    foreach (var item in ListVirtualDirectories(path + "/" + vdir.Path, child))
                        yield return item;
            }
        }

        public VirtualDirectory FindVirtualDirectory(string webSiteName, string virtualDirectoryPath)
        {
            VirtualDirectory? virtualDirectory = null;
            FindVirtualDirectory(webSiteName, virtualDirectoryPath, vd => virtualDirectory = vd);
            return virtualDirectory!;
        }

        public class VirtualDirectoryNode
        {
            public VirtualDirectoryNode(string fullVirtualPath, VirtualDirectory virtualDirectory)
            {
                FullVirtualPath = fullVirtualPath;
                VirtualDirectory = virtualDirectory;
            }

            public string FullVirtualPath { get; set; }
            public VirtualDirectory VirtualDirectory { get; set; }
        }

        public Site GetWebSite(string webSiteName)
        {
            var site = FindWebSite(webSiteName);
            if (site == null)
            {
                throw new Exception($"The site '{webSiteName}'  does not exist.");
            }

            return site;
        }

        public Site FindWebSite(string webSiteName)
        {
            return Execute(serverManager => serverManager.Sites.FirstOrDefault(x => String.Equals(x.Name, webSiteName, StringComparison.OrdinalIgnoreCase)));
        }

        public bool WebSiteExists(string webSiteName)
        {
            return FindWebSite(webSiteName) != null;
        }

        public ApplicationPool GetApplicationPool(string applicationPoolName)
        {
            var applicationPool = FindApplicationPool(applicationPoolName);
            if (applicationPool == null)
            {
                throw new Exception($"The application pool '{applicationPoolName}'  does not exist.");
            }

            return applicationPool;
        }

        public ApplicationPool FindApplicationPool(string applicationPoolName)
        {
            return Execute(serverManager => serverManager.ApplicationPools.FirstOrDefault(x => String.Equals(x.Name, applicationPoolName, StringComparison.OrdinalIgnoreCase)));              
        }

        public bool ApplicationPoolExists(string applicationPool)
        {
            return FindApplicationPool(applicationPool) != null;
        }

        private void Execute(Action<ServerManager> action)
        {
            using (var serverManager = ServerManager.OpenRemote(Localhost))
            {
                action(serverManager);
            }
        }

        private TResult Execute<TResult>(Func<ServerManager, TResult> func)
        {
            var result = default(TResult);
            Action<ServerManager> action = serverManager => result = func(serverManager);
            Execute(action);
            return result!;
        }
    }
}