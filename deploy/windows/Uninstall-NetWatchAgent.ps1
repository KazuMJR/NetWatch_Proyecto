[CmdletBinding()]
param([string]$InstallDirectory = "$env:ProgramFiles\NetWatch Agent")

$ErrorActionPreference = 'Stop'
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Ejecute PowerShell como Administrador.'
}

if (Get-ScheduledTask -TaskName 'NetWatch Agent' -ErrorAction SilentlyContinue) {
    Stop-ScheduledTask -TaskName 'NetWatch Agent' -ErrorAction SilentlyContinue
    Unregister-ScheduledTask -TaskName 'NetWatch Agent' -Confirm:$false
}
if (Test-Path -LiteralPath $InstallDirectory) {
    Remove-Item -LiteralPath $InstallDirectory -Recurse -Force
}
Write-Host 'NetWatch Agent fue desinstalado.'
