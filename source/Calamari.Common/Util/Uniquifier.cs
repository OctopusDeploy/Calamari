using System;

namespace Calamari.Common.Util
{
    public static class Uniquifier
    {
        public static string UniquifyString(string input, Func<string, bool> isInUse, string format = "-{0}", int startCounter = 1)
        {
            return UniquifyUntil(input, s => s, isInUse, format, startCounter);
        }

        public static T UniquifyUntil<T>(string input, Func<string, T> creator, Func<T, bool> isInUse, string format = "-{0}", int startCounter = 1)
        {
            var inputToTest = input;
            var i = startCounter;

            do
            {
                var item = creator(inputToTest);

                if (!isInUse(item))
                {
                    return item;
                }

                inputToTest = input + string.Format(format, i);
                i++;
            } while (true);
        }

        public static string UniquifyStringFriendly(string input, Func<string, bool> isInUse)
        {
            return UniquifyString(input, isInUse, " (#{0:n0})", 2);
        }

        public static string? Normalize(string? input)
        {
            return input?.Trim().ToLowerInvariant();
        }
    }
}