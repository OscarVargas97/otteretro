# Novedades

Lo que trae cada versión de Otteretro, lo más nuevo arriba.

## 0.3.0 · Carpetas y herramientas · en desarrollo

`fase/f3-carpetas`, `feat/146-pausar-cancelar-tareas`, `feat/148-almacenamiento-por-tipo`, `fix/151-pestanas-centradas`, `feat/157-discos-como-steam`, `feat/163-canal-local`, `feat/165-version-por-pr` y `feat/167-datos-en-disco` · F3 (#141): tus juegos y herramientas en la estructura definitiva antes de construir lo demás encima · sale como prerelease en el canal Pruebas mientras la fase sigue abierta.

### Nuevo
- **Canal Local** en Ajustes › General › Actualizaciones (#163): además de Estable y Pruebas, los paquetes que armas en este equipo. Su carpeta se recuerda aparte de Otro origen, así vuelves a ella de un clic.
- **Discos, como en Steam** (#157): en Ajustes › Almacenamiento eliges discos y Otteretro arma solo en cada uno `X:\Otteretro` con Juegos, Emuladores, Ports, Complementos y Sistema. Ves cuánto ocupa cada tipo en una barra, marcas juegos, emuladores o la carpeta de sistema de una consola y los mueves a otro disco. El primero es el **predeterminado ★**: ahí va lo nuevo y lo de Herramientas.
- **Pasar al estándar** (#157): `E:\Consolas` pasa a `E:\Otteretro\Juegos` (en el mismo disco, sin copiar nada) y Herramientas, al disco predeterminado, con las rutas de los emuladores y los enlaces al día. Adoptar una carpeta de antes lo hace solo.
- **Almacenamiento por tipo**, en Ajustes › Almacenamiento (#148): juegos, emuladores, ports, complementos y sistema (BIOS, keys) tienen cada uno sus **ubicaciones**, en cualquier disco o en la red. Adopta la carpeta donde ya tienes tus juegos o crea una nueva. Cada ubicación muestra su espacio libre y si está conectada; se puede renombrar, mover y quitar (quitar nunca borra archivos). Si un disco cambia de letra o vuelve a conectarse, la ubicación se encuentra sola.
- **La consola como centro**, en Ajustes › Consolas (#148): al elegir una ves juntos sus juegos (en qué ubicación), sus emuladores, sus ports con el juego que cubren, sus complementos y su sistema, y lo que le falta. Ahí eliges a qué ubicación van sus juegos nuevos.
- **Adoptar `E:\Consolas` la ordena sola**: los juegos quedan en `Consola\Juego`, Dolphin, Ryujinx SS4K y Dusklight pasan a Herramientas con su versión, y las texturas y packs, a Complementos. Sus rutas internas se ponen al día, así que siguen encontrando tus juegos y partidas.
- **Mover una ubicación, una consola o un emulador** a otro disco (la consola, desde su detalle en Ajustes › Consolas; un emulador, port o complemento, desde Herramientas a otra ubicación de su tipo): se copia, se verifica cada archivo y recién entonces se borra el origen. Si se corta, sigue donde quedó. Las rutas de los emuladores y los enlaces de texturas y partidas se actualizan solos.
- **Herramientas** ya se abre: pestañas Emuladores, Ports, Complementos y Sistema, con cada instalación, su versión, su consola y, en los ports, el juego que cubren.
- **Paso 4 de la bienvenida**: encuentra la carpeta con la estructura antigua y ofrece adoptarla.
- **Pausar, reanudar y cancelar tareas** desde el panel de tareas (#146): una tarea pausada, como la carga con Claude, se queda quieta con lo que ya hizo hasta que la reanudas, y el horario de Claude no lanza otra encima. Cancelar pide confirmar y conserva lo hecho; una mudanza cancelada no toca el origen.

### Cambios
- **La versión avanza con cada cambio** (#165): cada PR que entra a dev sube el último número (0.3.8, 0.3.9…), en vez de quedarse en 0.3.0 hasta la release. La prerelease se llama 0.3.N-dev y la estable, 0.3.N.
- El menú muestra las fases nuevas del plan: Steam llega en la F8, Descargas en la F10 y Descubrir en la F11.
- **Almacenamiento nunca aparece vacío** (#167): el disco donde están los datos de la app se agrega solo y es el ★. Los discos se eligen en un desplegable, como en Steam, y «+» abre una ventana para sumar otro. Los datos de la app se ven y se llevan a otro disco desde ahí: se mueven solo los datos, los juegos se quedan. La pestaña «Carpeta de datos» ya no está.
- **Ajustes en una sola columna centrada** (#151): siguen las pestañas de arriba y el contenido va al medio, sin los bloques en dos columnas. Herramientas, igual: pestañas Emuladores, Ports, Complementos y Sistema.

## 0.2.1 · Pulido de UI · en desarrollo

`fase/f2.5-pulido`, `fix/128-prueba-f25`, `feat/130-ajustes-menu` y `feat/132-bajar-portadas` · F2.5 es un extra de F2, así que sale como 0.2.1 (antes se numeró 0.3.0) · lo que entra para la próxima release; sale como prerelease en el canal Pruebas.

### Cambios
- **La barra de arriba queda fija** al bajar, y en una saga o un juego muestra el camino («Sagas › saga › juego») para volver con un clic.
- **Un solo aviso de claves**: dice todos los servicios que no tienen clave y lleva a Cuentas y claves.
- **Las portadas que aún no bajan** se ven con el color de su consola, no grises.
- **Descubrir, Descargas, Herramientas y Steam** se ven atenuadas en el menú con la fase en que llegan, y no se abren.
- **Cada pantalla vacía** dice qué hacer y lleva ahí con un botón.
- Los botones que son enlaces ya no van subrayados.
- **Inicio**: «Siguiente en tus rutas» es una tarjeta por ruta con la saga, la ruta, cuánto llevas y «Ver ficha»; el bloque Revisar aparece solo si hay algo por aprobar.
- **Sagas**: los filtros quedan fijos al bajar y dicen cuántas sagas hay; cada tarjeta muestra si es favorita, icónica o exclusiva de una empresa, y una barra con cuántos juegos tienes («3 de 14»).
- **Saga**: la ruta se elige con pestañas junto al título, con el avance al lado; los pasos van unidos por una línea, con su número sobre la portada y lo jugado a color. En la tabla, las consolas van abreviadas (hasta 4 y «+N»), y «Pedir a Claude que la recalcule» pasó al menú «⋯» del encabezado.
- **Juego**: el seguimiento es más compacto (el estado en un control de una línea; fechas, nota y tiempo en una fila; las notas plegadas). Versiones y comparativa son una sola tabla, con cada puntaje como barra de 5 y el mejor de cada criterio resaltado. En «Datos y fuentes», un lápiz junto a cada valor para corregirlo.
- **Colección**: la lista de consolas muestra solo las que tienen algo según el filtro, agrupadas por empresa, y Cuadrícula/Lista es un control con íconos.
- **Revisar**: una ruta por aprobar se ve con las portadas de sus juegos numeradas y la razón de cada paso.
- **Panel de tareas**: cada tarea con su barra de progreso, el estado como insignia de color y una ✕ para quitar una terminada.
- **Bienvenida** con la nutria y los 7 pasos, con la fase en que llega cada uno.
- **Menú rápido del mando**: además de «Ir a Steam», Inicio, Buscar, Tareas y Ajustes.
- **Ajustes › Claude**: arriba del registro de uso, el total de la página (llamadas, tokens y costo).
- **Ajustes usa el ancho de la pantalla**: con espacio, sus bloques van en dos columnas (`feat/130-ajustes-menu`).
- **El menú lateral se retrae** a una franja de íconos con su botón de abajo o Ctrl+B, y la app lo recuerda (`feat/130-ajustes-menu`).
- **Bajar portadas a pedido**: en Ajustes › Consolas, el bloque «Portadas» dice cuántos juegos tienen portada y con «Bajar portadas» la descarga vuelve a la cola si se cortó (`feat/132-bajar-portadas`).

### Arreglos
- **Abrir una saga o un juego empieza arriba**: antes la ventana se quedaba a la altura de la pantalla anterior; «Atrás» sigue volviendo a donde estabas (`fix/128-prueba-f25`).
- **Sagas ya no falla** con «getNextPageParam is not a function» al volver a la lista después de haberla bajado (`fix/128-prueba-f25`).

## 0.2.0 · Claude ordena el catálogo

`fase/f2-claude` y `feat/114-releases` · primera release publicada en GitHub, desde `main`

### Nuevo
- **Claude revisa las sagas dudosas y las ordena solo**: une fichas repetidas del mismo juego, pone en su saga los juegos sueltos que son de ella, separa sub-sagas (como Tingle) y corrige nombres. Primero tus sagas. Se lanza con `otteretro catalogo depurar`; con `--contar` dice cuántos casos hay y cuánto gastaría, sin llamar a Claude. Cada cambio se puede deshacer con `otteretro catalogo deshacer <n>`.
- **Claude trabaja de noche, sin quitarte cuota**: al empezar su horario (00:00 a 08:00, editable) se lanza solo y usa la ventana entera; si llega al límite de la suscripción, espera y sigue solo. De día, «Ejecutar ahora» en Ajustes › Claude (o `otteretro claude ahora`) lo lanza al instante; con «Con tope» marcado se queda en el porcentaje que elijas de cada ventana (la mitad, por defecto).
- **Claude prepara cada saga para que la apruebes**: las tres rutas (lanzamiento, historia y la que recomienda la comunidad, con sus fuentes), si es icónica y sus juegos icónicos, la mejor versión de cada juego con los cuatro criterios, las versiones mal clasificadas (remaster o remake) y la descripción en español. Nada se aplica sin ti: todo queda por aprobar en Revisar. Las descripciones de IGDB ahora se guardan al cargar el catálogo.
- **Revisar**: apruebas o rechazas lo que preparó Claude, y en Aprendizajes deshaces cualquier cambio que hizo al catálogo.
- **La ruta de cada saga**: en la saga eliges la ruta (lanzamiento, historia o comunidad) y la ves como línea de tiempo, con qué versión recomienda la comunidad, por qué ese orden y sus fuentes. Puedes pedirle a Claude que la recalcule.
- **Inicio te dice tu siguiente juego** de cada ruta que sigues, y cuánto espera en Revisar.
- **La ficha compara las versiones** con los cuatro criterios por separado y marca la recomendada; muestra la descripción en español.
- **Ajustes › Claude**: horario, tope de día opcional, «Ejecutar ahora», qué está haciendo Claude y el registro de cada llamada.
- **La carga inicial con Claude corre sola durante días**: `otteretro claude cargar` recorre Nintendo, Sony, Microsoft y PC; en cada parte primero ordena el catálogo y después prepara las sagas. Se pausa al llegar al límite de la suscripción y sigue sola, y en el panel de tareas ves en qué parte va. Con `--parte` corre solo una.
- **Cada tarea dice qué hizo**: al hacer clic en una tarea del panel ves su registro: cuándo empezó o esperó y por qué, cada llamada a Claude con sus tokens, cada cambio que aplicó la depuración (con su número para deshacerlo), lo que dejó en Revisar y cada imagen que bajó con su peso.
- **Ves y corriges lo que cambió Claude**: en Revisar › Aprendizajes cada cambio dice qué juegos movió y de qué saga a cuál, qué fichas unió o cómo se llamaba la saga, agrupado por tarea y con filtros. Si algo está mal, «Corregir» lo deshace y tu nota queda como regla de esa saga: Claude la cumple en la próxima depuración.
- **Panel de tareas más claro**: arriba lo que corre, después la cola y al final lo terminado, lo más reciente primero; y «Limpiar terminadas» las borra.
- **Consolas que habilitas tú**: en Ajustes › Consolas marcas o desmarcas cada consola, y «Agregar consola» busca entre todas las de IGDB. Al habilitar una, se cargan sus juegos; las deshabilitadas no se vuelven a cargar.
- **Catálogo al día solo**: cada semana (los días se cambian en Ajustes › Consolas) la app busca en IGDB lo nuevo o cambiado de cada consola habilitada y lo suma. «Actualizar ahora» lo hace al instante.
- **Reevaluar sagas icónicas**: en Ajustes › Claude, Claude vuelve a decidir qué sagas son icónicas, de todas las consolas o de una.
- **Sagas exclusivas**: el filtro «Exclusivas» muestra las sagas que solo salieron en consolas de una empresa (una versión de PC no les quita lo exclusivo). Se combina con la empresa o la consola, que ahora se elige en una lista.
- **Más reconocidas**: el orden por defecto de Sagas. Primero las icónicas que aprobaste y después las más calificadas en IGDB, contando solo los juegos de la consola elegida. Al actualizar, una tarea trae la popularidad de los juegos que ya tenías.
- **Tu catálogo y tus datos, separados**: el catálogo (sagas, juegos, versiones, rutas y consolas) vive en `catalogo/catalogo.db` junto con sus imágenes (`catalogo/imagenes`), y lo tuyo (lo que tienes, progreso, favoritas, claves, ajustes y tareas) sigue en `data/otteretro.db`. Así el catálogo se puede pasar a otra instalación sin nada tuyo. Al actualizar se separa solo y deja una copia de la base de antes (`data/otteretro.antes-del-catalogo.db`).
- **Revisar tiene contador** en la barra lateral, y una ruta se puede **editar antes de aprobarla**: subir, bajar o quitar pasos.
- **La nutria celebra** cuando terminas un juego y cuando con eso completas la ruta que sigues.
- **Sugerencias de automatizar** (Ajustes › Claude): cuando Claude hace 10 veces el mismo cambio por la misma razón, la app lo sugiere y «Crear issue» lo deja en GitHub con la etiqueta `automatizar` (usa `gh`).
- **No paga dos veces**: lo que Claude ya revisó no se vuelve a pedir mientras sus datos no cambien.

### Cambios
- **Las actualizaciones llegan de GitHub**: Ajustes › General › Actualizaciones elige de dónde: **Estable** (las versiones publicadas, por defecto), **Pruebas** (lo último en desarrollo) u **Otro origen** (una carpeta o el repo de quien arma su propia versión). Al cambiar, busca de inmediato. Las versiones ahora dicen qué traen: 0.2.0 es la fase 2.
- **La saga y el juego con su imagen**: la cabecera de una saga muestra el arte de su juego icónico más reciente (o del más reciente, si no hay icónicos), y la del juego, el suyo; el color de la pantalla sale de esa imagen. Aprobar las icónicas ahora guarda también qué juegos son icónicos (los ya aprobados se recuperan solos al actualizar).
- **Ajustes en 5 pestañas**: Carpeta de datos, Cuentas y claves, Consolas, Claude y General. **Cuentas y claves** es una lista: una fila por servicio con para qué sirve, su estado («Guardada» en verde, «Falta» en naranja) y «Configurar»; lo de fases posteriores queda en una línea gris al final. **Carpeta de datos** tiene «Abrir carpeta», «Mover…» como botón principal y «Usar otra carpeta» como enlace con una línea que explica la diferencia; el aviso de la mudanza se cierra. **General** junta el idioma (dos botones, Español | English, también en el asistente) con la versión y «Buscar actualización»; las direcciones de antes (`?s=language`, `?s=about`) llevan a ella.
- **Ajustes › Claude en tres bloques**: Horario («Corre de 00:00 a 08:00» y, con el interruptor, «De día, parar al 50 % de cada ventana de 5 h», todo se guarda solo, sin botón), Ahora (qué está haciendo, «Ejecutar ahora» y reevaluar icónicas) y Uso. El registro es más compacto: cuándo («hace 2 horas»), tarea, modelo, duración, tokens de entrada y salida y el costo de cada llamada; «ok» ya no se muestra y un error va en rojo con su motivo.
- **Ajustes › Consolas más claro**: arriba, cada cuántos días se pone al día el catálogo, cuándo fue la última vez y cuándo es la próxima, con «Actualizar ahora». Abajo, las consolas en filas por empresa (Nintendo, PlayStation, Xbox, PC) con su interruptor y sus juegos a la derecha. La fecha de carga aparece una sola vez, y una consola solo lleva insignia si difiere: «Sin cargar», «Carga incompleta» o «Cargando 40 %». Los días se guardan solos.
- **Pestañas sin barra**: en Ajustes y Revisar las pestañas van en una sola línea; las que no caben pasan a «Más ▾». Ajustes ya no repite el nombre de la pestaña como título y sus paneles no ocupan todo el ancho. Campos y botones tienen la misma altura en toda la app, con un tamaño chico para las acciones dentro de una lista.
- **Los cambios se ven al tocar**: Favorita y Seguir, el estado, la nota y las fechas de un juego, las consolas habilitadas y la ruta elegida cambian en el acto y se guardan por detrás; ningún botón queda gris esperando. Si el guardado falla, vuelven a como estaban y la app avisa.
- **Nada dice «Cargando…» ni salta**: el menú y la barra aparecen apenas abre la app; mientras llegan los datos, cada pantalla muestra su forma (tarjetas, filas, cabecera) y todo aparece junto, sin empujar lo de abajo. Lo que ya viste vuelve al instante, y al pasar el ratón o el mando por una saga, un juego o una pestaña, sus datos se piden antes del clic.
- **Sagas, una saga, Colección y Revisar › Aprendizajes cargan al instante, aunque el catálogo sea enorme**: llegan los primeros 60 y el resto aparece al bajar, sin espera. Las consultas ahora usan índices, y lo que pide recorrer el disco (las portadas, cuánto ocupa la carpeta de datos) se mide una vez y se recuerda; mientras se mide, Ajustes dice «Midiendo lo que ocupa».
- **Letras más claras y coherentes**: una sola escala de tamaños en toda la app, «← Sagas» y «← saga» se leen como botón, las consolas en letra normal (no en pixel y mayúsculas), las tablas sin subrayados, un solo título en Saga y Juego, «No tienes ninguno» y «Tiempo jugado: —» en una línea.
- **Barras de scroll delgadas y oscuras**, y la página ya no cambia de ancho cuando la barra aparece o desaparece (por ejemplo, al cambiar de pestaña en Ajustes).
- **Ajustes por pestañas**, una sección a la vez. Cada pestaña tiene su dirección, así «Abrir Ajustes» desde un aviso lleva directo a Cuentas y claves.
- **Colección**: «Lo tengo» queda marcado al entrar y el número de cada consola sigue al filtro (lo que tienes, lo que te falta o lo que tiene versión de PC).
- **Las sagas abren al instante**: abrir una saga ya no calcula las 5.800.
- **`catalogo depurar --contar` estima con lo real**: usa lo que gastó cada llamada a Claude (la primera depuración gastó unas tres veces lo que se estimaba, sobre todo en respuesta).

### Arreglos
- **El menú lateral queda fijo** al bajar por una página larga.
- **Ya no aparecen dos «Bajar portadas de SteamGridDB» a la vez**: si una corre, la carga de IGDB no encola otra; lo que entra mientras tanto se baja después, desde donde terminó.
- **Sagas abre al instante y se recorre sin esperas**: dibuja de a 60 sagas y agrega más antes de que llegues al final, en vez de las 5.800 juntas.
- **El registro de uso de Claude va de a 10 llamadas**, con «Anterior» y «Siguiente», y llega a todo el historial (antes mostraba las últimas 50 juntas).
- **La búsqueda de consolas en IGDB se puede cerrar** (Ajustes › Consolas), con «Cerrar» o con Escape.
- **Un corte al separar el catálogo ya no deja la app sin arrancar**: cada paso escribe un solo archivo y, si se interrumpe, se rehace al abrirla de nuevo.
- **Deshacer una saga creada por la depuración** también borra lo tuyo de esa saga (favorita, seguimiento).
- **El registro de una tarea** se pone al día cuando la tarea avanza, sin preguntar cada 3 segundos.
- **Mudar la carpeta de datos ya no se queda en «Preparando»**: si la mudanza terminaba antes de que llegara la respuesta de haberla empezado, la pantalla volvía a «empezó» y no mostraba el resultado.

## Portadas que no se cortan · en revisión

`fix/78-sgdb-respuesta-vacia`

### Arreglos
- **La descarga de portadas ya no se corta por una respuesta vacía**: si SteamGridDB (o cualquier otra fuente) responde sin contenido, la tarea lo reintenta más tarde en vez de quedar en error, y esa respuesta vacía no se guarda. Lo que ya había quedado guardado así se vuelve a pedir.

## Imágenes livianas · en revisión

`feat/64-imagenes-webp`

### Cambios
- **Las imágenes ocupan unas 13 veces menos**: portadas, fondos y logos se guardan en WebP y al tamaño que muestra la app. Las que ya estaban bajadas se convierten solas en segundo plano, sin volver a descargarlas. En la carpeta de prueba, 532 imágenes pasaron de 556 MB a 42 MB.

## Juegos de PC populares · en revisión

`feat/62-pc-populares`

### Nuevo
- **Los juegos de PC más conocidos entran al catálogo**: los 1.500 con más calificaciones en IGDB (triple A e indies famosos), cada uno en su saga y con las mismas reglas que el resto. Vienen con la carga completa (`otteretro catalogo cargar`) o solos con `--pc-populares`.

## Catálogo · en revisión

`fase/f1-catalogo` · PR #60

### Nuevo
- **La base ya tiene lugar para el catálogo**: sagas, juegos, versiones de consola y PC, y el seguimiento de cada juego (estado, fechas, nota y notas). Viene con las 23 consolas y PC cargadas. Todavía no hay juegos: llegan con los importadores de esta fase.
- **Panel de tareas con trabajo de verdad**: lo que corre en segundo plano se ve avanzar en el panel, una tarea a la vez por tipo. Si cierras la app a mitad de camino, al abrirla sigue desde donde quedó. Las tareas largas avisan con una notificación del sistema al terminar. Para probarlo: `otteretro tareas prueba`.
- **Carga del catálogo desde IGDB**, por saga o por consola y por partes: si se corta, sigue donde quedó. Se pide con `otteretro catalogo cargar` (todas las consolas), `--consola snes` o `--saga zelda`. Los remakes, remasters y ports quedan como versiones del juego original; el nombre en español cuando IGDB lo tiene, y el id de Steam de las versiones de PC. PC se carga solo por saga. Necesita la cuenta de IGDB (Twitch) en Ajustes › Cuentas y claves.
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
- **El catálogo trae solo lo que es de cada saga**: fuera los juegos cancelados o sin fecha, y los invitados (Smash Bros. o Mario Kart ya no aparecen en Zelda porque Link sale en ellos). Los ports y las ediciones («DX», «Definitive», «Nintendo Switch 2 Edition») y los juegos con el mismo nombre en la misma saga quedan como versiones del original. Los nombres usan un solo formato de guiones y espacios. **Switch 2 y PC VR** se suman como consolas, y lo que no tiene ninguna consola del catálogo (Game & Watch, CD-i…) se ve atenuado en su saga con «Sin consola en Otteretro».
- **Las sub-sagas tienen su lugar**: Counter-Strike ya no aparece dentro de Half-Life, sino en su propia saga con CS 2 y Condition Zero. Lo mismo con otras series que IGDB guarda dentro de una franquicia mayor.
- **Un corte de internet no arruina una carga**: si una fuente no responde o pide esperar, la tarea se reintenta sola a los 1, 5 y 15 minutos desde donde iba, y el panel lo dice.
- **Portadas sin equivocarse de juego**: sin id de Steam, de SteamGridDB solo se usa un juego con el mismo nombre y año; si no lo hay, la portada es la carátula oficial de IGDB.
- **Cada juego muestra su propia portada**: cuando un juego se fusiona con su original, sus imágenes se borran para que no las herede otro juego, y la ventana ya no se queda con una imagen vieja si cambió.
- **Cuentas y claves, más corta y clara**: una tarjeta por servicio con su estado y un botón «Configurar» que abre sus campos; lo que se usa en fases posteriores (GitHub, ScreenScraper, MyJDownloader, RetroAchievements) va plegado en «Más adelante». GitHub ya no dice «actualizaciones»: solo se usará para el respaldo de partidas.
- **La carpeta de datos no se muda mientras corre una tarea**: la app pide esperar a que termine.

### Arreglos
- **Las claves guardadas no se pierden** si `config.json` se edita a mano y no trae el id del equipo.
- **Un remake con seguimiento** que pasa a ser versión de su original lleva su estado y notas al original, sin dejar un juego vacío.

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
