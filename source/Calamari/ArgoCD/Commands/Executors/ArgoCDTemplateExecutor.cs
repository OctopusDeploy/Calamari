using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.ECR.Model;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Calamari.Util;
using LibGit2Sharp;
using Repository = LibGit2Sharp.Repository;

namespace Calamari.ArgoCD.Commands.Executors
{

public class ArgoCDTemplateExecutor
{
    private readonly string argoSubDirectory = "argo";
    readonly ICalamariFileSystem fileSystem;
    readonly ILog log;
    
    public async Task<bool> Execute(RunningDeployment deployment, Func<ResourceIdentifier[], Task> appliedResourcesCallback = null)
    {
        const string URL = "https://github.com/rain-on/argocd-example-apps";
        const string branchName = "main"; //this COULD be 'HEAD'
        const string username = "username";
        const string password = "password";
        List<string> globs = new List<string> { "*.yaml" };

        //This may not be needed, or maybe it is.
        const string proxyURL = "proxy.url.com";
        const string proxyUsername = "proxy.username";
        const string proxyPassword = "proxy.password";
        
        // at the time this is called - the package directory SHOULD be all populated and happy with templated files - we just need to copy them into
        // the git repository supplied via the variables
        
        //Does a deployment get its own directory every time? If so - this will work for now, if not, this is kinda messy.
        var argoRepoPath = Path.Combine(deployment.CurrentDirectory, argoSubDirectory);
        var options = new CloneOptions
        {
            BranchName = branchName,
        };

        var repoPath = Repository.Clone(URL, argoRepoPath, options);
        var repo = new Repository(repoPath);
        
        var relativeGlobber = new RelativeGlobber((@base, pattern) => fileSystem.EnumerateFilesWithGlob(@base, pattern), deployment.StagingDirectory);
        var files = globs.SelectMany(glob => relativeGlobber.EnumerateFilesWithGlob(glob)).ToList();

        foreach (var file in files)
        {
            File.Copy(file.MappedRelativePath, Path(repoPath, file.FilePath), true);
        }
        
        
    }
    
}
    
}