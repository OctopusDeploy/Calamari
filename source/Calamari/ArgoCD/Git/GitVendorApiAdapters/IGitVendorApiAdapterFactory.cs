#nullable enable
using System;

namespace Calamari.ArgoCD.Git.GitVendorApiAdapters
{
    /// <summary>
    /// The interface which each vendor specific api adapter reports its applicability given the connection details provided.
    /// The <see cref="CreateGitVendorApiAdaptor"/> method should NOT be called unless the class repots a success from the <see cref="CanInvokeWith"/> method.
    /// </summary>
    public interface IGitVendorApiAdapterFactory
    {
        //bool CanInvokeWith(IRepositoryConnection repositoryConnection);
        IGitVendorApiAdapter? TryCreateGitVendorApiAdaptor(IRepositoryConnection repositoryConnection);
    }
}