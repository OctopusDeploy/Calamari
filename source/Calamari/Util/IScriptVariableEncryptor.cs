namespace Calamari.Util
{
    public interface IScriptVariableEncryptor
    {
        string Encrypt(string text);

        string Decrypt(string text);
    }
}