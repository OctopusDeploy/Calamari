param(
    [Parameter(Position=0)]
    [string]$Operation
)

$ErrorActionPreference = "Stop"

# Get the Calamari executable path from environment variable
$calamariExe = $env:OCTOPUS_CALAMARI_EXECUTABLE
if (-not $calamariExe) {
    Write-Error "OCTOPUS_CALAMARI_EXECUTABLE environment variable not set"
    exit 1
}

# Execute Calamari docker-credential command, passing stdin/stdout through
$arguments = @("docker-credential", "--operation=$Operation")

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = $calamariExe
$psi.Arguments = ($arguments -join " ")
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true

$process = New-Object System.Diagnostics.Process
$process.StartInfo = $psi
$process.Start()

# Forward stdin to process
$input = [Console]::In.ReadToEnd()
if ($input) {
    $process.StandardInput.Write($input)
}
$process.StandardInput.Close()

# Forward stdout/stderr back
$stdout = $process.StandardOutput.ReadToEnd()
$stderr = $process.StandardError.ReadToEnd()

$process.WaitForExit()

if ($stdout) {
    Write-Output $stdout
}
if ($stderr) {
    Write-Error $stderr
}

exit $process.ExitCode