using System;

namespace Calamari.Tests.Shared.LogParser
{
    public class DeltaPackage
    {
        public DeltaPackage(string fullPathOnRemoteMachine, string hash, long size)
        {
            FullPathOnRemoteMachine = fullPathOnRemoteMachine;
            Hash = hash;
            Size = size;
        }

        public string FullPathOnRemoteMachine { get; }
        public string Hash { get; }
        public long Size { get; }
    }
}