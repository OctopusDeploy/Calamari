Calamari tests spin up various Azure resources that are not reliably removed once testing is complete. This cleanup is run as an [Octopus runbook](https://deploy.octopus.app/app#/Spaces-1/projects/calamari/operations/runbooks) to remove dangling resources.

Sourced from https://github.com/OctopusDeploy/sandbox-cleanup
These scripts have been explicitly left to reference `Sandbox` and similar as a means to avoid large changes from the original scripts. The slack functionality has been left, but not at all tested.