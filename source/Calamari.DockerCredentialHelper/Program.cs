using System;

namespace Calamari.DockerCredentialHelper
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("A credential operation is required (store, get, erase)");
                return 1;
            }

            var operation = args[0];
            // This helper is only ever invoked by Docker during a Calamari package acquisition.
            // Calamari sets DOCKER_CONFIG (where the encrypted .cred files live) and
            // OCTOPUS_CREDENTIAL_PASSWORD (the decryption key) before invoking Docker, and Docker
            // passes the environment through to this process. Both are therefore required.
            var encryptionPassword = Environment.GetEnvironmentVariable("OCTOPUS_CREDENTIAL_PASSWORD");
            var dockerConfigPath = Environment.GetEnvironmentVariable("DOCKER_CONFIG");

            if (string.IsNullOrEmpty(encryptionPassword))
            {
                Console.Error.WriteLine("OCTOPUS_CREDENTIAL_PASSWORD environment variable not set");
                return 1;
            }

            if (string.IsNullOrEmpty(dockerConfigPath))
            {
                Console.Error.WriteLine("DOCKER_CONFIG environment variable not set");
                return 1;
            }

            try
            {
                var protocol = new DockerCredentialProtocol(new DockerCredentialStore());
                return protocol.Run(operation, Console.In, Console.Out, Console.Error, encryptionPassword, dockerConfigPath);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Docker credential operation failed: {ex.Message}");
                return 1;
            }
        }
    }
}
