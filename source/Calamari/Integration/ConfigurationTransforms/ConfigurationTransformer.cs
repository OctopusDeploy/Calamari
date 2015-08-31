using System;
using Calamari.Commands.Support;
using Microsoft.Web.XmlTransform;

namespace Calamari.Integration.ConfigurationTransforms
{
    public class ConfigurationTransformer : IConfigurationTransformer
    {
        readonly bool suppressTransformationErrors;
        readonly bool suppressTransformationLogging;

        bool transformFailed;
        string transformWarning;

        public ConfigurationTransformer(bool suppressTransformationErrors = false, bool suppressTransformationLogging = false)
        {
            this.suppressTransformationErrors = suppressTransformationErrors;
            this.suppressTransformationLogging = suppressTransformationLogging;
        }

        public void PerformTransform(string configFile, string transformFile, string destinationFile)
        {
            var logger = SetupLogger();
            try
            {
                ApplyTransformation(configFile, transformFile, destinationFile, logger);
            }
            catch (Exception ex)
            {
                if (suppressTransformationErrors)
                {
                    Log.Warn(ex.Message);
                    Log.Warn(ex.StackTrace);
                }                    
                else throw;
            }
        }

        IXmlTransformationLogger SetupLogger()
        {
            transformFailed = false;
            transformWarning = default(string);
            
            var logger = new VerboseTransformLogger(suppressTransformationErrors, suppressTransformationLogging);
            logger.Warning += (sender, args) =>
            {
                transformWarning = args.Message;
                transformFailed = true;
            };
            if (suppressTransformationErrors)
            {
                Log.Info("XML Transformation warnings will be suppressed.");
            }

            return logger;
        }

        void ApplyTransformation(string configFile, string transformFile, string destinationFile, IXmlTransformationLogger logger)
        {
            var transformation = new XmlTransformation(transformFile, logger);

            var configurationFileDocument = new XmlTransformableDocument()
            {
                PreserveWhitespace = true
            };
            configurationFileDocument.Load(configFile);

            var success = transformation.Apply(configurationFileDocument);
            if (!suppressTransformationErrors && (!success || transformFailed))
            {
                Log.ErrorFormat("The XML configuration file {0} failed with transformation file {1}.", configFile, transformFile);
                throw new CommandException(transformWarning);
            }

            configurationFileDocument.Save(destinationFile);
        }
    }
}
