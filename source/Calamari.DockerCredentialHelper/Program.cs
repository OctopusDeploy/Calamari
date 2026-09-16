using System;
using System.IO;
using System.Text;

namespace Calamari.DockerCredentialHelper
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Out.WriteLine("A credential operation is required (store, get, erase)");
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
                Console.Out.WriteLine("OCTOPUS_CREDENTIAL_PASSWORD environment variable not set");
                return 1;
            }

            if (string.IsNullOrEmpty(dockerConfigPath))
            {
                Console.Out.WriteLine("DOCKER_CONFIG environment variable not set");
                return 1;
            }

            try
            {
                // Read/write the protocol streams as UTF-8 explicitly. Docker exchanges UTF-8 JSON over
                // stdin/stdout; relying on Console's default encoding would mangle non-ASCII credentials
                // on platforms whose console code page isn't UTF-8 (e.g. Windows).
                var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                using var input = new StreamReader(Console.OpenStandardInput(), utf8);
                using var output = new StreamWriter(Console.OpenStandardOutput(), utf8) { AutoFlush = true };

                var protocol = new DockerCredentialProtocol(new DockerCredentialStore());
                return protocol.Run(operation, input, output, encryptionPassword, dockerConfigPath);
            }
            catch (Exception ex)
            {
                Console.Out.WriteLine($"Docker credential operation failed: {ex.Message}");
                return 1;
            }
        }
    }
}
