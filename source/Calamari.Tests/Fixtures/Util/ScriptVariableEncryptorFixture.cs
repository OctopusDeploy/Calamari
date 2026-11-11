using Calamari.Common.Plumbing.Extensions;
using Calamari.Util;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Calamari.Tests.Fixtures.Util
{
    [TestFixture]
    class ScriptVariableEncryptorFixture
    {
        [Test]
        public void EncryptionIsSymmetrical()
        {
            var passphrase = "PurpleMonkeyDishwasher";
            var text = "Put It In H!";

            var encryptor = AesEncryption.ForScripts(passphrase);
            ClassicAssert.AreEqual(text, encryptor.Decrypt(encryptor.Encrypt(text)));
        }
    }
}
