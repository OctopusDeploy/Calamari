@echo off

REM to update tools versions update the versions in project.json so that they
REM are downloaded to the nuget folder, then update here and in copytools.sh
SET FSharpVersion=4.0.0.1
SET ScriptCSVersion=0.16.1
SET AzurePowershellVersion=1.6.0
SET NugetCommandlineVersion=2.8.3

SET ToolsFolder=%~dp0

SET FSharpFolder=%ToolsFolder%FSharp.Compiler.Tools.%FSharpVersion%\
REM if we have already copied this version, don't bother doing it again
IF EXIST %FSharpFolder% GOTO FSHARPEXISTS
   echo Copying FSharp to Tools folder
   REM if a previous version is in the tools folder, delete it
   for /d %%G in (%ToolsFolder%FSharp.Compiler.Tools.*) do rmdir /s /q "%%~G"
   xcopy /E %userprofile%\.nuget\packages\FSharp.Compiler.Tools\%FSharpVersion%\tools %FSharpFolder%
:FSHARPEXISTS

SET ScriptCSFolder=%ToolsFolder%ScriptCS.%ScriptCSVersion%\
IF EXIST %ScriptCSFolder% GOTO SCRIPTCSEXISTS
   echo Copying ScriptCS to Tools folder
   for /d %%G in (%ToolsFolder%ScriptCS.*) do rmdir /s /q "%%~G"
   xcopy /E %userprofile%\.nuget\packages\ScriptCS\%ScriptCSVersion%\tools %ScriptCSFolder%
:SCRIPTCSEXISTS

SET NugetCommandlineFolder=%ToolsFolder%NuGet.%NugetCommandlineVersion%\
IF EXIST %NugetCommandlineFolder% GOTO NUGETEXISTS
   echo Copying Nuget to Tools folder
   for /d %%G in (%ToolsFolder%Nuget.*) do rmdir /s /q "%%~G"
   xcopy /E %userprofile%\.nuget\packages\NuGet.CommandLine\%NugetCommandlineVersion%\tools %NugetCommandlineFolder%
:NUGETEXISTS

IF "%1" NEQ "azure" GOTO DONE
SET AzurePowershellFolder=%ToolsFolder%Octopus.Dependencies.AzureCmdlets.%AzurePowershellVersion%\
IF EXIST %AzurePowershellFolder% GOTO AZUREPOWERSHELLEXISTS
   echo Copying Azure PowerShell to Tools folder
   for /d %%G in (%ToolsFolder%Octopus.Dependencies.AzureCmdlets.*) do rmdir /s /q "%%~G"
   xcopy /E %userprofile%\.nuget\packages\Octopus.Dependencies.AzureCmdlets\%AzurePowershellVersion%\PowerShell %AzurePowershellFolder%
:AZUREPOWERSHELLEXISTS

:DONE
exit 0