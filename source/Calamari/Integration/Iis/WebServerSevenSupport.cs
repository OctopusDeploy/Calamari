using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using Microsoft.Web.Administration;

namespace Calamari.Integration.Iis
{
    public class WebServerSevenSupport : WebServerSupport
    {
        const string Localhost = "localhost";

        public override void CreateWebSiteOrVirtualDirectory(string webSiteName, string virtualDirectoryPath, string webRootPath, int port)
        {
            var virtualParts = (virtualDirectoryPath ?? String.Empty).Split('/', '\\').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();

            using (var serverManager = ServerManager.OpenRemote(Localhost))
            {
                var existing = serverManager.Sites.FirstOrDefault(x => String.Equals(x.Name, webSiteName, StringComparison.InvariantCultureIgnoreCase));
                if (existing == null)
                {
                    existing = serverManager.Sites.Add(webSiteName, webRootPath, port);                    
                }

                if (virtualParts.Length > 0)
                {
                    var app = existing.Applications.Add(virtualDirectoryPath, webRootPath);
                    //var vd = app.VirtualDirectories.Add(virtualDirectoryPath, webRootPath);
                }
                
                serverManager.CommitChanges();
            }
        }

        public override string GetHomeDirectory(string webSiteName, string virtualDirectoryPath)
        {
            string result = null;

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
            using (var serverManager = ServerManager.OpenRemote(Localhost))
            {
                var existing = serverManager.Sites.FirstOrDefault(x => String.Equals(x.Name, webSiteName, StringComparison.InvariantCultureIgnoreCase));
                if (existing == null)
                {
                    throw new Exception("The site does not exist");
                }

                existing.Delete();
                serverManager.CommitChanges();
            }
        }

        public override bool ChangeHomeDirectory(string webSiteName, string virtualDirectoryPath, string newWebRootPath)
        {
            var result = false;
            
            FindVirtualDirectory(webSiteName, virtualDirectoryPath, found =>
            {
                found.PhysicalPath = newWebRootPath;
                result = true;
            });
            
            return result;
        }

        static void FindVirtualDirectory(string webSiteName, string virtualDirectoryPath, Action<VirtualDirectory> found)
        {
            using (var serverManager = ServerManager.OpenRemote(Localhost))
            {
                var site = serverManager.Sites.FirstOrDefault(s => s.Name.ToLowerInvariant() == webSiteName.ToLowerInvariant());
                if (site == null) 
                    return;
                
                var virtuals = ListVirtualDirectories("", site);
                foreach (var vdir in virtuals)
                {
                    if (!string.Equals(Normalize(vdir.FullVirtualPath), Normalize(virtualDirectoryPath), StringComparison.InvariantCultureIgnoreCase)) 
                        continue;

                    found(vdir.VirtualDirectory);
                    serverManager.CommitChanges();
                }
            }
        }

        static string Normalize(string fullVirtualPath)
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
                yield return new VirtualDirectoryNode { FullVirtualPath = path + "/" + vdir.Path, VirtualDirectory = vdir };

                foreach (var child in vdir.ChildElements)
                    foreach (var item in ListVirtualDirectories(path + "/" + vdir.Path, child))
                        yield return item;
            }
        }

        public class VirtualDirectoryNode
        {
            public string FullVirtualPath { get; set; }
            public VirtualDirectory VirtualDirectory { get; set; }
        }
    }
}