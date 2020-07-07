using System;
using System.IO;
using System.Net;
using System.Net.Http;
using Autofac;
using Calamari.Commands;
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
        public void BasicGet()
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
    }
}