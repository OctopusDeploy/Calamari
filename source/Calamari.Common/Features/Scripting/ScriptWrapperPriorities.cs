﻿using System;

namespace Calamari.Common.Features.Scripting
{
    /// <summary>
    /// Defines some common priorities for script wrappers
    /// </summary>
    public static class ScriptWrapperPriorities
    {
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