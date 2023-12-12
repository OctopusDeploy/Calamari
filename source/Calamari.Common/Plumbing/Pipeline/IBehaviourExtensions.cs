using System;
 using System.Threading.Tasks;

 namespace Calamari.Common.Plumbing.Pipeline
{
    public static class IBehaviourExtensions
    {
        //TODO: NET462 Upgrade - This class can be removed
        public static Task CompletedTask(this IBehaviour _)
        {
            return Task.CompletedTask;
        }
    }
}