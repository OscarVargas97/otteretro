# Mapa funcional de Otteretro

Qué pantalla o comando dispara cada función, qué endpoint llama, qué módulo la resuelve y qué tablas o archivos toca. Rutas relativas a `src/Otteretro/` (backend) y `web/` (front).

**Vivo**: se actualiza en el mismo PR que agrega o cambia una pantalla, un endpoint, un comando o una tabla. Al día con F0 (2026-10-16).

## Arranque y modos del ejecutable

| Comando | Qué hace | Módulo |
|---|---|---|
| `otteretro` | Abre la app: una sola instancia, servidor local y ventana | `Inicio/Program.cs` → `Inicio/Arranque.cs`, `Inicio/UnaInstancia.cs`, `Inicio/Servidor.cs`, `Inicio/Ventana.cs` |
| `otteretro servidor` | API e interfaz sin ventana, para el navegador (desarrollo) | `Inicio/Modos.cs` → `Inicio/Servidor.cs` |
| `otteretro jugar <id>` | Todavía no implementado (F4): registra el aviso y sale con código 2 | `Inicio/Modos.cs` |
| `otteretro version` · `ayuda` · `openapi <archivo>` | Versión, ayuda y contrato OpenAPI para `npm run tipos` | `Inicio/Modos.cs` |

Al arrancar: migraciones de `Datos/Migraciones/` (`Datos/Migraciones.cs`) y registro del equipo actual en `equipo`. Logs: `Inicio/Registro.cs`, en `logs/` de la carpeta de datos (antes de elegirla, junto a `config.json`).

## Pantallas

| Pantalla (`web/app/`) | Qué muestra o hace | Endpoints | Backend | Datos |
|---|---|---|---|---|
| Todas (`layout.tsx` → `componentes/Marco.tsx`) | Barra lateral, barra superior con panel de tareas, aviso de actualización; lleva al asistente si falta la carpeta de datos | `GET /api/estado`, `GET /api/eventos` (SSE), `GET /api/actualizacion`, `GET /api/carpeta-datos/mudanza` | `Api/Endpoints.cs`, `Api/Eventos.cs` | `config.json` |
| `bienvenida/` | Asistente pasos 1 y 2: elegir la carpeta de datos (pasos 3 a 7 deshabilitados) | `GET /api/discos`, `POST /api/carpeta-datos` | `Inicio/CarpetaDatos.cs`, `Inicio/Configuracion.cs` | `config.json`; crea `data/`, `cache/imagenes/`, `Herramientas/`, `logs/` |
| `ajustes/` · carpeta de datos (`componentes/CarpetaDatos.tsx`) | Ver la carpeta, usar otra o mudarla con progreso | `GET /api/carpeta-datos`, `GET /api/discos`, `POST /api/carpeta-datos`, `POST /api/carpeta-datos/mover`, evento `mudanza` | `Inicio/Mudanza.cs` (copiar, verificar, cambiar `config.json`, borrar el origen) | carpeta de datos completa, `config.json` |
| `ajustes/` · versión (`componentes/Actualizacion.tsx`) | Versión, buscar actualización y reiniciar para aplicarla | `GET /api/actualizacion`, `POST /api/actualizacion/buscar`, `POST /api/actualizacion/reiniciar`, evento `actualizacion` | `Inicio/Actualizador.cs` (Velopack) | `origenActualizaciones` de `config.json` |
| `page.tsx` (Inicio), `sagas/`, `saga/?id=`, `coleccion/`, `juego/?id=`, `descubrir/`, `revisar/`, `descargas/`, `herramientas/`, `steam/` | Estado vacío con la nutria y la fase que la llena (`Proximamente`) | — | — | — |

## API (`Api/Endpoints.cs`)

Todo bajo `/api`, solo en `127.0.0.1` y con la clave de la sesión (`Inicio/Servidor.cs`). El front llama siempre con `web/lib/cliente.ts`; los tipos salen de `web/lib/api.d.ts`.

| Endpoint | Para qué |
|---|---|
| `GET /api/estado` | Versión, carpeta de datos y si está configurada |
| `GET /api/carpeta-datos` · `POST /api/carpeta-datos` | Ver la carpeta o usar otra (sin mover nada) |
| `GET /api/carpeta-datos/mudanza` · `POST /api/carpeta-datos/mover` | Estado de la mudanza o empezarla (una a la vez) |
| `GET /api/discos` | Unidades con espacio libre |
| `GET /api/actualizacion` · `POST …/buscar` · `POST …/reiniciar` | Actualización de la app |
| `GET /api/eventos` | SSE: `latido` cada 15 s, `mudanza`, `actualizacion` |

## Datos

| Tabla o archivo | Qué guarda | Quién escribe |
|---|---|---|
| `%LocalAppData%\Otteretro\config.json` | Carpeta de datos y origen de actualizaciones (escritura atómica) | `Inicio/Configuracion.cs` |
| `data/otteretro.db` · `equipo` | Equipos donde se usa la app | `Datos/Migraciones.cs` al arrancar |
| `data/otteretro.db` · `ajuste` | Ajustes globales o por equipo (todavía sin uso) | — |
| `data/otteretro.db` · `migracion` | Migraciones aplicadas | `Datos/Migraciones.cs` |
