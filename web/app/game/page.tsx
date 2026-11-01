"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useFormatter, useTranslations } from "next-intl";
import { Suspense, useEffect, useState } from "react";
import { Cover, hours, Platforms, useConsoleName, useIdParam } from "@/components/Catalog";
import { useHeroArt } from "@/components/HeroArt";
import { Empty } from "@/components/Shell";
import { request } from "@/lib/client";
import { PageSkeleton } from "@/components/Skeleton";
import { useDescribeError } from "@/lib/i18n";
import { optimistic } from "@/lib/optimistic";
import { compareQuery, gameQuery } from "@/lib/queries";
import type { FieldRow, GameDetail, ProgressInfo, RoutesPage, VersionCompare } from "@/lib/types";

const STATUSES = ["pending", "playing", "finished", "completed", "dropped"] as const;
const FIELDS = ["name_es", "original_name", "year"] as const;

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
  const owned = stores.length > 0;

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
        </div>

        {description && (
          <section className="sec">
            <div className="sec-h"><h2>{t("game.description")}</h2></div>
            <p style={{ margin: 0 }}>{description}</p>
          </section>
        )}

        <Versions game={g} compare={compare.data ?? []} />

        <Sources game={g} />
      </div>
    </div>
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

/** Datos y fuentes (§6.8): cada campo con el valor de cada fuente. Lo corregido a mano gana; se puede volver al de las fuentes. */
function Sources({ game }: { game: GameDetail }) {
  const t = useTranslations("catalog");
  const sources = [...new Set(game.fields.filter((f) => !f.manual).map((f) => t(`game.source.${f.source as "igdb"}`)))];
  return (
    <details className="fold">
      <summary>{t("game.sources")} <span className="small muted">{sources.join(" · ")}</span></summary>
      <div className="fold-body">
        {FIELDS.map((field) => <FieldEditor key={field} game={game} field={field} rows={game.fields.filter((f) => f.field === field)} />)}
        <p className="small muted" style={{ margin: 0 }}>{t("game.manualNote")}</p>
      </div>
    </details>
  );
}

function FieldEditor({ game, field, rows }: { game: GameDetail; field: (typeof FIELDS)[number]; rows: FieldRow[] }) {
  const t = useTranslations("catalog");
  const describe = useDescribeError();
  const cache = useQueryClient();
  const current = field === "name_es" ? game.nameEs : field === "original_name" ? game.originalName : game.year;
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState("");
  const done = (data: GameDetail) => {
    cache.setQueryData(["game", String(game.id)], data);
    cache.invalidateQueries({ queryKey: ["collection"] });
    setEditing(false);
  };
  const save = useMutation({ mutationFn: () => request<GameDetail>(`/api/games/${game.id}/fields/${field}`, { method: "PUT", body: JSON.stringify({ value }) }), onSuccess: done });
  const undo = useMutation({ mutationFn: () => request<GameDetail>(`/api/games/${game.id}/fields/${field}`, { method: "DELETE" }), onSuccess: done });
  const manual = rows.some((r) => r.manual);
  const label = t(`game.field.${field}`);
  const inputId = `field-${field}`;

  return (
    <div className="field-editor">
      <div className="version-row">
        <span className="field-line"><b>{label}</b>: {current ?? "—"} {!editing && <button className="ot-btn ot-btn-ghost ot-btn-icon" type="button" aria-label={`${t("game.fix")}: ${label}`} title={t("game.fix")} onClick={() => { setValue(String(current ?? "")); setEditing(true); save.reset(); }}>✎</button>}</span>
        {manual && !editing && <button className="ot-btn ot-btn-ghost" type="button" disabled={undo.isPending} onClick={() => undo.mutate()}>{t("game.undoFix")}</button>}
      </div>
      {rows.length > 0 && (
        <ul className="small muted source-list">
          {rows.map((r) => (
            <li key={r.source}>{t(`game.source.${r.source as "igdb"}`)}: {r.value ?? "—"}{r.winner && <span className="ot-badge ot-badge-have" style={{ marginLeft: 6 }}>{t("game.shown")}</span>}</li>
          ))}
        </ul>
      )}
      {editing && (
        <form className="form-block" onSubmit={(e) => { e.preventDefault(); save.mutate(); }}>
          <label className="small muted" htmlFor={inputId}>{label}</label>
          <input id={inputId} value={value} inputMode={field === "year" ? "numeric" : undefined} onChange={(e) => setValue(e.target.value)} autoFocus />
          <div style={{ display: "flex", gap: 8 }}>
            <button className="ot-btn" type="button" onClick={() => setEditing(false)}>{t("game.cancel")}</button>
            <button className="ot-btn ot-btn-primary" type="submit" disabled={save.isPending}>{t("game.save")}</button>
          </div>
        </form>
      )}
      {(save.error ?? undo.error) && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(save.error ?? undo.error)}</p>}
    </div>
  );
}

const CRITERIA = ["content", "gameplay", "technical", "original"] as const;

/** Puntaje de 1 a 5 como barra de 5 tramos (#171). */
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
                  <td>{v.stores.length ? <span className="ot-badge ot-badge-have">{v.stores.map((s) => t(`store.${s.store as "steam"}`)).join(" · ")}</span> : <span className="ot-badge">{t("missing")}</span>}</td>
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
