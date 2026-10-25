# Empaqueta Otteretro con Velopack en artefactos\paquetes (paquetes locales mientras no haya releases, #101).
# Uso: .\scripts\empaquetar.ps1 -Version 0.0.1 [-OutputDir <carpeta>]
# Cada versión empaquetada en la misma carpeta forma el origen de actualizaciones local
# (config.json → "origenActualizaciones": "<OutputDir>\\paquetes"). Desde un espacio de ia-workspace,
# OutputDir es la carpeta artefactos del espacio, para que todas las versiones queden juntas.
param(
    [Parameter(Mandatory)][string]$Version,
    [string]$OutputDir = (Join-Path (Split-Path $PSScriptRoot -Parent) 'artefactos')
)
$ErrorActionPreference = 'Stop'

$raiz = Split-Path $PSScriptRoot -Parent
$publicado = Join-Path $OutputDir 'publicado'
$paquetes = Join-Path $OutputDir 'paquetes'

Remove-Item $publicado -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish (Join-Path $raiz 'src\Otteretro') -c Release -r win-x64 --self-contained -p:Version=$Version -o $publicado
if ($LASTEXITCODE) { throw 'dotnet publish falló' }

vpk pack --packId Otteretro --packVersion $Version --packTitle Otteretro --packDir $publicado `
    --mainExe Otteretro.exe --icon (Join-Path $raiz 'src\Otteretro\nutria.ico') --outputDir $paquetes
if ($LASTEXITCODE) { throw 'vpk pack falló' }

Write-Host "Listo: $paquetes (instalador: Otteretro-win-Setup.exe)"
