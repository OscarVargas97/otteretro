# Plan por fases

- **Fecha:** 2026-10-14 · reordenado el 2026-10-16 (#138)
- **Base:** [REQUISITOS.md](REQUISITOS.md), [pantallas.md](pantallas.md), el [prototipo aprobado](https://claude.ai/artifact/Bo8gtfiPdRAumFhkFikHPV) y [arquitectura.md](arquitectura.md).
- **Orden (#138):** **P0 PlayOS y Linux → D1 Diseño → F1 a F9**. Primero se resuelve lo de Linux y la sesión Play, después se actualiza el diseño (centro de entretenimiento y mando) y recién entonces el catálogo, que sigue yendo antes que el resto para que la carga de Claude avance sola (#100). Desde F1, cada pantalla nueva se hace navegable con mando.

> **Detalle para implementar:** [plan-implementacion.md](plan-implementacion.md). **Avance:** en GitHub, un [milestone por fase](https://github.com/OscarVargas97/otteretro/milestones) y las tareas en el [Project otteretro](https://github.com/users/OscarVargas97/projects/2) (#122).

## Cómo se trabaja cada fase

1. Al abrir una fase, sus tareas se levantan como solicitudes y sub-issues en GitHub (skill `otter:fase`, con una encuesta solo para lo que el plan deja abierto). Se programa en su rama **`fase/N-nombre`**, creada desde `dev`, con Claude Code, siguiendo este plan y los documentos de arriba.
2. La fase está **lista** cuando se cumple su lista de "Listo cuando" y pasan sus pruebas (A4). Entonces se abre un pull request y se fusiona en **`dev`**.
3. **Por ahora nada va a `main`** (#101): todas las fases se acumulan en `dev`. Cuando decidas publicar, se fusiona `dev` en `main` y GitHub Actions publica la release (A6).
4. Si durante una fase aparece una decisión que no está en los documentos, se te pregunta antes de seguir y se registra en `decisiones.md`.

Las **acciones tuyas** de cada fase (cuentas y claves, A3) se piden al empezarla, no antes.

---

## F0 · Esqueleto (cerrada)

**Qué tendrás:** la app instalada que abre su ventana, con la navegación del prototipo y sin datos todavía.

- Solución .NET 10 con un proyecto y la carpeta de pruebas; `web/` con Next.js estático y el CSS del sistema de diseño.
- `Otteretro.exe` con los modos APP, JUGAR y CLI; una sola instancia; servidor en `127.0.0.1` con puerto y clave aleatorios.
- Ventana Photino; barra lateral y las 13 pantallas como cascarones con su estado vacío (la nutria).
- SQLite con Dapper y migraciones `.sql`; registro de errores en `logs\` (30 días).
- Server-Sent Events y panel de tareas vacío; tipos TypeScript generados desde OpenAPI.
- Asistente de primer arranque: pasos 1 (bienvenida) y 2 (carpeta de datos). El resto de pasos se activa en su fase.
- Velopack: empaquetado e instalador generados en local; el flujo de GitHub Actions queda escrito, pero solo se activará cuando algo llegue a `main` (#101). La autoactualización se prueba con dos paquetes locales.

**Listo cuando:** instalas desde el paquete local, eliges la carpeta de datos, navegas por todas las pantallas, y un segundo paquete local se instala solo como actualización.
**Tú:** crear el token de GitHub.

## P0 · PlayOS y Linux

**Qué tendrás:** El portátil de juego con PlayOS: al entrar eliges **Work** (tu Hyprland de siempre) o **Play** (la sesión de Steam, con Otteretro arriba a pantalla completa), y Otteretro F0 funcionando en Linux.

- Antes de tocar el código de F0: renombrar sus identificadores al inglés (#125, issue #4), para que P0 trabaje ya sobre los nombres nuevos.
- **Otteretro en Linux**: ventana con WebKitGTK, rutas XDG, discos y montajes reales, una sola instancia, mover la carpeta de datos entre discos de verdad.
- **Credenciales** cifradas con DPAPI (Windows) y libsecret (Linux), y Ajustes › Cuentas y claves (se adelanta de F1).
- En Linux la actualización es con el sistema: Ajustes › Acerca de lo dice; Velopack queda solo para Windows (#114).
- **Flake de Otteretro**: paquete y módulo de NixOS.
- **Repo `playos`** (público, importa workos): sesiones Work y Play, Steam (Proton-GE, Remote Play, extest, Steam ROM Manager), Heroic, Lutris, Bottles, itch, Discord, OBS, gpu-screen-recorder, GameMode, gamescope, MangoHud, LACT, mandos (xone), LenovoLegionLinux y OpenRGB.
- **GPU del portátil de juego**: comprobar en el equipo qué configuración funciona y dejar esa (#109).
- **Sesión Play**: Steam es la sesión y Otteretro arranca encima; pasar a Big Picture y volver (#129).
- El portátil de juego pasa a usar PlayOS (cambio en `workos-personal`).

**Listo cuando:** en el portátil de juego eliges Play al entrar, aparece Otteretro a pantalla completa, con el botón Steam vas a Big Picture y vuelves a Otteretro; en Work, Steam y Otteretro se abren como apps normales.
**Tú:** el rebuild del portátil de juego (sudo con TOTP) y tenerlo encendido para las pruebas por ssh.

## D1 · Diseño

**Qué tendrás:** el diseño actualizado en Claude Design para el centro de entretenimiento, aprobado por ti antes de programar F1.

- Inicio como centro de entretenimiento: continuar jugando (consola y PC), rutas, novedades.
- Juegos y sagas de PC en Sagas, Colección (PC como consola) y la ficha.
- Seguimiento en la ficha: estado, fechas, sesiones, tiempo y logros.
- Mando: foco, barra de botones, menú rápido del botón Guía, teclado en pantalla.
- Ficha: quién lanza el juego (Steam, Heroic, Lutris, itch, emulador), «jugar en el PC» y «traer a este equipo».

**Listo cuando:** apruebas el prototipo navegable actualizado.

## F1 · Catálogo

**Qué tendrás:** Sagas, fichas y Colección con datos reales de todas las consolas **y de PC**, y lo que tienes en Steam, todo manejable con mando.

- Cola de tareas real en SQLite y caché de API.
- Sagas sin plataforma: una saga no se repite y sus juegos se asocian a los sistemas, PC incluido (#131). La carga es **por partes** y se puede retomar (#131).
- Carga por scripts desde **IGDB** (sagas, juegos, versiones, plataformas, ids de Steam), **Wikidata** (sagas), **RAWG** (complemento) y **HowLongToBeat** (opcional). Prioridad fija por campo y correcciones manuales que no se sobrescriben.
- Nombre oficial en español cuando existe; las 23 consolas y PC.
- Imágenes de **SteamGridDB** en la caché.
- **Steam Web API**: lo comprado, las horas y la última vez → "lo tengo" y el seguimiento (cuenta detectada en `loginusers.vdf`) (#133).
- Pantallas con datos: **Sagas**, **detalle de saga** (sin rutas aún), **Colección** (por consola, lo tengo / me falta con Steam), **ficha** (versiones, datos y fuentes), **búsqueda global**.
- Favoritas, "sigo esta saga", seguimiento por juego (estado, fechas, nota de 1 a 10 y notas) (#133).
- Navegación con mando en todas estas pantallas (#134).

**Listo cuando:** ves la saga Zelda con todos sus juegos y versiones, sus portadas, y tus juegos de Steam marcados como "lo tengo" con sus horas; una saga de PC (por ejemplo Half-Life) aparece igual; todo se recorre con el mando.
**Tú:** claves de IGDB (Twitch), SteamGridDB, Steam Web API y RAWG.

## F2 · Claude

**Qué tendrás:** las rutas de cada saga, qué es icónico y la mejor versión de cada juego. La carga inicial empieza aquí y corre sola.

- Ejecutor de `claude -p` con prompts en `prompts/`, respuestas JSON validadas y herramientas limitadas a búsqueda web.
- **Lotes** de varias sagas por llamada; **pausa automática** al llegar al límite de la suscripción, con hora de reanudación.
- Rutas (lanzamiento, historia, comunidad) con fuentes; icónico; mejor versión con los 4 criterios; clasificación de versiones (remake, remaster); descripciones traducidas una vez.
- Revisar › Resultados por aprobar y Aprendizajes (con deshacer); registro de uso de Claude.
- Recalcular cuando cambian los datos o cuando lo pides; tope por ejecución después de la carga inicial.
- Pantallas: línea de tiempo con selector de ruta, "Por qué este orden", comparativa de versiones, **Inicio › Continuar**.
- Carga inicial completa, por partes: Nintendo → Sony → Microsoft → PC (#69, #131).

**Listo cuando:** apruebas la ruta de la comunidad de Zelda, Inicio te dice cuál es tu siguiente juego, y la carga avanza sola durante días, pausándose y siguiendo sin que hagas nada.

## F3 · Bibliotecas

**Qué tendrás:** tus archivos de juegos reconocidos, verificados y marcados como "lo tengo".

- Marcador `otteretro-biblioteca.json`, registro, crear, **adoptar `E:\Consolas`** (incluida su estructura antigua), renombrar, quitar sin borrar, reconectar.
- Escanear por nombre y TitleID/serial; BASE/UPD/DLC; verificación por hash con DAT de **libretro (No-Intro)** y **Redump**, que se descargan solos.
- Revisar › Identificaciones dudosas y Sin identificar; alias aprendidos.
- Mover o migrar bibliotecas (copiar → hash → BD → borrar origen), con la pantalla Ajustes › Bibliotecas.
- Bibliotecas **en red** (SMB/NFS) y **«traer a este equipo»** desde la ficha (#136). Juegos de PC sueltos, sin tienda (#132).
- Asistente: paso 4 (bibliotecas).

**Listo cuando:** `E:\Consolas` está adoptada, Twilight Princess y tus 7 juegos de Switch aparecen como "lo tengo" con sus UPD y DLC, y Twilight Princess sale verificado por Redump.

## F4 · Emuladores, ports y Jugar

**Qué tendrás:** el botón **Jugar** de la app abre cada juego con la mejor opción.

- **Recetas compartidas** (#135): URL y hash por versión y sistema; en Windows descarga Otteretro y en PlayOS las arma Nix, con **todas las versiones instaladas de base**. Dolphin, Dusklight, Ryujinx SS4K, Eden, Ryubing, Citron y los predeterminados de cada consola (referencia: GordoFriki, #72). Variantes elegibles por juego (texturas, rendimiento).
- Instalar desde la fuente oficial, versiones lado a lado, copia previa al actualizar, migrar partidas y configuración, reescritura de rutas internas, detección de lo instalado a mano.
- Mover los emuladores, el port y las texturas de `E:\Consolas` a `Herramientas\` (copiar, verificar, borrar).
- Herramientas › Sistema: subir BIOS, keys y firmware con versiones.
- Regla de cómo jugar y elección manual por juego; contenido instalado dentro del emulador (UPD/DLC de Switch).
- `otteretro jugar`: prepara, lanza, **espera al proceso lanzado** (V1) y ventana pequeña solo si prepara o falla. En Linux aplica GameMode, MangoHud y la NVIDIA, desactivable por juego (#116).
- **El dueño lanza** (#130): Jugar abre por Steam lo que está en Steam y por Heroic, Lutris o itch lo suyo. Lo que no corre en Linux se marca «jugar en el PC» (#112).
- **Seguimiento**: sesiones y tiempo medidos en lo que lanza Otteretro (#133).
- Portar `_Herramientas\Catalogo` a C# (S1): ports nuevos y compatibilidad → **Inicio › Novedades**.

**Listo cuando:** pulsas Jugar en Twilight Princess y abre Dusklight, y en Skyward Sword HD abre Ryujinx SS4K con su pack 4K.
**Tú:** poner `prod.keys` y el firmware de Switch (A2).
**Comprobar:** que la espera del proceso detecta el cierre en Dusklight, Dolphin, Eden y Ryujinx.

## F5 · Respaldo

**Qué tendrás:** tus partidas, configuraciones, BIOS y la base de datos respaldadas solas en GitHub.

- Repositorio privado `otteretro-datos`; JSON por tabla; configuración de emuladores al cambiar; partidas al cerrar el juego (cola persistente); BIOS y keys; Git LFS con presupuesto 0 (#70).
- Conflictos: gana lo más reciente y lo otro se guarda aparte (#77).
- Ajustes › Respaldo, restaurar en un PC nuevo; asistente paso 3.

**Listo cuando:** cierras Dusklight, a los segundos tu partida está en `otteretro-datos`, y una restauración en una carpeta vacía recupera todo.

## F6 · Steam

**Qué tendrás:** tus juegos en Steam, por saga, con su arte, abriendo con `otteretro jugar`. **Desde aquí juegas desde Steam.**

- Receta de Steam ROM Manager; manifiestos "Manual"; categorías por saga y "Emulación"; nombre "Juego (Consola)" en español; arte de SteamGridDB.
- Varias versiones por juego; lo que ya está en Steam no se exporta; diferencias con la última exportación; por equipo.
- Pantalla Steam; asistente paso 5.

**Listo cuando:** abres Twilight Princess desde Steam (o Big Picture), Steam cuenta las horas y, al cerrar, la partida se respalda.

## F7 · Tiendas y precios

**Qué tendrás:** el préstamo familiar, Epic, precios, Game Pass y avisos de rebajas.

- **SteamKit2**: préstamo familiar con token de larga duración (T1).
- Epic con la cuenta completa (si falla, solo lo instalado); después Xbox y Battle.net con lo instalado.
- En PlayOS, las bibliotecas de **Heroic** (Epic, GOG, Amazon), **Lutris** e **itch.io** (#132).
- IsThereAnyDeal, GamerPower y Game Pass; "seguir precio"; avisos según #84; solo con la app abierta (#75).
- Ficha › Tiendas; Inicio › Novedades con bajadas de precio.

**Listo cuando:** ves los juegos del préstamo familiar como "lo tengo", Ōkami muestra su precio y mínimo histórico, y un juego seguido que baja un 40 % aparece en Novedades.
**Tú:** claves de IsThereAnyDeal y la cuenta de Epic.
**Comprobar:** que la sesión de SteamKit2 no cierra la del cliente de Steam mientras juegas (T1).

## F8 · Ingesta

**Qué tendrás:** descargas con JDownloader y megapacks que se ordenan solos.

- MyJDownloader; enlaces desde la ficha o en lista (script primero, Claude después); biblioteca destino con GB libres.
- Megapacks con vista previa, 1G1R (español primero), duplicados, solo traducciones de fans.
- Conversión: DolphinTool (RVZ), chdman (CHD); lo que pide el port manda; PS4 extrae el `.pkg`.
- ScreenScraper para lo que no coincide con los DAT (credenciales propias, #65).
- Pantalla Descargas; asistente paso 6.

**Listo cuando:** importas un megapack de GBA de prueba y cada juego queda en su carpeta, verificado y sin duplicados.
**Tú:** cuenta de MyJDownloader; credenciales de ScreenScraper (pedidas en su foro).

## F9 · Descubrir, logros y pulido

- **Descubrir**: recomendaciones por afinidad, "no me interesa" con 6 meses (S8).
- **Logros**: los de Steam y RetroAchievements en la ficha (#133), y activar RetroAchievements en los emuladores compatibles.
- **Evolución**: sugerir y crear issues "automatizar" con `gh`.
- Asistente paso 7 (carga inicial) y repaso de estados vacíos, errores y la nutria en todas las pantallas.

**Listo cuando:** Descubrir recomienda sagas con su porqué y una tarea repetida de Claude genera su issue "automatizar".

---

## Después

- Un PC para la tele y una portátil de mano con PlayOS (#106).
- El NAS con todas las ROMs (#115): las bibliotecas en red ya lo admiten desde F3.
- Mejoras que vayan saliendo como issues "automatizar".
