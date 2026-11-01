"use client";

// Piezas que comparten las pantallas del catálogo (F1.9): portadas, tarjetas, consolas y el id de la URL.
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { imageUrl } from "@/lib/client";
import { consolesQuery } from "@/lib/queries";
import type { ConsoleCount, GameCard, SagaCard } from "@/lib/types";

/** Las 24 consolas con su nombre y cuántos juegos hay de cada una. */
export const useConsoles = () => useQuery(consolesQuery);

/** Nombre de una consola a partir de su id («n64» → «Nintendo 64»). */
export function useConsoleName() {
  const consoles = useConsoles().data;
  return (id: string) => consoles?.find((c) => c.id === id)?.name ?? id;
}

/** `?id=` de la URL (la exportación estática no admite rutas con parámetros). */
export function useIdParam() {
  return useSearchParams().get("id");
}

/** Tiempo jugado a partir de minutos: «14 h», «25 min» por debajo de una hora, o «—» si no hay. */
export function hours(minutes: number | string | null | undefined) {
  const m = Number(minutes ?? 0);
  return m <= 0 ? "—" : m < 60 ? `${m} min` : `${Math.round(m / 60)} h`;
}

// Tono de cada consola (#169): la portada que aún no se bajó es un degradado de su color.
const CONSOLE_HUE: Record<string, number> = {
  nes: 0, gb: 80, snes: 265, vb: 350, n64: 140, gba: 240, gc: 270, ds: 205, wii: 195, "3ds": 355, wiiu: 190, switch: 358, switch2: 358,
  ps1: 220, ps2: 228, psp: 232, ps3: 215, vita: 205, ps4: 222, ps5: 212, xbox: 110, x360: 100, xone: 120, xseries: 125, pc: 30, pcvr: 30,
};

/** Portada 2:3. Sin imagen de SteamGridDB, el nombre sobre un degradado del color de su consola. */
export function Cover({ gameId, name, hasCover, platform, style }: { gameId: number | string; name: string; hasCover: boolean; platform?: string | null; style?: React.CSSProperties }) {
  if (hasCover)
    return <div className="ot-cover" style={{ backgroundImage: `url("${imageUrl(gameId, "cover")}")`, backgroundSize: "cover", backgroundPosition: "center", ...style }} role="img" aria-label={name} />;
  const hue = platform ? CONSOLE_HUE[platform] : undefined;
  const tint = hue === undefined ? undefined : { background: `linear-gradient(160deg, hsl(${hue} 45% 34%), hsl(${hue} 35% 14%))` };
  return <div className="ot-cover" style={{ ...tint, ...style }}>{name}</div>;
}

export function Platforms({ ids }: { ids: string[] }) {
  const name = useConsoleName();
  return <>{ids.map((p) => <span key={p} className="ot-console">{name(p)}</span>)}</>;
}

/** Tarjeta de juego (componente GameCard del sistema de diseño). */
export function GameTile({ game }: { game: GameCard }) {
  const t = useTranslations("catalog");
  const id = Number(game.id);
  return (
    <Link className={`ot-game${game.owned ? "" : " is-missing"}${game.platforms.length === 0 ? " is-unplayable" : ""}`} href={`/game/?id=${id}`} style={{ textDecoration: "none", color: "var(--ink)" }}>
      <Cover gameId={id} name={game.name} hasCover={game.hasCover} platform={game.platforms[0]} />
      <p className="ot-game-name" title={game.name}>{game.name}</p>
      <div className="ot-game-meta">
        <Platforms ids={game.platforms} />
        {game.year && <span>{game.year}</span>}
        {game.stores.length > 0 && <span>{game.stores.map((s) => t(`store.${s as "steam"}`)).join(" · ")}</span>}
        {Number(game.playtime) > 0 && <span>{hours(game.playtime)}</span>}
      </div>
      <div className="ot-game-badges">
        {game.platforms.length === 0 ? <span className="ot-badge">{t("noConsole")}</span> : game.owned ? <span className="ot-badge ot-badge-have">{t("owned")}</span> : <span className="ot-badge">{t("missing")}</span>}
        {game.status && game.status !== "pending" && <span className="ot-badge ot-badge-info">{t(`status.${game.status as "playing"}`)}</span>}
      </div>
    </Link>
  );
}

/** Tarjeta de saga (componente SagaCard). La id 0 es «Emulación», los juegos sin saga. */
export function SagaTile({ saga }: { saga: SagaCard }) {
  const t = useTranslations("catalog");
  const id = Number(saga.id);
  const name = id === 0 ? t("emulation") : saga.name;
  const cover = saga.coverGameId ? { backgroundImage: `url("${imageUrl(saga.coverGameId, "cover")}")`, backgroundSize: "cover", backgroundPosition: "center 20%" } : undefined;
  return (
    <Link className="ot-saga" href={`/saga/?id=${id}`} style={{ textDecoration: "none", color: "var(--ink)" }}>
      <div className="ot-saga-art" style={cover}><span className="saga-art-name">{name}</span></div>
      <div className="ot-saga-body">
        <div className="ot-saga-stats">
          <span>{id === 0 ? t("noSaga") : [saga.favorite && `★ ${t("favorite")}`, saga.following && t("following")].filter(Boolean).join(" · ") || " "}</span>
          {Number(saga.playtime) > 0 && <span>{hours(saga.playtime)}</span>}
        </div>
        <div className="ot-saga-stats">
          <span>{t("games", { count: Number(saga.games) })}</span>
          <span>{t("ownedCount", { count: Number(saga.owned) })}</span>
        </div>
        <div className="chips" style={{ gap: 4 }}><Platforms ids={saga.platforms} /></div>
      </div>
    </Link>
  );
}
