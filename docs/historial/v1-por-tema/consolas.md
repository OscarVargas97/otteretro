# Requisito: consolas del sistema

- **Estado:** decidido
- **Fecha:** 2026-10-14
- **Fuente:** [cuestionario de consolas](https://claude.ai/artifact/H5MavLVrPF5n9or29Mi3wa) (respuestas en el doc `cuestionario/consolas`)

## Reglas de agrupación

| Pregunta | Decisión |
|---|---|
| Add-ons (64DD, FDS, Mega-CD…) | Van **dentro de su consola base**. Por ejemplo, 64DD es parte de N64. |
| Variantes (GBC, DSi, New 3DS) | Van **juntas** con su consola principal. |
| Consolas sin emulación madura | **Se catalogan igual**. Sirven para registrar sagas y "dónde jugarlo" (nativo en PC, remaster, otra consola). |

## Consolas incluidas (12 Nintendo · 7 Sony · 4 Microsoft = 23 sistemas)

### Nintendo
| Sistema | Incluye | Emulador de referencia | Emulación |
|---|---|---|---|
| NES / Famicom | | Mesen | ✅ |
| Game Boy | Game Boy Color | SameBoy / Gambatte | ✅ |
| Super Nintendo | | bsnes / Snes9x | ✅ |
| Virtual Boy | | Mednafen | ✅ |
| Nintendo 64 | 64DD | ares / simple64 + ports | 🟢 |
| Game Boy Advance | | mGBA | ✅ |
| GameCube | | Dolphin + ports | ✅ |
| Nintendo DS | DSi / DSiWare | melonDS | 🟢 |
| Wii | | Dolphin | ✅ |
| Nintendo 3DS | New 3DS | Azahar | 🟢 |
| Wii U | | Cemu | 🟢 |
| Switch | | Ryubing / Ryujinx | 🟢 |

### Sony
| Sistema | Emulador de referencia | Emulación |
|---|---|---|
| PlayStation | DuckStation + ports | ✅ |
| PlayStation 2 | PCSX2 + OpenGOAL | 🟢 |
| PSP | PPSSPP | ✅ |
| PlayStation 3 | RPCS3 | 🟢 |
| PS Vita | Vita3K | 🟡 |
| PlayStation 4 | shadPS4 + bbport | 🟡 |
| PlayStation 5 | SharpEmu / KytyPS5 (+ AnyPS5, recomp) | 🟡 temprana |

> **PS5, corregido gracias a tu nota:** sí se puede emular, pero apenas está empezando. En septiembre de 2026, SharpEmu tenía 6 juegos jugables a 60 FPS (Dreaming Sarah, Terrarium, Tetris Forever, Hoa, Tomb Raider IV-VI Remastered, New Joe & Mac). KytyPS5 ya llega a la parte jugable en algunos juegos 3D (XIII, TMNT: Wrath of the Mutants), a unos 25-30 FPS y con fallos gráficos. Los AAA (Demon's Souls, Astro Bot…) todavía no son jugables. Cuidado con las webs que ofrecen "emuladores de PS5" con soporte completo: son estafas.
> Fuentes: [Tom's Hardware](https://www.tomshardware.com/video-games/playstation/ps5-emulation-hits-new-milestone-in-record-time-multiple-3d-games-now-playable-in-kyty-performance-and-support-steadily-improving-with-30-fps-gameplay-possible-today) · [Yahoo Tech / SharpEmu](https://tech.yahoo.com/gaming/articles/sharpemu-now-run-six-ps5-132610192.html) · [FpsGen](https://fpsgen.com/guides/ps5-emulator-pc-2026)

### Microsoft
| Sistema | Emulador de referencia | Emulación |
|---|---|---|
| Xbox | xemu | 🟢 |
| Xbox 360 | Xenia Canary + recomps | 🟡 |
| Xbox One | ninguno | ❌ (solo se cataloga) |
| Xbox Series X\|S | ninguno | ❌ (solo se cataloga) |

## Fuera del sistema (49 sistemas)

Estas quedaron sin marcar en el cuestionario. Para sumar alguna después, hay que añadir una entrada en [decisiones.md](../decisiones.md).

### Nintendo (5)
| Sistema | Año | Motivo / nota |
|---|---|---|
| Game & Watch | 1980 | No marcada |
| Famicom Disk System | 1986 | No marcada. Si se incluye algún día, va dentro de NES (regla de add-ons) |
| Satellaview | 1995 | No marcada. Si se incluye algún día, va dentro de SNES (regla de add-ons) |
| Pokémon Mini | 2001 | No marcada |
| Switch 2 | 2025 | No marcada. Todavía no tiene emulación |

### Sega (8)
SG-1000 (1983) · Master System / Mark III (1985) · Mega Drive / Genesis (1988) · Game Gear (1990) · Mega-CD / Sega CD (1991) · 32X (1994) · Saturn (1994) · Dreamcast (1998)

### NEC / Hudson (4)
PC Engine / TurboGrafx-16 (1987) · PC Engine CD (1988) · SuperGrafx (1989) · PC-FX (1994)

### SNK (3)
Neo Geo AES/MVS (1990) · Neo Geo CD (1994) · Neo Geo Pocket / Color (1998)

### Atari (5)
Atari 2600 (1977) · Atari 5200 (1982) · Atari 7800 (1986) · Atari Lynx (1989) · Atari Jaguar / Jaguar CD (1993)

### Otras (8)
Intellivision (1979) · ColecoVision (1982) · Vectrex (1982) · Odyssey² / Videopac (1978) · Philips CD-i (1991) · 3DO (1993) · WonderSwan / Color (1999) · N-Gage (2003)

### Arcade (6)
Arcade clásico/MAME · Capcom CPS1/2/3 · Sega NAOMI / Atomiswave · Sega Model 2 · Sega Model 3 · Taito Type X / arcade basado en PC

### Computadoras retro (7)
MSX / MSX2 · Commodore 64 · Amiga · ZX Spectrum · Amstrad CPC · PC-98 · DOS

> Ojo con algunas sagas que nacieron en estos sistemas: **Sonic** (Mega Drive), **Metal Gear** (MSX), **Street Fighter** (arcade CPS) o **Castlevania Rondo of Blood** (PC Engine CD). Si alguna de esas sagas tiene entregas en consolas incluidas, se cataloga solo por esas entregas.

## Consecuencias para el sistema

- El estado de la emulación **no se fija a mano**: es un dato que se actualiza (lo hace hoy `_Herramientas/Catalogo/compat.mjs`), porque cambia rápido, como acaba de pasar con PS5.
- Un sistema puede tener **varios emuladores candidatos**, por ejemplo SharpEmu y KytyPS5, o Ryubing y Ryujinx SS4K. Cuál usar se decide por juego.
