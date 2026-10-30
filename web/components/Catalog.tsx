"use client";

// Piezas que comparten las pantallas del catálogo (F1.9): portadas, tarjetas, consolas y el id de la URL.
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useTranslations } from "next-intl";
import { imageUrl, request } from "@/lib/client";
import type { ConsoleCount, GameCard, SagaCard } from "@/lib/types";

/** Las 24 consolas con su nombre y cuántos juegos hay de cada una. */
export const useConsoles = () => useQuery({ queryKey: ["consoles"], queryFn: () => request<ConsoleCount[]>("/api/consoles") });

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

/** Portada 2:3. Sin imagen de SteamGridDB, el nombre sobre el fondo. */
export function Cover({ gameId, name, hasCover, style }: { gameId: number | string; name: string; hasCover: boolean; style?: React.CSSProperties }) {
  return hasCover ? (
    <div className="ot-cover" style={{ backgroundImage: `url("${imageUrl(gameId, "cover")}")`, backgroundSize: "cover", backgroundPosition: "center", ...style }} role="img" aria-label={name} />
  ) : (
    <div className="ot-cover" style={style}>{name}</div>
  );
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
      <Cover gameId={id} name={game.name} hasCover={game.hasCover} />
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
