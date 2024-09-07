using System;
using System.IO;
using Calamari.FullFrameworkTools.Command;
using Calamari.FullFrameworkTools.Utils;
using Calamari.FullFrameworkTools.WindowsCertStore;
using FluentAssertions;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.FullFrameworkTools.Tests.Command
{
    [TestFixture]
    public class CommandRequestInvokerTests
    {
        readonly IRequestTypeLocator requestTypeLocator = Substitute.For<IRequestTypeLocator>();
        readonly ICommandHandler commandHandler = Substitute.For<ICommandHandler>();

        [Test]
        public void Foo()
        {
            var invoker = new CommandRequestInvoker(requestTypeLocator, commandHandler);
            requestTypeLocator.FindType(nameof(MockRequest)).Returns(typeof(MockRequest));
            var requestObject = new MockRequest(4, 5);
            var jsonRequestObj = JsonConvert.SerializeObject(requestObject);
            commandHandler.Handle(Arg.Is<MockRequest>(d => d.NumberOne == requestObject.NumberOne && d.NumberTwo == requestObject.NumberTwo))
                          .Returns(77);
            
            var result = invoker.Run(nameof(MockRequest), jsonRequestObj);
            
            result.Should().BeEquivalentTo(77);
        }
        
        [Test]
        public void SimpleHandlerEncrypted()
        {
            var invoker = new CommandRequestInvoker(requestTypeLocator, commandHandler);
            requestTypeLocator.FindType(nameof(MockRequest)).Returns(typeof(MockRequest));
            var requestObject = new MockRequest(4, 5);
            var jsonRequestObj = JsonConvert.SerializeObject(requestObject);

            var password = "pass23HJka";
            var enc = new AesEncryption(password);
            var encRequestObj = enc.Encrypt(jsonRequestObj);

            using (var temp = new TempFile(encRequestObj))
            {
                var result = invoker.Run(nameof(MockRequest),  password, temp.FilePath);
                
                result.Should().BeEquivalentTo(77);
            }
        }
        
    }

    
    public class TempFile : IDisposable
    {
        public TempFile(byte[] content)
        {
            FilePath = Path.GetTempFileName();
            File.WriteAllBytes(FilePath, content);
        }
            
        public string FilePath { get; }
            
        public void Dispose()
        {
            try
            {
                File.Delete(FilePath);
            }
            catch
            {
                // ignored
            }
        }
    }
    public class MockRequest : IRequest
    {
        public MockRequest(int numberOne, int numberTwo)
        {
            NumberOne = numberOne;
            NumberTwo = numberTwo;
        }

        public int NumberOne { get;  }
        public int NumberTwo { get; }
    } 
}