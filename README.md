Calamari is the command-line tool invoked by Tentacle during a deployment. It knows how to extract and install NuGet packages, run the Deploy.ps1 etc. conventions, modify configuration files, and all the other things that happen during a deployment.

## Building

You will need the .NET SDK `5.0`, downloadable from https://dotnet.microsoft.com/download

Run `Build-Local.ps1` or `Build-Local.sh` to build the solution locally.

When the solution is built, several new Calamari nuget packages are created in the `artifacts` directory.

> **For Octopus Developers:**
> 
>The `Build-Local` scripts will also copy the nuget packages to the LocalPackages folder which can be
found in the same parent folder as the Calamari repo. If the Octopus Server repo exists in the same 
parent folder, `Build-Local` will also update the Octopus.Server.csproj to reference the Calamari 
version produced by the build. This means that you can simply rebuild Server locally to test the new
version of Calamari.
>
>folder structure example:
>```
>dev\
>    Calamari\
>    LocalPackages\ 
> OctopusDeploy\
>```

## Usage

> **Octopus Server 2020.3+: Using a custom version of Calamari may not work**
>
> Calamari is currently being filleted into [Sashimi](https://github.com/OctopusDeploy/Sashimi). Due to the architectural changes involved in this transformation, using a custom version of Calamari with Octopus Server version 2020.3+ may not work. Please get in touch with support@octopus.com if this affects you, to help us make decisions about how we can support custom implementations of deployment steps.

To use your own Calamari package with an Octopus 3.0 server, run the following commands
```
Octopus.Server.exe service --instance <instance> --stop --nologo --console
Octopus.Server.exe configure --instance <instance> --customBundledPackageDirectory=<directory> --nologo --console
Octopus.Server.exe service --instance <instance> --start --nologo --console
```

where `<directory>` is the directory containing the `Calamari.*.nupkg` files. If your server is setup as the default instance, you may ommit the `--instance <instance>` parameter.

This will add the following setting to your Octopus Server configuration file:

```
  <set key="Octopus.Deployment.CustomBundledPackageDirectory">C:\GitHub\Calamari\built-packages</set>
```

The exe and configuration file can normally be found at:

```
C:\Octopus\OctopusServer\OctopusServer.config
```

If you want to revert to the bundled package, run the following commands
```
Octopus.Server.exe service --instance <instance> --stop --nologo --console
Octopus.Server.exe configure --instance <instance> --customBundledPackageDirectory= --nologo --console
Octopus.Server.exe service --instance <instance> --start --nologo --console
```

** Ensure you update your build to the latest Calamari or revert to the bundled package when you upgrade Octopus Server **

## Releasing

After you finish merging to master to tag the Calamari NuGet package:

Firstly, find out what the latest tag is. There are two ways to do this:

* On your terminal, checkout `master` and `git pull` for good measure
* Run `git tag` and scroll to the bottom of the list to get the last known tag

Alternatively,

* Check the last build on master as it will be pre-release version of the next `<Major>.<Minor>.<Patch>` version

Finally, tag and push the new release

* Patch, Minor or Major Version the tag according to `<Major>.<Minor>.<Patch>`
* `git push --tag`

This will trigger our build server to build and publish a new version to feedz.io which can be seen here https://feedz.io/org/octopus-deploy/repository/dependencies/packages/Calamari.

## Debugging

Option 1 is recommended if you can use the default worker.

### Option 1: Reference local binary
1. Build Calamari in your IDE
2. Get the path to the executable (The one directly in the bin folder for the Calamari project, `Calamari` with no extension of macOS and Linux, `Calamari.exe` for Windows)
3. In Octopus, set an unscoped variable `Octopus.Calamari.Executable` to the full path to the executable. This is set per project.
4. Now when you run a deployment it will use your debug build.

#### Benefits:
- Extremely fast iteration, you don’t need to stop server between Calamari builds.
- It does not exercise the full deployment functionality of checking calamari versions etc (most of the time, this does not matter).

#### Drawbacks:
- You must run the step on your machine (default worker) - basically the executable has to be at the path on whatever machine is executing the step. This is a problem when working with Kubernetes Tentacle for example.

### Option 2: Package Calamari into your server build
1. In terminal, run `./build.sh --set-octopus-server-version true` (build.ps1 and build.cmd are available for Windows)
2. Nuke will build and package up Calamari then it will update the `Calamari.Consolidated` version in your `Octopus.Server.csproj`
3. Restart Server to force a rebuild with the new version of Calamari.

#### Benefits:
- It uses the “proper” mechanism to deploy Calamari.
- You can use it when you’re using a remote worker.

#### Drawbacks:
- It takes ~10 minutes to build and pack Calamari
- You need to restart Server for Calamari changes to take effect
- Currently it only increases the version number if you make commits, don’t forget to commit your changes or you’ll waste ~10 mins waiting for the build for nothing!

### Bonus Variables!
- Set `Octopus.Calamari.WaitForDebugger` to `True` to get a debug version of Calamari to wait for a Debugger to be attached before continuing. The log message from Calamari will show it’s waiting for the debugger and it will give the PID to use when you’re looking to attach.
- Set `OctopusPrintEvaluatedVariables` to `True` to get all variables that are sent to Calamari, printed to the verbose long when executing a step.

> Tip: Creating a variable set with your configuration makes it easy to toggle this behaviour per project  