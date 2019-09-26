 # returns the commandline powershell was called with which lets us check for -NoProfile in our test
ps -Ao args | grep -i pwsh | grep -v grep