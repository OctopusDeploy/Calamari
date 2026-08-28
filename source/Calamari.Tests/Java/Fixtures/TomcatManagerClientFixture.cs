using System;
using System.IO;
using System.Net;
using System.Text;
using Calamari.Common.Commands;
using Calamari.Integration.Tomcat;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Java.Fixtures
{
    /// <summary>
    /// Exercises TomcatManagerClient against a real local HTTP server that mimics Tomcat Manager's
    /// "/text/..." plain-text API, rather than mocking HttpClient - this is closer to what actually
    /// happens on the wire (headers, status codes, body encoding) than a mocked handler would be.
    /// </summary>
    [TestFixture]
    public class TomcatManagerClientFixture
    {
        HttpListener listener;
        string baseUrl;
        InMemoryLog log;
        string workingDirectory;

        string lastMethod;
        string lastPath;
        string lastAuthHeader;
        string lastRequestBody;
        int nextResponseStatusCode;
        string nextResponseBody;

        [SetUp]
        public void SetUp()
        {
            // NUnit reuses one fixture instance across every [Test] in this class, so these have to be
            // reset here rather than via field initializers - otherwise a status code set by one test
            // leaks into whichever test happens to run next.
            nextResponseStatusCode = 200;
            nextResponseBody = "OK - Deployed application";

            log = new InMemoryLog();
            workingDirectory = Path.Combine(Path.GetTempPath(), "TomcatManagerClientFixture-" + Guid.NewGuid());
            Directory.CreateDirectory(workingDirectory);

            var port = GetFreeTcpPort();
            baseUrl = $"http://localhost:{port}/manager";

            listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();
            listener.BeginGetContext(OnRequest, null);
        }

        [TearDown]
        public void TearDown()
        {
            listener.Stop();
            listener.Close();
            if (Directory.Exists(workingDirectory))
                Directory.Delete(workingDirectory, true);
        }

        void OnRequest(IAsyncResult result)
        {
            HttpListenerContext context;
            try
            {
                context = listener.EndGetContext(result);
            }
            catch (ObjectDisposedException)
            {
                return;
            }

            listener.BeginGetContext(OnRequest, null);

            lastMethod = context.Request.HttpMethod;
            lastPath = context.Request.Url?.PathAndQuery;
            lastAuthHeader = context.Request.Headers["Authorization"];

            using (var reader = new StreamReader(context.Request.InputStream))
            {
                lastRequestBody = reader.ReadToEnd();
            }

            context.Response.StatusCode = nextResponseStatusCode;
            var bytes = Encoding.UTF8.GetBytes(nextResponseBody);
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.OutputStream.Close();
        }

        static int GetFreeTcpPort()
        {
            var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        [Test]
        public void Deploy_SendsAPutWithTheFileContentsAndBasicAuth()
        {
            var applicationPath = Path.Combine(workingDirectory, "myapp.war");
            File.WriteAllText(applicationPath, "war file contents");

            var options = new TomcatManagerOptions(baseUrl, "admin", "sekret", "", "myapp", "", "");
            var client = new TomcatManagerClient(log);

            client.Deploy(options, applicationPath);

            lastMethod.Should().Be("PUT");
            lastPath.Should().Be("/manager/text/deploy?update=true&path=/myapp");
            lastAuthHeader.Should().Be("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:sekret")));
            lastRequestBody.Should().Be("war file contents");
        }

        [Test]
        public void Start_SendsAGetToTheStartEndpoint()
        {
            var options = new TomcatManagerOptions(baseUrl, "admin", "sekret", "", "myapp", "", "");
            var client = new TomcatManagerClient(log);

            client.Start(options);

            lastMethod.Should().Be("GET");
            lastPath.Should().Be("/manager/text/start?path=/myapp");
        }

        [Test]
        public void VerifyState_WhenListContainsAMatchingRunningLine_ReturnsTrue()
        {
            nextResponseBody = "OK - Listed applications for virtual host localhost\n/myapp:running:0:myapp\n/:running:0:ROOT\n";

            var options = new TomcatManagerOptions(baseUrl, "admin", "sekret", "", "myapp", "", "");
            var client = new TomcatManagerClient(log);

            client.VerifyState(options, expectRunning: true).Should().BeTrue();
        }

        [Test]
        public void VerifyState_WhenNoLineMatches_ReturnsFalseWithoutThrowing()
        {
            nextResponseBody = "OK - Listed applications for virtual host localhost\n/other:running:0:other\n";

            var options = new TomcatManagerOptions(baseUrl, "admin", "sekret", "", "myapp", "", "");
            var client = new TomcatManagerClient(log);

            client.VerifyState(options, expectRunning: true).Should().BeFalse();
        }

        [Test]
        public void VerifyState_WithAVersionedDeployment_OnlyMatchesTheExpectedVersion()
        {
            nextResponseBody = "OK - Listed applications for virtual host localhost\n/myapp:running:0:myapp##2.0\n/myapp:running:0:myapp##1.0\n";

            var options = new TomcatManagerOptions(baseUrl, "admin", "sekret", "", "myapp", "", "2.0");
            var client = new TomcatManagerClient(log);

            client.VerifyState(options, expectRunning: true).Should().BeTrue();
        }

        [Test]
        public void Start_WhenServerReturns401_ThrowsCommandExceptionMentioningUnauthorized()
        {
            nextResponseStatusCode = 401;
            nextResponseBody = "";

            var options = new TomcatManagerOptions(baseUrl, "admin", "wrong-password", "", "myapp", "", "");
            var client = new TomcatManagerClient(log);

            Action act = () => client.Start(options);

            act.Should().Throw<CommandException>().WithMessage("*401*");
        }

        [Test]
        public void Start_WhenServerReturns403_ThrowsCommandExceptionMentioningManagerScriptRole()
        {
            nextResponseStatusCode = 403;
            nextResponseBody = "";

            var options = new TomcatManagerOptions(baseUrl, "admin", "sekret", "", "myapp", "", "");
            var client = new TomcatManagerClient(log);

            Action act = () => client.Start(options);

            act.Should().Throw<CommandException>().WithMessage("*manager-script*");
        }
    }
}
