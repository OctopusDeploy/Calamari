Calamari is the command-line tool invoked by Tentacle during a deployment. It knows how to extract and install NuGet packages, run the Deploy.ps1 etc. conventions, modify configuration files, and all the other things that happen during an deployment.

## Building

You will need the .NET Core SDK `2.2`, downloadable from https://dotnet.microsoft.com/download

Run `Build.cmd` to build the solution

When the solution is built, a new Calamari package is created in the `artifacts` directory.

## Usage

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

* On your terminal, checkout `master` and `git pull` for good measure
* Run `git tag` and scroll to the bottom of the list to get the last known tag
* Patch, Minor or Major Version the tag according to `<Major>.<Minor>.<Patch>`
* `git push --tag`

This will trigger our build server to build and publish a new version to feedz.io.
