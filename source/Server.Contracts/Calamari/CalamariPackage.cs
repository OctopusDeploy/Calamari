using System;

namespace Sashimi.Server.Contracts.Calamari
{
    /// <summary>
    /// A package that provides Calamari to be used in the execution of an action.
    /// </summary>
    public class CalamariPackage
    {
        public CalamariPackage(CalamariFlavour flavour, string id, string executable, string? launcher = null)
        {
            Flavour = flavour;
            Id = id;
            Executable = executable;
            Launcher = launcher;
        }

        /// <summary>
        /// The tool that this package contains
        /// </summary>
        public CalamariFlavour Flavour { get; }

        /// <summary>
        /// Full Id of the package
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// The name of the executable to run from the package relative to the root of the package
        /// </summary>
        public string Executable { get; }

        /// <summary>
        /// Name of the launcher required for the Executable (eg `dotnet`, `mono`, `java`, etc) or
        /// null if the Executable can be run natively
        /// </summary>
        public string? Launcher { get; }
    }
}