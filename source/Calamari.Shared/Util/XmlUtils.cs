using System.Xml;

namespace Calamari.Shared.Util
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
                    //DtdProcessing = DtdProcessing.Parse, //Bug in netcore causes this to not build at 1/9/2016
                    DtdProcessing = (DtdProcessing)2,
                    MaxCharactersInDocument = MaxCharactersInDocument
                };
            }
        }
    }
}
