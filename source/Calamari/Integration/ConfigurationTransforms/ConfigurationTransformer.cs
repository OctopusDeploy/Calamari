﻿using System;
using System.Linq;
using System.Xml;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Integration.Processes;
#if USE_OCTOPUS_XMLT
using Octopus.Web.XmlTransform;
#else
using Microsoft.Web.XmlTransform;
#endif
namespace Calamari.Integration.ConfigurationTransforms
{
    public class ConfigurationTransformer : IConfigurationTransformer
    {
        readonly TransformLoggingOptions transformLoggingOptions;

        readonly ILog calamariLog;

        bool errorEncountered;

        public ConfigurationTransformer(TransformLoggingOptions transformLoggingOptions, ILog log = null)
        {
            this.transformLoggingOptions = transformLoggingOptions;
            calamariLog = log ?? new LogWrapper();
        }

        public void PerformTransform(string configFile, string transformFile, string destinationFile)
        {
            var logger = SetupLogger();
            try
            {
                ApplyTransformation(configFile, transformFile, destinationFile, logger);
            }
            catch (CommandException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogErrorFromException(ex);
                if (errorEncountered)
                {
                    throw;
                }
            }
        }

        IXmlTransformationLogger SetupLogger()
        {
            var logger = new VerboseTransformLogger(transformLoggingOptions, calamariLog);
            logger.Error += delegate { errorEncountered = true; };
            if (transformLoggingOptions.HasFlag(TransformLoggingOptions.DoNotLogVerbose))
            {
                calamariLog.Verbose($"Verbose XML transformation logging has been turned off because the variable {SpecialVariables.Package.SuppressConfigTransformationLogging} has been set to true.");
            }
            if (transformLoggingOptions.HasFlag(TransformLoggingOptions.LogExceptionsAsWarnings))
            {
                calamariLog.Verbose($"XML transformation warnings will be downgraded to information because the variable {SpecialVariables.Package.IgnoreConfigTransformationErrors} has been set to true.");
            }
            if (transformLoggingOptions.HasFlag(TransformLoggingOptions.LogExceptionsAsWarnings))
            {
                calamariLog.Verbose($"XML transformation exceptions will be downgraded to warnings because the variable {SpecialVariables.Package.IgnoreConfigTransformationErrors} has been set to true.");
            }
            if (transformLoggingOptions.HasFlag(TransformLoggingOptions.LogWarningsAsErrors))
            {
                calamariLog.Verbose($"Warning will be elevated to errors. Prevent this by adding the variable {SpecialVariables.Package.TreatConfigTransformationWarningsAsErrors} and setting it to false.");
            }

            return logger;
        }

        void ApplyTransformation(string configFile, string transformFile, string destinationFile, IXmlTransformationLogger logger)
        {
            errorEncountered = false;
            var transformation = new XmlTransformation(transformFile, logger);

            var configurationFileDocument = new XmlTransformableDocument()
            {
                PreserveWhitespace = true
            };
            configurationFileDocument.Load(configFile);

            var success = transformation.Apply(configurationFileDocument);
            if (!success || errorEncountered)
            {
                throw new CommandException($"The XML configuration file {configFile} failed with transformation file {transformFile}.");
            }

            if (!configurationFileDocument.ChildNodes.OfType<XmlElement>().Any())
            {
                logger.LogWarning("The XML configuration file {0} no longer has a root element and is invalid after being transformed by {1}", new object[] { configFile, transformFile });
            }

            configurationFileDocument.Save(destinationFile);
        }

        public static ConfigurationTransformer FromVariables(CalamariVariableDictionary variables, ILog log = null)
        {
            var treatConfigTransformationWarningsAsErrors = variables.GetFlag(SpecialVariables.Package.TreatConfigTransformationWarningsAsErrors, true);
            var ignoreConfigTransformErrors = variables.GetFlag(SpecialVariables.Package.IgnoreConfigTransformationErrors);
            var suppressConfigTransformLogging = variables.GetFlag(SpecialVariables.Package.SuppressConfigTransformationLogging);

            var transformLoggingOptions = TransformLoggingOptions.None;

            if (treatConfigTransformationWarningsAsErrors)
            {
                transformLoggingOptions |= TransformLoggingOptions.LogWarningsAsErrors;
            }

            if (ignoreConfigTransformErrors)
            {
                transformLoggingOptions |= TransformLoggingOptions.LogExceptionsAsWarnings;
                transformLoggingOptions |= TransformLoggingOptions.LogWarningsAsInfo;
                transformLoggingOptions &= ~TransformLoggingOptions.LogWarningsAsErrors;
            }

            if (suppressConfigTransformLogging)
            {
                transformLoggingOptions |= TransformLoggingOptions.DoNotLogVerbose;
            }

            return new ConfigurationTransformer(transformLoggingOptions, log);
        }
    }

    [Flags]
    public enum TransformLoggingOptions
    {
        None = 0,
        DoNotLogVerbose = 1,
        LogWarningsAsInfo = 2,
        LogWarningsAsErrors = 4,
        LogExceptionsAsWarnings = 8
    }
}
