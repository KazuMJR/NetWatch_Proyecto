[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$environmentPath = Join-Path $repository '.env'
$credentialsPath = Join-Path $repository 'NETWATCH-CREDENCIALES-LOCALES.txt'

function Protect-CredentialsFile([string]$Path) {
    $acl = New-Object System.Security.AccessControl.FileSecurity
    $currentAccount = [System.Security.Principal.NTAccount]::new([System.Security.Principal.WindowsIdentity]::GetCurrent().Name)
    $acl.SetOwner($currentAccount)
    $acl.SetAccessRuleProtection($true, $false)
    $inheritance = [System.Security.AccessControl.InheritanceFlags]::None
    $propagation = [System.Security.AccessControl.PropagationFlags]::None
    $allow = [System.Security.AccessControl.AccessControlType]::Allow
    $identities = @(
        $currentAccount,
        [System.Security.Principal.SecurityIdentifier]::new('S-1-5-18'),
        [System.Security.Principal.SecurityIdentifier]::new('S-1-5-32-544')
    )
    foreach ($identity in $identities) {
        $rule = New-Object System.Security.AccessControl.FileSystemAccessRule($identity, 'FullControl', $inheritance, $propagation, $allow)
        [void]$acl.AddAccessRule($rule)
    }
    Set-Acl -LiteralPath $Path -AclObject $acl
}

if (Test-Path -LiteralPath $environmentPath) {
    if (Test-Path -LiteralPath $credentialsPath) {
        Protect-CredentialsFile $credentialsPath
        Write-Host "Se conservaron las claves existentes y se protegió: $credentialsPath" -ForegroundColor Green
        return
    }
    throw "Ya existe $environmentPath. No se sobrescribió ninguna contraseña."
}

function New-LabPassword([string]$Prefix) {
    $buffer = [byte[]]::new(16)
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($buffer) }
    finally { $generator.Dispose() }
    $hex = ($buffer | ForEach-Object { $_.ToString('x2') }) -join ''
    return '{0}!{1}' -f $Prefix, $hex
}

$adminPassword = New-LabPassword 'Adm'
$technicianPassword = New-LabPassword 'Tec'
$mysqlPassword = New-LabPassword 'Sql'
$mysqlRootPassword = New-LabPassword 'Root'

& (Join-Path $PSScriptRoot 'Initialize-Environment.ps1') `
    -AdminPassword $adminPassword `
    -TechnicianPassword $technicianPassword `
    -MySqlPassword $mysqlPassword `
    -MySqlRootPassword $mysqlRootPassword

$environment = @{}
Get-Content -LiteralPath $environmentPath | ForEach-Object {
    if ($_ -match '^([^#=]+)=(.*)$') { $environment[$matches[1]] = $matches[2] }
}

$credentials = @"
NETWATCH NMS - CREDENCIALES LOCALES DEL LABORATORIO
Generadas: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')

Panel Web: http://localhost:8080
Administrador: admin
Contraseña administrador: $adminPassword

Técnico: technician
Contraseña técnico: $technicianPassword

Adminer: http://localhost:8082
Sistema: MySQL
Servidor: mysql
Base de datos: $($environment['MYSQL_DATABASE'])
Usuario MySQL: $($environment['MYSQL_USER'])
Contraseña MySQL: $mysqlPassword
Contraseña root MySQL: $mysqlRootPassword

Clave para agent.json de las VMs:
$($environment['AGENT_API_KEY'])

IMPORTANTE:
- Este archivo contiene secretos. No lo comparta ni lo suba a Git.
- Cambie todas las claves antes de cualquier despliegue fuera del laboratorio.
"@

[System.IO.File]::WriteAllText($credentialsPath, $credentials, [System.Text.UTF8Encoding]::new($false))
Protect-CredentialsFile $credentialsPath

Write-Host "Entorno creado: $environmentPath" -ForegroundColor Green
Write-Host "Credenciales locales: $credentialsPath" -ForegroundColor Green
