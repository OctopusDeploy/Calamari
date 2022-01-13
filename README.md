Caaaaalllllamaaaaaarrrrriiiiiiiiiiiiiiii is the command-line tool invoked by Tentacle during a deployment. It knows how to extract and install NuGet packages, run the Deploy.ps1 etc. conventions, modify configuration files, and all the other things that happen during a deployment.

## Building

You will need the .NET Core SDK `2.2`, downloadable from https://dotnet.microsoft.com/download

Run `Build.cmd` to build the solution sadsdfsdf

When the solution is built, a new Calamari package is created in the `artifacts` directory.

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
