Overview
===

**Sashimi** are packages that express new steps and account types that Octopus Server can use.

Things named **Sashimi.*** produce a dll that will be loaded into Octopus Server itself, and provides components that extend server with a new step capability.

Things named **Calamari.*** produce an executable that will be sent via Tentacle to a target or worker to do the work of the new step.

There are some intermediary libraries named **Sashimi.*** and **Calamari.***, like those located within this repository, or in the [Calamari repository](https://github.com/octopusdeploy/calamari). With these libraries, you can assume **Sashimi.*** libraries will be consumed by a **Sashimi** extension, and **Calamari.*** libraries will be consumed by a **Calamari** executable. _We will never cross the streams between Sashimi and Calamari_.

Limitations:

- Sashimi do not expose step UI. All required UI for Sashimi packages is still packaged within the Octopus Server web portal

For further information see the [Sashimi Wiki](https://github.com/OctopusDeploy/sashimi/wiki)
