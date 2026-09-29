[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateLength(12, 128)][ValidatePattern('^[A-Za-z0-9._!@%+\-]+$')][string]$AdminPassword,
    [Parameter(Mandatory)][ValidateLength(12, 128)][ValidatePattern('^[A-Za-z0-9._!@%+\-]+$')][string]$TechnicianPassword,
    [Parameter(Mandatory)][ValidateLength(12, 128)][ValidatePattern('^[A-Za-z0-9._!@%+\-]+$')][string]$MySqlPassword,
    [Parameter(Mandatory)][ValidateLength(12, 128)][ValidatePattern('^[A-Za-z0-9._!@%+\-]+$')][string]$MySqlRootPassword
)

$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$templatePath = Join-Path $repository '.env.example'
$environmentPath = Join-Path $repository '.env'

if (Test-Path -LiteralPath $environmentPath) {
    throw "Ya existe $environmentPath. Muévalo o elimínelo conscientemente antes de generar otro archivo."
}

function New-RandomHex([int]$ByteCount) {
    $buffer = [byte[]]::new($ByteCount)
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($buffer) }
    finally { $generator.Dispose() }
    return (($buffer | ForEach-Object { $_.ToString('x2') }) -join '')
}

$content = Get-Content -LiteralPath $templatePath -Raw
$replacements = @{
    'CAMBIAR_CLAVE_MYSQL' = $MySqlPassword
    'CAMBIAR_CLAVE_ROOT_MYSQL' = $MySqlRootPassword
    'CAMBIAR_POR_SECRETO_ALEATORIO_DE_64_CARACTERES_COMO_MINIMO' = (New-RandomHex 48)
    'CAMBIAR_POR_CLAVE_ALEATORIA_DEL_AGENTE' = (New-RandomHex 32)
    'CAMBIAR_CLAVE_ADMIN' = $AdminPassword
    'CAMBIAR_CLAVE_TECNICO' = $TechnicianPassword
}

foreach ($entry in $replacements.GetEnumerator()) {
    if ($entry.Value -match '[\r\n]') { throw 'Las claves no pueden contener saltos de línea.' }
    $content = $content.Replace($entry.Key, $entry.Value)
}

[System.IO.File]::WriteAllText($environmentPath, $content, [System.Text.UTF8Encoding]::new($false))
Write-Host "Configuración creada en $environmentPath"
Write-Host 'Conserve las credenciales en un gestor de contraseñas; .env está excluido de Git.'
