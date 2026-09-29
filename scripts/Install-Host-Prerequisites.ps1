[CmdletBinding()]
param(
    [switch]$SkipBuildTools,
    [switch]$SkipAgentBuild
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $shell = (Get-Process -Id $PID).Path
    $arguments = @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', ('"{0}"' -f $PSCommandPath)
    )
    if ($SkipBuildTools) { $arguments += '-SkipBuildTools' }
    if ($SkipAgentBuild) { $arguments += '-SkipAgentBuild' }

    Write-Host 'Se solicitará autorización de administrador de Windows.'
    $elevated = Start-Process -FilePath $shell -Verb RunAs -ArgumentList $arguments -Wait -PassThru
    exit $elevated.ExitCode
}

$repository = Split-Path -Parent $PSScriptRoot
$downloadDirectory = Join-Path $env:TEMP 'NetWatchPrerequisites'
$logPath = Join-Path $repository 'host-setup.log'
New-Item -ItemType Directory -Path $downloadDirectory -Force | Out-Null

Start-Transcript -LiteralPath $logPath -Append | Out-Null

function Write-Step([string]$Message) {
    Write-Host "`n=== $Message ===" -ForegroundColor Cyan
}

function Get-OfficialFile {
    param(
        [Parameter(Mandatory)][uri]$Uri,
        [Parameter(Mandatory)][string]$Destination
    )

    if (Test-Path -LiteralPath $Destination) {
        Write-Host "Ya descargado: $Destination"
        return
    }

    Write-Host "Descargando $Uri"
    Invoke-WebRequest -Uri $Uri -OutFile $Destination -UseBasicParsing
}

function Assert-ValidSignature([string]$Path) {
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid') {
        throw "Firma digital no válida para $Path. Estado: $($signature.Status)"
    }
    Write-Host "Firma válida: $($signature.SignerCertificate.Subject)"
}

function Invoke-CheckedProcess {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [string[]]$ArgumentList = @(),
        [int[]]$AllowedExitCodes = @(0, 3010)
    )

    $process = Start-Process -FilePath $FilePath -ArgumentList $ArgumentList -Wait -PassThru
    if ($process.ExitCode -notin $AllowedExitCodes) {
        throw "$FilePath terminó con código $($process.ExitCode)."
    }
    if ($process.ExitCode -eq 3010) {
        $script:RestartRequired = $true
    }
}

try {
    $script:RestartRequired = $false

    Write-Step 'Habilitar WSL y Virtual Machine Platform'
    foreach ($feature in @('Microsoft-Windows-Subsystem-Linux', 'VirtualMachinePlatform')) {
        $state = Get-WindowsOptionalFeature -Online -FeatureName $feature
        if ($state.State -ne 'Enabled') {
            $result = Enable-WindowsOptionalFeature -Online -FeatureName $feature -All -NoRestart
            if ($result.RestartNeeded) { $script:RestartRequired = $true }
        }
        else {
            Write-Host "$feature ya estaba habilitado."
        }
    }

    Write-Step 'Instalar la versión estable de WSL'
    $wslRelease = Invoke-RestMethod -Uri 'https://api.github.com/repos/microsoft/WSL/releases/latest'
    $wslAssets = @($wslRelease.assets | Where-Object { $_.name -match '\.x64\.msi$' })
    if ($wslAssets.Count -ne 1) {
        throw "No se encontró un único instalador WSL x64 en la versión estable $($wslRelease.tag_name)."
    }
    $wslInstaller = Join-Path $downloadDirectory $wslAssets[0].name
    Get-OfficialFile -Uri $wslAssets[0].browser_download_url -Destination $wslInstaller
    Assert-ValidSignature $wslInstaller
    Invoke-CheckedProcess -FilePath 'msiexec.exe' -ArgumentList @('/i', $wslInstaller, '/qn', '/norestart')

    $wslTemplate = Join-Path $repository 'deploy\windows\wslconfig.example'
    $wslConfiguration = Join-Path $env:USERPROFILE '.wslconfig'
    if (-not (Test-Path -LiteralPath $wslConfiguration)) {
        Copy-Item -LiteralPath $wslTemplate -Destination $wslConfiguration
        Write-Host "Configuración de memoria WSL creada en $wslConfiguration"
    }
    else {
        Write-Warning "Ya existe $wslConfiguration; no se sobrescribió. Compare su contenido con deploy\windows\wslconfig.example."
    }

    Write-Step 'Instalar Oracle VirtualBox 7.2.20'
    $virtualBoxName = 'VirtualBox-7.2.20-175154-Win.exe'
    $virtualBoxBase = 'https://download.virtualbox.org/virtualbox/7.2.20'
    $virtualBoxInstaller = Join-Path $downloadDirectory $virtualBoxName
    $virtualBoxChecksums = Join-Path $downloadDirectory 'VirtualBox-7.2.20-SHA256SUMS.txt'
    Get-OfficialFile -Uri "$virtualBoxBase/$virtualBoxName" -Destination $virtualBoxInstaller
    Get-OfficialFile -Uri "$virtualBoxBase/SHA256SUMS" -Destination $virtualBoxChecksums
    $checksumLine = Get-Content -LiteralPath $virtualBoxChecksums | Where-Object { $_ -match [regex]::Escape($virtualBoxName) } | Select-Object -First 1
    if (-not $checksumLine) { throw 'No se encontró el hash oficial de VirtualBox.' }
    $expectedHash = ($checksumLine -split '\s+')[0]
    $actualHash = (Get-FileHash -LiteralPath $virtualBoxInstaller -Algorithm SHA256).Hash
    if ($actualHash -ne $expectedHash) { throw 'El SHA256 de VirtualBox no coincide con Oracle.' }
    Assert-ValidSignature $virtualBoxInstaller
    $virtualBoxVersion = Get-ItemProperty @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'
    ) -ErrorAction SilentlyContinue |
        Where-Object { $_.DisplayName -eq 'Oracle VirtualBox 7.2.20' -or ($_.DisplayName -like 'Oracle VirtualBox*' -and $_.DisplayVersion -eq '7.2.20') } |
        Select-Object -First 1
    if (-not $virtualBoxVersion) {
        Invoke-CheckedProcess -FilePath $virtualBoxInstaller -ArgumentList @('--silent', '--ignore-reboot')
    }
    else {
        Write-Host 'VirtualBox 7.2.20 ya está instalado.'
    }

    Write-Step 'Instalar .NET SDK 8 x64'
    $dotnetExecutable = Join-Path $env:ProgramFiles 'dotnet\dotnet.exe'
    if (-not (Test-Path -LiteralPath $dotnetExecutable)) {
        $dotnetMetadata = Invoke-RestMethod -Uri 'https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/8.0/releases.json'
        $latestSdk = $dotnetMetadata.'latest-sdk'
        $sdkRelease = $dotnetMetadata.releases | Where-Object { $_.sdk.version -eq $latestSdk } | Select-Object -First 1
        $sdkFile = $sdkRelease.sdk.files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -like '*.exe' } | Select-Object -First 1
        if (-not $sdkFile) { throw "No se encontró el instalador .NET SDK $latestSdk para win-x64." }
        $dotnetInstaller = Join-Path $downloadDirectory $sdkFile.name
        Get-OfficialFile -Uri $sdkFile.url -Destination $dotnetInstaller
        $dotnetHash = (Get-FileHash -LiteralPath $dotnetInstaller -Algorithm SHA512).Hash
        if ($dotnetHash -ne $sdkFile.hash) { throw 'El SHA512 del instalador .NET no coincide con Microsoft.' }
        Assert-ValidSignature $dotnetInstaller
        Invoke-CheckedProcess -FilePath $dotnetInstaller -ArgumentList @('/install', '/quiet', '/norestart')
    }
    else {
        Write-Host '.NET ya está instalado.'
    }

    Write-Step 'Instalar CMake 4.4.3 x64'
    $cmakeExecutable = Join-Path $env:ProgramFiles 'CMake\bin\cmake.exe'
    if (-not (Test-Path -LiteralPath $cmakeExecutable)) {
        $cmakeName = 'cmake-4.4.3-windows-x86_64.msi'
        $cmakeBase = 'https://cmake.org/files/v4.4'
        $cmakeInstaller = Join-Path $downloadDirectory $cmakeName
        $cmakeChecksums = Join-Path $downloadDirectory 'cmake-4.4.3-SHA-256.txt'
        Get-OfficialFile -Uri "$cmakeBase/$cmakeName" -Destination $cmakeInstaller
        Get-OfficialFile -Uri "$cmakeBase/cmake-4.4.3-SHA-256.txt" -Destination $cmakeChecksums
        $cmakeLine = Get-Content -LiteralPath $cmakeChecksums | Where-Object { $_ -match [regex]::Escape($cmakeName) } | Select-Object -First 1
        if (-not $cmakeLine) { throw 'No se encontró el hash oficial de CMake.' }
        $cmakeExpected = ($cmakeLine -split '\s+')[0]
        $cmakeActual = (Get-FileHash -LiteralPath $cmakeInstaller -Algorithm SHA256).Hash
        if ($cmakeActual -ne $cmakeExpected) { throw 'El SHA256 de CMake no coincide con Kitware.' }
        Assert-ValidSignature $cmakeInstaller
        Invoke-CheckedProcess -FilePath 'msiexec.exe' -ArgumentList @('/i', $cmakeInstaller, '/qn', '/norestart', 'ADD_CMAKE_TO_PATH=System')
    }
    else {
        Write-Host 'CMake ya está instalado.'
    }

    if (-not $SkipBuildTools) {
        Write-Step 'Instalar Visual Studio 2022 Build Tools para C++'
        $vsWhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
        $hasCppTools = $false
        if (Test-Path -LiteralPath $vsWhere) {
            $cppInstall = & $vsWhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
            $hasCppTools = -not [string]::IsNullOrWhiteSpace(($cppInstall | Select-Object -First 1))
        }
        if (-not $hasCppTools) {
            $buildToolsInstaller = Join-Path $downloadDirectory 'vs_BuildTools.exe'
            Get-OfficialFile -Uri 'https://aka.ms/vs/17/release/vs_BuildTools.exe' -Destination $buildToolsInstaller
            Assert-ValidSignature $buildToolsInstaller
            Invoke-CheckedProcess -FilePath $buildToolsInstaller -ArgumentList @(
                '--quiet', '--wait', '--norestart', '--nocache',
                '--installPath', 'C:\BuildTools',
                '--add', 'Microsoft.VisualStudio.Workload.VCTools',
                '--includeRecommended'
            )
        }
        else {
            Write-Host 'Las herramientas C++ de Visual Studio ya están instaladas.'
        }
    }

    Write-Step 'Instalar Docker Desktop con backend WSL 2'
    $dockerExecutables = @(
        (Join-Path $env:LOCALAPPDATA 'Programs\DockerDesktop\Docker Desktop.exe'),
        (Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe')
    )
    $dockerInstalled = @($dockerExecutables | Where-Object { Test-Path -LiteralPath $_ -ErrorAction SilentlyContinue }).Count -gt 0
    if (-not $dockerInstalled) {
        $dockerInstaller = Join-Path $downloadDirectory 'DockerDesktopInstaller.exe'
        Get-OfficialFile -Uri 'https://desktop.docker.com/win/main/amd64/Docker%20Desktop%20Installer.exe' -Destination $dockerInstaller
        Assert-ValidSignature $dockerInstaller
        Invoke-CheckedProcess -FilePath $dockerInstaller -ArgumentList @(
            'install', '--user', '--accept-license', '--backend=wsl-2',
            '--no-windows-containers', '--quiet'
        )
    }
    else {
        Write-Host 'Docker Desktop ya está instalado.'
    }

    Write-Step 'Configurar firewall del laboratorio'
    & (Join-Path $PSScriptRoot 'Configure-Host-Firewall.ps1')

    if (-not $SkipBuildTools -and -not $SkipAgentBuild) {
        Write-Step 'Compilar netwatch-agent.exe'
        if (-not (Test-Path -LiteralPath $cmakeExecutable)) {
            $cmakeExecutable = 'cmake.exe'
        }
        $agentSource = Join-Path $repository 'src\NetWatch.Agent'
        $agentBuild = Join-Path $agentSource 'build'
        & $cmakeExecutable -S $agentSource -B $agentBuild -A x64
        if ($LASTEXITCODE -ne 0) { throw 'CMake no pudo configurar el agente.' }
        & $cmakeExecutable --build $agentBuild --config Release
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo compilar el agente Windows.' }
    }

    Write-Step 'Resultado'
    Write-Host "Registro: $logPath"
    $pendingReboot = $script:RestartRequired -or
        (Test-Path -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending') -or
        (Test-Path -LiteralPath 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired')
    if ($pendingReboot) {
        Write-Warning 'Windows necesita reiniciarse para completar WSL/Virtual Machine Platform. No se reinició automáticamente.'
    }
    else {
        Write-Host 'No se informó un reinicio obligatorio.'
    }
}
finally {
    Stop-Transcript | Out-Null
}
