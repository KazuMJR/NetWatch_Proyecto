[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('192.168.56.10', '192.168.56.30')]
    [string]$IPAddress,

    [string]$InterfaceAlias = 'Ethernet 2'
)

$ErrorActionPreference = 'Stop'

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Ejecute PowerShell como Administrador.'
}

$adapter = Get-NetAdapter -Name $InterfaceAlias -ErrorAction Stop
if ($adapter.Status -eq 'Disabled') {
    Enable-NetAdapter -Name $InterfaceAlias -Confirm:$false
}

$defaultGateway = Get-NetIPConfiguration -InterfaceAlias $InterfaceAlias |
    Select-Object -ExpandProperty IPv4DefaultGateway -ErrorAction SilentlyContinue
if ($defaultGateway) {
    throw "La interfaz '$InterfaceAlias' tiene una puerta de enlace y podría ser la NIC NAT. Seleccione el Adaptador 2 host-only."
}

$conflictingAddresses = Get-NetIPAddress -InterfaceAlias $InterfaceAlias -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object {
        $_.IPAddress -ne $IPAddress -and
        $_.IPAddress -notlike '169.254.*'
    }

if ($conflictingAddresses) {
    $values = ($conflictingAddresses.IPAddress -join ', ')
    throw "La interfaz ya tiene otra IPv4 ($values). Verifique que sea la NIC host-only y elimine solo la dirección incorrecta antes de repetir."
}

Set-NetIPInterface -InterfaceAlias $InterfaceAlias -AddressFamily IPv4 -Dhcp Disabled

$targetAddress = Get-NetIPAddress -InterfaceAlias $InterfaceAlias -AddressFamily IPv4 -ErrorAction SilentlyContinue |
    Where-Object { $_.IPAddress -eq $IPAddress }
if (-not $targetAddress) {
    New-NetIPAddress -InterfaceAlias $InterfaceAlias -IPAddress $IPAddress -PrefixLength 24 | Out-Null
}

try {
    Set-NetConnectionProfile -InterfaceAlias $InterfaceAlias -NetworkCategory Private
}
catch {
    Write-Warning "No fue posible cambiar el perfil a Privado: $($_.Exception.Message)"
}

$firewallRuleName = 'NetWatch-ICMPv4-Echo-Host'
if (-not (Get-NetFirewallRule -Name $firewallRuleName -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule `
        -Name $firewallRuleName `
        -DisplayName 'NetWatch ICMPv4 Echo desde host' `
        -Protocol ICMPv4 `
        -IcmpType 8 `
        -Direction Inbound `
        -Action Allow `
        -Profile Any `
        -RemoteAddress 192.168.56.1 | Out-Null
}

Write-Host "Interfaz '$InterfaceAlias' configurada con $IPAddress/24 sin gateway ni DNS."
Get-NetIPAddress -InterfaceAlias $InterfaceAlias -AddressFamily IPv4 |
    Format-Table InterfaceAlias, IPAddress, PrefixLength, AddressState

