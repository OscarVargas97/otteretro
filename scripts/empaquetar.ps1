# Empaqueta Otteretro con Velopack en <OutputDir>\paquetes. Lo usan las releases de GitHub Actions y las pruebas locales.
# Uso: .\scripts\empaquetar.ps1 [-Version 0.2.0] [-Channel win|dev] [-OutputDir <carpeta>]
# Sin -Version arma 0.FASE.N-local.M (#179, #207): una versión propia que no se confunde con las publicadas. Para instalarla,
# en Ajustes › General › Actualizaciones se elige «Otro origen» con la carpeta <OutputDir>\paquetes (#180).
param(
    [string]$Version,
    [string]$Channel = 'win',
    [string]$OutputDir = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artefactos')
)
$ErrorActionPreference = 'Stop'

$raiz = Split-Path $PSScriptRoot -Parent
if (-not $Version) { $Version = "$(& (Join-Path $PSScriptRoot 'version.ps1'))-local.$(git -C $raiz rev-list --count HEAD)" }
$publicado = Join-Path $OutputDir 'publicado'
$paquetes = Join-Path $OutputDir 'paquetes'

Remove-Item $publicado -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish (Join-Path $raiz 'src\Otteretro') -c Release -r win-x64 --self-contained -p:Version=$Version -o $publicado
if ($LASTEXITCODE) { throw 'dotnet publish falló' }

vpk pack --packId Otteretro --packVersion $Version --packTitle Otteretro --packDir $publicado `
    --channel $Channel --mainExe Otteretro.exe --icon (Join-Path $raiz 'src\Otteretro\nutria.ico') --outputDir $paquetes
if ($LASTEXITCODE) { throw 'vpk pack falló' }

Write-Host "Listo: $paquetes (instalador: Otteretro-win-Setup.exe)"
