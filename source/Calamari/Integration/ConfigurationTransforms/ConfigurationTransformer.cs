using System;
using System.Linq;
using System.Xml;
using Calamari.Commands.Support;
using Calamari.Deployment;
#if USE_OCTOPUS_XMLT
using Octopus.Web.XmlTransform;
#else
using Microsoft.Web.XmlTransform;
#endif
namespace Calamari.Integration.ConfigurationTransforms
{
    public class ConfigurationTransformer : IConfigurationTransformer
    {
        readonly bool failOnTransformationWarnings;
        readonly bool suppressTransformationErrors;
        readonly bool suppressVerboseTransformationLogging;

        readonly ILog log;

        public ConfigurationTransformer(bool failOnTransformationWarnings = true, bool suppressTransformationErrors = false, bool suppressVerboseTransformationLogging = false, ILog log = null)
        {
            this.failOnTransformationWarnings = failOnTransformationWarnings;
            this.suppressTransformationErrors = suppressTransformationErrors;
            this.suppressVerboseTransformationLogging = suppressVerboseTransformationLogging;
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
                    log.WarnFormat("The XML configuration file {0} failed with transformation file {1}.", configFile, transformFile);
                    log.Warn(ex.Message);
                    log.Warn(ex.StackTrace);
                }
                else
                {
                    log.ErrorFormat("The XML configuration file {0} failed with transformation file {1}.", configFile, transformFile);
                    throw;
                }
            }
        }

        IXmlTransformationLogger SetupLogger()
        {
            var logger = new VerboseTransformLogger(suppressTransformationErrors, suppressVerboseTransformationLogging);
            logger.Warning += (sender, args) =>
            {
                if (!failOnTransformationWarnings)
                {
                    return;
                }

                Log.Verbose($"A warning was encountered and has been elevated to an error. Prevent this by adding the variable {SpecialVariables.Package.FailOnConfigTransformationWarnings} and setting it to false.");
                throw new CommandException(args.Message);
            };
            if (suppressVerboseTransformationLogging)
            {
                log.Verbose($"Verbose XML transformation logging has been turned off because the variable {SpecialVariables.Package.SuppressConfigTransformationLogging} has been set to true.");
            }
            if (suppressTransformationErrors)
            {
                log.Info($"XML transformation warnings will be suppressed because the variable {SpecialVariables.Package.IgnoreConfigTransformationErrors} has been set to true.");
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
            if (!success)
            {
                throw new CommandException($"The XML configuration file {configFile} failed with transformation file {transformFile}.");
            }

            if (!configurationFileDocument.ChildNodes.OfType<XmlElement>().Any())
            {
                log.WarnFormat("The XML configuration file {0} no longer has a root element and is invalid after being transformed by {1}", configFile, transformFile);
            }

            configurationFileDocument.Save(destinationFile);
        }
    }
}
