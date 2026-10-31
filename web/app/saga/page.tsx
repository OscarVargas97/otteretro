"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { Suspense, useState } from "react";
import { Cover, Platforms, useConsoleName, useConsoles, useIdParam } from "@/components/Catalog";
import { Empty } from "@/components/Shell";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import type { RoutesPage, SagaDetail } from "@/lib/types";

// El detalle de una saga se abre como /saga/?id=… (0 = Emulación).
export default function SagaPage() {
  return <Suspense><Saga /></Suspense>;
}

/** Detalle de saga (§3.2): cabecera con favorita y seguir, la ruta que sigues (F2.6) y todos sus juegos. */
function Saga() {
  const t = useTranslations("catalog");
  const describe = useDescribeError();
  const id = useIdParam();
  const cache = useQueryClient();
  const consoles = useConsoles().data;
  const saga = useQuery({ queryKey: ["saga", id], queryFn: () => request<SagaDetail>(`/api/sagas/${id}`), enabled: id !== null });
  const flags = useMutation({
    mutationFn: (body: { favorite?: boolean; following?: boolean }) => request<SagaDetail>(`/api/sagas/${id}`, { method: "PUT", body: JSON.stringify(body) }),
    onSuccess: (data) => {
      cache.setQueryData(["saga", id], data);
      cache.invalidateQueries({ queryKey: ["sagas"] });
    },
  });

  if (id === null) return <div className="content"><Empty title={t("saga.noneTitle")} text={t("saga.noneText")} /></div>;
  if (saga.isError) return <div className="content"><p className="error" role="alert">{describe(saga.error)}</p><Link href="/sagas/">{t("saga.back")}</Link></div>;
  if (!saga.data) return <div className="content"><p className="muted">{t("loading")}</p></div>;

  const { saga: s, games } = saga.data;
  const emulation = Number(s.id) === 0;
  const companies = [...new Set(s.platforms.map((p) => consoles?.find((c) => c.id === p)?.company).filter(Boolean))];
  const years = games.map((g) => Number(g.year)).filter((y) => y > 0);
  const span = years.length ? (Math.min(...years) === Math.max(...years) ? `${years[0]}` : `${Math.min(...years)}–${Math.max(...years)}`) : null;

  return (
    <>
      <section className="hero">
        <div className="hero-main">
          <Link className="small muted" href="/sagas/">{t("saga.back")}</Link>
          <span className="small muted">{[...companies, span, t("games", { count: Number(s.games) }), t("ownedCount", { count: Number(s.owned) })].filter(Boolean).join(" · ")}</span>
          <h2 className="hero-title">{emulation ? t("emulation") : s.name}</h2>
          {!emulation && (
            <div className="hero-actions">
              <button className="ot-btn" type="button" aria-pressed={s.favorite} disabled={flags.isPending} onClick={() => flags.mutate({ favorite: !s.favorite })}>
                <span className="ot-fav">{s.favorite ? "★" : "☆"}</span> {t("saga.favorite")}
              </button>
              <button className="ot-btn" type="button" aria-pressed={s.following} disabled={flags.isPending} onClick={() => flags.mutate({ following: !s.following })}>
                {s.following ? t("saga.following") : t("saga.follow")}
              </button>
            </div>
          )}
          {flags.error && <p className="error small" role="alert">{describe(flags.error)}</p>}
        </div>
      </section>
      <div className="content">
        {!emulation && <Routes sagaId={String(s.id)} />}
        <section className="sec">
          <div className="sec-h"><h2>{t("saga.allGames")}</h2><span className="small muted">{t("games", { count: games.length })}</span></div>
          <div className="panel table-wrap" style={{ padding: 0 }}>
            <table className="t">
              <thead><tr><th>{t("saga.game")}</th><th>{t("saga.year")}</th><th>{t("saga.platforms")}</th><th>{t("saga.state")}</th></tr></thead>
              <tbody>
                {games.map((g) => (
                  <tr key={g.id} className={g.platforms.length === 0 ? "is-unplayable" : undefined}>
                    <td><Link href={`/game/?id=${g.id}`}>{g.name}</Link></td>
                    <td>{g.year ?? "—"}</td>
                    <td><span className="chips" style={{ gap: 4 }}><Platforms ids={g.platforms} /></span></td>
                    <td>
                      <span className="chips" style={{ gap: 4 }}>
                        {g.platforms.length === 0 ? <span className="ot-badge">{t("noConsole")}</span> : g.owned ? <span className="ot-badge ot-badge-have">{t("owned")}</span> : <span className="ot-badge">{t("missing")}</span>}
                        {g.status && g.status !== "pending" && <span className="ot-badge ot-badge-info">{t(`status.${g.status as "playing"}`)}</span>}
                      </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </>
  );
}

const DONE = ["finished", "completed"];

/** La línea de tiempo de la ruta elegida, con su selector, el porqué con fuentes y «pedir a Claude que la recalcule» (§3.2, §6.5). */
function Routes({ sagaId }: { sagaId: string }) {
  const t = useTranslations("catalog");
  const describe = useDescribeError();
  const consoleName = useConsoleName();
  const cache = useQueryClient();
  const [queued, setQueued] = useState(false);
  const routes = useQuery({ queryKey: ["saga", sagaId, "routes"], queryFn: () => request<RoutesPage>(`/api/sagas/${sagaId}/routes`) });
  const choose = useMutation({
    mutationFn: (routeId: number | null) => request<RoutesPage>(`/api/sagas/${sagaId}/route`, { method: "PUT", body: JSON.stringify({ routeId }) }),
    onSuccess: (data) => {
      cache.setQueryData(["saga", sagaId, "routes"], data);
      cache.invalidateQueries({ queryKey: ["saga", sagaId] });
      cache.invalidateQueries({ queryKey: ["home"] });
    },
  });
  const recalculate = useMutation({ mutationFn: () => request<number>(`/api/sagas/${sagaId}/recalculate`, { method: "POST" }), onSuccess: () => setQueued(true) });

  if (!routes.data) return routes.isError ? <p className="error" role="alert">{describe(routes.error)}</p> : null;
  const list = routes.data.routes;
  const chosen = list.find((r) => String(r.id) === String(routes.data.chosenRoute)) ?? list.find((r) => r.kind === "community") ?? list[0];
  const recalc = (
    <div className="hero-actions">
      <button className="ot-btn" type="button" disabled={recalculate.isPending || queued} onClick={() => recalculate.mutate()}>{t("saga.recalculate")}</button>
      {queued && <span className="small muted">{t("saga.queued")}</span>}
      {(recalculate.error || choose.error) && <span className="error small" role="alert">{describe(recalculate.error ?? choose.error)}</span>}
    </div>
  );
  if (!chosen) return <section className="sec"><p className="small muted">{t("saga.noRoutes")}</p>{recalc}</section>;

  const done = chosen.steps.filter((s) => DONE.includes(s.status ?? "")).length;
  return (
    <section className="sec">
      <div className="sec-h">
        <h2>{t("saga.route")}</h2>
        <div className="chips">
          {list.map((r) => (
            <button key={r.id} className="chip" type="button" aria-pressed={r.id === chosen.id && routes.data.chosenRoute !== null} disabled={choose.isPending}
              onClick={() => choose.mutate(Number(r.id))}>{t(`route.${r.kind as "release"}`)}</button>
          ))}
        </div>
        <span className="small muted">{t("saga.progress", { done, total: chosen.steps.length })}</span>
      </div>
      <ol className="timeline" style={{ display: "flex", gap: 12, overflowX: "auto", listStyle: "none", padding: 0, margin: 0 }}>
        {chosen.steps.map((s) => (
          <li key={s.position} style={{ minWidth: 140, maxWidth: 160 }}>
            <Link href={`/game/?id=${s.gameId}`} style={{ textDecoration: "none", color: "var(--ink)" }}>
              <Cover gameId={s.gameId} name={s.name} hasCover={s.hasCover} />
              <p className="ot-game-name" title={s.name}>{Number(s.position)}. {s.name}</p>
            </Link>
            <span className="chips" style={{ gap: 4 }}>
              {DONE.includes(s.status ?? "") ? <span className="ot-badge ot-badge-have">{t(`status.${s.status as "finished"}`)}</span>
                : s.status && s.status !== "pending" ? <span className="ot-badge ot-badge-info">{t(`status.${s.status as "playing"}`)}</span> : null}
              {s.owned ? <span className="ot-badge ot-badge-have">{t("owned")}</span> : <span className="ot-badge">{t("missing")}</span>}
            </span>
            {s.versionPlatform && <p className="small" style={{ margin: "4px 0 0" }}>{t("saga.recommends", { platform: consoleName(s.versionPlatform) })}</p>}
            {s.reason && <p className="small muted" style={{ margin: "2px 0 0" }}>{s.reason}</p>}
          </li>
        ))}
      </ol>
      <div className="panel" style={{ gap: 4 }}>
        <b>{t("saga.why")}</b>
        <p className="small" style={{ margin: 0 }}>{chosen.rationale}</p>
        {chosen.sources.length > 0 && (
          <p className="small muted" style={{ margin: 0 }}>{t("saga.sources")}: {chosen.sources.map((src, i) => <span key={src.url}>{i > 0 && " · "}<a href={src.url} target="_blank" rel="noreferrer">{src.title}</a></span>)}</p>
        )}
      </div>
      {recalc}
    </section>
  );
}
