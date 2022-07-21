$SilentlyContinuePreference = 'SilentlyContinue'

Write-Host "Hello, write-host!"
Write-Verbose "Hello, write-verbose!"
Write-Output "Hello, write-output!"
Write-Warning "Hello, write-warning!"

Write-Warning "This warning should not appear in logs!" -WarningAction $SilentlyContinuePreference

$WarningPreferenceToRestore = $WarningPreference
$WarningPreference = $SilentlyContinuePreference

Write-Warning "This warning should not appear in logs!"

$WarningPreference = $WarningPreferenceToRestore

# It would be nice to support the following but I haven't figure it out:
# Test-NetConnection -ComputerName abc123 -WarningAction SilentlyContinue

Write-Error "Hello-Error!"