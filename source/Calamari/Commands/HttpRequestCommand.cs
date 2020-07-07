using System;
using Calamari.Commands.Support;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Calamari.Deployment;

namespace Calamari.Commands
{
    [Command("http-request", Description = "Sends a HTTP request")]
    public class HttpRequestCommand : Command
    {
        readonly ILog log;
        readonly IVariables variables;
        readonly HttpMessageHandler mockMessageHandler; // For testing only

        public HttpRequestCommand(ILog log, IVariables variables, HttpMessageHandler httpMessageHandler = null)
        {
            this.log = log;
            this.variables = variables;
            mockMessageHandler = httpMessageHandler;
        }

        public override int Execute(string[] commandLineArguments)
        {
            var httpMethod = EvaluateHttpMethod(variables);
            var url = EvaluateUrl(variables);
            var authentication = EvaluateAuthentication(variables);
            var body = EvaluateBody(variables);
            var contentType = EvaluateContentType(variables);
            var timeout = EvaluateTimeout(variables);
            var expectedResponseStatus = EvaluateExpectedResponseStatus(variables);

            log.Info($"Sending HTTP {httpMethod.Method} to {url}");
            var request = new HttpRequestMessage(httpMethod, url);

            if (!string.IsNullOrEmpty(body))
            {
                request.Content = new StringContent(body, Encoding.Default, !string.IsNullOrWhiteSpace(contentType) ? contentType : null);
            }
            
            if (authentication == Authentication.Basic)
            {
                log.Verbose("Using basic authentication");
                var username = variables.Get(SpecialVariables.Action.HttpRequest.UserName, "");
                var password = variables.Get(SpecialVariables.Action.HttpRequest.Password, "");
                
                request.Headers.Authorization = 
                    new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));
            }

            var httpMessageHandler = GetHttpMessageHandler(authentication == Authentication.DefaultCredentials);
            using (var client = new HttpClient(httpMessageHandler))
            {
                if (timeout > 0)
                {
                    log.Verbose($"Timeout: {timeout} seconds");
                    client.Timeout = TimeSpan.FromSeconds(timeout);
                }
                
                try
                {
                    var response = client.SendAsync(request).Result;
                    var responseStatusCode = response.StatusCode.ToString("D");

                    if (!string.IsNullOrEmpty(expectedResponseStatus))
                    {
                        log.Verbose($"Confirming response status code matches {expectedResponseStatus}");
                        var match = Regex.Match(responseStatusCode, expectedResponseStatus);
                        if (!match.Success)
                        {
                            throw new CommandException($"Response status {response.StatusCode} does not match expected pattern: {expectedResponseStatus}");
                        }
                        
                        log.Info($"Response status {response.StatusCode} matches expected pattern: {expectedResponseStatus}");
                    }
                    
                    log.Info($"Response received with status {response.StatusCode}"); 
                    log.SetOutputVariableButDoNotAddToVariables( SpecialVariables.Action.HttpRequest.Output.ResponseStatusCode, 
                        responseStatusCode);
                    log.SetOutputVariableButDoNotAddToVariables( SpecialVariables.Action.HttpRequest.Output.ResponseContent, 
                        response.Content?.ReadAsStringAsync().Result ?? "");
                }
                catch (AggregateException ex)
                {
                    ex.Handle(inner =>
                    {
                        if (inner is TaskCanceledException) // HTTPClient treats timeouts as a canceled task
                        {
                            throw new CommandException("HTTP request timed out", inner);
                        }

                        return false;
                    });
                    
                }

                return 0;
            }
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

        static string EvaluateBody(IVariables variables)
        {
            return variables.Get(SpecialVariables.Action.HttpRequest.Body);
        }

        static string EvaluateContentType(IVariables variables)
        {
            return variables.Get(SpecialVariables.Action.HttpRequest.ContentType);
        }

        static int EvaluateTimeout(IVariables variables)
        {
            if (int.TryParse(variables.Get(SpecialVariables.Action.HttpRequest.Timeout), out var timeout))
            {
                return timeout;
            }

            return 0;
        }

        static string EvaluateExpectedResponseStatus(IVariables variables)
        {
            return variables.Get(SpecialVariables.Action.HttpRequest.ExpectedResponseStatus)?.Trim();
        }

        static Authentication EvaluateAuthentication(IVariables variables)
        {
            var value = variables.Get(SpecialVariables.Action.HttpRequest.Authentication);
            if (string.IsNullOrWhiteSpace(value))
                return Authentication.None;
            
            if (Enum.TryParse<Authentication>(value, out var authentication))
                return authentication;
            
            throw new CommandException($"{value} is not a valid authentication option. Should be one of None, Basic, DefaultCredentials");
        }

        HttpMessageHandler GetHttpMessageHandler(bool useDefaultCredentials)
        {
            // Use a mock HttpMessageHandler if supplied (testing) otherwise return the real deal 
            return mockMessageHandler ?? new HttpClientHandler{UseDefaultCredentials = useDefaultCredentials};
        }

        enum Authentication
        {
           None,
           Basic,
           DefaultCredentials
        }
    }
}