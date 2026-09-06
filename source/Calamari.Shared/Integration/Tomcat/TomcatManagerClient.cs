using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Polly;
using Polly.Retry;

namespace Calamari.Integration.Tomcat
{
    /// <summary>
    /// A client for Tomcat's Manager "/text/..." HTTP API, replacing the Java-based
    /// com.octopus.calamari.tomcat.TomcatDeploy/TomcatState (Octopus.Dependencies.Java) with a plain
    /// HttpClient. No JVM or Octopus.Dependencies.Java tool package is required to talk to Tomcat's
    /// Manager app - it's just an authenticated HTTP call.
    /// </summary>
    public class TomcatManagerClient
    {
        // Matches Constants.CONNECTION_TIMEOUT (300 seconds) used by the existing deploy/redeploy calls.
        static readonly TimeSpan DeployTimeout = TimeSpan.FromMinutes(5);

        readonly ILog log;
        readonly HttpClient httpClient;

        public TomcatManagerClient(ILog log, HttpClient? httpClient = null)
        {
            this.log = log;
            this.httpClient = httpClient ?? new HttpClient();
        }

        public void Deploy(TomcatManagerOptions options, string applicationPath)
        {
            ExecuteWithRetry(() =>
            {
                using (var request = new HttpRequestMessage(HttpMethod.Put, options.DeployUrl))
                {
                    AddAuth(request, options);
                    using (var fileStream = File.OpenRead(applicationPath))
                    {
                        request.Content = new StreamContent(fileStream);
                        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                        Send(request, DeployTimeout);
                    }
                }
            });
        }

        public void Redeploy(TomcatManagerOptions options)
        {
            ExecuteWithRetry(() => SendGet(options.RedeployUrl, options, DeployTimeout));
        }

        public void Undeploy(TomcatManagerOptions options)
        {
            ExecuteWithRetry(() => SendGet(options.UndeployUrl, options));
        }

        public void Start(TomcatManagerOptions options)
        {
            ExecuteWithRetry(() => SendGet(options.StartUrl, options));
        }

        public void Stop(TomcatManagerOptions options)
        {
            ExecuteWithRetry(() => SendGet(options.StopUrl, options));
        }

        public string List(TomcatManagerOptions options)
        {
            string? result = null;
            ExecuteWithRetry(() => { result = SendGet(options.ListUrl, options); });
            return result!;
        }

        /// <summary>
        /// Confirms the application is in the expected running/stopped state (and, if a version was
        /// specified, that the expected version is the one currently deployed) by parsing Tomcat
        /// Manager's plain-text "list" response. Matches com.octopus.calamari.tomcat.TomcatState's
        /// verification logic, including its notable leniency: if no matching line is found, this is
        /// logged as a warning rather than treated as a failure, because Tomcat's Manager API doesn't
        /// return an error in that case either.
        /// </summary>
        public bool VerifyState(TomcatManagerOptions options, bool expectRunning)
        {
            var listBody = List(options);
            var expectedPath = "/" + (options.UrlPath ?? "");
            var expectedState = expectRunning ? "running" : "stopped";

            foreach (var line in listBody.Split('\n'))
            {
                var tokens = line.Split(':').Select(t => t.Trim()).Where(t => t.Length > 0).ToArray();
                if (tokens.Length != 4)
                    continue;

                var path = tokens[0];
                var state = tokens[1];
                var displayName = tokens[3];

                if (path != expectedPath || state != expectedState)
                    continue;

                if (!string.IsNullOrWhiteSpace(options.Version))
                {
                    var displayNameParts = displayName.Split(new[] { "##" }, StringSplitOptions.None);
                    if (displayNameParts.Length < 2 || displayNameParts[^1] != options.Version)
                        continue;
                }
                else if (displayName.Contains("##"))
                {
                    continue;
                }

                return true;
            }

            log.Warn($"Could not confirm that \"{expectedPath}\" is {expectedState} - Tomcat's Manager API did not report an error, so this is not treated as a failure.");
            return false;
        }

        void SendGet(string url, TomcatManagerOptions options, TimeSpan? timeout = null)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                AddAuth(request, options);
                Send(request, timeout);
            }
        }

        string SendGet(string url, TomcatManagerOptions options)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                AddAuth(request, options);
                return Send(request, null);
            }
        }

        static void AddAuth(HttpRequestMessage request, TomcatManagerOptions options)
        {
            request.Headers.TryAddWithoutValidation("Authorization", options.AuthorizationHeaderValue);
        }

        string Send(HttpRequestMessage request, TimeSpan? timeout)
        {
            using (var cts = timeout.HasValue ? new CancellationTokenSource(timeout.Value) : null)
            {
                var response = httpClient.Send(request, cts?.Token ?? CancellationToken.None);
                using (var reader = new StreamReader(response.Content.ReadAsStream()))
                {
                    var body = reader.ReadToEnd();
                    ValidateResponse((int)response.StatusCode, body);
                    return body;
                }
            }
        }

        static void ValidateResponse(int statusCode, string body)
        {
            switch (statusCode)
            {
                case 401:
                    throw new TomcatAuthenticationException("Tomcat Manager returned 401 Unauthorized. Check the username and password supplied to the step.");
                case 403:
                    throw new TomcatAuthenticationException("Tomcat Manager returned 403 Forbidden. Make sure the user is part of the manager-script role in tomcat-users.xml.");
                default:
                    if (statusCode < 100 || statusCode > 399)
                        throw new CommandException($"Tomcat Manager response code {statusCode} indicated failure: {body}");
                    break;
            }
        }

        static void ExecuteWithRetry(Action action)
        {
            CreateRetryPipeline().Execute(action);
        }

        static ResiliencePipeline CreateRetryPipeline()
        {
            return new ResiliencePipelineBuilder()
                   .AddRetry(new RetryStrategyOptions
                   {
                       // Bad credentials will fail exactly the same way on every attempt, so retrying
                       // them wastes 75 seconds for no benefit. Only genuinely-could-be-transient
                       // failures (connection issues, unexpected response codes that might clear up on
                       // their own) are retried.
                       ShouldHandle = new PredicateBuilder().Handle<Exception>(ex => ex is not TomcatAuthenticationException),
                       MaxRetryAttempts = 4,
                       Delay = TimeSpan.FromSeconds(5),
                       BackoffType = DelayBackoffType.Exponential
                   })
                   .Build();
        }
    }

    public class TomcatAuthenticationException : CommandException
    {
        public TomcatAuthenticationException(string message) : base(message)
        {
        }
    }
}
