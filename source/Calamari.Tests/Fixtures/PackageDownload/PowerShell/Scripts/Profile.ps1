 # returns the commandline powershell was called with which lets us check for -NoProfile in our test
 (gwmi win32_process | ? { $_.processname -eq "powershell.exe" }) | select commandline
