$SilentlyContinuePreference = 'SilentlyContinue'

Write-Host "Hello, write-host!"
Write-Verbose "Hello, write-verbose!"
Write-Output "Hello, write-output!"
Write-Warning "Hello, write-warning!"
Write-Error "Hello-Error!"


Write-Warning "This warning should not appear in logs!" -WarningAction $SilentlyContinuePreference

$VerbosePreferenceToRestore = $VerbosePreference
$VerbosePreference = $SilentlyContinuePreference
$WarningPreferenceToRestore = $WarningPreference
$WarningPreference = $SilentlyContinuePreference

Write-Warning "This warning should not appear in logs!"
Write-Verbose "This verbose should not appear in logs!"

$VerbosePreference = $VerbosePreferenceToRestore
$WarningPreference = $WarningPreferenceToRestore

# It would be nice to support the following but I haven't figure it out:
# Test-NetConnection -ComputerName abc123 -WarningAction SilentlyContinue