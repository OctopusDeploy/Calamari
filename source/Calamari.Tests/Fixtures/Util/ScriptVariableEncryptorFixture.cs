using Calamari.Common.Plumbing.Extensions;
using Calamari.Util;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Util
{
    [TestFixture]
    class ScriptVariableEncryptorFixture
    {
        [TestCase(128)]
        [TestCase(256)]
        public void EncryptionIsSymmetrical(int keySize)
        {
            var passphrase = "PurpleMonkeyDishwasher";
            var text = "Put It In H!";

            var encryptor = new AesEncryption(passphrase, keySize);
            Assert.AreEqual(text, encryptor.Decrypt(encryptor.Encrypt(text)));
        }
    }
}
