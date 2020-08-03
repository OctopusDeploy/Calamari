if($OctopusParameters["Octopus.Action.Azure.ExtensionsDirectory"] -eq "") {
    if($env:AZURE_EXTENSION_DIR -eq "$($HOME)\.azure\cliextensions") {
        exit 0
    }
} else {
    if($env:AZURE_EXTENSION_DIR -eq $OctopusParameters["Octopus.Action.Azure.ExtensionsDirectory"]) {
        exit 0
    }
}

exit 1