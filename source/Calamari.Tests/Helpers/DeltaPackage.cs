namespace Calamari.Tests.Helpers
{
    // This file is a clone of how the server represents delta package responses from Calamari
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