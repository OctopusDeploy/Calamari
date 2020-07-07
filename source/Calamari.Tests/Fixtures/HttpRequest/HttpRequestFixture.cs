using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using Autofac;
using Calamari.Common.Variables;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.HttpRequest
{
    [TestFixture]
    public class HttpRequestFixture : CalamariFixture
    {
        ICalamariFileSystem FileSystem { get; set; }
        IVariables Variables { get; set; }
        HttpMessageHandlerMock HttpMessageHandlerMock { get; set; }
        
        
        [SetUp]
        public virtual void SetUp()
        {
            FileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
            Variables = new VariablesFactory(FileSystem).Create(new CommonOptions("test"));
            HttpMessageHandlerMock = new HttpMessageHandlerMock(); 
        }

        [Test]
        public void Get()
        {
            // Arrange
            Variables.Set(SpecialVariables.Action.HttpRequest.HttpMethod, "GET");
            Variables.Set(SpecialVariables.Action.HttpRequest.Url, "https://octopus.com");
            HttpMessageHandlerMock
                .Expect(request => request.RequestUri ==  new Uri("https://octopus.com") && request.Method == HttpMethod.Get)
                .Return(new HttpResponseMessage(HttpStatusCode.OK){Content = new StringContent("Hello!")});
            
            // Act
            var result = InvokeCommand();
            
            // Assert
            result.AssertSuccess();
            result.AssertOutputVariable(SpecialVariables.Action.HttpRequest.Output.ResponseStatusCode, Is.EqualTo("200"));
            result.AssertOutputVariable(SpecialVariables.Action.HttpRequest.Output.ResponseContent, Is.EqualTo("Hello!"));
        }

        [Test]
        public void PostWithBody()
        {
            // Arrange
            Variables.Set(SpecialVariables.Action.HttpRequest.HttpMethod, "POST");
            Variables.Set(SpecialVariables.Action.HttpRequest.Url, "https://octopus.com/greetings");
            Variables.Set(SpecialVariables.Action.HttpRequest.Body, "hello world");
            HttpMessageHandlerMock
                .Expect(request => request.RequestUri ==  new Uri("https://octopus.com/greetings") 
                                   && request.Method == HttpMethod.Post && HttpRequestContentsEquals(request, "hello world"))
                .Return(new HttpResponseMessage(HttpStatusCode.OK){Content = new StringContent("Hello!")});
            
            // Act
            var result = InvokeCommand();
            
            // Assert
            result.AssertSuccess();
            result.AssertOutputVariable(SpecialVariables.Action.HttpRequest.Output.ResponseStatusCode, Is.EqualTo("200"));
            result.AssertOutputVariable(SpecialVariables.Action.HttpRequest.Output.ResponseContent, Is.EqualTo("Hello!"));
        }

        [Test]
        public void ExpectedResponseStatusDoesNotMatch()
        {
            // Arrange
            Variables.Set(SpecialVariables.Action.HttpRequest.HttpMethod, "GET");
            Variables.Set(SpecialVariables.Action.HttpRequest.Url, "https://octopus.com");
            Variables.Set(SpecialVariables.Action.HttpRequest.ExpectedResponseStatus, "#{ResponseStatus}");
            Variables.Set("ResponseStatus", "200");
            
            HttpMessageHandlerMock
                .Expect(request => request.RequestUri ==  new Uri("https://octopus.com") && request.Method == HttpMethod.Get)
                .Return(new HttpResponseMessage(HttpStatusCode.Forbidden){Content = new StringContent("Nope!")});
            
            // Act
            var result = InvokeCommand();
            
            // Assert
            result.AssertFailure();
        }
        
        [Test]
        public void ExpectedResponseStatusDoesMatch()
        {
            // Arrange
            Variables.Set(SpecialVariables.Action.HttpRequest.HttpMethod, "GET");
            Variables.Set(SpecialVariables.Action.HttpRequest.Url, "https://octopus.com");
            Variables.Set(SpecialVariables.Action.HttpRequest.ExpectedResponseStatus, "#{ResponseStatus}");
            Variables.Set("ResponseStatus", "2.*");
            
            HttpMessageHandlerMock
                .Expect(request => request.RequestUri ==  new Uri("https://octopus.com") && request.Method == HttpMethod.Get)
                .Return(new HttpResponseMessage(HttpStatusCode.OK){Content = new StringContent("OK!")});
            
            // Act
            var result = InvokeCommand();
            
            // Assert
            result.AssertSuccess();
            result.AssertOutputVariable(SpecialVariables.Action.HttpRequest.Output.ResponseStatusCode, Is.EqualTo("200"));
            result.AssertOutputVariable(SpecialVariables.Action.HttpRequest.Output.ResponseContent, Is.EqualTo("OK!"));
        }

        [Test]
        public void TimeoutExceeded()
        {
            // Arrange
            Variables.Set(SpecialVariables.Action.HttpRequest.HttpMethod, "GET");
            Variables.Set(SpecialVariables.Action.HttpRequest.Url, "https://octopus.com");
            Variables.Set(SpecialVariables.Action.HttpRequest.Timeout, "10");
            HttpMessageHandlerMock
                .Expect(request => request.RequestUri ==  new Uri("https://octopus.com") && request.Method == HttpMethod.Get)
                .Return(new HttpResponseMessage(HttpStatusCode.OK){Content = new StringContent("Hello!")})
                .Duration(TimeSpan.FromSeconds(20));
            
            // Act
            var result = InvokeCommand();
            
            // Assert
            result.AssertFailure();
        }

        [Test]
        public void UsingBasicAuth()
        {
            // Arrange
            Variables.Set(SpecialVariables.Action.HttpRequest.HttpMethod, "GET");
            Variables.Set(SpecialVariables.Action.HttpRequest.Url, "https://octopus.com");
            Variables.Set(SpecialVariables.Action.HttpRequest.Authentication, "Basic");
            Variables.Set(SpecialVariables.Action.HttpRequest.UserName, "Roger.Ramjet");
            Variables.Set(SpecialVariables.Action.HttpRequest.Password, "!mN0tSup3rm4n");
            HttpMessageHandlerMock
                .Expect(request => request.RequestUri ==  new Uri("https://octopus.com") && request.Method == HttpMethod.Get 
                && BasicAuthorizationHeaderEquals(request, "Roger.Ramjet", "!mN0tSup3rm4n"))
                .Return(new HttpResponseMessage(HttpStatusCode.OK){Content = new StringContent("Hello!")});
            
            // Act
            var result = InvokeCommand();
            
            // Assert
            result.AssertSuccess();
            result.AssertOutputVariable(SpecialVariables.Action.HttpRequest.Output.ResponseStatusCode, Is.EqualTo("200"));
            result.AssertOutputVariable(SpecialVariables.Action.HttpRequest.Output.ResponseContent, Is.EqualTo("Hello!"));
        }
            
        CalamariResult InvokeCommand()
        {
            using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
            {
                Variables.Save(variablesFile.FilePath);

                return InvokeInProcess(Calamari()
                    .Action("http-request")
                    .Argument("variables", variablesFile.FilePath), 
                    configureContainer: containerBuilder => containerBuilder.RegisterInstance(HttpMessageHandlerMock).As<HttpMessageHandler>());
            }
        }

        static bool HttpRequestContentsEquals(HttpRequestMessage request, string expectedContents)
        {
            var contents = request.Content.ReadAsStringAsync().Result;
            return contents.Equals(expectedContents);
        }

        static bool BasicAuthorizationHeaderEquals(HttpRequestMessage request, string expectedUserName, string expectedPassword)
        {
            var expectedParameter = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{expectedUserName}:{expectedPassword}"));
            return request.Headers.Authorization.ToString() == $"Basic {expectedParameter}";
        }
    }
}