"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useFormatter, useLocale, useTranslations } from "next-intl";
import { Suspense, useEffect, useState } from "react";
import { size } from "@/lib/format";
import { Cover, hours, Platforms, useConsoleName, useConsoles, useIdParam } from "@/components/Catalog";
import { useHeroArt } from "@/components/HeroArt";
import { GamePorts } from "@/components/Ports";
import { Empty } from "@/components/Shell";
import { request } from "@/lib/client";
import { PageSkeleton } from "@/components/Skeleton";
import { useDescribeError } from "@/lib/i18n";
import { optimistic } from "@/lib/optimistic";
import { compareQuery, gameQuery } from "@/lib/queries";
import type { CapturedLink, FieldRow, GameDetail, ProgressInfo, RoutesPage, VersionCompare } from "@/lib/types";

const STATUSES = ["pending", "playing", "finished", "completed", "dropped"] as const;

// La ficha de un juego se abre como /game/?id=…
export default function GameScreen() {
  return <Suspense><Game /></Suspense>;
}

/** Ficha (§5): cabecera, seguimiento, versiones, dónde lo tienes (PC) y datos con sus fuentes. */
function Game() {
  const t = useTranslations("catalog");
  const describe = useDescribeError();
  const consoleName = useConsoleName();
  const format = useFormatter();
  const id = useIdParam();
  const game = useQuery({ ...gameQuery(id ?? ""), enabled: id !== null });
  // La comparativa va a la par con la ficha y se muestran juntas: si llegara después, empujaría lo de abajo (#105).
  const compare = useQuery({ ...compareQuery(id ?? ""), enabled: id !== null });
  const hero = useHeroArt(game.data?.hasHero ? Number(game.data.id) : null);

  if (id === null) return <div className="content"><Empty title={t("game.noneTitle")} text={t("game.noneText")}><Link className="ot-btn ot-btn-primary" href="/sagas/">{t("goToSagas")}</Link></Empty></div>;
  if (game.isError) return <div className="content"><p className="error" role="alert">{describe(game.error)}</p></div>;
  if (!game.data || compare.isPending) return <PageSkeleton cover />;

  const g = game.data;
  const platforms = [...new Set(g.versions.map((v) => v.platform))];
  // La traducción aprobada (Claude, F2.5) o, mientras no la haya, la de IGDB.
  const description = (g.fields.find((f) => f.field === "summary_es") ?? g.fields.find((f) => f.field === "summary"))?.value;
  const stores = g.versions.flatMap((v) => v.stores.map((s) => ({ ...s, platform: v.platform })));
  const files = g.versions.flatMap((v) => v.files.map((f) => ({ ...f, platform: v.platform })));
  const owned = stores.length > 0 || files.length > 0;

  return (
    <div className="accented" style={hero.style}>
      <section className="hero">
        {hero.art}
        <Cover gameId={Number(g.id)} name={g.nameEs} hasCover={g.hasCover} platform={platforms[0]} style={{ width: 150, flex: "none" }} />
        <div className="hero-main">
          <h2 className="hero-title">{g.nameEs}</h2>
          {g.originalName && g.originalName !== g.nameEs && <span className="small muted">{g.originalName}</span>}
          <div className="ot-game-meta" style={{ fontSize: 14 }}>
            <Platforms ids={platforms} />
            {g.year && <span>{g.year}</span>}
            {owned ? <span className="ot-badge ot-badge-have">{t("owned")}</span> : <span className="ot-badge">{t("missing")}</span>}
          </div>
          <FavoriteButton game={g} />
        </div>
      </section>
      <div className="content">
        <div className="columns">
          <Tracking game={g} />
          {stores.length > 0 && (
            <section className="sec column">
              <div className="sec-h"><h2>{t("game.where")}</h2><span className="small muted">{hours(g.playtime)}</span></div>
              <div className="panel table-wrap" style={{ padding: 0 }}>
                <table className="t">
                  <thead><tr><th>{t("collection.store")}</th><th>{t("game.version")}</th><th>{t("collection.hours")}</th><th>{t("collection.lastPlayed")}</th></tr></thead>
                  <tbody>
                    {stores.map((s, i) => (
                      <tr key={i}>
                        <td>{t(`store.${s.store as "steam"}`)}</td>
                        <td>{consoleName(s.platform)}</td>
                        <td className="tabular">{hours(s.playtime)}</td>
                        <td>{s.lastPlayed ? format.dateTime(new Date(s.lastPlayed), { dateStyle: "medium" }) : "—"}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          )}
          {files.length > 0 && <Files gameId={Number(g.id)} files={files} />}
        </div>

        {description && (
          <section className="sec">
            <div className="sec-h"><h2>{t("game.description")}</h2></div>
            <p style={{ margin: 0 }}>{description}</p>
          </section>
        )}

        <Versions game={g} compare={compare.data ?? []} />

        <GamePorts gameId={Number(g.id)} />

        <GameLinks gameId={Number(g.id)} />

      </div>
    </div>
  );
}

/** Tus enlaces de este juego (#210): datos privados, en tu base. Volver a bajar lo devuelve al capturador. */
function GameLinks({ gameId }: { gameId: number }) {
  const t = useTranslations("downloads");
  const describe = useDescribeError();
  const cache = useQueryClient();
  const router = useRouter();
  const links = useQuery({ queryKey: ["game-links", gameId], queryFn: () => request<CapturedLink[]>(`/api/games/${gameId}/links`) });
  const again = useMutation({
    mutationFn: (url: string) => request<number>("/api/downloads/links", { method: "POST", body: JSON.stringify({ text: url, gameId }) }),
    onSuccess: () => router.push("/downloads/?tab=links"),
  });
  const open = useMutation({ mutationFn: (url: string) => request<void>("/api/downloads/open", { method: "POST", body: JSON.stringify({ url }) }) });
  const remove = useMutation({
    mutationFn: (id: number) => request<void>(`/api/downloads/links/${id}`, { method: "DELETE" }),
    onSettled: () => cache.invalidateQueries({ queryKey: ["game-links", gameId] }),
  });
  if (!links.data?.length) return null;
  const failure = again.error ?? open.error ?? remove.error;
  return (
    <section className="sec">
      <div className="sec-h"><h2>{t("gameLinks")}</h2><span className="small muted">{t("gameLinksPrivate")}</span></div>
      {failure && <p className="error small" role="alert">{describe(failure)}</p>}
      <div className="panel" style={{ gap: 8 }}>
        {links.data.map((l) => (
          <div key={l.id} className="version-row small">
            <span title={l.url}>{l.name}</span>
            <span className="hero-actions">
              <button className="ot-btn ot-btn-sm" type="button" onClick={() => again.mutate(l.url)}>{t("downloadAgain")}</button>
              <button className="ot-btn ot-btn-sm" type="button" onClick={() => open.mutate(l.url)}>{t("openBrowser")}</button>
              <button className="ot-btn ot-btn-sm" type="button" aria-label={t("removeLink", { name: l.name })} onClick={() => remove.mutate(Number(l.id))}>{t("remove")}</button>
            </span>
          </div>
        ))}
      </div>
    </section>
  );
}

/** Guarda el seguimiento entero y deja la ficha al día. */
function useSaveProgress(game: GameDetail) {
  const cache = useQueryClient();
  return useMutation({
    mutationFn: (progress: ProgressInfo) => request<GameDetail>(`/api/games/${game.id}/progress`, { method: "PUT", body: JSON.stringify(progress) }),
    ...optimistic(cache, ["game", String(game.id)], (d: GameDetail, progress: ProgressInfo) => ({ ...d, progress })),
    onSuccess: (data) => {
      cache.setQueryData(["game", String(game.id)], data);
      cache.invalidateQueries({ queryKey: ["collection"] });
      cache.invalidateQueries({ queryKey: ["saga"] });
    },
  });
}

function FavoriteButton({ game }: { game: GameDetail }) {
  const t = useTranslations("catalog");
  const save = useSaveProgress(game);
  const p = game.progress;
  return (
    <div className="hero-actions">
      <button className="ot-btn" type="button" aria-pressed={p.favorite} onClick={() => save.mutate({ ...p, favorite: !p.favorite })}>
        {t("game.favorite")}
      </button>
      <Link className="ot-btn" href={`/downloads/?${new URLSearchParams({ game: String(game.id), name: game.nameEs })}`}>{t("game.download")}</Link>
    </div>
  );
}

/** Seguimiento (#133): estado, fechas solo cuando aplican, nota de 1 a 10 y notas. Cada cambio se guarda al momento. */
function Tracking({ game }: { game: GameDetail }) {
  const t = useTranslations("catalog");
  const format = useFormatter();
  const describe = useDescribeError();
  const save = useSaveProgress(game);
  const p = game.progress;
  const [notes, setNotes] = useState(p.notes ?? "");
  useEffect(() => setNotes(p.notes ?? ""), [p.notes]);
  const change = (patch: Partial<ProgressInfo>) => save.mutate({ ...p, ...patch });
  const today = new Date().toISOString().slice(0, 10);
  // La nutria celebra (D11) al terminar un juego y, si con eso completas la ruta que sigues, la ruta.
  const [cheer, setCheer] = useState<string | null>(null);
  const isDone = (s?: string | null) => s === "finished" || s === "completed";
  const setStatus = (s: (typeof STATUSES)[number]) => {
    const wasDone = isDone(p.status);
    save.mutate({ ...p, status: s, startedAt: s !== "pending" ? (p.startedAt ?? today) : p.startedAt, finishedAt: ["finished", "completed", "dropped"].includes(s) ? (p.finishedAt ?? today) : p.finishedAt }, {
      onSuccess: async () => {
        if (!isDone(s) || wasDone) return;
        let text = t("game.cheerGame", { name: game.nameEs });
        if (game.sagaId != null) {
          const r = await request<RoutesPage>(`/api/sagas/${game.sagaId}/routes`).catch(() => null);
          const route = r?.routes.find((x) => x.id === r.chosenRoute);
          if (route?.steps.some((st) => st.gameId === game.id) && route.steps.every((st) => isDone(st.status))) text = t("game.cheerRoute", { saga: game.sagaName ?? "" });
        }
        setCheer(text);
      },
    });
  };
  const withStart = p.status !== "pending";
  const withEnd = p.status === "finished" || p.status === "completed" || p.status === "dropped";

  return (
    <section className="sec column">
      <div className="sec-h">
        <h2>{t("game.tracking")}</h2>
        <span className="small muted">{game.lastPlayed ? t("game.lastPlayed", { date: format.dateTime(new Date(game.lastPlayed), { dateStyle: "medium" }) }) : t("game.never")}</span>
      </div>
      <div className="panel" style={{ gap: 14 }}>
        {/* Estado en un control de una línea (#171). */}
        <div className="segmented" role="radiogroup" aria-label={t("game.states")}>
          {STATUSES.map((s) => (
            <button key={s} type="button" role="radio" aria-checked={p.status === s} aria-pressed={p.status === s}
              onClick={() => setStatus(s)}>
              {t(`status.${s}`)}
            </button>
          ))}
        </div>
        {cheer && (
          <div className="cheer" role="status">
            <img src="/nutria.png" alt="" width={48} height={48} />
            <b>{cheer}</b>
            <button className="ot-btn" type="button" onClick={() => setCheer(null)}>{t("game.cheerClose")}</button>
          </div>
        )}
        <div className="fields-row">
          {withStart && (
            <label className="small muted field">{t("game.started")}
              <input type="date" value={p.startedAt ?? ""} onChange={(e) => change({ startedAt: e.target.value || null })} />
            </label>
          )}
          {withEnd && (
            <label className="small muted field">{t(`game.${p.status as "finished"}`)}
              <input type="date" value={p.finishedAt ?? ""} onChange={(e) => change({ finishedAt: e.target.value || null })} />
            </label>
          )}
          <label className="small muted field">{t("game.rating")}
            <select value={p.rating ?? ""} onChange={(e) => change({ rating: e.target.value ? Number(e.target.value) : null })}>
              <option value="">{t("game.noRating")}</option>
              {Array.from({ length: 10 }, (_, i) => <option key={i + 1} value={i + 1}>{i + 1}</option>)}
            </select>
          </label>
          {/* Fechas, nota y tiempo en una fila (#171). */}
          <div className="small muted field">{t("game.playtime")}<span className="stat-value field-value">{hours(game.playtime)}</span></div>
        </div>
        {!withStart && <p className="small muted" style={{ margin: 0 }}>{t("game.pendingHint")}</p>}
        {/* Notas plegadas (#171); abiertas si ya hay. */}
        <details className="fold-line" open={!!p.notes}>
          <summary className="small muted">{t("game.notes")}</summary>
          <textarea id="notes" aria-label={t("game.notes")} value={notes} maxLength={10000} onChange={(e) => setNotes(e.target.value)}
            onBlur={() => notes !== (p.notes ?? "") && change({ notes: notes || null })} />
        </details>
        <p className="small muted" role="status" style={{ margin: 0, minHeight: 18 }}>
          {save.isPending ? t("game.saving") : save.isError ? <span className="error">{describe(save.error)}</span> : save.isSuccess ? t("game.saved") : ""}
        </p>
      </div>
    </section>
  );
}

/** Puntaje de 1 a 5 como barra de 5 tramos (#171). */
const CRITERIA = ["content", "gameplay", "technical", "original"] as const;

function Score({ value, best }: { value: number; best: boolean }) {
  return (
    <span className={`score${best ? " is-best" : ""}`} role="img" aria-label={`${value}/5`}>
      {Array.from({ length: 5 }, (_, i) => <span key={i} className={i < value ? "on" : undefined} />)}
    </span>
  );
}

/** Versiones y comparativa en una sola tabla (§5.1, §6.4, #171): lo que tienes de cada una y los 4 criterios que aprobaste
 *  en Revisar, con el mejor de cada columna resaltado. */
function Versions({ game, compare }: { game: GameDetail; compare: VersionCompare[] }) {
  const t = useTranslations("catalog");
  const consoleName = useConsoleName();
  const rated = compare.length > 1 && compare.some((v) => v.criteria.some((c) => c.value));
  const scoreOf = (versionId: number | string, k: string) => Number(compare.find((v) => String(v.versionId) === String(versionId))?.criteria.find((x) => x.criterion === k)?.value ?? 0);
  const top = Object.fromEntries(CRITERIA.map((k) => [k, Math.max(0, ...game.versions.map((v) => scoreOf(v.id, k)))]));
  return (
    <section className="sec">
      <div className="sec-h"><h2>{t("game.versions")}</h2>{compare.length > 1 && !rated && <span className="small muted">{t("game.noCompare")}</span>}</div>
      <div className="panel table-wrap" style={{ padding: 0 }}>
        <table className="t">
          <thead>
            <tr><th>{t("game.version")}</th><th>{t("game.have")}</th>{rated && CRITERIA.map((c) => <th key={c}>{t(`game.criterion.${c}`)}</th>)}</tr>
          </thead>
          <tbody>
            {game.versions.map((v) => {
              const cmp = compare.find((x) => String(x.versionId) === String(v.id));
              return (
                <tr key={v.id}>
                  <td><b>{consoleName(v.platform)}</b> · {t(`kind.${v.kind as "original"}`)}{v.year ? ` · ${v.year}` : ""}{cmp?.best && <span className="ot-badge ot-badge-have" style={{ marginLeft: 6 }}>{t("game.best")}</span>}</td>
                  <td>{v.stores.length ? <span className="ot-badge ot-badge-have">{v.stores.map((s) => t(`store.${s.store as "steam"}`)).join(" · ")}</span> : v.files.length ? <span className="ot-badge ot-badge-have">{t("owned")}</span> : <span className="ot-badge">{t("missing")}</span>}</td>
                  {rated && CRITERIA.map((k) => {
                    const c = cmp?.criteria.find((x) => x.criterion === k);
                    const value = Number(c?.value ?? 0);
                    return <td key={k} className="score-cell">{value ? <><Score value={value} best={value === top[k]} /><span className="small muted">{c?.text}</span></> : "—"}</td>;
                  })}
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </section>
  );
}

/** Tus archivos de este juego (#178), con BASE, UPD y DLC, y «Verificar» por hash contra No-Intro o Redump (#179). */
function Files({ gameId, files }: { gameId: number; files: (GameDetail["versions"][number]["files"][number] & { platform: string })[] }) {
  const t = useTranslations("catalog");
  const locale = useLocale();
  const describe = useDescribeError();
  const consoleName = useConsoleName();
  const consoles = useConsoles().data;
  const cache = useQueryClient();
  // Solo si algún archivo es de una consola con DAT (#179): en PC o Switch no hay contra qué comparar.
  const verifiable = files.some((f) => consoles?.find((c) => c.id === f.platform)?.hasDat);
  // El ejecutable de un juego de PC suelto (#182).
  const executable = useMutation({
    mutationFn: ({ id, path }: { id: number; path: string }) => request<void>(`/api/sources/${id}/executable`, { method: "PUT", body: JSON.stringify({ path }) }),
    onSuccess: () => cache.invalidateQueries({ queryKey: ["game", String(gameId)] }),
  });
  const verify = useMutation({
    mutationFn: () => request<number>("/api/collection/verify", { method: "POST", body: JSON.stringify({ gameId }) }),
    onSuccess: () => cache.invalidateQueries({ queryKey: ["tasks"] }),
  });
  // Quitar del disco (#224): lo marcado va a la papelera. UPD y DLC vienen sin marcar: solo se van si los marcas (P7).
  const [marked, setMarked] = useState<Set<string> | null>(null);
  const trash = useMutation({
    mutationFn: (ids: string[]) => request<void>("/api/sources/trash", { method: "POST", body: JSON.stringify({ ids: ids.map(Number) }) }),
    onSuccess: () => {
      setMarked(null);
      cache.invalidateQueries({ queryKey: ["game", String(gameId)] });
      cache.invalidateQueries({ queryKey: ["collection"] });
      cache.invalidateQueries({ queryKey: ["consoles"] });
    },
  });
  const startRemoving = () => setMarked(new Set(files.filter((f) => f.component !== "UPD" && f.component !== "DLC").map((f) => String(f.id))));
  const toggle = (id: string) => setMarked((m) => {
    const next = new Set(m);
    if (next.has(id)) next.delete(id); else next.add(id);
    return next;
  });
  return (
    <section className="sec column">
      <div className="sec-h">
        <h2>{t("game.where")}</h2>
        <div className="hero-actions">
          {verifiable && <button className="ot-btn ot-btn-sm" type="button" disabled={verify.isPending || verify.isSuccess} onClick={() => verify.mutate()}>{t("game.verify")}</button>}
          {!marked && <button className="ot-btn ot-btn-sm" type="button" onClick={startRemoving}>{t("game.remove")}</button>}
        </div>
      </div>
      {marked && (
        <div className="panel" role="group" aria-label={t("game.remove")} style={{ display: "grid", gap: 8 }}>
          <p className="small" style={{ margin: 0 }}>{t("game.removeHint")}</p>
          <div className="hero-actions">
            <button className="ot-btn ot-btn-primary ot-btn-sm" type="button" disabled={marked.size === 0 || trash.isPending} onClick={() => trash.mutate([...marked])}>
              {t("game.removeConfirm", { count: marked.size })}
            </button>
            <button className="ot-btn ot-btn-sm" type="button" disabled={trash.isPending} onClick={() => setMarked(null)}>{t("game.removeCancel")}</button>
          </div>
        </div>
      )}
      {trash.isError && <p className="error small" role="alert">{describe(trash.error)}</p>}
      {verify.isSuccess && <p className="small muted" role="status" style={{ margin: 0 }}>{t("game.verifyQueued")}</p>}
      {verify.isError && <p className="error small" role="alert">{describe(verify.error)}</p>}
      {executable.isError && <p className="error small" role="alert">{describe(executable.error)}</p>}
      <div className="panel table-wrap" style={{ padding: 0 }}>
        <table className="t">
          <thead><tr>{marked && <th><span className="sr-only">{t("game.remove")}</span></th>}<th>{t("game.file")}</th><th>{t("game.version")}</th><th>{t("game.size")}</th><th>{t("game.verified")}</th></tr></thead>
          <tbody>
            {files.map((f) => (
              <tr key={f.id}>
                {marked && <td><input type="checkbox" aria-label={f.path} checked={marked.has(String(f.id))} onChange={() => toggle(String(f.id))} /></td>}
                <td>
                  <span className="folder-path">{f.path}</span>{f.component && <span className="ot-badge" style={{ marginLeft: 6 }}>{f.component}</span>}
                  {f.executable && (f.executables && f.executables.length > 1
                    ? <select className="ot-btn ot-btn-sm" style={{ display: "block", marginTop: 4 }} aria-label={t("game.executable")} value={f.executable}
                        disabled={executable.isPending} onChange={(e) => executable.mutate({ id: Number(f.id), path: e.target.value })}>
                        {f.executables.map((x) => <option key={x} value={x}>{x}</option>)}
                      </select>
                    : <span className="small muted" style={{ display: "block" }}>{t("game.executable")}: {f.executable}</span>)}
                </td>
                <td>{consoleName(f.platform)}</td>
                <td className="tabular">{f.size != null ? size(f.size, locale) : "—"}</td>
                <td>{f.verified ? <span className="ot-badge ot-badge-have">{t("game.verified")}</span> : <span className="ot-badge">{f.executable ? "—" : t("game.notVerified")}</span>}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
