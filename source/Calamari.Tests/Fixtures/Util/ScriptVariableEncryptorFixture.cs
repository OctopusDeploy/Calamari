using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Calamari.Util;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Util
{
    [TestFixture]
    class ScriptVariableEncryptorFixture
    {
        [Test]
        public void ScriptVariableEncryptor()
        {
            var passphrase = "PurpleMonkeyDishwasher";
            var text = "Put It In H!";

            var encryptor = new ScriptVariableEncryptor(passphrase);
            Assert.AreEqual(text, encryptor.Decrypt(encryptor.Encrypt(text)));
        }
    }
}
