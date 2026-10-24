# Checklist de revisión de Otteretro

Errores propios de este stack que `otter:pr-reviewer` revisa además de su checklist general. Cuando una revisión encuentre un error de un tipo nuevo, se agrega acá en el mismo PR que lo corrige.

## Datos del usuario y archivos

- **Mover es copiar, verificar y recién entonces borrar.** Un cambio que mueve o borra archivos de bibliotecas, emuladores o la carpeta de datos tiene que copiar, forzar la escritura a disco, verificar por hash y solo entonces borrar el origen, y poder deshacerse si algo falla a la mitad (`Inicio/Mudanza.cs` es el modelo).
- **Nunca** se borran versiones viejas de emuladores ni actualizaciones de juegos (BASE/UPD/DLC).
- **Operaciones largas, una a la vez**: si un doble clic o dos pestañas pueden lanzar la misma operación dos veces, tiene que haber un cerrojo (la revisión de F0 encontró dos mudanzas simultáneas que podían borrar origen y destino).
- **Escrituras atómicas**: `config.json` y cualquier archivo que la app reescribe se escriben a un temporal y se reemplazan; un corte no puede dejarlo a medias.
- **Rutas**: rutas de red, discos que no están, mayúsculas en Windows y separadores; en Linux todo puede parecer el mismo disco.

## Backend (C# .NET 10, ASP.NET Core, SQLite con Dapper)

- **SQL**: siempre con parámetros de Dapper, nunca interpolando valores.
- **Migraciones**: un archivo `.sql` nuevo y numerado en `Datos/Migraciones/`; una migración ya publicada no se edita. Aplicarlas dos veces no falla.
- **Transacciones**: leer un estado y escribir según él va en una transacción; SQLite está en WAL con `busy_timeout`, pero dos escrituras que dependen de la misma lectura igual compiten.
- **Servidor local**: todo endpoint nuevo pasa por el middleware de la clave y del `Host` (`Inicio/Servidor.cs`); nada escucha fuera de `127.0.0.1`.
- **Errores**: el registro nunca lanza; un error del usuario (ruta inválida, operación en curso) responde 4xx con un mensaje legible, no 500.
- **API y tipos**: si cambió un endpoint, se regeneraron los tipos del front (`npm run tipos`) y se actualizó `docs/mapa/README.md` en el mismo PR.
- **Claude**: solo con `claude -p`; nunca la API de Anthropic. La salida de Claude se valida contra un esquema antes de guardarla.
- **Lo propio de cada sistema operativo** detrás de su clase; nada de rutas de Windows sueltas en código que también corre en Linux.

## Front (Next.js estático, React, TanStack Query)

- **Exportación estática**: sin rutas dinámicas que no se conozcan al compilar (`/saga/?id=`, no `/saga/[id]`), sin APIs de servidor de Next.
- **Llamadas**: siempre por `web/lib/cliente.ts` (añade la clave); los tipos salen de `web/lib/api.d.ts`, no se escriben a mano.
- **Estados de cada pantalla**: vacío (con la nutria y la acción que la llena), cargando, error y con datos.
- **Progreso en vivo** por SSE (`/api/eventos`), no con sondeos.
- **Sistema de diseño**: clases `ot-` de `web/app/estilos/` (`componentes.css`); nada de estilos sueltos que lo contradigan.
- **Textos**: español neutro, sin voseo ni formas peninsulares.

## Idioma

- Identificadores nuevos en inglés (#125). Comentarios, mensajes y documentación en español.
