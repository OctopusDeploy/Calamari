namespace Calamari.Common.Variables
{
    public static class SpecialVariables
    {
        
        public static readonly string CalamariWorkingDirectory = "OctopusCalamariWorkingDirectory";
        public static readonly string RetentionPolicySet = "OctopusRetentionPolicySet";
        public static readonly string PrintVariables = "OctopusPrintVariables";
        public static readonly string PrintEvaluatedVariables = "OctopusPrintEvaluatedVariables";
        
        // Everything below here needs a review
        public static readonly string CopyWorkingDirectoryIncludingKeyTo = "Octopus.Calamari.CopyWorkingDirectoryIncludingKeyTo";
        public static readonly string SkipFreeDiskSpaceCheck = "OctopusSkipFreeDiskSpaceCheck";
        public static readonly string FreeDiskSpaceOverrideInMegaBytes = "OctopusFreeDiskSpaceOverrideInMegaBytes";
        public const string AdditionalVariablesPath = "Octopus.AdditionalVariablesPath";
        public const string OriginalPackageDirectoryPath = "OctopusOriginalPackageDirectoryPath";
        public const string LastErrorMessage = "OctopusLastErrorMessage";
        public const string LastError = "OctopusLastError";

        public static class Action
        {
            public const string SkipJournal = "Octopus.Action.SkipJournal";    
        }
        
    }
}