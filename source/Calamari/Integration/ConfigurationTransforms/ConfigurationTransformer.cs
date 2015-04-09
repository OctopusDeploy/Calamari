using System;
using System.IO;
using Microsoft.Web.XmlTransform;

namespace Calamari.Integration.ConfigurationTransforms
{
    public class ConfigurationTransformer : IConfigurationTransformer
    {
        readonly bool suppressTransformationErrors;

        public ConfigurationTransformer(bool suppressTransformationErrors = false)
        {
            this.suppressTransformationErrors = suppressTransformationErrors;
        }

        public void PerformTransform(string configFile, string transformFile, string destinationFile)
        {
            var transformation = new XmlTransformation(transformFile, new VerboseTransformLogger());

            var configurationFileDocument = new XmlTransformableDocument();
            configurationFileDocument.PreserveWhitespace = true;
            configurationFileDocument.Load(configFile);

            try
            {
                var success = transformation.Apply(configurationFileDocument);
                if (!success)
                {
                    Console.Error.WriteLine("The XML configuration transform failed. Please see the output log for more details.");
                }
            }
            catch (Exception)
            {
                if (!suppressTransformationErrors)
                {
                    throw;
                }
                Log.ErrorFormat("The XML configuration transform failed due to an exception while transforming. Please see the output log for more details.");
            }
            configurationFileDocument.Save(destinationFile);
        }
    }
}
