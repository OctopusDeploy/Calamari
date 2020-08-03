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
            return Net40CompletedTask;
#endif
        }

#if NET40
        static readonly Task Net40CompletedTask = CreateNet40CompletedTask();

        static Task<int> CreateNet40CompletedTask()
        {
            var taskCompletionSource = new TaskCompletionSource<int>();
            taskCompletionSource.SetResult(0);
            return taskCompletionSource.Task;
        }
#endif
    }
}