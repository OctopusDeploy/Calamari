using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Calamari.Extensibility.Docker
{
    public class RandomStringGenerator
    {
        public virtual string Generate(int length)
        {
            const string allowedCharacters = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var password = new StringBuilder(length);

            using (var random = RandomNumberGenerator.Create())
            {
                for (var i = 0; i < length; i++)
                {
                    password.Append(allowedCharacters[Next(random, allowedCharacters.Length - 1)]);
                }

                return password.ToString();
            }
        }

        static byte Next(RandomNumberGenerator rngCsp, int numberSides)
        {
            if (numberSides <= 0)
                throw new ArgumentOutOfRangeException("numberSides");

            var randomNumber = new byte[1];
            do
            {
                rngCsp.GetBytes(randomNumber);
            } while (!IsFairRoll(randomNumber[0], numberSides));
            return (byte)((randomNumber[0] % numberSides) + 1);
        }

        static bool IsFairRoll(byte roll, int numSides)
        {
            var fullSetsOfValues = Byte.MaxValue / numSides;
            return roll < numSides * fullSetsOfValues;
        }
    }
}
