using System;

namespace Calamari.Common.Features.Scripting
{
    /// <summary>
    /// Defines some common priorities for script wrappers
    /// </summary>
    public static class ScriptWrapperPriorities
    {
        /// <summary>
        /// TODO:
        /// </summary>
        public const int KubernetesCustomResourcePriority = 1003;

        /// <summary>
        /// The priority for script wrappers that configure kubernetes authentication
        /// </summary>
        public const int KubernetesContextPriority = 1002;

        /// <summary>
        /// The priority for the script wrapper that checks deployed Kubernetes resources status
        /// </summary>
        public const int KubernetesStatusCheckPriority = 1001;

        /// <summary>
        /// The priority for script wrappers that configure cloud authentication
        /// </summary>
        public const int CloudAuthenticationPriority = 1000;

        /// <summary>
        /// The priority for tools that configure individual tools
        /// </summary>
        public const int ToolConfigPriority = 100;

        /// <summary>
        /// The priority for tools the terminal script wrapper, which should always
        /// be run last
        /// </summary>
        public const int TerminalScriptPriority = -1;
    }
}