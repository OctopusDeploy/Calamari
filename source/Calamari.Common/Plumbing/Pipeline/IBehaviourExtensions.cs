﻿using System;
 using System.Threading.Tasks;

 namespace Calamari.Common.Plumbing.Pipeline
{
    public static class IBehaviourExtensions
    {
        public static Task CompletedTask(this IBehaviour _)
        {
            return Task.CompletedTask;
        }
    }
}