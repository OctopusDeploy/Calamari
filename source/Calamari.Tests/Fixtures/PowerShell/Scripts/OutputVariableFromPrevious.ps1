$FirstName = $OctopusParameters["Octopus.Action[PreviousStep].Output.FirstName"]
$LastName = $OctopusParameters["Octopus.Action[PreviousStep].Output.LastName"]

Write-host "Hello $FirstName $LastName"
