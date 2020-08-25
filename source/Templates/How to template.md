# Background

Sashimi uses templates distributed via `dotnet new` command to help developers getting started.



# Using templates

### Installing latest templates

```bash
dotnet new -i Sashimi.Templates
```

### Installing local templates for testing

From a local folder:
```bash
dotnet new -i <sourcecodelocation>/Sashimi/source/Templates
```
From a prerelease nuget package:
```bash
dotnet new -i Sashimi.Templates::version-prerelease
```

### Uninstalling local templates for testing

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



