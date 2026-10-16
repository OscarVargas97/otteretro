# Plan por fases

- **Fecha:** 2026-10-14
- **Base:** [REQUISITOS.md](REQUISITOS.md), [pantallas.md](pantallas.md), el [prototipo aprobado](https://claude.ai/artifact/Bo8gtfiPdRAumFhkFikHPV) y [arquitectura.md](arquitectura.md).
- **Orden:** **catálogo primero** (#100). Así la carga inicial de Claude, que dura días, avanza sola mientras se construyen las demás fases. Jugar desde Steam llega en la fase 6; hasta entonces sigues jugando como hoy.

> **Detalle para implementar y estado actual:** [plan-implementacion.md](plan-implementacion.md).

## Cómo se trabaja cada fase

1. Cada fase se programa en su rama **`fase/N-nombre`**, creada desde `dev`, con Claude Code, siguiendo este plan y los documentos de arriba.
2. La fase está **lista** cuando se cumple su lista de "Listo cuando" y pasan sus pruebas (A4). Entonces se abre un pull request y se fusiona en **`dev`**.
3. **Por ahora nada va a `main`** (#101): todas las fases se acumulan en `dev`. Cuando decidas publicar, se fusiona `dev` en `main` y GitHub Actions publica la release (A6).
4. Si durante una fase aparece una decisión que no está en los documentos, se te pregunta antes de seguir y se registra en `decisiones.md`.

Las **acciones tuyas** de cada fase (cuentas y claves, A3) se piden al empezarla, no antes.

---

## F0 · Esqueleto ✅

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

## F1 · Catálogo

**Qué tendrás:** Sagas, fichas y Colección con datos reales de todas las consolas, y lo que tienes en Steam.

- Credenciales cifradas (DPAPI) y Ajustes › Cuentas y claves.
- Cola de tareas real en SQLite y caché de API.
- Carga por scripts desde **IGDB** (sagas, juegos, versiones, plataformas, ids de Steam), **Wikidata** (sagas), **RAWG** (complemento) y **HowLongToBeat** (opcional). Prioridad fija por campo y correcciones manuales que no se sobrescriben.
- Nombre oficial en español cuando existe; las 23 consolas y PC.
- Imágenes de **SteamGridDB** en la caché.
- **Steam Web API**: lo comprado y las horas → "lo tengo · versión PC" (cuenta detectada en `loginusers.vdf`).
- Pantallas con datos: **Sagas**, **detalle de saga** (sin rutas aún), **Colección** (por consola, lo tengo / me falta con Steam), **ficha** (versiones, datos y fuentes), **búsqueda global**.
- Favoritas, "sigo esta saga", progreso por juego y nota de 1 a 10.

**Listo cuando:** ves la saga Zelda con todos sus juegos y versiones, sus portadas, y tus juegos de Steam marcados como "lo tengo".
**Tú:** claves de IGDB (Twitch), SteamGridDB, Steam Web API y RAWG.

## F2 · Claude

**Qué tendrás:** las rutas de cada saga, qué es icónico y la mejor versión de cada juego. La carga inicial empieza aquí y corre sola.

- Ejecutor de `claude -p` con prompts en `prompts/`, respuestas JSON validadas y herramientas limitadas a búsqueda web.
- **Lotes** de varias sagas por llamada; **pausa automática** al llegar al límite de la suscripción, con hora de reanudación.
- Rutas (lanzamiento, historia, comunidad) con fuentes; icónico; mejor versión con los 4 criterios; clasificación de versiones (remake, remaster); descripciones traducidas una vez.
- Revisar › Resultados por aprobar y Aprendizajes (con deshacer); registro de uso de Claude.
- Recalcular cuando cambian los datos o cuando lo pides; tope por ejecución después de la carga inicial.
- Pantallas: línea de tiempo con selector de ruta, "Por qué este orden", comparativa de versiones, **Inicio › Continuar**.
- Carga inicial completa: Nintendo → Sony → Microsoft (#69).

**Listo cuando:** apruebas la ruta de la comunidad de Zelda, Inicio te dice cuál es tu siguiente juego, y la carga avanza sola durante días, pausándose y siguiendo sin que hagas nada.

## F3 · Bibliotecas

**Qué tendrás:** tus archivos de juegos reconocidos, verificados y marcados como "lo tengo".

- Marcador `otteretro-biblioteca.json`, registro, crear, **adoptar `E:\Consolas`** (incluida su estructura antigua), renombrar, quitar sin borrar, reconectar.
- Escanear por nombre y TitleID/serial; BASE/UPD/DLC; verificación por hash con DAT de **libretro (No-Intro)** y **Redump**, que se descargan solos.
- Revisar › Identificaciones dudosas y Sin identificar; alias aprendidos.
- Mover o migrar bibliotecas (copiar → hash → BD → borrar origen), con la pantalla Ajustes › Bibliotecas.
- Asistente: paso 4 (bibliotecas).

**Listo cuando:** `E:\Consolas` está adoptada, Twilight Princess y tus 7 juegos de Switch aparecen como "lo tengo" con sus UPD y DLC, y Twilight Princess sale verificado por Redump.

## F4 · Emuladores, ports y Jugar

**Qué tendrás:** el botón **Jugar** de la app abre cada juego con la mejor opción.

- Recetas: Dolphin, Dusklight, Ryujinx SS4K, Eden, Ryubing, Citron y los predeterminados de cada consola (referencia: GordoFriki, #72).
- Instalar desde la fuente oficial, versiones lado a lado, copia previa al actualizar, migrar partidas y configuración, reescritura de rutas internas, detección de lo instalado a mano.
- Mover los emuladores, el port y las texturas de `E:\Consolas` a `Herramientas\` (copiar, verificar, borrar).
- Herramientas › Sistema: subir BIOS, keys y firmware con versiones.
- Regla de cómo jugar y elección manual por juego; contenido instalado dentro del emulador (UPD/DLC de Switch).
- `otteretro jugar`: prepara, lanza, **espera al proceso lanzado** (V1) y ventana pequeña solo si prepara o falla.
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
- **RetroAchievements**: logros en la ficha y activarlos en los emuladores compatibles.
- **Evolución**: sugerir y crear issues "automatizar" con `gh`.
- Asistente paso 7 (carga inicial) y repaso de estados vacíos, errores y la nutria en todas las pantallas.

**Listo cuando:** Descubrir recomienda sagas con su porqué y una tarea repetida de Claude genera su issue "automatizar".

---

## Después

- **Linux / PlayOS** (#103): el destino es WorkOS, que es NixOS. Ahí lo natural es declarativo: Otteretro como paquete y módulo Nix y los emuladores y Steam desde nixpkgs, en vez de AppImage/Flatpak y la autoactualización de Velopack. Se decide al trabajar PlayOS; mientras, lo propio de Windows queda detrás de su clase (§8.9).
- Mejoras que vayan saliendo como issues "automatizar".
