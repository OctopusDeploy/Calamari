Calamari is the command-line tool invoked by Tentacle during a deployment. It knows how to extract and install NuGet packages, run the Deploy.ps1 etc. conventions, modify configuration files, and all the other things that happen during a deployment.

## Building

You will need the .NET SDK `6.0`, downloadable from https://dotnet.microsoft.com/download

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

After you finish merging to main to tag the Calamari NuGet package:

Firstly, find out what the latest tag is. There are two ways to do this:

* On your terminal, checkout `main` and `git pull` for good measure
* Run `git tag` and scroll to the bottom of the list to get the last known tag

Alternatively,

* Check the last build on main as it will be pre-release version of the next `<Major>.<Minor>.<Patch>` version

Finally, tag and push the new release

* Patch, Minor or Major Version the tag according to `<Major>.<Minor>.<Patch>`
* `git push origin <Major>.<Minor>.<Patch>` to push your newly created tag to Github.

> [!WARNING]
> Avoid using `git push --tags` as it will push all of your local tags to the remote repository.  
> This is not recommended as it can cause confusion and potential issues with the build server when it attempts to calculate the release version number due to the potential of unexpected tags being pushed.

This will trigger our build server to build and publish a new version to feedz.io which can be seen here https://feedz.io/org/octopus-deploy/repository/dependencies/packages/Calamari.

## Debugging

Option 1 is recommended if you can use the default worker.

### Option 1: Reference local binary
1. Build Calamari in your IDE
2. Get the path to the executable (The one directly in the bin folder for the Calamari project for the flavour you want to debug e.g. `Calamari` or `Calamari.AzureAppService` with no extension for macOS and Linux, `Calamari.exe` or `Calamari.AzureAppService.exe` for Windows)
3. In Octopus, set an unscoped variable `Octopus.Calamari.Executable` to the full path to the executable. This is set per project.
4. Now when you run a deployment it will use your debug build.

#### Benefits:
- Extremely fast iteration, you don’t need to stop server between Calamari builds.
- It does not exercise the full deployment functionality of checking calamari versions etc (most of the time, this does not matter).

#### Drawbacks:
- You must run the step on your machine (default worker) - basically the executable has to be at the path on whatever machine is executing the step. This is a problem when working with Kubernetes Tentacle for example.

### Option 2: Package Calamari into your server build
1. In terminal, run `./build-local.sh` (build-local.ps1 is available for Windows)
2. Nuke will build and package up Calamari then it will update the `Calamari.Consolidated` version in your `Octopus.Server.csproj`
3. Restart Server to force a rebuild with the new version of Calamari.

> Note: You must have a local nuget feed setup for the path `../LocalPackages` relative to this repo
>
> eg. `dotnet nuget add source -n local ~/path/to/LocalPackages`

#### Benefits:
- It uses the “proper” mechanism to deploy Calamari.
- You can use it when you’re using a remote worker.

#### Drawbacks:
- It takes ~10 minutes to build and pack Calamari, however you can reduce this significantly by targeting a specific runtime/framework if you don't need the rest
    - eg `./build-local.sh -y --framework "net6.0" --runtime "linux-x64"`
- You need to restart Server for Calamari changes to take effect

### Bonus Variables!
- Set `Octopus.Calamari.WaitForDebugger` to `True` to get a debug version of Calamari to wait for a Debugger to be attached before continuing. The log message from Calamari will show it’s waiting for the debugger and it will give the PID to use when you’re looking to attach.
- Set `OctopusPrintEvaluatedVariables` to `True` to get all variables that are sent to Calamari, printed to the verbose long when executing a step.

> Tip: Creating a variable set with your configuration makes it easy to toggle this behaviour per project

## Testing:

Many of the tests require credentials for accessing external services.
These credentials can be provided via environment variables, or read from OctopusDeploy's 1Password repository.
There are two environment variables which define how credentials are provided:
* CALAMARI__Tests__SecretManagerEnabled - if true, read from 1password; if false read from environment variables
* CALAMARI__Tests__SecretManagerAccount - if set defines the account to use. If unset, defaults to "octopusdeploy.1password.com"

To use the 1password integration you are required to have installed:
* 1Password application, and associated
* 1Password cli application (op)

For details on their configuration see the [README.md](https://github.com/OctopusDeploy/1password-sdk/blob/main/README.md) in the 1password-sdk repository
