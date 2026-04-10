using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using LibGit2Sharp;


namespace Calamari.ArgoCD.Git
{
    /// <summary>
    /// We found some credential caching issues in the default sub-transport implemented
    /// in LibGit2Sharp, and there was no easy way to fix it in the upstream library
    /// without breaking other existing authentication or exhausting sockets. Read
    /// more here: https://github.com/libgit2/libgit2sharp/issues/1894
    ///
    /// For now, we're have implemented this sub-transport to get around these issues.
    /// This code is pretty much taken wholesale from the LibGit2Sharp ManagedHttpSmartSubtransport
    /// https://github.com/OctopusDeploy/libgit2sharp/blob/master/LibGit2Sharp/Core/ManagedHttpSmartSubtransport.cs
    /// with a few changes (and simplifications) around authentication. We currently only
    /// support basic auth, but it seems to work nicely for what we need.
    /// </summary>
    /// <remarks>
    /// This was taken (almost) verbatim from the OctopusDeploy server code base Octopus.Core.Git.GitHttpSmartSubTransport.
    /// https://github.com/OctopusDeploy/OctopusDeploy/blob/2026.2.3071/source/Octopus.Core/Git/GitHttpSmartSubTransport.cs
    /// </remarks>
    public class GitHttpSmartSubTransport : RpcSmartSubtransport
    {
        protected override SmartSubtransportStream Action(string url, GitSmartSubtransportAction action)
        {
            string endpointUrl;
            string? contentType = null;
            var isPost = false;

            switch (action)
            {
                case GitSmartSubtransportAction.UploadPackList:
                    endpointUrl = string.Concat(url, "/info/refs?service=git-upload-pack");
                    break;

                case GitSmartSubtransportAction.UploadPack:
                    endpointUrl = string.Concat(url, "/git-upload-pack");
                    contentType = "application/x-git-upload-pack-request";
                    isPost = true;
                    break;

                case GitSmartSubtransportAction.ReceivePackList:
                    endpointUrl = string.Concat(url, "/info/refs?service=git-receive-pack");
                    break;

                case GitSmartSubtransportAction.ReceivePack:
                    endpointUrl = string.Concat(url, "/git-receive-pack");
                    contentType = "application/x-git-receive-pack-request";
                    isPost = true;
                    break;

                default:
                    throw new InvalidOperationException();
            }

            return new GitHttpSmartSubTransportStream(this, endpointUrl, isPost, contentType);
        }

        class GitHttpSmartSubTransportStream : SmartSubtransportStream
        {
            static readonly int MAX_REDIRECTS = 7;
            static readonly HttpClient HttpClient;

            readonly MemoryStream postBuffer = new();
            HttpResponseMessage? response;
            Stream? responseStream;

            static GitHttpSmartSubTransportStream()
            {
                var handler = new SocketsHttpHandler
                {
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                    AllowAutoRedirect = false
                };

#pragma warning disable HttpClientInstantiation
                HttpClient = new HttpClient(handler, false)
                {
                    DefaultRequestHeaders =
                    {
                        // This worked fine when it was on, but git.exe doesn't specify this header, so we don't either.
                        ExpectContinue = false
                    }
                };
#pragma warning restore HttpClientInstantiation
            }

            public GitHttpSmartSubTransportStream(GitHttpSmartSubTransport parent,
                string endpointUrl,
                bool isPost,
                string? contentType)
                : base(parent)
            {
                EndpointUrl = new Uri(endpointUrl);
                IsPost = isPost;
                ContentType = contentType;
            }

            Uri EndpointUrl { get; set; }
            bool IsPost { get; set; }
            string? ContentType { get; set; }

            string GetUserAgent()
            {
                var userAgent = GlobalSettings.GetUserAgent();

                if (string.IsNullOrEmpty(userAgent))
                {
                    userAgent = "LibGit2Sharp " + GlobalSettings.Version.InformationalVersion;
                }

                return userAgent;
            }

            HttpRequestMessage CreateRequest(Uri endpointUrl, bool isPost)
            {
                var verb = isPost ? new HttpMethod("POST") : new HttpMethod("GET");
                var request = new HttpRequestMessage(verb, endpointUrl);
                request.Headers.Add("User-Agent", $"git/2.0 ({GetUserAgent()})");
                request.Headers.Remove("Expect");

                return request;
            }

            HttpResponseMessage GetResponseWithRedirects()
            {
                var url = EndpointUrl;
                var credentials = GetCredentials();

                for (var retries = 0; retries < MAX_REDIRECTS; retries++)
                {
                    var request = CreateRequest(url, IsPost);

                    // This is the main difference between out implementation and the LibGit2Sharp
                    // library. Rather than first making an unauthorized request and then adding
                    // credentials to a CredentialCache if the request is rejected, we always
                    // add credentials to the first request.
                    if (credentials is UsernamePasswordCredentials usernamePasswordCredentials)
                    {
                        request.Headers.Authorization = GetBasicAuthenticationHeader(
                            usernamePasswordCredentials.Username,
                            usernamePasswordCredentials.Password
                        );
                    }

                    if (IsPost && postBuffer.Length > 0)
                    {
                        var bufferDup = new MemoryStream(postBuffer.GetBuffer(), 0, (int)postBuffer.Length);

                        request.Content = new StreamContent(bufferDup);
                        request.Content.Headers.Add("Content-Type", ContentType);
                    }

                    // This was originally configured with HttpCompletionOption.ResponseContentRead which would
                    // cause HttpClient timeout and buffer errors. Using the Stack Overflow post linked below
                    // as a guide, this has been changed to ResponseHeadersRead.
                    //
                    // The caller of this method was reads the content from the HttpResponseMessage as a stream
                    // anyway, so loads the response as it goes
                    // https://stackoverflow.com/questions/18720435/httpclient-buffer-size-limit-exceeded?rq=1
                    var response = HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                        .GetAwaiter()
                        .GetResult();

                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.OK:
                            return response;
                        case HttpStatusCode.Unauthorized:
                        case HttpStatusCode.Forbidden:
                        case HttpStatusCode.NotFound:
                            var responseContent = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                            throw new Exception(responseContent);
                        case HttpStatusCode.Moved or HttpStatusCode.Redirect:
                            url = new Uri(response.Headers.GetValues("Location").First());
                            continue;
                        default:
                            throw new Exception($"Unexpected HTTP response: {response.StatusCode}");
                    }
                }

                throw new Exception("Too many redirects");
            }

            Credentials GetCredentials()
            {
                var ret = SmartTransport.AcquireCredentials(
                    out var cred,
                    null,
                    typeof(UsernamePasswordCredentials));

                // GitErrorCode.PassThrough is returned when the credentialsProvider returns null
                // (https://github.com/libgit2/libgit2sharp/blob/5085a0c6173cdb2a3fde205330b327a8eb0a26c4/LibGit2Sharp/RemoteCallbacks.cs#L294)
                if (ret != 0 && ret != (int)GitErrorCode.PassThrough)
                {
                    throw new InvalidOperationException("Authentication cancelled");
                }

                return cred;
            }

            AuthenticationHeaderValue GetBasicAuthenticationHeader(string username, string password)
            {
                var authorizationHeaderValue = EncodeAuthorizationHeaderValue($"{username}:{password}");
                return new AuthenticationHeaderValue("Basic", authorizationHeaderValue);
            }

            string EncodeAuthorizationHeaderValue(string input)
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                return Convert.ToBase64String(bytes);
            }

            public override int Write(Stream dataStream, long length)
            {
                var buffer = new byte[4096];
                long writeTotal = 0;

                while (length > 0)
                {
                    var readLen = dataStream.Read(buffer, 0, (int)Math.Min(buffer.Length, length));

                    if (readLen == 0)
                    {
                        break;
                    }

                    postBuffer.Write(buffer, 0, readLen);
                    length -= readLen;
                    writeTotal += readLen;
                }

                if (writeTotal < length)
                {
                    throw new EndOfStreamException("Could not write buffer (short read)");
                }

                return 0;
            }

            public override int Read(Stream dataStream, long length, out long readTotal)
            {
                var buffer = new byte[4096];
                readTotal = 0;

                if (responseStream == null)
                {
                    response = GetResponseWithRedirects();
                    responseStream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                }

                while (length > 0)
                {
                    var readLen = responseStream.Read(buffer, 0, (int)Math.Min(buffer.Length, length));

                    if (readLen == 0)
                    {
                        break;
                    }

                    dataStream.Write(buffer, 0, readLen);
                    readTotal += readLen;
                    length -= readLen;
                }

                return 0;
            }

            protected override void Free()
            {
                if (responseStream != null)
                {
                    responseStream.Dispose();
                    responseStream = null;
                }

                if (response != null)
                {
                    response.Dispose();
                    response = null;
                }

                base.Free();
            }
        }

        /// <summary>
        /// Copy of internal enum representing error codes presented by LibGit2Sharp
        /// (https://github.com/libgit2/libgit2sharp/blob/5085a0c6173cdb2a3fde205330b327a8eb0a26c4/LibGit2Sharp/Core/GitErrorCode.cs#L122)
        /// </summary>
        enum GitErrorCode
        {
            /// <summary>
            /// Skip and passthrough the given ODB backend.
            /// </summary>
            PassThrough = -30,
        }
    }
}
