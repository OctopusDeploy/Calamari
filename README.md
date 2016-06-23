Calamari is the command-line tool invoked by Tentacle during a deployment. It knows how to extract and install NuGet packages, run the Deploy.ps1 etc. conventions, modify configuration files, and all the other things that happen during an deployment.

When the solution is built, a new Calamari package is created in the `built-packages` directory.

To use your own Calamari package with an Octopus 3.0 server, run the following commands
```
Octopus.Server.exe service --instance <instance> --stop --nologo --console}
Octopus.Server.exe configure --instance <instance> --customBundledPackageDirectory <directory> --nologo --console}
Octopus.Server.exe service --instance <instance> --start --nologo --console}
```

where `<directory>` is the directory containing the `Calamari.*.nupkg` files.

This will add the following setting to your Octopus Server configuration file:

```
  <set key="Octopus.Deployment.CustomBundledPackageDirectory">C:\GitHub\Calamari\built-packages</set>
```

The exe and configuration file can normally be found at:

```
C:\Octopus\OctopusServer\OctopusServer.config
```
