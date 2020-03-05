Write-Host "HTTP_PROXY:$env:HTTP_PROXY"
Write-Host "HTTPS_PROXY:$env:HTTPS_PROXY"
Write-Host "NO_PROXY:$env:NO_PROXY"

$testUri = New-Object Uri("http://octopustesturl.com")
$octopusProxyUri = [System.Net.WebRequest]::DefaultWebProxy.GetProxy($testUri)
if ($octopusProxyUri.Host -ne "octopustesturl.com")
{
    Write-Host "WebRequest.DefaultProxy:$octopusProxyUri"
}
else
{
    Write-Host "WebRequest.DefaultProxy:None"
}