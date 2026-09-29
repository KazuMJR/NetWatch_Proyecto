[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $shell = (Get-Process -Id $PID).Path
    $arguments = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', ('"{0}"' -f $PSCommandPath))
    $elevated = Start-Process -FilePath $shell -Verb RunAs -ArgumentList $arguments -Wait -PassThru
    exit $elevated.ExitCode
}

$rules = @(
    @{ Name = 'NetWatch-Web-Lab'; DisplayName = 'NetWatch Web Lab'; Port = 8080 },
    @{ Name = 'NetWatch-API-Lab'; DisplayName = 'NetWatch API Lab'; Port = 8081 }
)

foreach ($definition in $rules) {
    $rule = Get-NetFirewallRule -Name $definition.Name -ErrorAction SilentlyContinue
    if (-not $rule) {
        New-NetFirewallRule `
            -Name $definition.Name `
            -DisplayName $definition.DisplayName `
            -Direction Inbound `
            -Action Allow `
            -Enabled True `
            -Protocol TCP `
            -LocalPort $definition.Port `
            -Profile Any `
            -RemoteAddress '192.168.56.0/24' | Out-Null
        Write-Host "Regla creada: $($definition.DisplayName)"
    }
    else {
        Write-Host "Regla ya existente: $($definition.DisplayName)"
    }
}

Write-Host 'El panel y la API solo admiten entrada desde 192.168.56.0/24 mediante estas reglas.' -ForegroundColor Green
