using System;
using System.IO;
using System.Reflection;
using Calamari.Util;
using Octostache.Templates;

namespace Calamari.Commands.Support
{
    public class ConsoleFormatter
    {
        public static int PrintError(Exception ex)
        {
            if (ex is CommandException)
            {
                Log.Error(ex.Message);
                return 1;
            }
            if (ex is RecursiveDefinitionException)
            {
                //dont log these - they have already been logged earlier
                return 101;
            }
            if (ex is ReflectionTypeLoadException)
            {
                Log.Error(ex.ToString());

                foreach (var loaderException in ((ReflectionTypeLoadException)ex).LoaderExceptions)
                {
                    Log.Error(loaderException.ToString());
                    if (!(loaderException is FileNotFoundException))
                        continue;

                    var exFileNotFound = loaderException as FileNotFoundException;
                    if (!string.IsNullOrEmpty(exFileNotFound.FusionLog))
                    {
                        Log.Error(exFileNotFound.FusionLog);
                    }
                }

                return 43;
            }

            Log.Error(ex.PrettyPrint());
            return 100;
        }
    }
}
