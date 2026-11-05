# Investigación de emuladores y modding (F5)

Foto al **2026-11-04** (#272, solicitud #271, fase 5 #263). Para cada emulador o port de la §8.0 de `REQUISITOS.md`: de dónde se baja, cómo queda portable, dónde guarda la configuración (global y por juego), los mandos, los mods, las partidas, la instalación de contenido, cómo se lee la versión, los ajustes más usados y la licencia. Al final, qué implica todo esto para los perfiles y los mods sobre una instalación compartida (`Emuladores\<Emulador>\<versión>`, decisión 302).

**Cómo leerlo:**
- Cada dato cita su fuente. La fuente primaria es el repositorio oficial (código o docs), la wiki oficial o la API de releases, consultados el 2026-11-04.
- **Sin verificar** = no se encontró en una fuente primaria. Puede venir de una fuente secundaria (se dice cuál) o de cómo funcionaba antes.
- Las rutas son relativas a la carpeta de datos del emulador en modo portable, salvo que se diga otra cosa. `<TitleID>` y `<serial>` son el ID del juego que usa cada consola.
- Las claves de configuración se escriben `[Sección] Clave`. Son las de la rama principal a esta fecha; si una cambió de nombre se indica.

## Resumen

| Emulador | Consola | Fuente Windows | Estable / desarrollo | Portable | Config por juego (clave) | Mods por juego | Instalar contenido | Licencia |
|---|---|---|---|---|---|---|---|---|
| Eden | Switch | Forgejo propio, `.zip` | estables y RC; nightlies aparte | carpeta `user\` | `config\custom\<TitleID>.ini` | `load\<TitleID>\<mod>\` + activar por juego | carpetas externas (`external_content_dirs`) o «Install to NAND» | GPL-3.0+ (sin verificar en el Forgejo) |
| Ryubing | Switch | Forgejo propio, `.zip` | Stable y Canary (repos distintos) | carpeta `portable\` o `-r` | `games\<titleid>\Config.json` (copia completa) | `mods\contents\<titleid>\<mod>\` + `games\<titleid>\mods.json` | `updates.json`/`dlc.json` apuntan a archivos externos | MIT (sin verificar en el Forgejo) |
| Citron (Neo) | Switch | GitHub, `.zip` | estables manuales + nightly | carpeta `user\` (por defecto) | `config\custom\<TitleID>.ini` | `load\<TitleID>\` (heredado de yuzu) | «Install to NAND» (sin verificar externo) | GPL-3.0 |
| Ryujinx SS4K | Switch | Google Drive del autor | una sola, con el pack | `portable\` (ya armado) | como Ryujinx | el pack es el mod | como Ryujinx | sin verificar |
| Dolphin | GameCube/Wii | dolphin-emu.org, `.7z` | releases (pista `beta`) y dev | `portable.txt` | `User\GameSettings\<GameID>.ini` | `User\Load\Textures\<GameID>\`, GraphicMods, Riivolution | — | GPL-2.0+ |
| Dusklight | GameCube (TP) | GitHub, `.zip` | estables | carpeta `data\` o `--user-dir` | — (un solo juego) | `.dusk` en `mods\` | — | CC0-1.0 |
| RMG | N64 | GitHub, `.zip` portable | estables | `portable.txt` | sección por juego en `mupen64plus.cfg` | texturas HD de GLideN64 (sin verificar ruta) | — | GPL-3.0 |
| Azahar | 3DS | GitHub, `.zip` | estables + alpha (prerelease) | carpeta `user\` | `config\custom\<TitleID>` | `load\mods\<TitleID>\`, `load\textures\<TitleID>\` | `--install` (copia el .cia) | GPL-2.0+ |
| MesenCE | NES/SNES/GB/GBA/PCE… | GitHub, `.zip` | estables | `settings.json` junto al exe | — | HD Packs (NES) | — | GPL-3.0 |
| bsnes | SNES | GitHub, `.zip` | v115 (2020) + `nightly` | sin verificar | — | — | — | GPL-3.0 |
| Snes9x | SNES | GitHub, `.zip` | estables | `snes9x.conf` junto al exe (sin verificar) | — | parches IPS/BPS/UPS en `Patches\` | — | no comercial |
| BlastEm | Mega Drive | retrodev.com, `.zip` | 0.6.x estable (sin verificar) + nightlies | `default.cfg` junto al exe | — | — | — | GPL-3.0 |
| mGBA | GB/GBA | GitHub, `.7z` | estables | `portable.ini` | overrides por juego en `config.ini` (sin verificar detalle) | parches IPS/UPS/BPS con el nombre de la ROM | — | MPL-2.0 |
| DuckStation | PS1 | GitHub, `.zip` | `latest` y `preview` | `portable.txt` | `gamesettings\<serial>.ini` | `textures\<serial>\` | — | CC BY-NC-ND 4.0 |
| PCSX2 | PS2 | GitHub, `.7z` | estables y nightlies (prerelease) | `portable.txt`/`.ini` | `gamesettings\<serial>_<CRC>.ini` | `textures\<serial>\`, `patches\<serial>_<CRC>.pnach` | — | GPL-3.0 |
| PPSSPP | PSP | GitHub/ppsspp.org, `.zip` | estables | por defecto (`memstick\`) | `PSP\SYSTEM\<GAMEID>_ppsspp.ini` (sin verificar nombre) | `PSP\TEXTURES\<GAMEID>\` | — | GPL-2.0+ |
| RPCS3 | PS3 | GitHub `rpcs3-binaries-win`, `.7z` | solo desarrollo | por defecto (carpeta del exe) | `config\custom_configs\config_<TITLEID>.yml` | parches `patch.yml`; reemplazo de archivos | `--installpkg`, `--installfw` | GPL-2.0 |
| shadPS4 | PS4 | GitHub, `.zip` | estables + diarias (prerelease) | carpeta `user\` | `user\custom_configs\<serial>.json` | carpeta `<juego>-mods` junto al juego, `user\patches\` | `--set-addon-folder` (DLC externo) | GPL-2.0 |
| Vita3K | Vita | GitHub (`continuous`), `.zip` | solo desarrollo | carpeta `portable\` | sí (ruta sin verificar) | sin verificar | `--pkg` + `--zrif`, `.vpk` | GPL-2.0 |
| melonDS | DS | GitHub, `.zip` | estables | sin verificar | no encontrado | — | — | GPL-3.0 |
| Cemu | Wii U | GitHub, `.zip` | estables (2.6, feb. 2025) | carpeta `portable\` | `gameProfiles\<titleid>.ini` | graphic packs (activación global) | `--mlc` apunta el almacenamiento; instalar, solo GUI | MPL-2.0 |
| xemu | Xbox | GitHub, `.zip` | estables + `pre-release` | `xemu.toml` junto al exe | no | no | — | GPL-2.0 |
| Xenia Canary | Xbox 360 | GitHub `xenia-canary-releases`, `.zip` | solo desarrollo | por defecto en Windows | `config\<TitleID>.config.toml` | `patches\` | menú «Install Content» | BSD-3 (sin verificar) |
| Flycast | Dreamcast | GitHub, `.zip` | estables | siempre (carpeta del exe) | por ID del juego en `emu.cfg` (sin verificar sección) | `data\textures\<gameId>\` | — | GPL-2.0 |
| KytyPS5 | PS5 | GitHub, `.zip` | solo desarrollo (diaria) | sin verificar | sin verificar | sin verificar | sin verificar | GPL-2.0 |
| Steam ROM Manager | herramienta | GitHub, `.exe` portable | estables | carpeta `userData\` junto al exe | — | — | CLI `srm` | GPL-3.0 |

## Riesgo legal de los emuladores de Switch

- **2024:** Nintendo demandó a los autores de yuzu, que acordaron pagar 2,4 millones de dólares y cerraron; Ryujinx también cerró ese año. Fuente secundaria: [Hitmarker](https://hitmarker.net/news/nintendo-continues-war-against-switch-emulators-with-several-new-dmca-takedowns-1649825).
- **Febrero de 2026:** aviso DMCA de Nintendo a GitHub ([2026-02-12-nintendo.md](https://github.com/github/dmca/blob/master/2026/02/2026-02-12-nintendo.md)). Alega que los emuladores eluden las medidas de protección (TPM) de la Switch. Pide retirar repositorios enteros, entre ellos `eden-emulator/Releases`, `pkgforge-dev/Citron-AppImage`, MeloNX, forks de Ryujinx, suyu, sudachi y yuzu. GitHub lo aplicó a redes completas de forks (127 y 218 repositorios). Hoy `github.com/eden-emulator/Releases` responde HTTP 451 (bloqueado por DMCA). Eden sigue en su propio Forgejo ([Nintendo Life](https://www.nintendolife.com/news/2026/02/13-switch-emulators-hit-by-latest-wave-of-nintendo-takedowns), secundaria).
- **Agosto de 2026:** hay siete avisos de Nintendo del 2026-08-17 en [github/dmca 2026/08](https://github.com/github/dmca/tree/master/2026/08). No se revisó su contenido. Una fuente secundaria habla de 401 repositorios retirados.
- **Qué implica:** los emuladores de Switch ya no viven en GitHub. Eden y Ryubing tienen Forgejo propio, Citron Neo sigue en GitHub. Cualquier fuente puede desaparecer de un día para otro, y por eso la librería privada guarda dos copias (decisión 293). Las recetas solo llevan el enlace oficial y el hash, sin binarios (§8.1.1). Nada de keys ni firmware en un perfil compartido (decisión 297).

## Switch

### Eden

- **Descarga:** releases en el Forgejo oficial, [git.eden-emu.dev/eden-emu/eden](https://git.eden-emu.dev/eden-emu/eden/releases). API: `GET https://git.eden-emu.dev/api/v1/repos/eden-emu/eden/releases` (Forgejo/Gitea). Última estable: **v0.2.1** (2026-06-01). Hay varios `.zip` de Windows por versión: `Eden-Windows-v0.2.1-amd64-msvc-standard.zip`, `-amd64-clang-pgo.zip`, `-amd64-gcc-standard.zip`, `-rog-ally-*` y `arm64`. Según las notas de la release, las PGO rinden un 10 a 30 % más y son las recomendadas. Los archivos se sirven desde `stable.eden-emu.dev/<versión>/`.
- **Estable o desarrollo:** [Basics.md](https://git.eden-emu.dev/eden-emu/eden/src/branch/master/docs/user/Basics.md) separa las estables (con número; las RC llevan `-rcN`) de las nightly (diarias, «no recomendadas para el usuario medio»), master y PR. En la API, las RC no vienen marcadas como `prerelease`: hay que distinguirlas por el `-rc` del tag. De dónde se bajan las nightlies: sin verificar (el repo de GitHub `pflyly/eden-nightly` fue retirado por el DMCA).
- **Portable:** si existe una carpeta `user` junto al exe, se usa en lugar de `%AppData%\eden` ([path_util.cpp](https://git.eden-emu.dev/eden-emu/eden/src/branch/master/src/common/fs/path_util.cpp), `PORTABLE_DIR "user"`). Adentro van `config\`, `cache\`, `keys\`, `load\`, `nand\`, `sdmc\`, `shader\`, `screenshots\` y `tas\` ([fs_paths.h](https://git.eden-emu.dev/eden-emu/eden/src/branch/master/src/common/fs/fs_paths.h)).
- **Configuración global:** `user\config\qt-config.ini` (ini de Qt). Fuente: `qt_config.h`, `uisettings.cpp`.
- **Configuración por juego:** `user\config\custom\<TitleID>.ini`, con el TitleID en 16 hex mayúsculas; si el juego no tiene TitleID, `<archivo>.ini` ([frontend_common/config.cpp](https://git.eden-emu.dev/eden-emu/eden/src/branch/master/src/frontend_common/config.cpp), `qt_common/util/game.cpp`). Lleva solo lo que difiere de la global: cada ajuste tiene su par `\use_global`. En la misma carpeta, la sección `[DisabledAddOns]` dice qué mods, actualizaciones y DLC están apagados para ese juego.
- **Mandos:** perfiles en `user\config\input\<nombre>.ini` (`ConfigType::InputProfile`). `--input-profile/-i <nombre>` elige el perfil del jugador 1 al lanzar ([CommandLine.md](https://git.eden-emu.dev/eden-emu/eden/src/branch/master/docs/user/CommandLine.md)). El ini por juego también puede llevar mandos (`Category::Controls`).
- **Mods:** `user\load\<TitleID>\<NombreDelMod>\`, con `exefs\` (parches IPS e `.pchtxt`), `romfs\`, `romfs_ext\` y `cheats\<BuildID>.txt` ([patch_manager.cpp](https://git.eden-emu.dev/eden-emu/eden/src/branch/master/src/core/file_sys/patch_manager.cpp), [Mods.md](https://git.eden-emu.dev/eden-emu/eden/src/branch/master/docs/user/Mods.md)). Se activan y desactivan por juego en Configure Game › Add-Ons. Los mods con framework, como ARCropolis y Skyline en Smash, van en la SD emulada (`user\sdmc\atmosphere\contents\<TitleID>\…` y `sdmc\ultimate\mods\`), no en `load`.
- **Partidas:** `user\nand\user\save\0000000000000000\<UserID>\<TitleID>\`, en el formato de yuzu (sin verificar la ruta exacta en Eden; es la heredada). Eden puede **enlazar** las partidas de un juego con las de Ryujinx: crea un symlink o junction y usa el `imkvdb.arc` de Ryujinx para ubicar la carpeta ([ryujinx_compat.cpp](https://git.eden-emu.dev/eden-emu/eden/src/branch/master/src/common/fs/ryujinx_compat.cpp), `qt_common/util/fs.cpp`). Esto confirma que el contenido de una partida es el mismo en los dos y solo cambia la carpeta. También lee las rutas viejas de Citron, Sudachi, Yuzu y Suyu.
- **UPD y DLC:** se pueden instalar en la NAND (File › Install Files to NAND, [InstallingUpdatesDLC.md](https://git.eden-emu.dev/eden-emu/eden/src/branch/master/docs/user/InstallingUpdatesDLC.md)). Además, la config global tiene una lista **`external_content_dirs`** ([qt_config.cpp](https://git.eden-emu.dev/eden-emu/eden/src/branch/master/src/qt_common/config/qt_config.cpp)): carpetas donde Eden busca actualizaciones y DLC sin copiarlas. La v0.2.1 menciona «external content files» en sus notas. Sirve para la decisión 295: apuntar sin instalar.
- **Línea de comandos:** `eden.exe <juego>`, `-g`, `-f` (pantalla completa), `-c <config>` (otro archivo de configuración), `-u <usuario>`, `-i <perfil de mando>`, `-v` (versión y sale) ([CommandLine.md](https://git.eden-emu.dev/eden-emu/eden/src/branch/master/docs/user/CommandLine.md)).
- **Versión instalada:** `eden.exe --version`, o el nombre del zip. La versión del recurso del exe: sin verificar.
- **Ajustes** ([settings.h](https://git.eden-emu.dev/eden-emu/eden/src/branch/master/src/common/settings.h)): `[Renderer] resolution_setup`, `backend`, `scaling_filter`, `aspect_ratio`, `anti_aliasing`, `fsr_sharpening_slider`, `max_anisotropy`; `[Core] use_speed_limit` y `speed_limit` (límite de velocidad).
- **Licencia:** GPL-3.0-or-later, heredada de yuzu (sin verificar en el Forgejo). Riesgo legal: el más alto de la lista (ver arriba).

### Ryubing (Ryujinx)

- **Descarga:** Forgejo propio, [git.ryujinx.app/projects/Ryubing](https://git.ryujinx.app/projects/Ryubing) (estable) y [git.ryujinx.app/Ryubing/Canary](https://git.ryujinx.app/Ryubing/Canary) (desarrollo). Los dos tienen API Forgejo (`/api/v1/repos/projects/Ryubing/releases` y `/api/v1/repos/Ryubing/Canary/releases`). Además hay una API propia de actualización: `GET https://update.ryujinx.app/latest/query?os=win&arch=x64&rc=stable` (o `rc=canary`), que devuelve `{"tag","download_url"}`. Últimas: estable **1.3.3** (2025-10-11, `ryujinx-1.3.3-win_x64.zip`) y Canary **1.3.352** (2026-11-02, `ryujinx-canary-1.3.352-win_x64.zip` y `.7z`).
- **Portable:** una subcarpeta `portable` junto al exe, o `ryujinx.exe -r <carpeta>` (`--root-data-dir`) ([wiki: Setup & Configuration Guide](https://git.ryujinx.app/projects/Ryubing/wiki/Setup-&-Configuration-Guide), [AppDataManager.cs](https://git.ryujinx.app/projects/Ryubing/src/branch/master/src/Ryujinx.Common/Configuration/AppDataManager.cs)). Sin ella usa `%AppData%\Ryujinx`.
- **Configuración global:** `Config.json` en la carpeta de datos; si hay uno junto al exe, gana ese ([Program.cs](https://git.ryujinx.app/projects/Ryubing/src/branch/master/src/Ryujinx/Program.cs), `ReloadConfig`). Es JSON con claves en snake_case.
- **Configuración por juego:** `games\<titleid>\Config.json`, en 16 hex minúsculas (`GetDirGameUserConfig`). Es una **copia completa** de la configuración, no una diferencia con la global: al crear una, conviene partir de la global y cambiar solo lo necesario.
- **Mandos:** `input_config` dentro de `Config.json`, global o por juego. Los perfiles con nombre van en `profiles\` (ya está en la receta de SS4K). `--profile/-p` elige el perfil de usuario de la consola, no el de mando ([CommandLineState.cs](https://git.ryujinx.app/projects/Ryubing/src/branch/master/src/Ryujinx/Utilities/CommandLineState.cs)).
- **Mods:** `mods\contents\<titleid>\<NombreDelMod>\` con `romfs\`, `exefs\` (o `romfs.bin` y `exefs.nsp`), además de `sdcard\atmosphere\contents\<titleid>\` para los mods armados para consola ([ModLoader.cs](https://git.ryujinx.app/projects/Ryubing/src/branch/master/src/Ryujinx.HLE/HOS/ModLoader.cs), wiki). Cuáles están activos va en `games\<titleid>\mods.json`. El Mod Manager de la GUI agrega un mod desde una carpeta cualquiera.
- **Texturas HD:** la versión oficial no las reemplaza. Lo hace el fork de gdkchan, la base de SS4K (ver abajo).
- **Partidas:** `bis\user\save\<id de partida en hex>\`, con la correspondencia título→partida en `bis\system\save\8000000000000000\0\imkvdb.arc` (deducido de [ryujinx_compat.cpp de Eden](https://git.eden-emu.dev/eden-emu/eden/src/branch/master/src/common/fs/ryujinx_compat.cpp)). El contenido es el mismo que en Eden (ver Eden).
- **UPD y DLC sin copiar:** `games\<titleid>\updates.json` y `dlc.json` guardan **rutas a los archivos externos** ([TitleUpdatesHelper.cs](https://git.ryujinx.app/projects/Ryubing/src/branch/master/src/Ryujinx/Utilities/TitleUpdatesHelper.cs), [DownloadableContentsHelper.cs](https://git.ryujinx.app/projects/Ryubing/src/branch/master/src/Ryujinx/Utilities/DownloadableContentsHelper.cs)). Desde la 1.2.0, «Autoadd DLC/Game Updates» toma carpetas y elige la actualización más alta (wiki). No hace falta instalar nada.
- **Línea de comandos:** `-r`, `-p`, `-f` (pantalla completa), `-g` (backend gráfico), `--docked-mode`/`--handheld-mode`, `--install-firmware`, `-i/--application-id` ([CommandLineState.cs](https://git.ryujinx.app/projects/Ryubing/src/branch/master/src/Ryujinx/Utilities/CommandLineState.cs)).
- **Versión:** la versión del exe (`version.from: exe` ya funciona en la receta de SS4K) o el tag.
- **Ajustes** ([ConfigurationFileFormat.cs](https://git.ryujinx.app/projects/Ryubing/src/branch/master/src/Ryujinx/Systems/Configuration/ConfigurationFileFormat.cs)): `res_scale`, `res_scale_custom`, `max_anisotropy`, `aspect_ratio`, `anti_aliasing`, `scaling_filter`, `scaling_filter_level`, `graphics_backend` y `vsync_mode` (antes `enable_vsync`; ahora conviven `vsync_mode` y `enable_custom_vsync_interval`). Las rutas de juegos van en `game_dirs` (ya en la receta) y las de UPD/DLC en `autoload_dirs`. Los nombres en snake_case se deducen de las propiedades C# y la política de nombres de `JsonHelper` (sin verificar en un `Config.json` real).
- **Licencia:** MIT, la del Ryujinx original (sin verificar en el Forgejo: la API no la declara). Riesgo legal: forks de Ryujinx figuran en el DMCA de 2026.

### Citron (Citron Neo)

- **Estado:** el Citron original ya no responde. `git.citron-emu.org` no contesta y `citron-emu.org` es un dominio estacionado (`/lander`) al 2026-11-04. Sigue como **Citron Neo**, que se presenta como continuación del código de Citron ([README](https://github.com/citron-neo/emulator)).
- **Descarga:** [github.com/citron-neo/emulator/releases](https://github.com/citron-neo/emulator/releases) para las versiones manuales (tag por fecha, p. ej. `2026-04-27`: `Citron-windows-nightly-<hash>-x64-msvc.zip` y `-clangtron.zip`; el 2026-03-20 se llamó «stable»). Las diarias están en [citron-neo/CI](https://github.com/citron-neo/CI/releases), tag fijo `nightly-windows` (2026-11-04). Estables y diarias se distinguen por el repo y el tag, no por `prerelease`. API: la de releases de GitHub.
- **Portable:** carpeta `user` junto al exe ([fs_paths.h](https://github.com/citron-neo/emulator/blob/HEAD/src/common/fs/fs_paths.h)). Desde la release del 2026-03-27 viene así por defecto ([citron-neo.github.io](https://citron-neo.github.io/)).
- **Configuración, mandos y mods:** misma estructura que Eden, porque los dos salen de yuzu: `config\custom\<TitleID>.ini`, `config\input\<perfil>.ini` ([frontend_common/config.cpp](https://github.com/citron-neo/emulator/blob/HEAD/src/frontend_common/config.cpp)) y `load\<TitleID>\<mod>\` (`LOAD_DIR`). Las claves de cada ajuste son las de yuzu (sin verificar una por una).
- **UPD y DLC:** en esta revisión no aparece `external_content_dirs` en `frontend_common`. Queda «Install Files to NAND» (heredado; sin verificar).
- **Licencia:** GPL-3.0-or-later. Riesgo legal: figura en el DMCA de 2026 (el AppImage de `pkgforge-dev`).

### Ryujinx SS4K

- **Qué es:** el [pack de texturas 4K de Skyward Sword HD](https://www.henrikomagnifico.com/zelda-skyward-sword-4k) de Henriko Magnifico (más de 550 texturas). Exige «un fork específico de Ryujinx», que viene **dentro del pack** y ya armado en modo portable: se abre con `Zelda Skyward Sword 4K (Ryujinx).exe` y las keys van en `portable\system`. No anda en otros emuladores ni en un Ryujinx normal.
- **El fork:** se basa en el reemplazo de texturas de gdkchan, que documenta [esta guía](https://gist.github.com/gdkchan/33405fc557d1f29c54906bae837a42de): una carpeta de texturas configurada en Opciones › Gráficos, en DDS o PNG. **No se encontró un repositorio público del fork** con releases. El pack se baja de carpetas de Google Drive enlazadas en esa página.
- **Para Otteretro:** el pack es emulador y mod a la vez. La receta actual (`ryujinx-ss4k` más el complemento `ryujinx-ss4k-pack` en `portable\mods`) sigue siendo válida. La versión sale del pack («Download 1.1.0c»), no de una API: los avisos de versión no se pueden automatizar y hay que revisar la página a mano. Las partidas y la configuración son las de Ryujinx (ver Ryubing). En Linux, el autor dice que hay que bajar el fork a mano.
- **Licencia:** sin verificar (Ryujinx es MIT; el pack es obra del autor).

## Nintendo (resto)

### Dolphin (GameCube y Wii)

- **Descarga:** [dolphin-emu.org/download](https://dolphin-emu.org/download/). La API que usa su actualizador devuelve JSON con `shortrev`, `hash`, `changelog_html` y `artifacts[].url` por sistema: `GET https://dolphin-emu.org/update/latest/dev/` (desarrollo; hoy `2609-182`, 2026-11-04) y `GET https://dolphin-emu.org/update/latest/beta/` (releases; hoy `2609a`, 2026-10-08, `https://dl.dolphin-emu.org/releases/2609a/dolphin-2609a-x64.7z`). No es una API documentada: es la del actualizador. Formato `.7z`. Las releases se nombran `AAMM` más letra; las de desarrollo, `AAMM-N`.
- **Portable:** un archivo `portable.txt` junto al exe ([wiki: Controlling the Global User Directory](https://wiki.dolphin-emu.org/index.php?title=Controlling_the_Global_User_Directory)); la carpeta de datos queda en `User\`. Sin él, `%AppData%\Dolphin Emulator`. En el registro, `HKCU\Software\Dolphin Emulator`, `UserConfigPath` y `LocalUserConfig` lo fuerzan para todas las instalaciones; hay que evitarlos, porque afectarían a todas las versiones.
- **Configuración global:** `User\Config\Dolphin.ini` (núcleo), `GFX.ini` (gráficos), `GCPadNew.ini` y `WiimoteNew.ini` (mandos) ([CommonPaths.h](https://github.com/dolphin-emu/dolphin/blob/master/Source/Core/Common/CommonPaths.h)).
- **Configuración por juego:** `User\GameSettings\<GameID>.ini`, con el ID de 6 caracteres; también lee los de 3 caracteres (región) y 1. Una sección por sistema (`[Core]`, `[Video_Settings]`, `[Video_Enhancements]`…) solo con lo que difiere. Los mandos van en `[Controls]` con `PadType0..3`, `WiimoteSource0..3` y los perfiles **`PadProfile1..4`** y **`WiimoteProfile1..4`** ([GameConfigLoader.cpp](https://github.com/dolphin-emu/dolphin/blob/master/Source/Core/Core/ConfigLoaders/GameConfigLoader.cpp), [InputConfig.cpp](https://github.com/dolphin-emu/dolphin/blob/master/Source/Core/InputCommon/InputConfig.cpp)). Los perfiles viven en `User\Config\Profiles\GCPad\<nombre>.ini` y `…\Wiimote\<nombre>.ini`. Es el mejor caso para perfiles por juego.
- **Mods:**
  - Texturas: `User\Load\Textures\<GameID>\`. Se activan con `[Settings] HiresTextures = True` en `GFX.ini` (o en el ini del juego); `CacheHiresTextures` las precarga.
  - Graphics mods: `User\Load\GraphicMods\`, con `[Settings] EnableMods`.
  - Riivolution: `User\Load\Riivolution\`.
  - Resource packs: `User\ResourcePacks\`.
  - Parches de código y Gecko: en el `GameSettings\<GameID>.ini` (`[OnFrame]`, `[Gecko]` y sus `_Enabled`), con `[Core] EnableCheats` para los códigos.
  - Fuente: [CommonPaths.h](https://github.com/dolphin-emu/dolphin/blob/master/Source/Core/Common/CommonPaths.h) y [GraphicsSettings.cpp](https://github.com/dolphin-emu/dolphin/blob/master/Source/Core/Core/Config/GraphicsSettings.cpp). Los nombres de sección de parches y Gecko vienen del formato de Dolphin conocido, sin verificar en esta revisión.
- **Partidas:**
  - GameCube: por defecto, **carpeta GCI** (`[Core] SlotA = MemoryCardFolder`), en `User\GC\<región>\Card A\*.gci`. También tarjeta `.raw` (`MemcardAPath`) ([MainSettings.cpp](https://github.com/dolphin-emu/dolphin/blob/master/Source/Core/Core/Config/MainSettings.cpp)).
  - Wii: la NAND en `User\Wii\title\…`.
  - Estados: `User\StateSaves\`.
  - Los `.gci` son el formato de intercambio de GameCube: sirven para Dusklight (ver abajo) y para la consola real con herramientas como GCMM.
- **Versión:** la receta usa la fecha del exe. Mejor el `shortrev` de la API, comparado con el nombre del `.7z`; la versión del recurso del exe: sin verificar.
- **Ajustes:**
  - En `GFX.ini`: `[Settings] InternalResolution`, `[Settings] AspectRatio`, `[Hardware] VSync`, `[Settings] MSAA`, `[Settings] ShaderCompilationMode`, `[Enhancements] MaxAnisotropy`, `[Enhancements] OutputResampling`.
  - En `Dolphin.ini`: `[Core] GFXBackend` y `[Core] EmulationSpeed`.
  - Fuente: [GraphicsSettings.cpp](https://github.com/dolphin-emu/dolphin/blob/master/Source/Core/Core/Config/GraphicsSettings.cpp) y [MainSettings.cpp](https://github.com/dolphin-emu/dolphin/blob/master/Source/Core/Core/Config/MainSettings.cpp).
- **Licencia:** GPL-2.0-or-later.

### Dusklight (port de Twilight Princess)

- **Qué es:** una reimplementación del juego hecha por ingeniería inversa, sobre la decompilación de TP; necesita el disco del jugador ([README](https://github.com/TwilitRealm/dusklight)). Web oficial: [twilitrealm.dev](https://twilitrealm.dev/). Acepta todos los discos comerciales salvo el coreano de Wii.
- **Descarga:** [github.com/TwilitRealm/dusklight/releases](https://github.com/TwilitRealm/dusklight/releases). Última: **v2.0.3** (2026-09-30), `Dusklight-v2.0.3-win32-x86_64.zip`. API de releases de GitHub. Solo estables.
- **Portable (cambio respecto a la receta actual):** el gestor de datos tiene `portableRelativePath = "data"`, o sea una carpeta `data` junto al exe, y la opción `--user-dir` fija la carpeta de la sesión ([src/dusk/data.cpp](https://github.com/TwilitRealm/dusklight/blob/main/src/dusk/data.cpp)). Sin ella usa `%APPDATA%\TwilitRealm\Dusklight` y migra la del nombre viejo «Dusk». **La receta `dusklight.json` usa una junction a `%APPDATA%`, y eso ya no hace falta**: conviene pasar a `portable: folder data` y probar.
- **Configuración:** claves con punto, como `backend.isoPath` (la que reescribe la receta), `backend.graphicsBackend`, `game.internalResolutionScale`, `video.maxFrameRate`, `video.enableVsync`, `video.lockAspectRatio`, `video.enableFullscreen` y muchas `game.*` de mejoras ([src/dusk/settings.cpp](https://github.com/TwilitRealm/dusklight/blob/main/src/dusk/settings.cpp)). El nombre del archivo, `config.json` según la receta, no se verificó en el código.
- **Mods:** paquetes **`.dusk`**, que son zips con `mod.json` (`id`, `name`, `version`…), `overlay/` (archivos que reemplazan los del disco), `textures/` (nombres compatibles con Dolphin) y código nativo o Luau. Van en `<datos>\mods\`, se activan y recargan desde el gestor de mods del juego, y `--mods <carpeta>` reemplaza esa carpeta ([docs/modding.md](https://github.com/TwilitRealm/dusklight/blob/main/docs/modding.md), [mod-template](https://github.com/TwilitRealm/mod-template)). Además existe `<datos>\texture_replacements\`, el complemento de texturas de la receta actual. Hay mods oficiales en releases: [dusklight-randomizer](https://github.com/TwilitRealm/dusklight-randomizer) y [dusklight-cosmetics](https://github.com/TwilitRealm/dusklight-cosmetics). Existe `backend.checkForModUpdates`. Dónde se guarda qué mod está activo: sin verificar.
- **Partidas:** por defecto, carpeta GCI por región (`backend.cardFileType = CARD_GCIFOLDER`), en `USA\`, `EUR\` y `JAP\` dentro de los datos (`data.cpp`). Al ser GCI, **probablemente** sean intercambiables con la carpeta GCI de Dolphin. Sin verificar: hay que probarlo con un `.gci` real.
- **Licencia:** CC0-1.0 (el código). Los datos del juego los pone el jugador.

### Rosalie's Mupen GUI (RMG, N64)

- **Descarga:** [github.com/Rosalie241/RMG/releases](https://github.com/Rosalie241/RMG/releases). Última: **v0.9.1** (2026-11-04), `RMG-Portable-Windows64-v0.9.1.zip` (portable) y `RMG-Setup-Windows64-v0.9.1.exe`. Solo estables.
- **Portable:** basta con `portable.txt` o con `Config\mupen64plus.cfg` junto al exe; el zip portable ya lo trae. Carpetas: `Config\`, `Save\Game\`, `Save\State\`, `Data\`, `Cache\` y `Screenshots\` ([Directories.cpp](https://github.com/Rosalie241/RMG/blob/master/Source/RMG-Core/Directories.cpp)). Sin portable, `%AppData%`.
- **Configuración:** `Config\mupen64plus.cfg`, ini de mupen64plus. Los ajustes por juego son una **sección por juego** dentro del mismo archivo: `Game_OverrideSettings`, `Game_GFX_Plugin`, `Game_INPUT_Plugin`, `Game_CPU_Emulator`, `Game_SaveType`, etc. ([Settings.cpp](https://github.com/Rosalie241/RMG/blob/master/Source/RMG-Core/Settings.cpp)). El nombre de la sección (MD5 o ID de la ROM): sin verificar.
- **Mandos:** perfiles en la sección `Rosalie's Mupen GUI - Input Plugin` (`Profiles`), con `UseGameProfile` para un perfil por juego (`Settings.cpp`).
- **Mods:** texturas HD con el plugin GLideN64 (`txHiresEnable`, la carpeta `hires_texture`). La ruta en RMG no está verificada. Si aplica parches IPS o BPS al cargar: sin verificar.
- **Partidas:** `Save\Game\` en el formato de mupen64plus (`.eep`, `.sra`, `.fla`, `.mpk`), que comparte cualquier frontend de mupen64plus. Para Project64 hay que invertir el orden de bytes de algunos tipos (sin verificar).
- **Versión:** el tag o el nombre del zip; la del exe: sin verificar.
- **Licencia:** GPL-3.0.

### Azahar (3DS)

- **Descarga:** [github.com/azahar-emu/azahar/releases](https://github.com/azahar-emu/azahar/releases). Estable: **2126.2** (2026-10-08), `azahar-windows-msvc-2126.2.zip` (también `-installer.exe` y variantes `msys2` y `mxe`). Las de desarrollo son las `2127.0-alphaN`, con `prerelease: true`.
- **Portable:** carpeta `user` junto al exe ([file_util.cpp](https://github.com/azahar-emu/azahar/blob/master/src/common/file_util.cpp), `USERDATA_DIR`). Sin ella, `%AppData%\Azahar`, y migra la de Citra o Lime3DS.
- **Configuración:** `user\config\qt-config.ini`. **Por juego:** `user\config\custom\<archivo>` ([citra_qt/configuration/config.cpp](https://github.com/azahar-emu/azahar/blob/master/src/citra_qt/configuration/config.cpp), `PerGameConfig`). El archivo se llama con el TitleID en hex, como en Citra (sin verificar el formato exacto).
- **Mods:** `user\load\mods\<TitleID>\`, con `exheader.bin`, `romfs\`, `exefs\` y parches `code.ips`/`code.bps` ([ncch_container.cpp](https://github.com/azahar-emu/azahar/blob/master/src/core/file_sys/ncch_container.cpp); los parches, sin verificar). **Texturas:** `user\load\textures\<TitleID>\`, con `custom_textures` y `preload_textures` activados ([custom_tex_manager.cpp](https://github.com/azahar-emu/azahar/blob/master/src/video_core/custom_textures/custom_tex_manager.cpp)).
- **Partidas:** `user\sdmc\Nintendo 3DS\…\title\00040000\<id>\data\`, en el formato de Citra: compatibles con Citra y Lime3DS (sin verificar la ruta exacta).
- **Instalar .cia:** `azahar.exe --install <archivo.cia>` (`-i`) ([citra_qt.cpp](https://github.com/azahar-emu/azahar/blob/master/src/citra_qt/citra_qt.cpp)). Copia el contenido a la NAND o la SD emulada; no apunta a archivos externos.
- **Ajustes** ([settings.h](https://github.com/azahar-emu/azahar/blob/master/src/common/settings.h)): `resolution_factor`, `frame_limit`, `graphics_api`, `texture_filter`, `aspect_ratio`, `use_vsync`, `custom_textures`.
- **Licencia:** GPL-2.0-or-later.

### melonDS (DS)

- **Descarga:** [github.com/melonDS-emu/melonDS/releases](https://github.com/melonDS-emu/melonDS/releases). Última: **1.1** (2025-11-18), `melonDS-1.1-windows-x86_64.zip`. Solo estables.
- **Configuración:** `melonDS.toml`; antes `melonDS.ini`, que migra ([Config.cpp](https://github.com/melonDS-emu/melonDS/blob/master/src/frontend/qt_sdl/Config.cpp)). Si queda junto al exe (portable): sin verificar. Claves: `3D.Renderer`, `3D.GL.ScaleFactor`, `LimitFPS`, `TargetFPS`, `Instance*.Window*.ScreenAspectTop/Bot`. La configuración se agrupa por instancia (`Instance0…`), no por juego. **No se encontró configuración por juego.**
- **Partidas:** `.sav` crudo junto a la ROM o en `SaveFilePath`. Con DeSmuME (`.dsv`) se convierte quitando su pie de metadatos (sin verificar).
- **Licencia:** GPL-3.0.

### Cemu (Wii U)

- **Descarga:** [github.com/cemu-project/Cemu/releases](https://github.com/cemu-project/Cemu/releases). Última estable: **v2.6** (2025-02-06), `cemu-2.6-windows-x64.zip`. Las experimentales (`v2.0-NN`, `prerelease`) dejaron de salir en 2024.
- **Portable:** carpeta `portable` junto al exe. Por compatibilidad, también si hay un `settings.xml` junto al exe ([CemuApp.cpp](https://github.com/cemu-project/Cemu/blob/main/src/gui/wxgui/CemuApp.cpp)).
- **Configuración global:** `settings.xml` ([CemuConfig.cpp](https://github.com/cemu-project/Cemu/blob/main/src/config/CemuConfig.cpp)). **Por juego:** `gameProfiles\<titleid>.ini` (16 hex minúsculas). Hay de fábrica en `gameProfiles\default\`, con secciones `[Graphics]` y `[Controller]`, y `controller1..8 = <perfil>` para los mandos ([GameProfile.cpp](https://github.com/cemu-project/Cemu/blob/main/src/Cafe/GameProfile/GameProfile.cpp)). Los perfiles de mando están en `controllerProfiles\` (sin verificar).
- **Mods:** **graphic packs** en `graphicPacks\`, cada uno con su `rules.txt` y los TitleID a los que aplica. La **resolución también es un graphic pack**. Cuáles están activos, y con qué preset, se guarda en `settings.xml` (`GraphicPack`), es decir **en la config global**, aunque cada pack aplique solo a sus juegos (`CemuConfig.cpp`, [GraphicPack2.cpp](https://github.com/cemu-project/Cemu/blob/main/src/Cafe/GraphicPack/GraphicPack2.cpp)).
- **Juegos, UPD y DLC:** los juegos se cargan sueltos (`.wua`, `.wud`/`.wux` o carpeta con `.rpx`). Las actualizaciones y los DLC tienen que estar en `mlc01\usr\title\…`: la GUI los instala ahí. **No hay comando para instalar**, pero `--mlc/-m <carpeta>` usa otro `mlc01` ([LaunchSettings.cpp](https://github.com/cemu-project/Cemu/blob/main/src/config/LaunchSettings.cpp)). Con eso se puede tener un `mlc01` fuera de la instalación, compartido entre versiones. Otras opciones: `-g <juego>`, `-t <titleid>`, `-f`.
- **Partidas:** `mlc01\usr\save\<titleid alto>\<bajo>\user\…`.
- **Licencia:** MPL-2.0.

## Sistemas retro

### Mesen (MesenCE)

- **Estado:** `SourMesen/Mesen2` está **archivado**. Su README dice que es una copia de la 2.1.1 de mediados de 2025 y remite a **[nesdev-org/MesenCE](https://github.com/nesdev-org/MesenCE)**. Última: **2.2.1** (2026-06-05), `Mesen_2.2.1_Windows.zip`. La receta debe usar MesenCE.
- **Portable y configuración:** un `settings.json` junto al exe hace que esa carpeta sea la de datos; si no, `Documentos\Mesen2`. Adentro, `Saves\`, `SaveStates\` y `HdPacks\` ([ConfigManager.cs](https://github.com/SourMesen/Mesen2/blob/master/UI/Config/ConfigManager.cs), revisado en el repo archivado). **Sin configuración por juego** (no encontrada).
- **Mods:** HD Packs de NES en `HdPacks\<juego>\`. Si aplica parches IPS o BPS: sin verificar.
- **Licencia:** GPL-3.0.

### bsnes y Snes9x (SNES)

- **bsnes:** [github.com/bsnes-emu/bsnes](https://github.com/bsnes-emu/bsnes). La última estable es **v115** (2020-03-20); hay un tag `nightly` (2026-05-23, `bsnes-windows.zip`). La configuración va en `settings.bml` ([settings.cpp](https://github.com/bsnes-emu/bsnes/blob/master/bsnes/target-bsnes/settings/settings.cpp)); si es portable: sin verificar. Licencia GPL-3.0 (`LICENSE.txt`).
- **Snes9x:** [github.com/snes9xgit/snes9x/releases](https://github.com/snes9xgit/snes9x/releases). Última **1.63** (2024-07-09), `snes9x-1.63-win32-x64.zip`. Configuración en `snes9x.conf`. Rutas por defecto relativas al exe: `Dir:SRAM = .\Saves`, `Dir:Savestates = .\Saves` y **`Dir:Patches = .\Patches`**, donde busca `.ips`, `.bps` y `.ups` con el nombre de la ROM (soft-patching, `Patch` e `IgnorePatchChecksum`) ([wconfig.cpp](https://github.com/snes9xgit/snes9x/blob/master/win32/wconfig.cpp)). Sin configuración por juego. **Licencia no comercial** (la propia de Snes9x, `LICENSE`): se puede usar, pero no redistribuir con fines comerciales.

### BlastEm (Mega Drive)

- **Descarga:** [retrodev.com/blastem](https://www.retrodev.com/blastem/), con versiones en `downloads.html` y nightlies en [retrodev.com/blastem/nightlies](https://www.retrodev.com/blastem/nightlies/) (`blastem-win32-0.5.0-pre-<hash>.zip`). No hay API: es un repo Mercurial (`/repos/blastem`), así que la última versión sale de leer el índice HTML. La versión estable vigente: sin verificar (el README de la punta dice «BlastEm 1.0.0»).
- **Configuración:** `%localappdata%\blastem\blastem.cfg` si existe; si no, `default.cfg` junto al exe. Las partidas en `save_path`. Antes de la 0.4.1 usaba rutas tipo Unix ([README](https://www.retrodev.com/repos/blastem/raw-file/tip/README)). Para dejarlo portable, poner `default.cfg` junto al exe y no crear la de `%localappdata%` (sin verificar). Sin configuración por juego.
- **Licencia:** GPL-3.0.

### mGBA (GB y GBA)

- **Descarga:** [github.com/mgba-emu/mgba/releases](https://github.com/mgba-emu/mgba/releases). Última **0.10.5** (2025-03-09), `mGBA-0.10.5-win64.7z` (y el instalador).
- **Portable:** `portable.ini` junto al exe ([config.c](https://github.com/mgba-emu/mgba/blob/master/src/core/config.c)). Configuración en `config.ini` y `qt.ini`. Tiene una tabla de **overrides** por juego (`overridesTable`); el detalle del formato: sin verificar.
- **Parches:** aplica solo `.bps`, `.ups` o `.ips` con el nombre de la ROM, o en `patchPath` ([core.c](https://github.com/mgba-emu/mgba/blob/master/src/core/core.c)). Es la forma más simple de usar una ROM modificada sin tocar la base (decisión 290).
- **Ajustes:** `fpsTarget`, `lockAspectRatio`, escala de video (sin verificar la clave).
- **Licencia:** MPL-2.0.

## PlayStation

### DuckStation (PS1)

- **Descarga:** [github.com/stenzek/duckstation/releases](https://github.com/stenzek/duckstation/releases). Tags fijos **`latest`** (estable, se reemplaza; 2026-10-25) y **`preview`** (desarrollo; 2026-11-04), más tags numerados (`v0.1-12083`). Windows: `duckstation-windows-x64-release.zip` (o `-installer.exe`). La versión real va en el tag numerado o en el nombre de la release, no en `latest`.
- **Portable:** `portable.txt` junto al exe ([qthost.cpp](https://github.com/stenzek/duckstation/blob/master/src/duckstation-qt/qthost.cpp)). La carpeta por defecto en Windows: sin verificar.
- **Configuración:** `settings.ini`. **Por juego:** `gamesettings\<serial>.ini`; en un juego de varios discos usa el del primer disco ([system.cpp](https://github.com/stenzek/duckstation/blob/master/src/core/system.cpp)). **Mandos:** `inputprofiles\<nombre>.ini`, elegible por juego.
- **Mods:** texturas en `textures\<serial>\` (con `config.yaml` y alias), activadas con `[TextureReplacements] EnableTextureReplacements` y `EnableVRAMWriteReplacements` ([gpu_hw_texture_cache.cpp](https://github.com/stenzek/duckstation/blob/master/src/core/gpu_hw_texture_cache.cpp)). También `cheats\` y `patches\` ([settings.cpp](https://github.com/stenzek/duckstation/blob/master/src/core/settings.cpp)).
- **Partidas:** `memcards\*.mcd`, imagen cruda de 128 KB, compatible con ePSXe y PCSX-R y convertible con herramientas como MemcardRex (la compatibilidad, sin verificar). Hay tarjeta por juego o compartida (`[MemoryCards]`).
- **Ajustes** ([settings.cpp](https://github.com/stenzek/duckstation/blob/master/src/core/settings.cpp)): `[GPU] Renderer`, `[GPU] ResolutionScale`, `[GPU] TextureFilter`, `[Display] AspectRatio`, `[Display] VSync`, `[Main] EmulationSpeed`.
- **Licencia:** **CC BY-NC-ND 4.0** (`LICENSE`): no comercial y sin obras derivadas. Se puede bajar de la fuente oficial, pero no redistribuir modificado. La librería privada guarda el original, así que no hay problema.

### PCSX2 (PS2)

- **Descarga:** [github.com/PCSX2/pcsx2/releases](https://github.com/PCSX2/pcsx2/releases). Las diarias vienen marcadas `prerelease` (`v2.9.155`, `pcsx2-v2.9.155-windows-x64-Qt.7z`); las estables, sin marca (`/releases/latest`; no se consultó cuál es hoy).
- **Portable:** `portable.txt` o `portable.ini` junto al exe, o el argumento `-portable`. Si `portable.txt` tiene una ruta, usa esa ([Pcsx2Config.cpp](https://github.com/PCSX2/pcsx2/blob/master/pcsx2/Pcsx2Config.cpp)). Sin portable, `Documentos\PCSX2`.
- **Configuración:** `inis\PCSX2.ini` (sin verificar el nombre). **Por juego:** `gamesettings\<serial>_<CRC>.ini`, o `<CRC>.ini` sin serial ([VMManager.cpp](https://github.com/PCSX2/pcsx2/blob/master/pcsx2/VMManager.cpp)). **Mandos:** `inputprofiles\`.
- **Mods:** parches `patches\<serial>_<CRC>.pnach` (y `cheats\`) ([Patch.cpp](https://github.com/PCSX2/pcsx2/blob/master/pcsx2/Patch.cpp)). Texturas en `textures\<serial>\replacements\` con `LoadTextureReplacements` (la subcarpeta y la clave, sin verificar en esta revisión).
- **Partidas:** `memcards\Mcd001.ps2` (8 MB) o tarjeta en carpeta. Se convierten con mymc o la herramienta de PCSX2.
- **Ajustes** (sección del GS; [Pcsx2Config.cpp](https://github.com/PCSX2/pcsx2/blob/master/pcsx2/Pcsx2Config.cpp)): `upscale_multiplier`, `Renderer`, `AspectRatio`, `filter`, `MaxAnisotropy`, `linear_present_mode`. `EnableWideScreenPatches` se migró y ya no existe como tal.
- **Licencia:** GPL-3.0.

### PPSSPP (PSP)

- **Descarga:** [github.com/hrydgard/ppsspp/releases](https://github.com/hrydgard/ppsspp/releases) (y ppsspp.org). Última **v1.20.4** (2026-05-16), `PPSSPP-v1.20.4-Windows-x64.zip`. Las de desarrollo solo están en buildbot.ppsspp.org (sin verificar).
- **Portable por defecto:** sin `installed.txt` junto al exe, la memory stick es `memstick\` en la carpeta del exe; con él, `Documentos\PPSSPP` o la ruta que diga ([Windows/main.cpp](https://github.com/hrydgard/ppsspp/blob/master/Windows/main.cpp)).
- **Configuración:** `memstick\PSP\SYSTEM\ppsspp.ini` y `controls.ini` ([Config.cpp](https://github.com/hrydgard/ppsspp/blob/master/Core/Config.cpp)). **Por juego:** `PSP\SYSTEM\<GAMEID>_ppsspp.ini`, solo con los ajustes marcados `PER_GAME` (el nombre del archivo, sin verificar en esta revisión). Mandos por juego: sin verificar.
- **Mods:** texturas en `PSP\TEXTURES\<GAMEID>\` con `textures.ini` ([TextureReplacer.cpp](https://github.com/hrydgard/ppsspp/blob/master/GPU/Common/TextureReplacer.cpp)) y `[Graphics] ReplaceTextures`. Hay plugins en `PSP\PLUGINS\` (sin verificar).
- **Partidas:** `PSP\SAVEDATA\<GAMEID…>\`, en el formato de la PSP.
- **Ajustes** (`[Graphics]`): `InternalResolution`, `GraphicsBackend`, `TextureFiltering`, `TexScalingLevel`, `DisplayStretch`. El límite de fps cambió: `FrameRate` y `FrameRate2` quedaron como legado (`DONT_SAVE`) y el actual: sin verificar.
- **Licencia:** GPL-2.0-or-later.

### RPCS3 (PS3)

- **Descarga:** [github.com/RPCS3/rpcs3-binaries-win/releases](https://github.com/RPCS3/rpcs3-binaries-win/releases). Una por commit: tag `build-<hash>`, asset `rpcs3-v0.0.43-20564-eac87909_win64_msvc.7z` con su `.sha256` (2026-11-04). **Solo desarrollo.** El listado no sale ordenado por fecha: usar `/releases/latest`.
- **Portable:** si hay carpeta `portable\` junto al exe, la usa; si no, `RPCS3_CONFIG_DIR`; si no, la carpeta del exe ([Utilities/File.cpp](https://github.com/RPCS3/rpcs3/blob/master/Utilities/File.cpp), `get_config_dir`). En Windows ya es portable sin hacer nada.
- **Configuración:** `config\config.yml`. **Por juego:** `config\custom_configs\config_<TITLEID>.yml` ([system_utils.cpp](https://github.com/RPCS3/rpcs3/blob/master/rpcs3/Emu/system_utils.cpp)). Mandos por juego: `config\input_configs\<TITLEID>\` (sin verificar).
- **Mods:** parches de la comunidad en `patches\patch.yml` (más `imported_patch.yml`). Los mods de archivos se aplican reemplazando archivos del juego instalado (`dev_hdd0\game\<TITLEID>\USRDIR\`) o del disco en carpeta: no hay superposición (sin verificar).
- **Partidas:** `dev_hdd0\home\00000001\savedata\<carpeta>\`, sin cifrar.
- **Instalar:** `rpcs3.exe --installpkg <archivo.pkg>` y `--installfw <PS3UPDAT.PUP>`. También `--no-gui`, `--config <yml>` e `--input-config` ([rpcs3.cpp](https://github.com/RPCS3/rpcs3/blob/master/rpcs3/rpcs3.cpp)). El `.pkg` se copia a `dev_hdd0`: no apunta a archivos externos. Los `.rap` de licencia van a `dev_hdd0\home\00000001\exdata\` (sin verificar).
- **Ajustes** (`Video:` en el yml; [system_config.h](https://github.com/RPCS3/rpcs3/blob/master/rpcs3/Emu/system_config.h)): `Renderer`, `Resolution Scale` (porcentaje), `Aspect ratio`, `Frame limit`, `Anisotropic Filter Override`, `Write Color Buffers`.
- **Licencia:** GPL-2.0.

### shadPS4 (PS4)

- **Descarga:** [github.com/shadps4-emu/shadPS4/releases](https://github.com/shadps4-emu/shadPS4/releases). Estable **v.0.21.0** (2026-10-30), `shadps4-win64-sdl-0.21.0.zip`, más diarias marcadas `prerelease` (`Pre-release-shadPS4-<fecha>-<hash>`).
- **Portable:** carpeta `user` en el directorio actual. En Windows la crea ahí siempre ([path_util.cpp](https://github.com/shadps4-emu/shadPS4/blob/main/src/common/path_util.cpp)). Ojo: es relativa al **directorio de trabajo**, no al exe; hay que lanzarlo con el directorio de trabajo en la instalación.
- **Configuración:** `user\config.json`, que migra del viejo `config.toml`. **Por juego:** `user\custom_configs\<serial>.json` ([emulator_settings.cpp](https://github.com/shadps4-emu/shadPS4/blob/main/src/core/emulator_settings.cpp)). Las claves cambiaron de nombre con esa migración. Hoy, entre otras: `full_screen`, `window_width`, `fsr_enabled`, `vblank_frequency` y `null_gpu` ([emulator_settings.h](https://github.com/shadps4-emu/shadPS4/blob/main/src/core/emulator_settings.h)).
- **Mods:** una carpeta hermana **`<carpeta del juego>-mods`**, que se superpone a los archivos del juego ([emulator.cpp](https://github.com/shadps4-emu/shadPS4/blob/main/src/emulator.cpp), `OverlayPath(game_folder, "-mods")`), y parches en `user\patches\` (`-p/--patch <archivo>`; `-i` ignora el parche del juego).
- **Juegos y DLC:** los juegos son carpetas ya extraídas (CUSA…). shadPS4 no extrae `.pkg` por línea de comandos (sin verificar; la decisión #243 dice que Otteretro no los extrae). `--add-game-folder` agrega carpetas de juegos y **`--set-addon-folder`** fija la carpeta de DLC, que puede estar afuera ([main.cpp](https://github.com/shadps4-emu/shadPS4/blob/main/src/main.cpp)). `--override-root` cambia la raíz del juego.
- **Partidas:** `user\savedata\<usuario>\<serial>\` (sin verificar la ruta exacta).
- **Licencia:** GPL-2.0.

### Vita3K (Vita)

- **Descarga:** [github.com/Vita3K/Vita3K/releases](https://github.com/Vita3K/Vita3K/releases). Una sola release móvil, `continuous` (`windows-latest.zip`, 2026-11-04). **Solo desarrollo**, sin número en el nombre del archivo: la versión sale del commit de la release.
- **Portable:** carpeta `portable` junto al exe ([app_init.cpp](https://github.com/Vita3K/Vita3K/blob/master/vita3k/app/src/app_init.cpp)). En portable, el sistema de archivos de la Vita (`ux0`…) también va adentro ([config.cpp](https://github.com/Vita3K/Vita3K/blob/master/vita3k/config/src/config.cpp)).
- **Configuración:** `config.yml`, que puede ir en otra ubicación con `--config-location/-c`. **Por juego:** existe (`has_custom_config`, [settings.h](https://github.com/Vita3K/Vita3K/blob/master/vita3k/config/include/config/settings.h)); la ruta, `config\config_<TITLEID>.xml`, sin verificar.
- **Instalar:** `--pkg <archivo.pkg> --zrif <licencia>`, `--firmware <.pup>`, y como argumento suelto un `.vpk`, `.zip` o carpeta para instalar y abrir ([config.cpp](https://github.com/Vita3K/Vita3K/blob/master/vita3k/config/src/config.cpp)). Copia a `ux0\app\`.
- **Ajustes** ([config.h](https://github.com/Vita3K/Vita3K/blob/master/vita3k/config/include/config/config.h)): `backend-renderer`, `resolution-multiplier`, `screen-filter`, `v-sync`, `anisotropic-filtering`.
- **Mods:** sin verificar (los mods de Vita suelen ser reemplazos en `ux0\rePatch\<TITLEID>\` en la consola real).
- **Licencia:** GPL-2.0.

### PS5: KytyPS5 (y SharpEmu)

- **Estado real:** hay dos proyectos activos, los dos muy tempranos.
  - **[KytyPS5](https://github.com/KytyPS5/KytyPS5)** se describe como «PlayStation 5 translation layer» para Windows y Linux. Es un fork muy modificado de Kyty (InoriRus), en «active development»; advierte que el comportamiento cambia mucho entre builds y publica una [lista de compatibilidad](https://kytyps5.github.io/) y novedades semanales. Las fuentes secundarias lo ubican como el más avanzado, con juegos 3D a unos 30 fps en algunos casos ([Tom's Hardware](https://www.tomshardware.com/video-games/playstation/ps5-emulation-hits-new-milestone-in-record-time-multiple-3d-games-now-playable-in-kyty-performance-and-support-steadily-improving-with-30-fps-gameplay-possible-today)). Ninguno de los dos, según [allthings.how](https://allthings.how/ps5-emulation-in-what-sharpemu-and-kytyps5-can-run-now/), corre un juego de principio a fin.
  - **[SharpEmu](https://github.com/sharpemu/sharpemu)** está escrito en C# y es «experimental». Carga el `eboot.bin` de juegos reales y tiene un modo de compatibilidad con PS4. Se lanza con `SharpEmu.exe <ruta\eboot.bin>`, configura variables de entorno desde su GUI y tiene [parches](https://github.com/sharpemu/sharpemu/blob/main/docs/patches.md).
- **Recomendación para la decisión 291 («el más avanzado»):** KytyPS5, con SharpEmu como alternativo.
- **Descarga KytyPS5:** releases diarias en GitHub, `KytyPS5-2026-11-05-07e87b9-Windows-x64.zip` (tag = nombre). Solo desarrollo.
- **Descarga SharpEmu:** `sharpemu-0.0.7-pulse-win-x64.zip` (2026-11-01).
- **Portable, configuración, mods y partidas:** sin verificar en los dos. El README de KytyPS5 no los documenta: se ven al escribir su receta con la build en la mano.
- **Licencia:** GPL-2.0 (KytyPS5) y GPL-2.0-or-later (SharpEmu).

## Xbox y Dreamcast

### xemu (Xbox)

- **Descarga:** [github.com/xemu-project/xemu/releases](https://github.com/xemu-project/xemu/releases). Estable **v0.8.139** (2026-10-21) y `pre-release` móvil marcada `prerelease`. Los `.zip` de Windows vienen con y sin `-dbg` (`xemu-0.8.139-windows-x86_64.zip`; el nombre sin `-dbg`, sin verificar).
- **Portable:** si existe `xemu.toml` junto al exe, todo queda ahí; si no, la carpeta de preferencias de SDL (`%AppData%\xemu\xemu`). `-config_path` elige otro archivo ([xemu-settings.cc](https://github.com/xemu-project/xemu/blob/master/ui/xemu-settings.cc), [xemu.c](https://github.com/xemu-project/xemu/blob/master/ui/xemu.c)).
- **Configuración:** solo global: `xemu.toml` ([config_spec.yml](https://github.com/xemu-project/xemu/blob/master/config_spec.yml)). Claves: `display.renderer`, `display.quality.surface_scale` (resolución), `display.filtering`, `display.window.vsync`, `display.ui.fit` y `aspect_ratio` (la sección exacta de las dos últimas, sin verificar), y `sys.files.bootrom_path`, `flashrom_path`, `eeprom_path`, `hdd_path`, `dvd_path`. **Sin configuración por juego.**
- **Partidas:** dentro de la imagen del disco duro (`hdd_path`, qcow2), no por juego. Para separarlas haría falta otra imagen de disco.
- **Licencia:** GPL-2.0.

### Xenia Canary (Xbox 360)

- **Descarga:** [github.com/xenia-canary/xenia-canary-releases/releases](https://github.com/xenia-canary/xenia-canary-releases/releases). Una por commit (tag = hash corto), `xenia_canary_windows.zip`; el último se llama `xenia_canary_windows_.zip` (2026-03-24, tag `02d2cb5`). **Solo desarrollo.** No salen builds desde marzo de 2026, aunque el código sigue en la rama `canary_experimental`: si hay otro canal, sin verificar.
- **Portable:** en Windows viene así por defecto (cvar `portable = true`) y también se activa con `portable.txt` junto al exe ([xenia_main.cc](https://github.com/xenia-canary/xenia-canary/blob/canary_experimental/src/xenia/app/xenia_main.cc)). Existen `storage_root`, `content_root` y `cache_root`.
- **Configuración:** `xenia-canary.config.toml`. **Por juego:** `config\<TitleID>.config.toml` ([config.cc](https://github.com/xenia-canary/xenia-canary/blob/canary_experimental/src/xenia/config.cc), `LoadGameConfig`).
- **Mods:** parches en `patches\` (archivos `.patch.toml` por TitleID), con `apply_patches = true` ([patch_db.cc](https://github.com/xenia-canary/xenia-canary/blob/canary_experimental/src/xenia/patcher/patch_db.cc)).
- **Contenido (DLC y TU):** `content\<xuid>\<TitleID>\<tipo>\…` ([content_manager.cc](https://github.com/xenia-canary/xenia-canary/blob/canary_experimental/src/xenia/kernel/xam/content_manager.cc)), instalado con el menú «Install Content» ([emulator_window.cc](https://github.com/xenia-canary/xenia-canary/blob/canary_experimental/src/xenia/app/emulator_window.cc)). Por línea de comandos: sin verificar.
- **Ajustes** (`[GPU]`; [gpu_flags.cc](https://github.com/xenia-canary/xenia-canary/blob/canary_experimental/src/xenia/gpu/gpu_flags.cc)): `vsync`, `framerate_limit`, `anisotropic_override`; `gpu` (`any`, `d3d12`, `vulkan`). La resolución, `draw_resolution_scale_x` y `_y`, no se encontró en `render_target_cache.cc`: sin verificar dónde está hoy.
- **Licencia:** BSD-3-Clause, según el encabezado de Ben Vanik en `LICENSE` (sin verificar el tipo exacto).

### Flycast (Dreamcast)

- **Descarga:** [github.com/flyinghead/flycast/releases](https://github.com/flyinghead/flycast/releases). Última **v2.7** (2026-08-19), `flycast-win64-2.7.zip`. Solo estables; las de desarrollo, en flyinghead.github.io (sin verificar).
- **Portable siempre** en Windows: la configuración va en la carpeta del exe y los datos en `data\` a su lado ([winmain.cpp](https://github.com/flyinghead/flycast/blob/master/core/windows/winmain.cpp)).
- **Configuración:** `emu.cfg`. **Por juego:** al arrancar fija el ID del disco (`product_number`) y recarga los ajustes por juego ([emulator.cpp](https://github.com/flyinghead/flycast/blob/master/core/emulator.cpp), `loadGameSpecificSettings`); quedan en `emu.cfg` bajo ese ID (la sección exacta, sin verificar).
- **Mods:** texturas en `data\textures\<gameId>\` (o `<ruta>\<gameId>`), con `rend.CustomTextures` ([oslib.cpp](https://github.com/flyinghead/flycast/blob/master/core/oslib/oslib.cpp)).
- **Ajustes** ([option.cpp](https://github.com/flyinghead/flycast/blob/master/core/cfg/option.cpp)): `rend.Resolution`, `rend.WideScreen`, `rend.WidescreenGameHacks`, `rend.vsync`, `rend.TextureFiltering`; el renderizador (`pvr.rend`), sin verificar la clave.
- **Partidas:** VMU en `data\` (`vmu_save_*.bin`, sin verificar).
- **Licencia:** GPL-2.0.

## Steam ROM Manager

- **Descarga:** [github.com/SteamGridDB/steam-rom-manager/releases](https://github.com/SteamGridDB/steam-rom-manager/releases). Última **v2.6.2** (2026-11-02): `Steam-ROM-Manager-portable-2.6.2.exe`, `-Setup-2.6.2.exe`, `.msi`, y la CLI suelta `srm-cli-2.6.2-win-x64.exe`. `latest.yml` es el manifiesto de electron-updater. Solo estables (decisión 292).
- **Portable:** la CLI usa una carpeta `userData` junto a ella si existe; si no, los datos de la app instalada (`steam-rom-manager\userData` en AppData). `SRM_USER_DATA_DIR` manda sobre las dos ([README](https://github.com/SteamGridDB/steam-rom-manager#command-line-interface)). Para la app portable, se supone el mismo `userData` (sin verificar).
- **Línea de comandos:** `srm` crea, edita, ve y borra parsers (`create`, `edit`, `view`, `delete`, con `--ptype`), además de los comandos de la [wiki](https://github.com/SteamGridDB/steam-rom-manager/wiki/Command-Line-Interface). Otteretro puede armar los parsers sin tocar la GUI.
- **Licencia:** GPL-3.0.

## Qué implica para perfiles y mods

Cada emulador vive una sola vez por versión en `Emuladores\<Emulador>\<versión>` (decisión 302) y lo comparten todos los juegos que lo usan. Un perfil (decisión 286) es la configuración y los mods de **un juego** en **una versión de emulador**, y solo hay uno activo por juego. Por eso, lo de un juego tiene que ir a lugares que **solo lee ese juego**. Si no existen, se aplica justo antes de lanzarlo y se deshace al cerrar.

### Tres formas de aplicar un perfil

1. **Archivo por juego nativo (la mejor).** El emulador ya tiene un archivo por juego con la clave del juego en el nombre. Otteretro guarda el archivo del perfil en su librería y, al activarlo, lo copia (o enlaza) a la ruta del emulador; al cambiar de perfil, guarda el que estaba y pone el nuevo. Sirve con varias instancias abiertas a la vez, porque cada juego tiene su archivo.
2. **Carpeta de mods por juego (con la clave del juego).** El perfil guarda sus mods en su carpeta de la librería, y la carpeta del juego dentro del emulador pasa a ser una **junction** a la carpeta del perfil activo, como ya hacen los complementos (`link` en la receta). Cambiar de perfil es cambiar a dónde apunta la junction: no se copia nada. Si además el emulador guarda qué mod está activo (Eden `[DisabledAddOns]`, Ryubing `mods.json`), ese dato va en el archivo por juego del punto 1.
3. **Solo configuración global.** El emulador no distingue juegos. Hay dos salidas:
   - **Configuración alternativa al lanzar**, si el emulador la acepta (Eden `-c`, RPCS3 `--config`, Vita3K `-c`, xemu `-config_path`). El perfil es un archivo completo que se pasa en la línea de comandos y la global no se toca. Es seguro y admite varias instancias.
   - **Cambio y vuelta:** antes de lanzar se respalda la global, se escribe la del perfil, y al cerrar el proceso se restaura. Dos juegos del mismo emulador abiertos a la vez se pisan: hay que bloquear el segundo o avisar. Si la app se cae con el juego abierto, al volver a abrir tiene que restaurar la global (dejar una marca del cambio pendiente).

### Por emulador

| Emulador | Configuración del perfil | Mods del perfil | Lo que no se puede, y la alternativa |
|---|---|---|---|
| Eden / Citron Neo | (1) `user\config\custom\<TitleID>.ini`; mandos con `--input-profile` o en el ini | (2) junction `user\load\<TitleID>` → perfil; activos en `[DisabledAddOns]` del ini | Los mods de framework (ARCropolis) van en `sdmc\` compartida: (2) sobre `sdmc\atmosphere\contents\<TitleID>` y avisar si el mod toca `sdmc\ultimate` (común a todos los perfiles) |
| Ryubing | (1) `games\<titleid>\Config.json` (copia completa: crearla desde la global del momento) | (2) junction `mods\contents\<titleid>` → perfil; `games\<titleid>\mods.json` va con el perfil | Como el archivo por juego es completo, un cambio en la global no llega a los juegos con perfil: avisar o volver a mezclar |
| Ryujinx SS4K | Igual que Ryubing, dentro de su `portable\` | El pack es la instalación: no se separa | Se instala como emulador propio (una versión = un pack) y no se comparte con Ryubing |
| Dolphin | (1) `User\GameSettings\<GameID>.ini`, con mandos `PadProfileN` y `WiimoteProfileN`; los perfiles de mando en `User\Config\Profiles\` se comparten por nombre | (2) junction `User\Load\Textures\<GameID>`; Riivolution y GraphicMods, igual por juego; `HiresTextures = True` en el ini del juego | Nada. Ojo: un ini por ID de 6 caracteres; Wii y GameCube del mismo juego tienen IDs distintos (encaja con la decisión 301) |
| Dusklight | Un solo juego: el perfil es todo su `data\config` (3, cambio y vuelta, o `--user-dir` por perfil) | `--mods <carpeta del perfil>` al lanzar: lo mejor, sin junction | `--user-dir` por perfil separaría también las partidas: no usarlo para eso, solo `--mods`. Pasar la receta a la carpeta `data` |
| RMG | (1) sección del juego en `Config\mupen64plus.cfg`: el mismo archivo para todos, así que se escribe solo esa sección; perfil de mando con `UseGameProfile` | Texturas HD por nombre interno de la ROM (2), ruta por verificar | Escribir un ini compartido exige leerlo y escribirlo con el emulador cerrado |
| Azahar | (1) `user\config\custom\<TitleID>` | (2) junctions `user\load\mods\<TitleID>` y `user\load\textures\<TitleID>` | — |
| DuckStation, PCSX2, PPSSPP | (1) `gamesettings\<serial>.ini`, `gamesettings\<serial>_<CRC>.ini`, `PSP\SYSTEM\<GAMEID>_ppsspp.ini`; mandos con perfil por juego (DuckStation y PCSX2) | (2) junction `textures\<serial>`; los parches `.pnach` de PCSX2 son un archivo por juego (1) | La CRC de PCSX2 se calcula del ejecutable del juego: se lee del log o de la caché del emulador al primer arranque (sin verificar el mejor camino) |
| RPCS3 | (1) `config\custom_configs\config_<TITLEID>.yml` | Parches: `patch.yml` es uno para todo, con activación por juego dentro (3, cambio y vuelta). Mods de archivos: reemplazan archivos del juego instalado, sin superposición | Un mod de archivos es destructivo: respaldar los archivos que pisa y restaurarlos al quitarlo (decisión 288, siempre con respaldo) |
| shadPS4 | (1) `user\custom_configs\<serial>.json` | (2) la carpeta `<juego>-mods` está junto al **juego**, no al emulador: junction desde ahí a la del perfil | Los parches van en `user\patches` (global) o con `--patch <archivo>` al lanzar: mejor el argumento |
| Vita3K | (1) config por juego (ruta por verificar) | Sin verificar | — |
| Cemu | (1) `gameProfiles\<titleid>.ini` (incluye `controllerN`) | Graphic packs: los archivos se comparten (`graphicPacks\`), pero **cuáles están activos va en `settings.xml` global** (3, cambio y vuelta de la sección `GraphicPack`). La resolución también es un pack | Alternativa: una carpeta de datos por perfil no sirve, porque `--mlc` cambia solo `mlc01`, no `settings.xml` |
| Flycast | (1) los ajustes por juego de `emu.cfg` (el mismo archivo para todos: escribir solo la sección del juego) | (2) junction `data\textures\<gameId>` | — |
| Xenia Canary | (1) `config\<TitleID>.config.toml` | (2) parches por TitleID en `patches\` | — |
| mGBA | Overrides por juego en `config.ini`, compartido (1, escribir solo lo del juego; formato por verificar) | Parches IPS/BPS/UPS con el nombre de la ROM: la ROM modificada se arma con un parche, sin copiar la base (decisión 290) | — |
| melonDS, Mesen, bsnes, Snes9x, BlastEm, xemu | (3) cambio y vuelta de la global (xemu: `-config_path` con el archivo del perfil) | Snes9x: parche con el nombre de la ROM en `Patches\`; Mesen: HD Packs por juego (2) | Sin configuración por juego. xemu guarda las partidas dentro del disco duro virtual: separar partidas exige otra imagen de disco, fuera del alcance del perfil |
| KytyPS5 / SharpEmu | Por verificar con la build | Por verificar | Por estabilidad, solo el perfil por defecto al principio |

### Partidas al cambiar de emulador (decisión 288)

- **Switch:** Eden y Citron usan la misma estructura. Entre Eden y Ryubing el contenido es el mismo y solo cambia la carpeta, que se calcula con el `imkvdb.arc` de Ryujinx, como hace el enlace de Eden. Se copia (o se enlaza) con un respaldo previo.
- **GameCube:** el `.gci` de Dolphin y el de Dusklight (por verificar con un archivo real).
- **PS1:** `.mcd` crudo entre emuladores. **PS2:** el `.ps2` de PCSX2. **3DS:** Azahar con Citra y Lime3DS.
- **N64:** el formato de mupen64plus entre frontends; con Project64 hay que convertir.
- **Sin camino conocido:** Xbox (dentro del disco duro virtual) y PS5. Se avisa.

### Contenido instalado (decisión 295)

- **Apuntar sin copiar:**
  - Eden: `external_content_dirs` en la global.
  - Ryubing: `autoload_dirs` en la global, o `games\<titleid>\updates.json`/`dlc.json` por juego.
  - shadPS4: `--set-addon-folder`.
  - Cemu: `--mlc` con un `mlc01` compartido fuera de la instalación, que sobrevive a cada versión nueva.
- **Instalador por línea de comandos (copia):** Azahar `--install`, RPCS3 `--installpkg`/`--installfw`, Vita3K `--pkg --zrif`.
- **Solo desde la GUI (aviso):** Citron Neo (NAND), Cemu (instalar), Xenia («Install Content») y PS4 (decisión 295).
- El contenido que se copia al emulador (NAND, `dev_hdd0`, `mlc01`) es **común a todos los perfiles** y a todas las versiones que comparten esa carpeta de datos. Si cada versión tiene su propia carpeta portable, hay que copiarlo o enlazarlo al pasar de versión (decisión 288). Lo más simple: esas carpetas grandes (NAND, `dev_hdd0`, `mlc01`) como junction a una carpeta del emulador común a sus versiones.

### Modo portable con varias versiones al lado

Si cada versión es portable, cada una tiene su propia configuración, partidas y caché. Las partidas y el contenido instalado deberían ser **del emulador, no de la versión**: una carpeta `Emuladores\<Emulador>\_datos\` (nombre por decidir) enlazada desde cada versión, para que actualizar no obligue a migrar todo. La configuración sí queda por versión (decisión 298: los ajustes se describen por versión). Esto choca con la idea de que cada versión sea un portable independiente. Se decide al diseñar los perfiles, con la persona.

### Cambios que pide esta investigación a las recetas actuales

- `dusklight.json`: portable por carpeta `data` en lugar de la junction a `%APPDATA%`, y `--mods` para los mods.
- `dolphin.json`: versión desde la API (`shortrev`) y no desde la fecha del exe. `GameSettings` como configuración por juego y `Load\Textures\<GameID>` como mods por juego.
- Mesen va desde `nesdev-org/MesenCE`; `SourMesen/Mesen2` está archivado.
- El esquema de recetas necesita, para cada versión del emulador:
  - la ruta de configuración por juego con la plantilla de la clave (`config/custom/{TitleID:X16}.ini`),
  - la carpeta de mods por juego (`load/{TitleID:X16}`),
  - el tipo de aplicación del perfil (archivo, junction, argumento, cambio y vuelta),
  - los argumentos de lanzamiento (`--input-profile`, `--mods`, `-c`),
  - de dónde sale la versión (API Forgejo, GitHub, Dolphin o «manual» para SS4K y BlastEm).
