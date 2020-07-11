# https://www.reddit.com/r/PowerShell/comments/6a6gnd/powershell_console_is_slow_to_start/

Set-Alias ngen (Join-Path ([Runtime.InteropServices.RuntimeEnvironment]::GetRuntimeDirectory()) ngen.exe)
ngen update