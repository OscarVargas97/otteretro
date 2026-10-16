# Registro de decisiones

Una entrada por decisión, la más reciente al final. Si una decisión cambia, se añade una entrada nueva que la reemplaza; las anteriores no se borran.

Los requisitos vigentes están en [../REQUISITOS.md](../REQUISITOS.md). Este registro y [v1-por-tema/](v1-por-tema/) son historia. Las respuestas originales de cada encuesta están en [respuestas/](respuestas/).

| # | Fecha | Decisión | Estado |
|---|---|---|---|
| 1 | 2026-10-14 | Consolas incluidas: 23 sistemas de Nintendo, Sony y Microsoft. Ver [v1-por-tema/consolas.md](v1-por-tema/consolas.md) | Decidido |
| 2 | 2026-10-14 | La exportación a Steam se hace a través de **Steam ROM Manager**: la app genera manifiestos "Manual" con el ejecutable y los argumentos de cada juego, y SRM crea los accesos y descarga las carátulas de SteamGridDB | Decidido |
| 3 | 2026-10-14 | Metadatos: **IGDB** para franquicias, colecciones, plataformas, fechas y portadas, y **Claude sin interfaz** para decidir qué es icónico y cuál es la mejor versión | Decidido |
| 4 | 2026-10-14 | Stack: C# multiplataforma (Windows y Linux). Propuesta: backend en ASP.NET Core con SQLite, interfaz en Next.js exportado como estático, ventana nativa con Photino | Decidido (cuestionario de requisitos) |
| 5 | 2026-10-14 | ~~Almacenamiento con `Sistema\` y `Biblioteca\` dentro de `E:\Consolas`~~ Reemplazada por la #7 | Reemplazada |
| 6 | 2026-10-14 | Nombre: **Otteretro**. Repositorio de desarrollo en `D:\dev\personal\otteretro`. La build final es portable y su ubicación no importa | Decidido |
| 7 | 2026-10-14 | El sistema va separado de las bibliotecas. Las bibliotecas se crean desde la app en cualquier carpeta o disco, con un marcador `otteretro-biblioteca.json`. Ver [v1-por-tema/bibliotecas.md](v1-por-tema/bibliotecas.md) | Decidido |
| 8 | 2026-10-14 | Interfaz en español, pensada para escritorio con ratón y teclado. Para jugar en la tele se usa Steam Big Picture | Decidido |
| 9 | 2026-10-14 | Los datos se guardan por consola, pero se muestran y se exportan **por saga**. Lo icónico y tus gustos son ejes separados, y hay una vista para descubrir sagas. Ver [v1-por-tema/catalogo.md](v1-por-tema/catalogo.md) | Decidido |
| 10 | 2026-10-14 | Mejor versión: cuatro criterios (contenido, jugabilidad hoy, técnico, original) que se muestran por separado, sin un veredicto único | Decidido |
| 11 | 2026-10-14 | Emulador o port: elección automática que se puede cambiar a mano por juego, y los valores de la regla son editables | Decidido |
| 12 | 2026-10-14 | Colección: escaneo rápido por nombre y TitleID, verificación por hash bajo demanda. BASE, UPD y DLC se detectan y nunca se borran. Ver [v1-por-tema/coleccion.md](v1-por-tema/coleccion.md) | Decidido |
| 13 | 2026-10-14 | Los juegos que no tengo se marcan como faltantes. Habrá un flujo con JDownloader a partir de los enlaces que tú das, con descarga, identificación, reubicación y marcado como listo | Decidido (detalle cerrado en #24) |
| 14 | 2026-10-14 | Steam: cuenta detectada automáticamente, solo la opción elegida de cada juego, categorías por saga (sin saga va a "Emulación") y nombre con la consola. Ver [v1-por-tema/steam.md](v1-por-tema/steam.md) | Decidido |
| 15 | 2026-10-14 | Las actualizaciones se lanzan a mano y se ejecutan solas. Claude se usa a través de **tu Claude Code sin interfaz**, nunca la API. Ver [v1-por-tema/automatizacion.md](v1-por-tema/automatizacion.md) | Decidido |
| 16 | 2026-10-14 | La app no registra tiempo de juego, pero muestra el que guarda Steam. No hace copias de seguridad de partidas | Decidido |
| 17 | 2026-10-14 | La app reescribe sola las rutas internas de emuladores y ports, y permite mover y migrar bibliotecas, consolas y juegos | Decidido |
| 18 | 2026-10-14 | ~~Temas abiertos: emuladores, remakes, JDownloader y metadatos~~ Cerrados en las entradas #19 a #27 | Reemplazada |
| 19 | 2026-10-14 | Emuladores y ports: se descargan de la fuente oficial y también se detectan los manuales. Cada uno tiene una receta JSON, hay emulador predeterminado por consola y uno específico por juego si hace falta. Las versiones se instalan una al lado de otra y la vieja nunca se reemplaza. La migración se hace con copia de seguridad previa. Ver [v1-por-tema/emuladores.md](v1-por-tema/emuladores.md) | Decidido |
| 20 | 2026-10-14 | BIOS, keys y firmware se suben desde la app a un almacén común compartido por todos los emuladores, con control de versiones | Decidido |
| 21 | 2026-10-14 | Emuladores y ports van en una **biblioteca de herramientas** separada de las de juegos. Esto cambia la estructura actual (`<Consola>\Emuladores`). Ver [v1-por-tema/bibliotecas.md](v1-por-tema/bibliotecas.md#tipos-de-biblioteca) | Reemplazada por #33 |
| 22 | 2026-10-14 | Todo es una versión del mismo juego, incluidos remakes y remasters. IGDB los clasifica y Claude revisa. Se muestran en una tarjeta con versiones desplegables, se pueden mandar varias a Steam y la comparación abarca todas | Decidido |
| 23 | 2026-10-14 | **Nuevo:** cronologías y rutas por saga (orden de lanzamiento, orden de la historia, ruta recomendada por la comunidad), con la versión que recomienda la comunidad en cada paso | Decidido (detalle cerrado en #35) |
| 24 | 2026-10-14 | Ingesta: JDownloader a través de la API de MyJDownloader. Los enlaces se pegan en la ficha o se importan en lista (Claude identifica cada uno). Destino por biblioteca, mostrando los GB libres. Se convierte al formato más liviano salvo que un port exija el original. El comprimido se borra. Ver [v1-por-tema/ingesta.md](v1-por-tema/ingesta.md) | Decidido |
| 25 | 2026-10-14 | **Nuevo:** megapacks y carpetas locales entran por el mismo proceso de ingesta (extraer, identificar, convertir, ubicar, marcar) | Decidido (detalle cerrado en #32) |
| 26 | 2026-10-14 | **Nuevo:** la biblioteca de Steam cuenta como "lo tengo". Esos juegos aparecen en su saga como versión PC y no hace falta conseguirlos para un emulador | Decidido (detalle cerrado en #31) |
| 27 | 2026-10-14 | Catálogo: la mayor cantidad de juegos posible, con su saga y si tienen versión de PC. Todo lo que tienes aparece siempre. Lo icónico sale de Reddit, ResetEra, rankings, notas y ventas; tus gustos, de sagas favoritas, puntuaciones y horas en Steam; Descubrir lo hace Claude Code. Metadatos: las 6 fuentes, imágenes de SteamGridDB y prioridad por campo. Mover y migrar se hace en modo seguro | Decidido |
| 28 | 2026-10-14 | Quedan abiertos: Steam y PC, megapacks, biblioteca de herramientas, formatos de ports, cronologías e identificación con poca confianza. Ver [v1-por-tema/temas-abiertos.md](v1-por-tema/temas-abiertos.md) | Cerrada con #31 a #37 |
| 29 | 2026-10-14 | **Nuevo principio: Claude decide, los scripts repiten.** Claude se usa solo para lo que requiere criterio o para la primera vez. Lo que se repite y no requiere decidir queda automatizado en scripts para no gastar tokens | Decidido (detalle en la encuesta 4) |
| 30 | 2026-10-14 | Los requisitos se consolidan en un solo documento vigente, `docs/REQUISITOS.md`. Los documentos por tema pasan a `historial/v1-por-tema/`. Después de cada encuesta se actualiza el consolidado y se guardan las respuestas en `historial/respuestas/` | Decidido |
| 31 | 2026-10-14 | Steam: se lee con la Web API, desde la cuenta detectada. Los juegos del préstamo familiar también cuentan como "lo tengo". Solo Steam cuenta para eso; Game Pass, Epic gratis y el resto de tiendas son información y se refrescan periódicamente. Si tienes la versión de PC oficial, va primero. Si existe pero no la tienes, se muestra con un enlace a la tienda | Decidido |
| 32 | 2026-10-14 | Megapacks: se mueven después de verificar, con vista previa. 1G1R con el español primero. Solo se importan traducciones de fans. Los duplicados se saltan salvo que el nuevo esté verificado. Con poca confianza, se asigna y se marca "revisar" | Decidido |
| 33 | 2026-10-14 | Una sola biblioteca de herramientas, **dentro de la carpeta de Otteretro** (`Otteretro\Herramientas\`), con los ports y los complementos (texturas, shaders, ReShade, mods) junto a su emulador o port. Reemplaza la ubicación libre de la #21 | Decidido |
| 34 | 2026-10-14 | Formatos: lo que pide el port manda. Si el port exige el original, se guarda solo el original sin recortes, regenerado desde RVZ o CHD si hace falta | Decidido |
| 35 | 2026-10-14 | Rutas por saga: lanzamiento, historia y comunidad. Claude las prepara y tú las apruebas. Eliges qué ruta seguir y se registra el progreso (pendiente, en curso, terminado, abandonado), con indicación del siguiente juego | Decidido |
| 36 | 2026-10-14 | Automatización: scripts en C# dentro de la app (interfaz y línea de comandos). El script va primero y Claude después. Lo aprendido se guarda solo, con registro reversible. Mientras la app funciona, Claude solo genera datos. Se registra cada uso de Claude y se sugiere qué automatizar. Lo que requiere criterio se recalcula cuando cambian los datos y cuando lo pides | Decidido |
| 37 | 2026-10-14 | **La app evoluciona**: las tareas que conviene automatizar se registran como GitHub Issues. El repositorio va a GitHub en privado, con ramas `dev` y `main`. Las releases las genera GitHub Actions desde `main`, y la app se autoactualiza sin tocar `data\` ni `Herramientas\` | Decidido |
| 38 | 2026-10-14 | ~~Los requisitos del usuario quedan cerrados~~ Reemplazada por la #39 | Reemplazada |
| 39 | 2026-10-14 | Una revisión de punta a punta encontró temas sin tratar: uso diario e interfaz, credenciales, copias de la base de datos, varios PCs, contenido dentro de los emuladores, Steam ROM Manager, RetroAchievements, tokens, escaneo y Linux. Se abre la encuesta 5 (REQUISITOS §12.1) | Cerrada con las entradas #40 a #46 |
| 40 | 2026-10-14 | Interfaz: botón "Jugar" en la app además de Steam, que se conecta con Steam (y quizás con otras tiendas). Pantallas principales: Inicio, Sagas, Colección y Descubrir. Estilo oscuro con portadas grandes. Mandos con Steam Input. **Primero se diseña y después se programa el front** | Decidido |
| 41 | 2026-10-14 | Credenciales cifradas con el almacén del sistema operativo. Se considera Bitwarden, que ya usas | Decidido (lo de Bitwarden sigue abierto) |
| 42 | 2026-10-14 | **Cambia la #16**: los datos de la app y las partidas guardadas se respaldan en un repositorio git privado aparte. La base de datos se exporta como texto | Decidido (el detalle sigue abierto) |
| 43 | 2026-10-14 | Migración entre equipos (formatear, pasar a Linux, cambiar de PC): no se pierde nada y la configuración que depende de cada equipo se guarda por equipo | Decidido |
| 44 | 2026-10-14 | La app instala sola el contenido que va dentro de los emuladores (UPD/DLC, Cemu, pkg, cia) y avisa si no puede. Steam ROM Manager lo gestiona la app. Se integra RetroAchievements | Decidido |
| 45 | 2026-10-14 | Carga inicial completa, con tope de uso de Claude por ejecución. Las descripciones se traducen con Claude una sola vez. Las bibliotecas se escanean a mano | Decidido (falta cómo combinar la carga completa con el tope) |
| 46 | 2026-10-14 | Linux: el formato de los emuladores se decide al migrar | Decidido |
| 47 | 2026-10-14 | Bitwarden es un respaldo manual tuyo. La app usa el almacén del sistema operativo. Cierra la #41 | Decidido |
| 48 | 2026-10-14 | Respaldo en el repositorio privado `otteretro-datos`: la base de datos y la configuración cuando cambian, las partidas al cerrar el juego o el emulador, y también las BIOS y keys. Las texturas no. Cierra la #42 | Decidido |
| 49 | 2026-10-14 | **Reemplaza la #31**: todo lo que tienes en cualquier tienda de PC (Steam con el préstamo familiar, Epic, GOG, Xbox, EA, Ubisoft) cuenta como "lo tengo" y se puede lanzar desde la app | Decidido |
| 50 | 2026-10-14 | La carga inicial va sin tope, de una vez; el tope de Claude se aplica después. Cierra la #45 | Decidido |
| 51 | 2026-10-14 | Interfaz: se diseña en Claude Design (primero el sistema de diseño, luego las pantallas). Barra lateral; referencias Steam y Playnite; densidad mixta; el acento se adapta a la portada; mascota nutria retro. Inicio con lo siguiente, lo pendiente de revisar, novedades y tareas. Sagas como línea de tiempo. Colección por consola con filtros. Ficha con 4 secciones a la vista y el resto desplegable. Panel de tareas con notificaciones | Decidido |
| 52 | 2026-10-14 | Los pendientes técnicos (T1 a T7) se investigan **ahora**, antes de desarrollar | Decidido |
| 53 | 2026-10-14 | Se derivan dos cosas nuevas: el lanzador `otteretro jugar <id>`, que hace falta para respaldar las partidas al cerrar el juego, y la investigación T6 y T7 | Decidido |
| 54 | 2026-10-14 | Resultado de la investigación técnica (docs/investigacion.md): préstamo familiar con inicio de sesión manual en Steam; tiendas con IsThereAnyDeal, GamerPower y el catálogo no oficial de Game Pass; actualizador propio en vez de Velopack (que no es portable); otras tiendas por etapas; Job Objects para el lanzador; Dusklight acepta RVZ | Revisada en #55 a #60 |
| 55 | 2026-10-14 | **Reemplaza P1 (portable) y la ubicación de la #33**: la app se instala con Velopack (en AppData, o como AppImage en Linux). La base de datos, `Herramientas` y la caché de imágenes van en una **carpeta de datos** que eliges tú | Decidido |
| 56 | 2026-10-14 | T1: el préstamo familiar se lee con SteamKit2. Se inicia sesión una vez y el token de larga duración se renueva solo | Decidido |
| 57 | 2026-10-14 | T2: se usan las tres fuentes (IsThereAnyDeal, GamerPower, Game Pass) con toda la información, incluido el mínimo histórico, más un **aviso cuando baja de precio** | Decidido |
| 58 | 2026-10-14 | T3/T4: dónde guarda partidas cada port y qué formatos acepta se averigua al instalarlo | Decidido |
| 59 | 2026-10-14 | T6: tiendas Steam, Epic, Xbox y Battle.net. Etapa 1: Steam y Epic con la cuenta completa. Etapa 2: Xbox y Battle.net, solo lo instalado. Fuera GOG, EA, Ubisoft y Amazon | Decidido |
| 60 | 2026-10-14 | T7: sin procesos que vigilen de fondo. `otteretro jugar` solo existe mientras juegas y el sistema operativo avisa cuando el juego se cierra. El respaldo de partidas se encola y se hace en silencio | Decidido |
| 61 | 2026-10-14 | **Precisa la #60** (V1): se espera a que termine el proceso lanzado, como ES-DE, porque esa espera está garantizada. El Job Object y una comprobación cada 60 s solo se usan si ese proceso termina dejando otros vivos (lanzadores, juegos de tienda) | Decidido |
| 62 | 2026-10-14 | V2: el consenso de Reddit y ResetEra lo lee Claude con búsqueda web y cita fuentes. No se usa ninguna API de Reddit | Decidido |
| 63 | 2026-10-14 | V3: las notas salen de IGDB. En las sagas icónicas, Claude cita además Metacritic y OpenCritic | Decidido |
| 64 | 2026-10-14 | **Reemplaza No-Intro en §7.2 y §9.4** (V4): como ES-DE, los cartuchos se verifican por hash en línea con ScreenScraper. Los discos se verifican con los DAT de Redump, que se descargan solos. No se usa No-Intro | Decidido |
| 65 | 2026-10-14 | V5: Otteretro pide credenciales de desarrollador propias en ScreenScraper, como ES-DE. Mientras no lleguen, se identifica por nombre, TitleID y Redump | Decidido |
| 66 | 2026-10-14 | V6: HowLongToBeat es un dato opcional; si falla, se muestra "sin datos" | Decidido |
| 67 | 2026-10-14 | Se confirman los supuestos S1 a S7 tal como estaban | Decidido |
| 68 | 2026-10-14 | Un solo token de GitHub, cifrado, sirve para las releases (Velopack) y para `otteretro-datos` | Decidido |
| 69 | 2026-10-14 | Carga inicial: **todas las sagas**, en orden Nintendo → Sony → Microsoft. Los scripts bajan primero todos los datos de las APIs; Claude solo juzga, por lotes de varias sagas, para gastar los mínimos tokens. La cola se pausa al llegar al límite de la suscripción y continúa sola | Decidido |
| 70 | 2026-10-14 | Git LFS con presupuesto 0: si se supera la cuota gratis, se bloquea hasta el mes siguiente y la app avisa | Decidido |
| 71 | 2026-10-14 | PS4: la ingesta extrae el `.pkg` con una herramienta externa (receta) y guarda el juego extraído, porque shadPS4 ya no instala `.pkg` | Decidido |
| 72 | 2026-10-14 | **Varios emuladores por consola**: uno predeterminado y alternativos instalables, tomando como referencia las recomendaciones de GordoFriki (Claude las revisa al escribir las recetas). Switch: Eden predeterminado, más Ryubing, Citron y Ryujinx SS4K | Decidido |
| 73 | 2026-10-14 | Epic: se acepta el método no oficial para leer la cuenta completa; si se rompe, se cae a solo lo instalado | Decidido |
| 74 | 2026-10-14 | Orden de trabajo: especificación de pantallas → Claude Design (sistema de diseño y pantallas) → arquitectura → plan por fases → código | Decidido |
| 75 | 2026-10-14 | Las comprobaciones periódicas (precios, tiendas, versiones nuevas de emuladores) se hacen **solo con la app abierta**: al abrirla y cada cierto intervalo mientras está abierta. Con la app cerrada no corre nada; los avisos llegan la próxima vez que la abras | Decidido |
| 76 | 2026-10-14 | **Reemplaza la #64**: los cartuchos se verifican **sin conexión** con los DAT de No-Intro que publica libretro en GitHub (`libretro-database`, sin captcha) y los discos con Redump. Ambos se descargan solos. ScreenScraper queda para identificar lo que no coincida y para medios | Decidido |
| 77 | 2026-10-14 | Un solo PC activo a la vez. Si hay un conflicto en `otteretro-datos`, gana lo más reciente y lo otro se guarda aparte, sin perderlo | Decidido |
| 78 | 2026-10-14 | Se acepta que el respaldo no incluya binarios de emuladores, packs de texturas ni juegos: se vuelven a conseguir (los emuladores, con sus recetas) | Decidido |
| 79 | 2026-10-14 | Especificación de pantallas en `docs/pantallas.md`. D1: búsqueda global con Ctrl+K | Decidido |
| 80 | 2026-10-14 | D2: Inicio con orden fijo (Continuar, Revisar, Novedades, En curso, Avisos) | Decidido |
| 81 | 2026-10-14 | D3: Revisar es una pantalla propia, con contador en la barra lateral | Decidido |
| 82 | 2026-10-14 | D4 y D4b: Colección en cuadrícula con alternativa de lista. La tarjeta muestra lo tengo, consola, año, progreso, versión PC, Game Pass, cómo se juega y tamaño | Decidido |
| 83 | 2026-10-14 | D5: puntuación de 1 a 10 | Decidido |
| 84 | 2026-10-14 | **Precisa la #57** (D6, D6b): se avisa de los juegos de las sagas que sigues que no tienes y de los marcados "seguir precio", cuando llegan al mínimo histórico, quedan gratis o tienen un 40 % o más de descuento | Decidido |
| 85 | 2026-10-14 | D7: "No me interesa" oculta la recomendación; pasado un tiempo vuelve a poder recomendarse (S8: 6 meses, configurable) | Decidido |
| 86 | 2026-10-14 | D8: las bibliotecas se gestionan en Ajustes | Decidido |
| 87 | 2026-10-14 | D9: al lanzar un juego solo aparece una ventana si hay que preparar algo o si falla | Decidido |
| 88 | 2026-10-14 | D10: nombre oficial en español si existe; si no, el original | Decidido |
| 89 | 2026-10-14 | **Precisa §10** (D10b): en Steam se usa el mismo nombre que en la app, con "(Consola)" | Decidido |
| 90 | 2026-10-14 | **Precisa §12.2** (D11): la nutria aparece en el icono, pantallas vacías, avisos, la animación de carga, cuando Claude trabaja y al terminar un juego o completar una ruta | Decidido |
| 91 | 2026-10-14 | Se aprueba la primera versión del sistema de diseño y del prototipo navegable de pantallas en Claude Design ("es exactamente lo que pensaba") | Decidido |
| 92 | 2026-10-14 | Arquitectura en `docs/arquitectura.md`. A1: acceso a datos con Dapper + SQL y migraciones en archivos .sql numerados | Decidido |
| 93 | 2026-10-14 | A2: el progreso en vivo llega a la interfaz con Server-Sent Events | Decidido |
| 94 | 2026-10-14 | A3: los tipos del front se generan desde la API (OpenAPI → TypeScript) | Decidido |
| 95 | 2026-10-14 | A4: pruebas de lo delicado (mover, verificar, respaldar, rutas internas, regla de cómo jugar) y una de cada flujo clave | Decidido |
| 96 | 2026-10-14 | A5: registro de errores en archivo dentro de la carpeta de datos, 30 días | Decidido |
| 97 | 2026-10-14 | A6: cada fusión en main publica una release con versión calculada sola | Decidido |
| 98 | 2026-10-14 | A7: un solo proyecto C# con una carpeta por módulo, más uno de pruebas | Decidido |
| 99 | 2026-10-14 | A8: .NET 10 (LTS). Se aceptan las decisiones marcadas como (propia) en la arquitectura | Decidido |
| 100 | 2026-10-14 | Plan por fases en `docs/plan.md`, con el **catálogo primero**: F0 esqueleto, F1 catálogo, F2 Claude, F3 bibliotecas, F4 emuladores/ports/Jugar, F5 respaldo, F6 Steam, F7 tiendas, F8 ingesta, F9 Descubrir y pulido | Decidido |
| 101 | 2026-10-14 | **Precisa la #97 y el plan**: cada fase se trabaja en su rama `fase/N-nombre` desde `dev` y se fusiona en `dev` con un pull request. Por ahora no se fusiona nada en `main`, así que no hay releases publicadas; F0 prueba Velopack con paquetes locales | Decidido |
| 102 | 2026-10-15 | La autoactualización se ve: aparece como tarea en el panel de Tareas, muestra un aviso con **Reiniciar ahora** cuando la versión está lista y Ajustes › Acerca de tiene un botón **Buscar actualización** junto a la versión. Sigue instalándose sola al cerrar la app | Decidido |
