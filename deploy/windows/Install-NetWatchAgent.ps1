[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$AgentExecutable,
    [Parameter(Mandatory)][string]$AgentConfiguration,
    [string]$InstallDirectory = "$env:ProgramFiles\NetWatch Agent"
)

$ErrorActionPreference = 'Stop'
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Ejecute PowerShell como Administrador.'
}

$sourceExe = (Resolve-Path -LiteralPath $AgentExecutable).Path
$sourceConfig = (Resolve-Path -LiteralPath $AgentConfiguration).Path
New-Item -ItemType Directory -Path $InstallDirectory -Force | Out-Null
$targetExe = Join-Path $InstallDirectory 'netwatch-agent.exe'
$targetConfig = Join-Path $InstallDirectory 'agent.json'
Copy-Item -LiteralPath $sourceExe -Destination $targetExe -Force
Copy-Item -LiteralPath $sourceConfig -Destination $targetConfig -Force

$action = New-ScheduledTaskAction -Execute $targetExe -Argument ('"{0}"' -f $targetConfig) -WorkingDirectory $InstallDirectory
$trigger = New-ScheduledTaskTrigger -AtStartup
$settings = New-ScheduledTaskSettingsSet -RestartCount 10 -RestartInterval (New-TimeSpan -Minutes 1) -ExecutionTimeLimit ([TimeSpan]::Zero)
$task = New-ScheduledTask -Action $action -Trigger $trigger -Settings $settings -Principal (New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest)
Register-ScheduledTask -TaskName 'NetWatch Agent' -InputObject $task -Force | Out-Null
Start-ScheduledTask -TaskName 'NetWatch Agent'
Write-Host "Agente instalado en $InstallDirectory y tarea 'NetWatch Agent' iniciada."
