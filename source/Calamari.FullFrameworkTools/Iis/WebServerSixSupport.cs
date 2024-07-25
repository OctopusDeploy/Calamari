using System;
using System.DirectoryServices;
using System.Linq;

namespace Calamari.FullFrameworkTools.Iis
{
    public class WebServerSixSupport : WebServerSupport
    {
        public override void CreateWebSiteOrVirtualDirectory(string webSiteName, string virtualDirectoryPath, string webRootPath, int port)
        {
            var siteId = GetSiteId(webSiteName);
            if (siteId == null)
            {
                using (var w3Svc = new DirectoryEntry("IIS://localhost/w3svc"))
                {
                    siteId = ((int) w3Svc.Invoke("CreateNewSite", new object[] { webSiteName, new object[] { "*:" + port + ":" }, webRootPath })).ToString();
                    w3Svc.CommitChanges();
                }
            }

            var virtualParts = (virtualDirectoryPath ?? string.Empty).Split('/', '\\').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();

            if (virtualParts.Length == 0)
                return;

            CreateVirtualDirectory("IIS://localhost/w3svc/" + siteId + "/Root", virtualParts[0], webRootPath, virtualParts.Skip(1).ToArray());
        }

        static void CreateVirtualDirectory(string parentPath, string name, string homeDirectory, string[] remainingPaths)
        {
            name = name.Trim('/');

            using (var parent = new DirectoryEntry(parentPath))
            {
                string existingChildPath = null;

                foreach (var child in parent.Children.OfType<DirectoryEntry>())
                {
                    if (child.SchemaClassName == "IIsWebVirtualDir" && child.Name.ToLowerInvariant() == name.ToLowerInvariant())
                    {
                        existingChildPath = child.Path;
                    }

                    child.Close();
                }

                if (existingChildPath == null)
                {
                    var child = parent.Children.Add(name.Trim('/'), "IIsWebVirtualDir");
                    child.Properties["Path"][0] = homeDirectory;
                    child.CommitChanges();
                    parent.CommitChanges();
                    existingChildPath = child.Path;
                    child.Close();
                }

                if (remainingPaths.Length > 0)
                {
                    CreateVirtualDirectory(existingChildPath, remainingPaths.First(), homeDirectory, remainingPaths.Skip(1).ToArray());
                }
            }
        }

        public override string GetHomeDirectory(string webSiteName, string virtualDirectoryPath)
        {
            var siteId = GetSiteId(webSiteName);
            if (siteId == null)
            {
                throw new Exception("The site: " + webSiteName + " does not exist");
            }

            var root = "IIS://localhost/w3svc/" + siteId + "/Root";

            var virtualParts = (virtualDirectoryPath ?? string.Empty).Split('/', '\\').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
            if (virtualParts.Length == 0)
            {
                using (var entry = new DirectoryEntry(root))
                {
                    return (string) entry.Properties["Path"][0];
                }
            }

            return FindHomeDirectory(root, virtualParts[0], virtualParts.Skip(1).ToArray());
        }

        static string FindHomeDirectory(string parentPath, string virtualDirectoryName, string[] childDirectoryNames)
        {
            using (var parent = new DirectoryEntry(parentPath))
            {
                string existingChildPath = null;

                foreach (var child in parent.Children.OfType<DirectoryEntry>())
                {
                    if (child.SchemaClassName == "IIsWebVirtualDir" && child.Name.ToLowerInvariant() == virtualDirectoryName.ToLowerInvariant())
                    {
                        if (childDirectoryNames.Length == 0)
                        {
                            return (string) child.Properties["Path"][0];
                        }

                        existingChildPath = child.Path;
                    }

                    child.Close();
                }

                if (existingChildPath == null)
                {
                    throw new Exception("The virtual directory: " + virtualDirectoryName + " does not exist");
                }
                
                return FindHomeDirectory(existingChildPath, childDirectoryNames.First(), childDirectoryNames.Skip(1).ToArray());
            }
        }

        public override void DeleteWebSite(string webSiteName)
        {
            var id = GetSiteId(webSiteName);
            if (id == null)
                return;

            using (var w3Svc = new DirectoryEntry("IIS://localhost/w3svc"))
            {
                var child = w3Svc.Children.Find(id, "IIsWebServer");
                child.DeleteTree();
                child.Dispose();

                w3Svc.CommitChanges();
            }
        }

        public override bool ChangeHomeDirectory(string iisWebSiteName, string virtualDirectory, string newWebRootPath)
        {
            var iisRoot = new DirectoryEntry("IIS://localhost/W3SVC");
            iisRoot.RefreshCache();
            var result = false;

            foreach (DirectoryEntry webSite in iisRoot.Children)
            {
                if (webSite.SchemaClassName == "IIsWebServer"
                    && webSite.Properties.Contains("ServerComment")
                    && (string)webSite.Properties["ServerComment"].Value == iisWebSiteName)
                {
                    foreach (DirectoryEntry webRoot in webSite.Children)
                    {
                        if (virtualDirectory == null)
                        {
                            if (webRoot.Properties.Contains("Path"))
                            {
                                webRoot.Properties["Path"].Value = newWebRootPath;
                                webRoot.CommitChanges();
                                result = true;
                            }
                        }
                        else
                        {
                            try
                            {
                                var virtualDir = webRoot.Children.Find(virtualDirectory, "IIsWebVirtualDir");
                                virtualDir.Properties["Path"].Value = newWebRootPath;
                                virtualDir.CommitChanges();
                                result = true;
                            }
                            catch (Exception ex)
                            {
                                Console.Error.Write("Unable to find the virtual directory '{0}': {1}", virtualDirectory, ex.Message);
                                result = false;
                            }
                        }

                        webRoot.Close();
                    }
                }

                webSite.Close();
            }

            iisRoot.Close();
            return result;
        }

        static string GetSiteId(string webSiteName)
        {
            string result = null;

            using (var w3Svc = new DirectoryEntry("IIS://localhost/w3svc"))
            {
                foreach (var child in w3Svc.Children.OfType<DirectoryEntry>())
                {
                    if (child.SchemaClassName == "IIsWebServer" && (string)child.Properties["ServerComment"].Value == webSiteName)
                    {
                        result = child.Name;
                    }

                    child.Dispose();
                }
            }

            return result;
        }
    }
}