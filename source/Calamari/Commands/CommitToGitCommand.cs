using System;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Commands;

[Command(Name, Description = "Update a Git repository with selected package content, then transform with optional script")]
public class CommitToGitCommand : Command
{
    public const string Name = "commit-to-git";
    readonly ILog log;

    public CommitToGitCommand(ILog log)
    {
        this.log = log;
    }

    public override int Execute(string[] commandLineArguments)
    {
        Options.Parse(commandLineArguments);
        log.Error("This step should not be executed");
        return 1;
    }
}
