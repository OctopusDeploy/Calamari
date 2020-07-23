using System;
using System.Xml;

namespace Calamari.Common.Features.ConfigurationVariables
{
    public static class XmlUtils
    {
        const int MaxCharactersInDocument = (1024*1024*1024); // Max 1GB
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
