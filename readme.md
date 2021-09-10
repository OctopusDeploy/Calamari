Overview
===

**Sashimi** are packages that express new steps and account types that Octopus Server can use.

Things named **Sashimi.*** produce a dll that will be loaded into Octopus Server itself, and provides components that extend server with a new step capability.

Things named **Calamari.*** produce an executable that will be sent via Tentacle to a target or worker to do the work of the new step.

Each **Sashimi.*** repository ([example](https://github.com/octopusdeploy/sashimi.azureappservice)), sometimes referred to as a "flavour" 🌮, will contain a "slice of Sashimi", and a Calamari. This is is to say - it will provide the bits that extend server, and the thing that does the work of the step. These things work together to make steps work for Octopus Server.

There are some intermediary libraries named **Sashimi.*** and **Calamari.***, like those located within this repository, or in the [Calamari repository](https://github.com/octopusdeploy/calamari). With these libraries, you can assume **Sashimi.*** libraries will be consumed by a **Sashimi** extension, and **Calamari.*** libraries will be consumed by a **Calamari** executable. _We will never cross the streams between Sashimi and Calamari_.

Limitations:

- Sashimi do not expose step UI. All required UI for Sashimi packages is still packaged within the Octopus Server web portal

For further information see the [Sashimi Wiki](https://github.com/OctopusDeploy/sashimi/wiki)

Deprecation
---

Sashimi are being deprecated in favour of a new architecture for developing steps that focusses on providing a simplified development experience in a language other than .NET, named [Step Packages](https://github.com/OctopusDeploy/step-api/blob/main/docs/StepPackages.md).
