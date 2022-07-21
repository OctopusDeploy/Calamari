 # returns the commandline powershell was called with which lets us check for -NoProfile in our test
 (Get-CimInstance win32_process | ? { $_.processId -eq $PID }) | select commandline
