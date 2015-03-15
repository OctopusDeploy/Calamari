using System;
using System.IO;
using Microsoft.Web.XmlTransform;
using Octopus.Deploy.Startup;

namespace Octopus.Deploy.ConfigurationTransforms
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                if (args.Length != 3)
                {
                    Console.Error.WriteLine("Usage: Octopus.Deploy.ConfigurationTransforms.exe <config> <transform> <destination>");
                    return 1;
                }

                var configFile = EnsureExists(MapPath(args[0]));
                var transformFile = EnsureExists(MapPath(args[1]));
                var destinationFile = MapPath(args[2]);

                var transformation = new XmlTransformation(transformFile, new VerboseTransformLogger());

                var configurationFileDocument = new XmlTransformableDocument();
                configurationFileDocument.PreserveWhitespace = true;
                configurationFileDocument.Load(configFile);

                var success = transformation.Apply(configurationFileDocument);
                if (!success)
                {
                    Console.Error.WriteLine("The XML configuration transform failed. Please see the output log for more details.");
                    return 2;
                }

                configurationFileDocument.Save(destinationFile);

                return 0;
            }
            catch (Exception ex)
            {
                return ConsoleFormatter.PrintError(ex);
            }
        }

        static string MapPath(string path)
        {
            return Path.GetFullPath(path);
        }

        static string EnsureExists(string path)
        {
            if (!File.Exists(path))
            {
                throw new ArgumentException("Could not find file: " + path);
            }

            return path;
        }
    }
}
