using System;
using Calamari.Commands.Support;
using Microsoft.Web.XmlTransform;

namespace Calamari.Integration.ConfigurationTransforms
{
    public class ConfigurationTransformer : IConfigurationTransformer
    {
        readonly bool _suppressTransformationErrors;

        public ConfigurationTransformer(bool suppressTransformationErrors = false)
        {
            _suppressTransformationErrors = suppressTransformationErrors;
        }

        public void PerformTransform(string configFile, string transformFile, string destinationFile)
        {
            var transformFailed = false;
            var transformWarning = "";
            var logger = new VerboseTransformLogger(_suppressTransformationErrors);
            logger.Warning += (sender, args) =>
            {
                transformWarning = args.Message;
                transformFailed = true;
            };
            if (_suppressTransformationErrors)
            {
                Log.Info("XML Transformation warnings will be suppressed.");
            }

            var transformation = new XmlTransformation(transformFile, logger);

            var configurationFileDocument = new XmlTransformableDocument();
            configurationFileDocument.PreserveWhitespace = true;
            configurationFileDocument.Load(configFile);

            var success = transformation.Apply(configurationFileDocument);

            if (!_suppressTransformationErrors && (!success || transformFailed))
            {
                Log.ErrorFormat("The XML configuration file {0} failed with transformation file {1}.", configFile, transformFile);
                throw new CommandException(transformWarning);
            }

            configurationFileDocument.Save(destinationFile);
        }
    }
}
