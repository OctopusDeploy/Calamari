using System;
using System.IO;
using System.Net;
using System.Net.Http;
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
        public void BasicGet()
        {
            // Arrange
            Variables.Set(SpecialVariables.Action.HttpRequest.HttpMethod, "GET");
            Variables.Set(SpecialVariables.Action.HttpRequest.Url, "https://octopus.com");
            HttpMessageHandlerMock
                .Expect(request => request.RequestUri ==  new Uri("https://octopus.com") && request.Method == HttpMethod.Get)
                .Return(new HttpResponseMessage(HttpStatusCode.OK){Content = new StringContent("Hello!")});
            
            var result = InvokeCommand();
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
    }
}