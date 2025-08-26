using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes;

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages;

public class ArgoCDImageUpdateOutputWriter(ILog log)
{
    readonly ILog log = log;

    public void WriteImageUpdateOutput(IEnumerable<ArgoCDGatewayId> gateways, IEnumerable<Uri> gitRepos,
        IEnumerable<string> totalApplications, IEnumerable<string> updatedApplications, int imagesUpdatedCount)
    {
        var gatewayIds = ToCommaSeparatedString(gateways.Select(g => g.Value));
        var gitUris = ToCommaSeparatedString(gitRepos.Select(u => u.ToString()));
        var totalApps = ToCommaSeparatedString(totalApplications);
        var updatedApps = ToCommaSeparatedString(updatedApplications);
        IV
    }

    static string ToCommaSeparatedString(IEnumerable<string> items)
    {
        return string.Join(", ", items);
    }
}
