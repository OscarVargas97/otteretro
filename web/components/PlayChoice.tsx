"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useFormatter, useTranslations } from "next-intl";
import { useEffect, useState } from "react";
import { hours, useConsoleName } from "@/components/Catalog";
import { GameProfiles, Recommended, useRecommended } from "@/components/GameProfiles";
import { PortRow } from "@/components/Ports";
import { useTasks } from "@/components/Tasks";
import { installRecipe, recipesQuery } from "@/components/Tools";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import type { GameDetail, PlayOrder, PlaySession, PortInfo, VersionPlay } from "@/lib/types";

type Criterion = "pc" | "port" | "perfect" | "playable";
type Option = VersionPlay["options"][number];

const playQuery = (gameId: number) => ({ queryKey: ["play", gameId], queryFn: () => request<VersionPlay[]>(`/api/games/${gameId}/play`) });
const portsQuery = (gameId: number) => ({ queryKey: ["ports", "game", gameId], queryFn: () => request<PortInfo[]>(`/api/games/${gameId}/ports`) });

function useLabel() {
  const t = useTranslations("play");
  const consoleName = useConsoleName();
  return (o: Option) => o.kind === "pc" ? t("pc", { platform: consoleName(o.name) }) : t(o.kind === "port" ? "port" : "emulator", { name: o.name });
}

/**
 * El botón principal de la ficha (#400, #410): «Descargar» si no lo tienes, «Instalar X» si la opción elegida falta y «Jugar» si
 * está todo; «Jugando…» mientras la sesión sigue abierta. Debajo, con qué se abre y «Cambiar», que lleva a «Cómo jugar».
 */
export function PlayButton({ game, owned, onChange }: { game: GameDetail; owned: boolean; onChange: () => void }) {
  const t = useTranslations("play");
  const format = useFormatter();
  const describe = useDescribeError();
  const label = useLabel();
  const cache = useQueryClient();
  const gameId = Number(game.id);
  // Lanzar no espera al juego: unos segundos dice «Jugando…» hasta que aparece la sesión (por Steam, solo si se vio arrancar el juego: decisión 386).
  const [launching, setLaunching] = useState(false);
  useEffect(() => {
    if (!launching) return;
    const timer = setTimeout(() => setLaunching(false), 15000);
    return () => clearTimeout(timer);
  }, [launching]);
  const play = useQuery({ ...playQuery(gameId), enabled: owned });
  const sessions = useQuery({
    queryKey: ["sessions", gameId], queryFn: () => request<PlaySession[]>(`/api/games/${gameId}/sessions`), enabled: owned,
    refetchInterval: (q) => (launching || q.state.data?.some((s) => s.playing) ? 2000 : false),
  });
  const tasks = useTasks();
  const install = useMutation({
    mutationFn: (o: Option) => o.kind === "port"
      ? request<number>(`/api/ports/${o.key.slice(5)}/install`, { method: "POST", body: JSON.stringify({}) })
      : installRecipe(o.key.slice(9)),
    onSuccess: () => cache.invalidateQueries({ queryKey: ["tasks"] }),
  });
  const start = useMutation({
    mutationFn: (version: number) => request<void>(`/api/versions/${version}/play`, { method: "POST" }),
    onSuccess: () => { setLaunching(true); cache.invalidateQueries({ queryKey: ["sessions", gameId] }); },
  });

  if (!owned) return (
    <div className="play-box">
      <Link className="ot-btn ot-btn-primary" href={`/downloads/?${new URLSearchParams({ game: String(game.id), name: game.nameEs })}`}>{t("download")}</Link>
      <span className="small muted">{t("notOwned")}</span>
    </div>
  );

  const optionOf = (v: VersionPlay) => v.options.find((o) => o.key === v.chosen);
  const versions = play.data ?? [];
  // shortcut: con varias versiones, la primera lista para jugar (o la primera con algo elegido), no la mejor; si molesta, que lo diga la comparativa.
  const v = versions.find((x) => optionOf(x)?.installed) ?? versions.find((x) => x.chosen);
  const chosen = v && optionOf(v);
  const task = install.isSuccess ? tasks.find((x) => String(x.id) === String(install.data)) : undefined;
  const installing = install.isPending || (install.isSuccess && (!task || task.status === "queued" || task.status === "running"));
  const playing = launching || !!sessions.data?.some((s) => s.playing);
  const last = sessions.data?.find((s) => !s.playing);
  const failure = install.error ?? start.error;
  if (play.isPending) return <div className="play-box"><button className="ot-btn ot-btn-primary" type="button" disabled>{t("play")}</button></div>;

  return (
    <div className="play-box">
      {!v || !chosen
        ? <button className="ot-btn ot-btn-primary" type="button" disabled>{t("play")}</button>
        : !chosen.installed
          ? (
            <button className="ot-btn ot-btn-primary" type="button" disabled={installing} onClick={() => install.mutate(chosen)}>
              {installing ? t("installing", { name: chosen.name }) : t("install", { name: chosen.name })}
            </button>
          )
          // La versión de PC se abre como ella misma (por Steam si está exportada).
          : (
            <button className="ot-btn ot-btn-primary" type="button" disabled={playing || start.isPending}
              onClick={() => start.mutate(chosen.kind === "pc" ? Number(chosen.key.slice(3)) : Number(v.versionId))}>
              {playing ? t("playing") : t("play")}
            </button>
          )}
      <span className="small muted">
        {!v || !chosen ? <>{t("noOption")} <Link href="/tools/">{t("openTools")}</Link></> : (
          <>
            {t("with", { name: label(chosen) })}{v.reason && ` · ${t(`reason.${v.reason as Criterion | "manual"}`)}`}{" · "}
            <button className="link-button" type="button" onClick={onChange}>{t("change")}</button>
          </>
        )}
      </span>
      {playing
        ? <span className="small muted" role="status">{t("playingHint")}</span>
        : last && <span className="small muted">{t("lastSession", { date: format.dateTime(new Date(last.startedAt), { dateStyle: "medium", timeStyle: "short" }), time: hours(Math.round(Number(last.duration ?? 0) / 60)) })}</span>}
      {failure && <span className="error small" role="alert">{describe(failure)}</span>}
    </div>
  );
}

/**
 * Pestaña «Cómo jugar» (#398, #410): una tarjeta por versión que tienes con sus opciones (un clic elige), «Mejor con otra versión»
 * (ports y emuladores recomendados de versiones que no tienes), los ports que se instalan a mano y los perfiles en «Avanzado».
 */
export function HowToPlay({ gameId, consoles, owned }: { gameId: number; consoles: string[]; owned: string[] }) {
  const t = useTranslations("play");
  const versions = useQuery(playQuery(gameId));
  const ports = useQuery(portsQuery(gameId)).data?.filter((p) => p.status !== "no-port") ?? [];
  const recipes = useQuery(recipesQuery).data ?? [];
  const missing = consoles.filter((c) => !owned.includes(c) && recipes.some((r) => r.kind === "emulator" && r.consoles.includes(c)));
  const recommended = useRecommended(gameId, missing);
  const better = ports.filter((p) => !owned.includes(p.console));
  // Los de una consola que tienes sin receta ni instalados no son opción: se instalan con la URL de su release.
  const byHand = ports.filter((p) => owned.includes(p.console) && !p.recipeId && !p.installed.length);
  const shown = versions.data?.filter((v) => v.options.length > 0) ?? [];
  const recommendedIds = recipes.filter((r) => r.recommendedFor.some((g) => Number(g.gameId) === gameId)).map((r) => `emulator:${r.id}`);
  return (
    <>
      {shown.length > 0 && (
        <section className="sec">
          <div className="sec-h"><h2>{t("title")}</h2><Link className="small" href="/settings/?s=play">{t("orderLink")}</Link></div>
          <p className="small muted" style={{ margin: 0 }}>{t("help")}</p>
          {shown.map((v) => <VersionChoice key={String(v.versionId)} gameId={gameId} version={v} recommended={recommendedIds} />)}
        </section>
      )}
      {(better.length > 0 || recommended.length > 0) && (
        <section className="sec" aria-label={t("better")}>
          <div className="sec-h"><h2>{t("better")}</h2><span className="small muted">{t("betterHint")}</span></div>
          <div className="panel" style={{ display: "grid", gap: 12 }}>
            {recommended.map((x) => <Recommended key={x.console} shown={x} />)}
            {better.length > 0 && <ul className="detail-list">{better.map((p) => <PortRow key={p.id} port={p} />)}</ul>}
          </div>
        </section>
      )}
      {byHand.length > 0 && (
        <section className="sec" aria-label={t("byHand")}>
          <div className="sec-h"><h2>{t("byHand")}</h2></div>
          <div className="panel"><ul className="detail-list">{byHand.map((p) => <PortRow key={p.id} port={p} />)}</ul></div>
        </section>
      )}
      <GameProfiles gameId={gameId} />
    </>
  );
}

function VersionChoice({ gameId, version: v, recommended }: { gameId: number; version: VersionPlay; recommended: string[] }) {
  const t = useTranslations("play");
  const consoleName = useConsoleName();
  const describe = useDescribeError();
  const label = useLabel();
  const cache = useQueryClient();
  const url = `/api/games/${gameId}/play/${v.versionId}`;
  const change = useMutation({
    mutationFn: (key: string | null) => request<VersionPlay[]>(url, key === null ? { method: "DELETE" } : { method: "PUT", body: JSON.stringify({ key }) }),
    onSuccess: (data) => cache.setQueryData(["play", gameId], data),
  });
  const chosen = v.options.find((o) => o.key === v.chosen);
  return (
    <div className="panel" style={{ gap: 8, marginTop: 12 }}>
      <div className="sec-h">
        <b>{consoleName(v.platform)}</b>
        {v.reason === "manual" && (
          <span className="sentence small">
            <span className="ot-badge ot-badge-info">{t("reason.manual")}</span>
            <button className="ot-btn ot-btn-sm" type="button" disabled={change.isPending} onClick={() => change.mutate(null)}>{t("reset")}</button>
          </span>
        )}
      </div>
      <div role="radiogroup" aria-label={t("chooseFor", { console: consoleName(v.platform) })} style={{ display: "grid", gap: 6 }}>
        {v.options.map((o) => (
          <button key={o.key} type="button" role="radio" className="opt" aria-checked={o.key === v.chosen} disabled={change.isPending}
            onClick={() => o.key !== v.chosen && change.mutate(o.key)}>
            <span className="radio" aria-hidden="true" />
            <span className="grow"><b>{label(o)}</b><span className="small muted" style={{ display: "block" }}>{t(`criterion.${o.criterion as Criterion}`)}</span></span>
            {recommended.includes(o.key) && <span className="ot-badge ot-badge-have">{t("recommended")}</span>}
            {!o.installed && <span className="ot-badge">{t("notInstalled")}</span>}
          </button>
        ))}
      </div>
      {chosen && !chosen.installed && <p className="small muted" style={{ margin: 0 }}>{t("installNote", { name: chosen.name })}</p>}
      {change.isError && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(change.error)}</p>}
    </div>
  );
}

/** Ajustes › Cómo jugar (#398, §8.3): el orden de los criterios de la regla automática. */
export function PlayOrderSettings() {
  const t = useTranslations("play");
  const describe = useDescribeError();
  const cache = useQueryClient();
  const order = useQuery({ queryKey: ["playOrder"], queryFn: () => request<PlayOrder>("/api/play/order") });
  const save = useMutation({
    mutationFn: (criteria: string[]) => request<PlayOrder>("/api/play/order", { method: "PUT", body: JSON.stringify({ criteria }) }),
    onSuccess: (data) => cache.setQueryData(["playOrder"], data),
  });
  const criteria = order.data?.criteria ?? [];
  const move = (i: number, to: number) => {
    const next = [...criteria];
    [next[i], next[to]] = [next[to], next[i]];
    save.mutate(next);
  };
  return (
    <section className="panel">
      <h2>{t("order")}</h2>
      <p className="small muted" style={{ margin: 0 }}>{t("orderHelp")}</p>
      <ol>
        {criteria.map((c, i) => (
          <li key={c} className="sentence">
            <span>{t(`criterion.${c as Criterion}`)}</span>
            <button className="ot-btn ot-btn-sm" type="button" disabled={i === 0 || save.isPending} aria-label={t("up", { criterion: t(`criterion.${c as Criterion}`) })}
              onClick={() => move(i, i - 1)}>↑</button>
            <button className="ot-btn ot-btn-sm" type="button" disabled={i === criteria.length - 1 || save.isPending} aria-label={t("down", { criterion: t(`criterion.${c as Criterion}`) })}
              onClick={() => move(i, i + 1)}>↓</button>
          </li>
        ))}
      </ol>
      {save.isError && <p className="error small" role="alert">{describe(save.error)}</p>}
    </section>
  );
}
