# Novedades

Lo que trae cada versión de Otteretro, lo más nuevo arriba.

## Catálogo · en desarrollo

`fase/f1-catalogo` · PR —

### Nuevo
- **La base ya tiene lugar para el catálogo**: sagas, juegos, versiones de consola y PC, y el seguimiento de cada juego (estado, fechas, nota y notas). Viene con las 23 consolas y PC cargadas. Todavía no hay juegos: llegan con los importadores de esta fase.
- **Panel de tareas con trabajo de verdad**: lo que corre en segundo plano se ve avanzar en el panel, una tarea a la vez por tipo. Si cierras la app a mitad de camino, al abrirla sigue desde donde quedó. Las tareas largas avisan con una notificación del sistema al terminar. Para probarlo: `otteretro tareas prueba`.
- **Carga del catálogo desde IGDB**, por saga o por consola y por partes: si se corta, sigue donde quedó. Los remakes, remasters y ports quedan como versiones del juego original; el nombre en español cuando IGDB lo tiene, y el id de Steam de las versiones de PC. PC se carga solo por saga. Necesita la cuenta de IGDB (Twitch) en Ajustes › Cuentas y claves.
- **Portadas, fondos y logos de SteamGridDB**, que se bajan solos después de cada carga y quedan guardados en tu carpeta de datos. Necesita la clave de SteamGridDB en Ajustes › Cuentas y claves.
- **Tus juegos de Steam cuentan como «lo tengo»**, estén instalados o no, con las horas jugadas y la última vez. Los que no estaban en el catálogo se buscan solos en IGDB, y los que IGDB no conoce aparecen igual con su nombre. Se lee con `otteretro steam sincronizar` y la clave de Steam Web API.
- **Otteretro se usa con mando**: la cruceta mueve el foco al elemento más cercano, abajo aparece qué hace cada botón (con la forma de tu mando: Xbox, PlayStation o Switch), X abre la búsqueda, Guía abre el menú rápido con «Ir a Steam» y los campos de texto tienen su propio teclado en pantalla. Al mover el ratón, todo vuelve a la vista normal.
- **Sagas, Colección y la ficha con tus datos**: las sagas con sus consolas y filtros (favoritas, las que sigues, icónicas, por empresa, con juegos de PC), la colección por consola con PC como una más (tienda y horas, y lo de Steam que aún no está en el catálogo), y la ficha de cada juego con sus versiones y dónde lo tienes.
- **Favoritas, seguir una saga y el seguimiento de cada juego**: estado (pendiente, jugando, terminado, 100 % o abandonado) con sus fechas, nota de 1 a 10 y notas. Se guarda al momento.
- **Búsqueda global** con Ctrl+K (o X con el mando): encuentra sagas y juegos por su nombre en español u original.
- **Wikidata y RAWG completan a IGDB**: los juegos que IGDB deja sin saga van a la suya según Wikidata, y el año que falta sale de RAWG si pegas su clave (opcional) en Ajustes › Cuentas y claves. Si dos fuentes no coinciden, gana la de más prioridad que tenga el dato.
- **Inicio avisa qué claves faltan** para cargar el catálogo, con un botón a Ajustes.
- **Lo que corriges a mano no se pisa** al volver a cargar: en la ficha, «Datos y fuentes» muestra qué dijo cada fuente, deja corregir el nombre o el año y volver al de las fuentes.

### Cambios
- **La carpeta de datos no se muda mientras corre una tarea**: la app pide esperar a que termine.

## Diseño del centro de entretenimiento con mando · en revisión

`fase/d1-diseno` · PR #42

### Nuevo
- **Diseño para usar Otteretro con mando desde el sofá**: foco grande, barra con los botones, menú rápido del botón Guía y teclado en pantalla, con un recorrido de ejemplo en el prototipo.
- **Inicio con «Continuar jugando»**: lo último que jugaste, de consola o de PC, antes del siguiente juego de cada ruta. Con sus estados vacío, cargando y error.
- **Juegos de PC en Sagas, Colección y la ficha**, con quién lanza cada juego (Steam, Heroic, Lutris…), «jugar en el PC» y «traer a este equipo».
- **Seguimiento en la ficha**: estado con fechas, nota, notas, tiempo jugado, sesiones y logros.

Por ahora es diseño: se programa a partir de F1.

## Carpeta de datos correcta dentro de la sesión Play · en revisión

`fix/39-carpetas-linux` · PR #40

### Arreglos
- **El asistente ya no propone carpetas que se borran**: abierto desde Steam en PlayOS, ofrecía `/Otteretro` y `/homeOtteretro`, que caían en una memoria temporal y se perdían al cerrar Steam. Ahora sugiere tu carpeta personal (`~/Otteretro`) y no ofrece discos en memoria. En Windows sigue sugiriendo el disco con más espacio libre.

## Otteretro público · en desarrollo

`chore/30-publico` · PR — (apilado sobre `fase/p0-playos`)

### Cambios
- **El código de Otteretro va a ser público**, para que PlayOS lo pueda instalar. La app no cambia.
- **Documentación sin datos personales**: las máquinas se nombran por su rol (el PC de escritorio y el portátil de juego) y las respuestas de las encuestas ya no se guardan en el repo; van en el issue de cada encuesta.

## Botones y buscador que no se mueven · en revisión

`fix/20-buscador` · PR #29 (apilado sobre `fase/p0-playos`)

### Arreglos
- **El buscador se lee entero en los dos idiomas**: el texto de ejemplo ahora es corto («Buscar sagas y juegos…» / «Search sagas and games…») y cabe en el buscador; si algún día no cabe, se corta con «…». La lupa y el atajo ya no se achican.
- **Los botones no cambian de tamaño al usarlos**: «Buscar actualización» y «Reiniciar ahora» muestran la ruedita en su lugar mientras trabajan, en vez de cambiar de texto, así no mueven lo que tienen al lado. Mientras busca, Ajustes dice «Comprobando si hay una versión nueva».
- **Tareas**: el botón dice siempre «Tareas» y el número va en un contador aparte, también con 0, que se distingue del fondo del botón.

### Cambios
- **Botones parejos**: todos los botones miden al menos 96 px de ancho.

## El plan se ve en GitHub · en revisión

`docs/21-plan-en-github` · PR #26 (apilado sobre `fase/p0-playos`)

### Cambios
- **El plan está en el Project**: cada fase es un milestone con lo que trae, lo que se te pide y su «Listo cuando», y lo que viene en cada fase son tarjetas del Project otteretro agrupables por `Fase` (vista «Plan»). Se pueden cambiar o borrar mientras la fase no empieza.
- **Menos documentos que mantener**: `docs/plan-implementacion.md` ya no existe y `docs/plan.md` cabe en una página (el orden de las fases y por qué). Cómo se trabaja y los comandos pasaron al `AGENTS.md`, y el stack a `arquitectura.md`.

## Otteretro en Linux y PlayOS (en curso) · 2026-10-23

`fase/p0-playos` · PR —

### Nuevo
- **Otteretro en inglés**: elige el idioma en Ajustes o en el primer paso del asistente; la interfaz cambia al momento y la próxima vez abre en el mismo. La primera vez usa el idioma de tu sistema. Todos los textos, también los errores, salen en el idioma elegido.
- **Otteretro abre en Linux**: la ventana funciona con WebKitGTK, la configuración va en `~/.config/otteretro/`, los discos que ofrece son los montajes reales (la raíz, `/home` y lo montado en `/mnt`, `/media` o `/run/media`) y mover la carpeta de datos entre montajes copia y verifica antes de borrar. Abrir Otteretro otra vez trae al frente la ventana que ya está abierta.
- **Se instala con Nix**: el repo trae un flake con el paquete de Otteretro (con su entrada en el menú de aplicaciones) y un módulo para NixOS, `programs.otteretro.enable = true;`. Es lo que va a usar PlayOS.
- **Ajustes › Cuentas y claves**: pega las claves de GitHub, Steam, IGDB, ScreenScraper, SteamGridDB, RetroAchievements y MyJDownloader; cada una dice si está guardada o falta, y «Conseguirla» abre en el navegador dónde se saca. Se guardan cifradas y solo en ese equipo (en Windows con el cifrado del sistema; en Linux, con una llave guardada en gnome-keyring). Nunca se vuelven a mostrar. En otro PC, se pegan de nuevo desde Bitwarden. Si en Linux el llavero no responde (por ejemplo, se cancela el aviso para abrirlo), Otteretro avisa y no toca lo guardado.
- **En Linux, Otteretro se actualiza con el sistema**: Ajustes › Acerca de lo dice y no ofrece buscar actualizaciones; llegan con la próxima actualización de PlayOS o NixOS. En Windows sigue igual.
- **Pantalla completa para la sesión Play**: dentro de la sesión de Steam (gamescope) Otteretro abre solo a pantalla completa, o con `Otteretro --fullscreen`. Ahí la barra superior trae «Ir a Steam», que pasa a Big Picture; Otteretro sigue abierto detrás. Si Steam no está instalado, lo dice.

### Cambios
- **Código en inglés**: el código, la API y las tablas de la base de datos pasaron al inglés. La app se ve y funciona igual. Al abrir la versión nueva, la base de datos de tu carpeta de datos se actualiza sola y conserva todo, y la configuración se lee como antes.
- **Las fuentes vienen con la app**: ya no se descargan al compilar. Se ven igual.

## La arquitectura dice que el código va en inglés · en revisión

`docs/27-idioma-arquitectura` · PR #28

### Arreglos
- **Idioma del código**: la arquitectura todavía decía que las clases y carpetas iban en español. Ahora dice lo que ya se decidió: el código, las tablas y las columnas van en inglés, y la interfaz y la documentación en español. No cambia nada en la app.

## Novedades del proyecto · 2026-10-23

`docs/changelog` · PR #15

### Nuevo
- **Este archivo**: cada cambio de Otteretro cuenta acá qué trae, con lo nuevo, los cambios y los arreglos. Empieza con todo lo que ya se hizo.

## Otteretro se trabaja desde un espacio nuevo · 2026-10-17

`chore/espacio-ia-workspace` · PR #14

### Cambios
- **Cómo se trabaja el proyecto**: Otteretro y PlayOS se trabajan juntos desde un espacio de ia-workspace, y las reglas propias de Otteretro viajan con el repo. No cambia nada en la app.
- **Actualizaciones locales**: los paquetes de prueba de la app se movieron a la carpeta del espacio y la app instalada los busca ahí. Se copiaron y verificaron uno por uno antes de borrar los viejos.

## Otteretro va a PlayOS · 2026-10-17

`docs/encuesta-12-playos` · PR #6

### Nuevo
- **Plan nuevo**: antes del catálogo vienen dos fases. *P0 · PlayOS y Linux* hace que Otteretro corra en Linux y sea lo primero que ves en la sesión de juegos de PlayOS. *D1 · Diseño* rediseña las pantallas para usarlas con mando, como un centro de entretenimiento.
- **Lo que se decidió para PlayOS**: los juegos de Heroic, Lutris, itch.io y los sueltos también cuentan; seguimiento propio de lo que juegas (sesiones, fechas, terminado o abandonado); bibliotecas en discos de red; y las versiones de emuladores fijas y compartidas entre Windows y PlayOS.

## Otteretro adopta su forma de trabajo · 2026-10-16

`chore/metodologia` · PR #5

### Cambios
- **Cómo se planifica**: cada idea pasa por una encuesta, de ahí salen las tareas en GitHub, y el avance se sigue en el Project y en los milestones de cada fase. No cambia nada en la app.

## Mudanzas de datos más seguras · 2026-10-16

`fase/0-correcciones` · PR #2

### Arreglos
- **Doble clic en «Mover»**: dos mudanzas de la carpeta de datos a la vez podían borrar el origen y el destino. Ahora solo arranca una.
- **Si fallaba guardar la configuración** a mitad de una mudanza, la app quedaba apuntando a una carpeta y el archivo a otra, y el mensaje no decía dónde estaban los datos. Ahora se deshace la mudanza y el aviso dice dónde quedaron.
- **Verificación de verdad**: cada archivo copiado se escribe en el disco antes de comprobarlo, para no verificar contra la caché de Windows.
- **Al limpiar tras un fallo** solo se borra lo que la mudanza copió, nunca lo que ya estaba en el destino.
- **Buscar actualización** dos veces seguidas ya no se pisa, y la búsqueda se corta si tarda más de un minuto.
- Cambiar la carpeta de datos durante una mudanza ahora avisa que hay una en curso; las rutas de red dan un error legible.

## Primera versión: Otteretro se instala y se actualiza solo · 2026-10-16

`fase/0-esqueleto` · PR #1

### Nuevo
- **Instalador y actualizaciones**: Otteretro se instala como cualquier programa y se actualiza solo. Muestra la descarga como una tarea, avisa cuando hay versión nueva y ofrece **Reiniciar ahora**.
- **La ventana de Otteretro**: con la nutria, la barra lateral y las once pantallas del prototipo (todavía vacías), búsqueda con **Ctrl+K** y un panel con las tareas en curso. Se abre una sola vez aunque hagas doble clic de nuevo.
- **Primer arranque**: un asistente te pide dónde guardar tus datos.
- **Mover tus datos**: en Ajustes puedes llevar la carpeta de datos a otro disco. Otteretro copia, verifica cada archivo y recién entonces borra el original. También puedes apuntar a otra carpeta que ya tengas.
- **Ajustes › Versión**: la versión instalada y el botón **Buscar actualización**.
