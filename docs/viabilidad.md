# Revisión de viabilidad

- **Fecha:** 2026-10-14
- **Alcance:** todo lo que dice [REQUISITOS.md](REQUISITOS.md), revisado punto por punto contra documentación y proyectos que existen hoy.
- **Ajustado** el 2026-10-14 con lo que hace ES-DE (V1 y V5). Se aprueba en la encuesta 8.
- **Resultado:** la app es viable. **Hay 6 puntos que no funcionan tal como están escritos** y necesitan un ajuste (❌/🟡 abajo). Los ajustes son **propuestas**: no entran en REQUISITOS.md hasta que los apruebes.

## 1. Puntos que hay que ajustar

| # | Requisito | Problema encontrado | Ajuste propuesto |
|---|---|---|---|
| V1 | T7: esperar el aviso `ACTIVE_PROCESS_ZERO` del Job Object "sin volver a revisar" | ❌ Microsoft documenta que **la entrega de esos avisos no está garantizada**. Si se pierde uno, `otteretro jugar` quedaría abierto para siempre, Steam seguiría contando horas y no se respaldarían las partidas. | **Como hace ES-DE:** se espera a que termine **el proceso que se lanzó** (`WaitForSingleObject` en Windows, `pidfd` en Linux). Esa espera **sí está garantizada** y cubre casi todos los emuladores y ports. El Job Object y una **comprobación de seguridad cada 60 s** solo se usan si ese proceso termina dejando otros vivos (lanzadores, juegos de tienda). |
| V2 | Icónico y rutas: "consenso de Reddit, ResetEra" | ❌ Desde noviembre de 2025 **Reddit cerró su API** a apps nuevas y exige aprobación manual. ResetEra nunca tuvo API. | Ninguna API. **Claude lee Reddit y ResetEra con búsqueda web** dentro de `claude -p`, cita las fuentes y el resultado se guarda. Ya era Claude quien lo juzgaba, así que no cambia nada visible. |
| V3 | Notas de OpenCritic / Metacritic | ❌ Metacritic no tiene API. OpenCritic la ofrece **solo con licencia**. | Las notas salen de **IGDB** (nota de crítica agregada) y, para las sagas icónicas, Claude cita Metacritic/OpenCritic por búsqueda web. |
| V4 | Verificación por hash contra **No-Intro** | 🟡 DAT-o-MATIC pone **captchas y bloquea temporalmente** a quien descarga de forma automática. Redump sí se puede descargar sin problema. | Redump se descarga solo. Los DAT de No-Intro **los bajas tú** (el pack diario, de vez en cuando) y la app los importa desde una carpeta. Pasa a ser la acción A4. ES-DE no verifica contra DAT, así que no sirve de referencia aquí. |
| V5 | ScreenScraper para identificar y medios | 🟡 La API necesita **credenciales de desarrollador** (`devid`/`devpassword`) que se piden en su foro, además de tu usuario. Sin ellas no responde. | **Como hace ES-DE:** Otteretro se registra una vez como software y pide **sus propias** credenciales en el foro de ScreenScraper (las de ES-DE no se pueden reutilizar). Tú pones además tu usuario. Queda **previsto, pendiente de las credenciales** (acción A3). Mientras no lleguen, se identifica por nombre, TitleID y DAT (§9.4). |
| V6 | HowLongToBeat | 🟡 No tiene API. Solo hay un scraper comunitario en C# que se rompe cada pocos meses cuando cambian el sitio (último arreglo: julio de 2026). | Se usa ese scraper como dato **opcional**: si falla, la ficha muestra "sin datos" y nada se detiene. |

## 2. Viable, con una condición a tener en cuenta

| Tema | Condición |
|---|---|
| **Velopack + repositorio privado** | Para descargar las actualizaciones desde las releases de un repositorio privado hace falta un **token de GitHub de solo lectura**. Se guarda cifrado igual que el resto de las claves (§5.6). El mismo token, con permiso de escritura en `otteretro-datos`, sirve para el respaldo. |
| **Carga inicial "sin tope" con Claude** | Claude Code con suscripción tiene **límites por ventana de 5 horas y por semana**. La carga inicial completa **no cabe en una sesión**: dura días. La cola tiene que poder **pausarse y retomarse** sola cuando el límite se renueva. Los datos de APIs (IGDB, etc.) los baja el script sin tocar Claude. Claude solo juzga sagas, no los ~cientos de miles de juegos. |
| **Git LFS** | El plan gratis da **10 GiB** de almacenamiento y 10 GiB de transferencia al mes. Partidas, BIOS, keys y firmware caben. Si se pasa del límite, LFS se bloquea hasta el mes siguiente; no se cobra si el presupuesto está en 0. |
| **BIOS y keys en GitHub** | Técnicamente sin problema en un repositorio privado. Son archivos con copyright: el riesgo es bajo, pero existe. |
| **SteamKit2 (T1)** | El inicio de sesión da un *refresh token* de larga duración; con él se pide un *access token* (`GenerateAccessTokenForApp`) y se llama a `IFamilyGroupsService` por HTTPS. Steam permite varias sesiones a la vez; solo hay conflicto si dos sesiones **juegan** a la vez, y la de Otteretro nunca juega. Se sigue confirmando en el prototipo. |
| **Epic, cuenta completa** | Funciona con el mismo método que Legendary/Heroic (OAuth no oficial). Puede romperse si Epic lo cambia, como cualquier API no documentada. |
| **Game Pass** | Catálogo no oficial (ya estaba asumido en T2). |
| **PS4 (shadPS4)** | Las versiones recientes **ya no instalan `.pkg`**: piden el juego extraído. La ingesta de PS4 tiene que extraer el `.pkg` con una herramienta externa (receta). §8.6 no lo menciona. |
| **Ryujinx** | El proyecto original cerró en 2024. "Fuente oficial" significa su continuación (Ryubing) o el build que uses, como SS4K. |
| **MyJDownloader** | Hay una librería .NET (`My.JDownloader.Api`), vieja pero usable. Si falla, la API es HTTP y se llama directo. |
| **Steam ROM Manager** | Tiene línea de comandos desde la 2.4 y acepta manifiestos "Manual". Viable. |

## 3. Viable sin observaciones

Stack (ASP.NET Core en `localhost` + Next.js estático + Photino), SQLite, Velopack en Windows y AppImage, bibliotecas con marcador, rutas relativas y reescritura de rutas internas por receta, conversiones (DolphinTool para RVZ, chdman para CHD, y a la inversa para regenerar el ISO), IGDB (incluye remakes, remasters y ports), Wikidata, RAWG, SteamGridDB, Steam Web API, IsThereAnyDeal, GamerPower, RetroAchievements, detección de Xbox (paquetes AppX) y Battle.net instalados, DPAPI/libsecret, `pidfd`/`waitpid` en Linux, Steam Input y horas de Steam en accesos que no son de Steam, `claude -p` desde la app, Issues con `gh`, GitHub Actions y Claude Design.

## Fuentes

- [Microsoft: JOBOBJECT_ASSOCIATE_COMPLETION_PORT](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-jobobject_associate_completion_port): "their delivery to the completion port is not guaranteed"
- [Reddit Responsible Builder Policy (r/redditdev, nov. 2025)](https://red.applefritter.com/r/redditdev) · [n8n: credenciales de Reddit](https://docs.n8n.io/integrations/builtin/credentials/reddit.md)
- [PCGamesN: OpenCritic y la licencia de su API](https://www.pcgamesn.com/b/review)
- [Datoso: advertencia sobre captchas en DAT-o-MATIC](https://www.mintlify.com/laromicas/datoso/guides/downloading-dats) · [ROMSearch: No-Intro solo se baja a mano](https://romsearch.readthedocs.io/en/main/_sources/modules/datparser.rst.txt)
- [ES-DE: espera al proceso lanzado y credenciales de desarrollador de ScreenScraper](https://repo.retrodeck.net/Xargon/ES-DE/commit/5558557dc862398fdb96ce99e46bddd9d8ffa04e)
- [HowLongToBeatScraper (C#)](https://codeberg.org/jkrehm/HowLongToBeatScraper)
- [GitHub: facturación de Git LFS](https://docs.github.com/en/billing/concepts/product-billing/git-lfs)
- [Velopack: GithubSource](https://docs.velopack.io/reference/cs/Velopack/Sources/GithubSource/constructors)
- [steamapi.xpaw.me: IAuthenticationService](https://steamapi.xpaw.me/IAuthenticationService) · [IFamilyGroupsService](https://steamapi.xpaw.me/IFamilyGroupsService)
- [Guía de shadPS4: PKG ya no se admite](https://www.joeysretrohandhelds.com/guides/shadps4-pc-setup-guide/)
- [My.JDownloader.Api (NuGet)](https://www.nuget.org/packages/My.JDownloader.Api/2.4.3)
- [Steam ROM Manager](https://github.com/doZennn/steam-rom-manager) · [RetroDECK: manifiestos para SRM](https://retrodeck.readthedocs.io/en/latest/wiki_development/functions/steam-sync/)
