﻿using System;
 using System.Threading.Tasks;

 namespace Calamari.Common.Plumbing.Pipeline
{
    public static class IBehaviourExtensions
    {
        public static Task CompletedTask(this IBehaviour _)
        {
#if NETSTANDARD
            return Task.CompletedTask;
#elif NET452
            return Task.FromResult(0);
#else
            return new Task<int>(() => 0);;
#endif
        }
    }
}