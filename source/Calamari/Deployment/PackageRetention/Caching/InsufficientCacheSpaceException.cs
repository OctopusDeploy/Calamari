using System;
using System.Globalization;
using System.Runtime.Serialization;
using Calamari.Common.Features.Scripting.Python;

namespace Calamari.Deployment.PackageRetention.Caching
{
    public class InsufficientCacheSpaceException : Exception
    {
        public long SpaceFound { get; }
        public long SpaceRequired { get; }

        public InsufficientCacheSpaceException(long spaceFound, long spaceRequired)
            : base($"Could only free {BytesToString(spaceFound)} for the required {BytesToString(spaceRequired)}.")
        {
            SpaceFound = spaceFound;
            SpaceRequired = spaceRequired;
        }

        public InsufficientCacheSpaceException(long spaceFound, long spaceRequired, string message)
            : base(message)
        {
            SpaceFound = spaceFound;
            SpaceRequired = spaceRequired;
        }

        //From https://stackoverflow.com/a/4975942
        static string BytesToString(long byteCount)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
            if (byteCount == 0)
                return "0" + suf[0];
            var bytes = Math.Abs(byteCount);
            var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            var num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString(CultureInfo.CurrentCulture) + suf[place];
        }
    }
}