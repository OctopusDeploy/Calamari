Function Log($Message, $IncludeTimestamp) {
    $logEntry = $Message

    if ($IncludeTimestamp)
    {
        $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm"
        $newLine = [System.Environment].NewLine
        $logEntry = "$timestamp $Message $newLine"
    }

    Write-Host $logEntry
}