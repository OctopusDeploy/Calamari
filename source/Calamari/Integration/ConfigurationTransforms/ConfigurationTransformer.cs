using System;
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
        readonly TransformWarningAction transformWarningAction;
        readonly TransformErrorAction transformErrorAction;
        readonly TransformLoggingOptions transformLoggingOptions;

        readonly ILog log;

        public ConfigurationTransformer(TransformWarningAction transformWarningAction, TransformErrorAction transformErrorAction, TransformLoggingOptions transformLoggingOptions, ILog log = null)
        {
            this.transformWarningAction = transformWarningAction;
            this.transformErrorAction = transformErrorAction;
            this.transformLoggingOptions = transformLoggingOptions;
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
                if (transformErrorAction == TransformErrorAction.Succeed)
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
            var logger = new VerboseTransformLogger(transformLoggingOptions, log);
            logger.Warning += (sender, args) =>
            {
                if (transformWarningAction == TransformWarningAction.Succeed)
                {
                    return;
                }

                log.Verbose($"A warning was encountered and has been elevated to an error. Prevent this by adding the variable {SpecialVariables.Package.FailOnConfigTransformationWarnings} and setting it to false.");
                throw new CommandException(args.Message);
            };
            if (transformLoggingOptions.HasFlag(TransformLoggingOptions.DoNotLogVerbose))
            {
                log.Verbose($"Verbose XML transformation logging has been turned off because the variable {SpecialVariables.Package.SuppressConfigTransformationLogging} has been set to true.");
            }
            if (transformLoggingOptions.HasFlag(TransformLoggingOptions.LogExceptionsAsWarnings))
            {
                log.Verbose($"XML transformation warnings will be downgraded to information because the variable {SpecialVariables.Package.IgnoreConfigTransformationErrors} has been set to true.");
            }
            if (transformLoggingOptions.HasFlag(TransformLoggingOptions.LogExceptionsAsWarnings))
            {
                log.Verbose($"XML transformation exceptions will be downgraded to warnings because the variable {SpecialVariables.Package.IgnoreConfigTransformationErrors} has been set to true.");
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

        public static ConfigurationTransformer FromVariables(CalamariVariableDictionary variables, ILog log = null)
        {
            var failOnConfigTransformWarnings =
                variables.GetFlag(SpecialVariables.Package.FailOnConfigTransformationWarnings, true);
            var ignoreConfigTransformErrors =
                variables.GetFlag(SpecialVariables.Package.IgnoreConfigTransformationErrors, false);
            var suppressConfigTransformLogging =
                    variables.GetFlag(SpecialVariables.Package.SuppressConfigTransformationLogging, false);

            var transformWarningAction = failOnConfigTransformWarnings
                ? TransformWarningAction.Fail
                : TransformWarningAction.Succeed;

            var transformErrorAction = ignoreConfigTransformErrors
                ? TransformErrorAction.Succeed
                : TransformErrorAction.Fail;

            var transformLoggingOptions = TransformLoggingOptions.None;

            if (ignoreConfigTransformErrors)
            {
                transformLoggingOptions = transformLoggingOptions & TransformLoggingOptions.LogExceptionsAsWarnings;
                transformLoggingOptions = transformLoggingOptions & TransformLoggingOptions.LogWarningsAsInfo;

                if (!variables.IsSet(SpecialVariables.Package.FailOnConfigTransformationWarnings))
                {
                    transformWarningAction= TransformWarningAction.Succeed;
                }
            }

            if (suppressConfigTransformLogging)
            {
                transformLoggingOptions = transformLoggingOptions & TransformLoggingOptions.DoNotLogVerbose;
            }

            return new ConfigurationTransformer(transformWarningAction, transformErrorAction, transformLoggingOptions, log);
        }
    }

    public enum TransformWarningAction
    {
        Succeed,
        Fail
    }

    public enum TransformErrorAction
    {
        Succeed,
        Fail
    }

    [Flags]
    public enum TransformLoggingOptions
    {
        None = 0,
        DoNotLogVerbose = 1,
        LogWarningsAsInfo = 2,
        LogExceptionsAsWarnings = 4
    }
}
