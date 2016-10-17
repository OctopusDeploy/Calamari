using System;
using System.IO;
using System.Reflection;

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
            if (ex is ReflectionTypeLoadException)
            {
                Log.Error(ex.ToString());

                foreach (var loaderException in ((ReflectionTypeLoadException)ex).LoaderExceptions)
                {
                    Log.Error(loaderException.ToString());
#if NET40
                    if (!(loaderException is FileNotFoundException))
                        continue;

                    var exFileNotFound = loaderException as FileNotFoundException;
                    if (!string.IsNullOrEmpty(exFileNotFound.FusionLog))
                    {
                        Log.Error(exFileNotFound.FusionLog);
                    }
#endif
                }

                return 43;
            }

            Log.Error(ex.ToString());
            return 100;
        }
    }
}
