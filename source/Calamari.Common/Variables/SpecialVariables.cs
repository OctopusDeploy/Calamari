namespace Calamari.Common.Variables
{
    public static class SpecialVariables
    {
        
        public static readonly string RetentionPolicySet = "OctopusRetentionPolicySet";
        public static readonly string PrintVariables = "OctopusPrintVariables";
        public static readonly string PrintEvaluatedVariables = "OctopusPrintEvaluatedVariables";
        public static readonly string AdditionalVariablesPath = "Octopus.AdditionalVariablesPath";
        public static readonly string OriginalPackageDirectoryPath = "OctopusOriginalPackageDirectoryPath";

        public static class Action
        {
            public const string SkipJournal = "Octopus.Action.SkipJournal";    
        }
        
    }
}