# Background

Sashimi uses templates distributed via `dotnet new` command to help developers getting started.



# Using templates

At any point, run ```dotnet new -u``` to see a list of installed templates and where they were installed from.

### Installing latest templates from Feedz.io

For release versions:
```bash
dotnet new -i Sashimi.Templates --nuget-source https://f.feedz.io/octopus-deploy/dependencies/nuget/index.json
```
For pre-release versions (where the version is `8.1.1-branch0001`):
```bash
dotnet new -i Sashimi.Templates::8.1.1-branch0001 --nuget-source https://f.feedz.io/octopus-deploy/dependencies/nuget/index.json
```

### Installing local templates (without packaging) for testing

From a local folder:
```bash
dotnet new -i <sourcecodelocation>/Sashimi/source/Templates
```

Note that you'll still [see usage information](https://stackoverflow.com/questions/56259025/dotnet-new-install-shows-usage-information) after running this.
### Uninstalling local templates for testing

Note the folder path needs to be absolute. Run ```dotnet new -u``` to see the command needed to uninstall.
```bash
dotnet new -u <folderpath|nuget package name>
```

### How cake builds the templates
There is a target in cake named `CreateTemplatesPackage` that creates the nuget `Sashimi.Templates` package.
You will notice that there are cake scripts under the `templates` folder, these are called by the cake target.
These cake scripts name is important because we use the cake file name to look for a folder named the same, this folder is the root of the files for the template being executed.

# Resources

The following resources are useful while learning how to create templates for `dotnet new`

- This is the [github repo](https://github.com/dotnet/templating) for the templating engine
- The place to start exploring https://github.com/dotnet/templating/wiki
- The [official MS docs](https://docs.microsoft.com/en-us/dotnet/core/tools/custom-templates)



