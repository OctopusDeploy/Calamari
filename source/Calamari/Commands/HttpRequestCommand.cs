using System;
using Calamari.Commands.Support;
using System.Net.Http;
using Calamari.Deployment;

namespace Calamari.Commands
{
    [Command("http-request", Description = "Sends a HTTP request")]
    public class HttpRequestCommand : Command
    {
        readonly ILog log;
        readonly IVariables variables;

        public HttpRequestCommand(ILog log, IVariables variables)
        {
            this.log = log;
            this.variables = variables;
        }

        public override int Execute(string[] commandLineArguments)
        {
            var httpMethod = EvaluateHttpMethod(variables);
            var url = EvaluateUrl(variables);
            var timeout = EvaluateTimeout(variables);

            log.Info($"Sending HTTP {httpMethod.Method} to {url}");
            var request = new HttpRequestMessage(httpMethod, url);
            using var client = new HttpClient();
            if (timeout > 0)
            {
                log.Verbose($"Timeout: {timeout} seconds");
                client.Timeout = TimeSpan.FromSeconds(timeout);
            }

            var task = client.SendAsync(request);
            task.Wait();

            var response = task.Result;
            log.Info($"Response received with status {response.StatusCode}");
            return 0;
        }

        static HttpMethod EvaluateHttpMethod(IVariables variables)
        {
            var evaluatedMethod = variables.Get(SpecialVariables.Action.HttpRequest.HttpMethod);
            if (string.IsNullOrWhiteSpace(evaluatedMethod))
                throw new CommandException($"Variable value not supplied for {SpecialVariables.Action.HttpRequest.HttpMethod}");

            return new HttpMethod(evaluatedMethod);
        }

        static Uri EvaluateUrl(IVariables variables)
        {
            if (Uri.TryCreate(variables.Get(SpecialVariables.Action.HttpRequest.Url), UriKind.Absolute, out var url)
            && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps))
            {
                return url;
            }

            throw new CommandException($"Variable {SpecialVariables.Action.HttpRequest.Url} did not contain a valid HTTP URL");
        }

        static int EvaluateTimeout(IVariables variables)
        {
            if (int.TryParse(variables.Get(SpecialVariables.Action.HttpRequest.Timeout), out var timeout))
            {
                return timeout;
            }

            return 0;
        }
    }
}