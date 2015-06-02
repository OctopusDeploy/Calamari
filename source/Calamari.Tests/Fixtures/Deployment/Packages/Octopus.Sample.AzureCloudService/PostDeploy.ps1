$cloudService = Get-AzureService -ServiceName "octopustestapp" 
Write-Host "Service Name: $($cloudService.ServiceName)"