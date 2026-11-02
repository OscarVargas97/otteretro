# Baja DolphinTool y chdman de sus fuentes oficiales a src/Otteretro/herramientas, que van en el repo (#232, #241).
# Solo hace falta para actualizarlas: borra el archivo, cambia la versión y el hash, y córrelo.
# Fija la versión y el hash: un archivo distinto no se usa. Hace falta 7-Zip (7z en el PATH o en Program Files).
$ErrorActionPreference = 'Stop'
$raiz = Split-Path $PSScriptRoot -Parent
$destino = Join-Path $raiz 'src\Otteretro\herramientas'
New-Item -ItemType Directory -Force $destino | Out-Null

$fuentes = @(
    @{ Url = 'https://dl.dolphin-emu.org/releases/2512/dolphin-2512-x64.7z'; Sha256 = '315a78b30fd9723923634efd426b0ad68d4ec61a4ad579847507f91e2fa12f91'; Extraer = 'Dolphin-x64/DolphinTool.exe'; Licencia = 'Dolphin-x64/license.txt'; Archivo = 'DolphinTool.exe'; Texto = 'COPYING-Dolphin.txt' },
    @{ Url = 'https://github.com/mamedev/mame/releases/download/mame0289/mame0289b_x64.exe'; Sha256 = 'a1aa7912168c9d1b05e611906bc21b8b9be3935822aead36d12a1da363150b7d'; Extraer = 'chdman.exe'; Licencia = 'COPYING'; Archivo = 'chdman.exe'; Texto = 'COPYING-MAME.txt' }
)

$siete = (Get-Command 7z -ErrorAction SilentlyContinue).Source
if (-not $siete) { $siete = 'C:\Program Files\7-Zip\7z.exe' }

foreach ($f in $fuentes) {
    if (Test-Path (Join-Path $destino $f.Archivo)) { continue }
    $temporal = Join-Path ([IO.Path]::GetTempPath()) ([IO.Path]::GetRandomFileName())
    New-Item -ItemType Directory $temporal | Out-Null
    try {
        $bajado = Join-Path $temporal ([IO.Path]::GetFileName($f.Url))
        Invoke-WebRequest $f.Url -OutFile $bajado -UseBasicParsing
        $hash = (Get-FileHash $bajado -Algorithm SHA256).Hash
        if ($hash -ne $f.Sha256) { throw "Hash distinto en $($f.Url): $hash" }
        & $siete e "-o$temporal" -y $bajado $f.Extraer $f.Licencia | Out-Null
        if ($LASTEXITCODE) { throw "7-Zip falló con $bajado" }
        Copy-Item (Join-Path $temporal $f.Archivo) $destino
        Copy-Item (Join-Path $temporal (Split-Path $f.Licencia -Leaf)) (Join-Path $destino $f.Texto)
    }
    finally { Remove-Item $temporal -Recurse -Force }
}
