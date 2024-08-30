# Static test infrastructure 

Static clusters for cloud provider specific authentication tests are provisioned using Terraform Cloud.

- [EKS configuration](https://app.terraform.io/app/octopus-deploy/workspaces/calamari-testing-kubernetes-static-infrastructure-eks)
- [AKS configuration (In progress)](https://app.terraform.io/app/octopus-deploy/workspaces/calamari-testing-kubernetes-static-infrastructure-sks)
- [GKE configuration](https://app.terraform.io/app/octopus-deploy/workspaces/calamari-testing-kubernetes-static-infrastructure-gke)

Ensure all the tests that are written against these clusters do not interact with each other.
Tests that do anything more than test authentication/cloud provider specific features should be written to target a local kind instance.