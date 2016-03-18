using System.Xml;

namespace Calamari.Util
{
    public static class XmlUtils
    {
        private const int MaxCharactersInDocument = (1024*1024*1024); // Max 1GB
        public static XmlReaderSettings DtdSafeReaderSettings
        {
            get
            {
                return new XmlReaderSettings()
                {
                    DtdProcessing = DtdProcessing.Parse,
                    MaxCharactersInDocument = MaxCharactersInDocument
                };
            }
        }
    }
}
