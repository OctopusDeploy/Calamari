using System;
using System.Linq;
using System.Xml;
using Calamari.Commands.Support;
using Calamari.Extensibility;
#if USE_OCTOPUS_XMLT
using Octopus.Web.XmlTransform;
#else
using Microsoft.Web.XmlTransform;
#endif
namespace Calamari.Integration.ConfigurationTransforms
{
    public class ConfigurationTransformer : IConfigurationTransformer
    {
        readonly bool suppressTransformationErrors;
        readonly bool suppressTransformationLogging;

        bool transformFailed;
        string transformWarning;
        readonly ILog log;

        public ConfigurationTransformer(bool suppressTransformationErrors = false, bool suppressTransformationLogging = false, ILog log = null)
        {
            this.suppressTransformationErrors = suppressTransformationErrors;
            this.suppressTransformationLogging = suppressTransformationLogging;
            this.log = log ?? new LogWrapper();
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
                    log.Warn(ex.Message);
                    log.Warn(ex.StackTrace);
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
                log.Info("XML Transformation warnings will be suppressed.");
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
                log.ErrorFormat("The XML configuration file {0} failed with transformation file {1}.", configFile, transformFile);
                throw new CommandException(transformWarning);
            }

            if (!configurationFileDocument.ChildNodes.OfType<XmlElement>().Any())
            {
                log.WarnFormat("The XML configuration file {0} no longer has a root element and is invalid after being transformed by {1}", configFile, transformFile);
            }

            configurationFileDocument.Save(destinationFile);
        }
    }
}
