using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Util;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Common.Util;

[TestFixture]
public class TypeExtensionMethodsFixture
{
    public class GetCommandNameFromAttribute
    {
        [Test]
        public void WhenAttributeNotPresent_ReturnsEmptyString()
        {
            var input = new object();
            
            var result = input.GetType().GetCommandNameFromAttribute();
            
            result.Should().BeEmpty();
        }

        [Test]
        public void WhenAttributePresent_ReturnsCommandName()
        {
            

            var input = new InputClass();
            
            var result = input.GetType().GetCommandNameFromAttribute();

            result.Should().Be("command-name");
        }
    }
    
    
    [Command("command-name") ]
    class InputClass : ICommandWithArgs
    {
        public int Execute(string[] args)
        {
            return 0;
        }
    }
    
}