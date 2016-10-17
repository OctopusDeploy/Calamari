#!/usr/bin/env bash

# to update tools versions update the versions in project.json so that they
# are downloaded to the nuget folder, then update here and in copytools.cmd
FSharpVersion=4.0.1.10
ScriptCSVersion=0.16.1

ToolsFolder="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

FSharpFolder=${ToolsFolder}/FSharp.Compiler.Tools.${FSharpVersion}/
# if we have already copied this version, don't bother doing it again
if [ ! -d "$FSharpFolder" ]; then
    echo Copying FSharp to Tools folder
    # if a previous version is in the tools folder, delete it
    rm -r ${ToolsFolder}/FSharp.Compiler.Tools.*
    cp -r ${HOME}/.nuget/packages/FSharp.Compiler.Tools/${FSharpVersion}/tools ${FSharpFolder}
fi

ScriptCSFolder=${ToolsFolder}/ScriptCS.${ScriptCSVersion}/
if [ ! -d "$ScriptCSFolder" ]; then
    echo Copying ScriptCS to Tools folder
    rm -r ${ToolsFolder}/ScriptCS.*
    cp -r ${HOME}/.nuget/packages/ScriptCS/${ScriptCSVersion}/tools ${ScriptCSFolder}
fi

exit 0