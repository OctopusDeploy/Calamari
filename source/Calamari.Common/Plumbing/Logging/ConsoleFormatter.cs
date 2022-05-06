using System;
using System.IO;
using System.Reflection;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Extensions;
using Octostache.Templates;

namespace Calamari.Common.Plumbing.Logging
{
    public class ConsoleFormatter
    {
        public static int PrintError(ILog log, Exception ex)
        {
            if (ex is CommandException)
            {
                log.Error(ex.Message);
                return ExitStatus.CommandExceptionError;
            }

            if (ex is RecursiveDefinitionException)
                //dont log these - they have already been logged earlier
                return ExitStatus.RecursiveDefinitionExceptionError;
            if (ex is ReflectionTypeLoadException)
            {
                log.Error(ex.ToString());

                foreach (var loaderException in ((ReflectionTypeLoadException)ex).LoaderExceptions)
                {
                    log.Error(loaderException.ToString());
                    if (!(loaderException is FileNotFoundException))
                        continue;

                    var exFileNotFound = loaderException as FileNotFoundException;
                    if (!string.IsNullOrEmpty(exFileNotFound?.FusionLog))
                        log.Error(exFileNotFound.FusionLog);
                }

                return ExitStatus.ReflectionTypeLoadExceptionError;
            }

            log.Error(ex.PrettyPrint());
            return ExitStatus.OtherError;
        }
    }
}