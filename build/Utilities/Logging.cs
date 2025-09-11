using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Nuke.Common.CI.TeamCity;
using Serilog;

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

    public static async Task InBlock(string blockName, Func<Task> action)
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

        try
        {
            await action();
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
    }
}
