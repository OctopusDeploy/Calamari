using System.IO;
using System.Text;
using Octodiff.Core;

namespace Calamari.Tests.Fixtures.ApplyDelta
{
    class BinaryFormat
    {
        public static readonly byte[] SignatureHeader = Encoding.ASCII.GetBytes("OCTOSIG");
        public static readonly byte[] DeltaHeader = Encoding.ASCII.GetBytes("OCTODELTA");
        public static readonly byte[] EndOfMetadata = Encoding.ASCII.GetBytes(">>>");
        public const byte CopyCommand = 0x60;
        public const byte DataCommand = 0x80;

        public const byte Version = 0x01;
    }

    class SignatureWriter : ISignatureWriter
    {
        private readonly BinaryWriter signatureStream;

        public SignatureWriter(Stream signatureStream)
        {
            this.signatureStream = new BinaryWriter(signatureStream);
        }

        public void WriteMetadata(IHashAlgorithm hashAlgorithm, IRollingChecksum rollingChecksumAlgorithm, byte[] hash)
        {
            signatureStream.Write(BinaryFormat.SignatureHeader);
            signatureStream.Write(BinaryFormat.Version);
            signatureStream.Write(hashAlgorithm.Name);
            signatureStream.Write(rollingChecksumAlgorithm.Name);
            signatureStream.Write(BinaryFormat.EndOfMetadata);
        }

        public void WriteChunk(ChunkSignature signature)
        {
            signatureStream.Write(signature.Length);
            signatureStream.Write(signature.RollingChecksum);
            signatureStream.Write(signature.Hash);
        }
    }
}
