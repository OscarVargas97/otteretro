# Empaqueta Otteretro con Velopack en artefactos\paquetes (paquetes locales mientras no haya releases, #101).
# Uso: .\scripts\empaquetar.ps1 -Version 0.0.1
# Cada versión empaquetada en la misma carpeta forma el origen de actualizaciones local
# (config.json → "origenActualizaciones": "<repo>\\artefactos\\paquetes").
param([Parameter(Mandatory)][string]$Version)
$ErrorActionPreference = 'Stop'

$raiz = Split-Path $PSScriptRoot -Parent
$publicado = Join-Path $raiz 'artefactos\publicado'
$paquetes = Join-Path $raiz 'artefactos\paquetes'

Remove-Item $publicado -Recurse -Force -ErrorAction SilentlyContinue
dotnet publish (Join-Path $raiz 'src\Otteretro') -c Release -r win-x64 --self-contained -p:Version=$Version -o $publicado
if ($LASTEXITCODE) { throw 'dotnet publish falló' }

vpk pack --packId Otteretro --packVersion $Version --packTitle Otteretro --packDir $publicado `
    --mainExe Otteretro.exe --icon (Join-Path $raiz 'src\Otteretro\nutria.ico') --outputDir $paquetes
if ($LASTEXITCODE) { throw 'vpk pack falló' }

Write-Host "Listo: $paquetes (instalador: Otteretro-win-Setup.exe)"
