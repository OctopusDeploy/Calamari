Calamari is the command-line tool invoked by Tentacle during a deployment. It knows how to extract and install NuGet packages, run the Deploy.ps1 etc. conventions, modify configuration files, and all the other things that happen during an deployment.

This repository is currently a work-in-progress as we migrate the tools and scripts from Tentacle 2.x. 

[Learn more about our plans for Tentacle 3.0.](http://octopusdeploy.com/blog/deployment-targets-in-octopus-3)

To use your own build of Calamari with an Octopus 3.0 server, add the following setting to your Octopus Server configuration file:

```
  <set key="Octopus.Deployment.CustomCalamariPackageDirectory">C:\GitHub\Calamari\built-packages</set>
```

The configuration file can normally be found at:

```
C:\Octopus\OctopusServer\OctopusSever.config
```
