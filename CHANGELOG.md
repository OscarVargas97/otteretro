# Novedades

Lo que trae cada versión de Otteretro, lo más nuevo arriba.

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
