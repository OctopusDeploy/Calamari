using System;
using System.Security.Cryptography;

namespace Calamari.Tests.Fixtures.Util
{
    /// <summary>
    /// This class largely implements the original method at https://referencesource.microsoft.com/#System.Web/Security/Membership.cs,302
    /// which is currently not available in netstandard.
    /// Feel free to remove this if\when it becomes available again in a standard library
    /// </summary>
    public static class PasswordGenerator
    {
        static readonly char[] Punctuations = "!@#$%^&*()_-+=[{]};:>|./?".ToCharArray();
        static readonly char[] StartingChars = {'<', '&'};
        
        public static string Generate(int length, int numberOfNonAlphanumericCharacters)
        {
            if (length < 1 || length > 128)
            {
                throw new ArgumentException(nameof(length));
            }

            if (numberOfNonAlphanumericCharacters > length || numberOfNonAlphanumericCharacters < 0)
            {
                throw new ArgumentException(nameof(numberOfNonAlphanumericCharacters));
            }

            using (var rng = RandomNumberGenerator.Create())
            {
                string password;
                do
                {
                    var byteBuffer = new byte[length];

                    rng.GetBytes(byteBuffer);

                    var count = 0;
                    var characterBuffer = new char[length];

                    for (var iter = 0; iter < length; iter++)
                    {
                        var i = byteBuffer[iter] % 87;

                        if (i < 10)
                        {
                            characterBuffer[iter] = (char) ('0' + i);
                        }
                        else if (i < 36)
                        {
                            characterBuffer[iter] = (char) ('A' + i - 10);
                        }
                        else if (i < 62)
                        {
                            characterBuffer[iter] = (char) ('a' + i - 36);
                        }
                        else
                        {
                            characterBuffer[iter] = Punctuations[i - 62];
                            count++;
                        }
                    }

                    if (count < numberOfNonAlphanumericCharacters)
                    {
                        int j;
                        var rand = new Random();

                        for (j = 0; j < numberOfNonAlphanumericCharacters - count; j++)
                        {
                            int k;
                            do
                            {
                                k = rand.Next(0, length);
                            } while (!char.IsLetterOrDigit(characterBuffer[k]));

                            characterBuffer[k] = Punctuations[rand.Next(0, Punctuations.Length)];
                        }
                    }

                    password = new string(characterBuffer);
                }
                while (IsDangerousString(password, out _));

                return password;
            }
        }

        static bool IsAtoZ(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        internal static bool IsDangerousString(string s, out int matchIndex)
        {
            //bool inComment = false;
            matchIndex = 0;

            for (int i = 0;;)
            {

                // Look for the start of one of our patterns
                int n = s.IndexOfAny(StartingChars, i);

                // If not found, the string is safe
                if (n < 0) return false;

                // If it's the last char, it's safe
                if (n == s.Length - 1) return false;

                matchIndex = n;

                switch (s[n])
                {
                    case '<':
                        // If the < is followed by a letter or '!', it's unsafe (looks like a tag or HTML comment)
                        if (IsAtoZ(s[n + 1]) || s[n + 1] == '!' || s[n + 1] == '/' || s[n + 1] == '?') return true;
                        break;
                    case '&':
                        // If the & is followed by a #, it's unsafe (e.g. &#83;)
                        if (s[n + 1] == '#') return true;
                        break;
                }

                // Continue searching
                i = n + 1;
            }
        }
    }
}