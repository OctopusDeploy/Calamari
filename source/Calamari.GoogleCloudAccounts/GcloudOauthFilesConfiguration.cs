using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.GoogleCloudAccounts
{
    /// <summary>
    /// Gcloud oauth uses the jwt (as a file on disk) to create another creds config file.
    /// This file is only used once the first request is made from either the CLI or terraform and is required on disk for the lifetime of the auth session.
    /// We do not want this file persisted on disk after the session (deployment) is over.
    /// </summary>
    public class GcloudOAuthFileConfiguration : IDisposable
    {
        public readonly TemporaryFile JwtFile;
        public readonly TemporaryFile JsonAuthFile;
        public readonly string WorkingDirectory;

        /// <summary>
        /// Configures the auth file locations of the gcloud cli oauth flows.
        /// </summary>
        /// <param name="workingDirectory">The provided workingDirectory should always be within the Work folder and be cleaned up post deployment.</param>
        public GcloudOAuthFileConfiguration(string workingDirectory)
        {
            // Create temporary files for JWT authentication
            JwtFile = new TemporaryFile(Path.Combine(workingDirectory, "jwt_token.txt"));
            JsonAuthFile = new TemporaryFile(Path.Combine(workingDirectory, "auth_config.json"));
            WorkingDirectory = workingDirectory;
        }

        public void Dispose()
        {
            JwtFile.Dispose();
            JsonAuthFile.Dispose();
        }
    }
}