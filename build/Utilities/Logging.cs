using System;
using System.Diagnostics;

namespace Calamari.Build.Utilities;

public class Logging
{
    public static void InBlock(string blockName, Action action)
    {
        InBlock(blockName, () =>
                           {
                               action.Invoke();
                               return Task.CompletedTask;
                           }).GetAwaiter().GetResult();;
    }
    
    public static T InBlock<T>(string blockName, Func<T> action)
    {
        return InBlock(blockName, async () =>
                           {
                               await Task.CompletedTask;
                               return action.Invoke();
                           }).GetAwaiter().GetResult();
    }

    public static async Task<T> InBlock<T>(string blockName, Func<Task<T>> action)
    {
        var stopWatch = Stopwatch.StartNew();

        if (TeamCity.Instance != null)
        {
            TeamCity.Instance.OpenBlock(blockName);
        }
        else
        {
            Log.Information("{BlockName}{HeaderDelimiter}", blockName, new string('-', 30));
        }

        T result;
        try
        {
            result = await action();
            Log.Information("{BlockName} SUCCEEDED in {Elapsed:000}", blockName, stopWatch.Elapsed);
        }
        catch (Exception e)
        {
            Log.Error(e, "{BlockName} FAILED in {Elapsed:000}: {Message}", blockName, stopWatch.Elapsed, e.Message);
            throw;
        }
        finally
        {
            if (TeamCity.Instance != null)
            {
                TeamCity.Instance.CloseBlock(blockName);
            }
        }

        return result;
    }
}
