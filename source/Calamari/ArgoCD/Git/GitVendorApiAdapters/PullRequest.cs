#nullable enable
using System;

namespace Calamari.ArgoCD.Git.GitVendorApiAdapters
{
    public class PullRequest
    {
        public PullRequest(string title, long number, string url)
        {
            Title = title;
            Number = number;
            Url = url;
        }

        public string Title { get; set; }
        public long Number { get; set; }
        public string Url { get; set; }
    }
}