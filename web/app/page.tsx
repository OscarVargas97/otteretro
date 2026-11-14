"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useFormatter, useTranslations } from "next-intl";
import { Cover, hours, Progress, useConsoleName } from "@/components/Catalog";
import { PlayButton } from "@/components/PlayChoice";
import { Empty } from "@/components/Shell";
import { NextCards } from "@/components/Skeleton";
import { FINISHED, TaskRows, tasksQuery } from "@/components/Tasks";
import { request } from "@/lib/client";
import { useDescribeError, useDynamicT } from "@/lib/i18n";
import { consolesQuery, nextQuery, noticesQuery, portNewsQuery, recentQuery, reviewQuery, toolUpdatesQuery } from "@/lib/queries";

/**
 * Inicio (§2, #428, tablero Main): arriba Continuar jugando; en dos columnas, a la izquierda Siguiente en tus rutas y Novedades,
 * a la derecha Revisar, Tareas en curso y Avisos. Sin juegos, el estado vacío ocupa la columna izquierda.
 */
export default function Home() {
  const t = useTranslations("home");
  const tn = useTranslations("notices");
  const tc = useTranslations("catalog");
  const tp = useTranslations("ports");
  const dt = useDynamicT();
  const describe = useDescribeError();
  const format = useFormatter();
  const consoleName = useConsoleName();
  const cache = useQueryClient();
  const notices = useQuery(noticesQuery);
  const next = useQuery(nextQuery);
  const recent = useQuery(recentQuery);
  const review = useQuery(reviewQuery);
  const ports = useQuery(portNewsQuery);
  const updates = useQuery(toolUpdatesQuery);
  const consoles = useQuery(consolesQuery);
  const tasks = useQuery(tasksQuery);
  const seen = useMutation({
    mutationFn: () => request<void>("/api/ports/news/seen", { method: "POST" }),
    onSettled: () => cache.invalidateQueries({ queryKey: ["ports"] }),
  });
  // Todo junto (#105): si una sección llegara después, empujaría a las demás.
  const all = [notices, next, recent, review, ports, updates, consoles, tasks];
  if (all.some((q) => q.isPending)) return <Loading />;
  // Tablero Main › error: sin esto, una consulta caída se vería como «Todavía no hay juegos».
  const failed = all.find((q) => q.isError);
  if (failed) return (
    <div className="content" key="error" style={{ justifyContent: "center", flex: 1 }}>
      <div className="panel" role="alert" style={{ maxWidth: 520, margin: "0 auto", gap: 12 }}>
        <div className="sentence"><span className="ot-badge ot-badge-danger">{t("error")}</span><h2 style={{ margin: 0 }}>{t("errorTitle")}</h2></div>
        <p style={{ margin: 0 }}>{describe(failed.error)}</p>
        <p className="small muted" style={{ margin: 0 }}>{t("errorSafe")}</p>
        <div className="sentence">
          <button className="ot-btn ot-btn-primary" type="button" onClick={() => all.forEach((q) => q.isError && q.refetch())}>{t("retry")}</button>
          <Link className="ot-btn" href="/settings/">{tn("openSettings")}</Link>
        </div>
      </div>
    </div>
  );

  const files = review.data?.files ?? [];
  const reviewTiles = [
    { key: "doubtful", count: files.filter((f) => f.kind === "doubtful").length, color: "var(--review)" },
    { key: "unidentified", count: files.filter((f) => f.kind === "unidentified").length, color: "var(--danger)" },
    { key: "packages", count: review.data?.packages.length ?? 0, color: undefined },
  ] as const;
  const missing = notices.data?.filter((n) => n.code === "notices.missingKey") ?? [];
  const recipeBackups = notices.data?.filter((n) => n.code === "notices.recipeBackup") ?? [];
  const active = tasks.data?.filter((x) => !FINISHED.includes(x.status)) ?? [];
  const news = updates.data ?? [];
  const portNews = ports.data ?? [];
  const recentGames = recent.data ?? [];
  const noGames = recentGames.length === 0 && !consoles.data?.some((c) => Number(c.owned) > 0);

  return (
    <div className="content" key="ready">
      {recentGames.length > 0 && (
        <section className="sec">
          <div className="sec-h"><h2>{t("continue")}</h2><span className="muted small">{t("continueHint")}</span></div>
          <div className="recent-list">
            {recentGames.map((g) => (
              <Link key={g.gameId} href={`/game/?id=${g.gameId}`} className="ot-game recent-game">
                <Cover gameId={g.gameId} name={g.name} hasCover={g.hasCover} platform={g.platform} />
                <span className="ot-game-name" title={g.name}>{g.name}</span>
                <div className="ot-game-meta">
                  <span className="ot-console">{consoleName(g.platform)}</span>
                  {g.launcher !== "otteretro" && <span>{dt.has(`catalog.store.${g.launcher}`) ? dt(`catalog.store.${g.launcher}`) : g.launcher}</span>}
                </div>
                {/* una línea por dato: así todas las tarjetas miden lo mismo (#437) */}
                <span className="ot-game-meta recent-line">{g.playing ? t("playingNow") : format.relativeTime(new Date(g.lastPlayed), new Date())}</span>
                <span className="ot-game-meta recent-line">{Number(g.minutes) > 0 ? t("played", { time: hours(Number(g.minutes)) }) : " "}</span>
              </Link>
            ))}
          </div>
        </section>
      )}

      <div className="home-cols">
        <div className="home-main">
          {noGames ? (
            <Empty title={t("emptyTitle")} text={t("emptyText")}>
              <div className="sentence" style={{ justifyContent: "center" }}>
                <Link className="ot-btn ot-btn-primary" href="/settings/?s=storage">{t("scan")}</Link>
                <Link className="ot-btn" href="/sagas/">{t("exploreSagas")}</Link>
              </div>
            </Empty>
          ) : (
            <section className="sec">
              <div className="sec-h"><h2>{t("next")}</h2><Link className="ot-btn ot-btn-ghost" href="/sagas/">{tc("goToSagas")}</Link></div>
              {next.data?.length === 0 && <Empty title={t("nextEmptyTitle")} text={t("nextEmpty")} />}
              {next.data?.map((n) => (
                <article key={n.sagaId} className="panel route-card">
                  <Cover gameId={n.step.gameId} name={n.step.name} hasCover={n.step.hasCover} platform={n.step.versionPlatform}
                    style={{ width: 96, flex: "none", filter: n.step.owned ? undefined : "grayscale(1) brightness(.7)" }} />
                  <div className="route-card-body">
                    <span className="muted small">{t("step", { saga: n.sagaName, route: tc(`route.${n.routeKind as "release"}`), step: Number(n.done) + 1, total: Number(n.total) })}</span>
                    <h3 className="route-card-name" title={n.step.name}>{n.step.name}</h3>
                    <div className="ot-game-meta">
                      {n.step.versionPlatform && <span className="ot-console">{consoleName(n.step.versionPlatform)}</span>}
                      {n.step.owned ? <span className="ot-badge ot-badge-have">{tc("owned")}</span> : <span className="ot-badge">{tc("missing")}</span>}
                    </div>
                    <Progress done={Number(n.done)} total={Number(n.total)} label={t("progress", { done: Number(n.done), total: Number(n.total) })} />
                    <div className="sentence">
                      <PlayButton game={{ id: n.step.gameId, nameEs: n.step.name }} owned={n.step.owned} compact />
                      <Link className="ot-btn" href={`/saga/?id=${n.sagaId}`} aria-label={`${t("viewRoute")}: ${n.sagaName}`}>{t("viewRoute")}</Link>
                    </div>
                  </div>
                </article>
              ))}
            </section>
          )}

          {/* Novedades: versiones nuevas de lo instalado (decisión 294) y ports nuevos de lo que tienes o sigues (296); una fila y un botón cada una. */}
          {(news.length > 0 || portNews.length > 0) && (
            <section className="sec">
              <div className="sec-h"><h2>{t("news")}</h2><Link className="ot-btn ot-btn-ghost" href="/tools/">{t("viewAll")}</Link></div>
              <div className="panel news-list" aria-label={tp("news")}>
                {news.map((u) => (
                  <div key={`${u.recipeId}-${u.channel}`} className="news-row">
                    <span className="ot-badge">{dt(`home.kind.${u.kind}`)}</span>
                    <span className="news-text">
                      {tn("toolUpdate", { name: u.name, version: u.version, channel: u.channel })}
                      {u.installed.length > 0 && ` ${t("youHave", { versions: format.list(u.installed) })}`}
                    </span>
                    <Link className="ot-btn ot-btn-sm" href="/tools/">{t("update")}</Link>
                  </div>
                ))}
                {portNews.map((p) => (
                  <div key={p.id} className="news-row">
                    <span className={`ot-badge${p.fullyPlayable ? " ot-badge-have" : ""}`}>{t("newPort")}</span>
                    <span className="news-text">
                      <b>{p.project}</b>: {t("portFor", { game: p.gameName ?? "", console: consoleName(p.console) })}{" "}
                      <span className="muted">{tp(`status.${p.status as "complete"}`)}</span>
                      {p.delisted && <> <span className="ot-badge ot-badge-danger">{tp("delisted")}</span></>}
                    </span>
                    <Link className="ot-btn ot-btn-sm" href={`/game/?id=${p.gameId}&tab=play`}>{t("viewPort")}</Link>
                  </div>
                ))}
                {portNews.length > 0 && (
                  <button type="button" className="ot-btn ot-btn-sm ot-btn-ghost news-seen" disabled={seen.isPending} onClick={() => seen.mutate()}>{tp("seen")}</button>
                )}
              </div>
            </section>
          )}
        </div>

        <div className="home-side">
          {/* Lo que espera en Revisar, por tipo (#170: solo si hay algo). */}
          {reviewTiles.some((x) => x.count > 0) && (
            <section className="sec">
              <div className="sec-h"><h2>{t("review")}</h2><Link className="ot-btn ot-btn-ghost" href="/collection/?review=1">{t("goToReview")}</Link></div>
              <div className="review-tiles">
                {reviewTiles.map((x) => (
                  <Link key={x.key} className="panel review-tile" href="/collection/?review=1">
                    <span className="review-tile-count" style={{ color: x.color }}>{x.count}</span>
                    <span className="small muted">{t(`reviewKind.${x.key}`)}</span>
                  </Link>
                ))}
              </div>
            </section>
          )}

          {active.length > 0 && (
            <section className="sec">
              <div className="sec-h"><h2>{t("tasks")}</h2></div>
              <div className="ot-tasks home-tasks"><TaskRows active /></div>
            </section>
          )}

          {(missing.length > 0 || recipeBackups.length > 0) && (
            <section className="sec">
              <div className="sec-h"><h2>{tn("title")}</h2></div>
              <div className="panel" style={{ gap: 10 }}>
                {/* Un solo aviso con todos los servicios que no tienen clave (#169). */}
                {missing.length > 0 && (
                  <div className="notice-row" role="note" aria-label={tn("title")}>
                    <span className="ot-badge ot-badge-review">{t("pending")}</span>
                    <span className="small">
                      {tn("missingKeys", { services: format.list(missing.map((n) => dt(`credentials.service.${n.params?.service}`))) })}{" "}
                      <Link href="/settings/?s=credentials">{tn("openCredentials")}</Link>
                    </span>
                  </div>
                )}
                {/* Recetas locales que chocaron con la del catálogo: ganó la publicada y la local quedó respaldada (#357). */}
                {recipeBackups.map((n) => (
                  <div key={n.params?.file} className="notice-row" role="note" aria-label={tn("title")}>
                    <span className="ot-badge ot-badge-info">{t("backup")}</span>
                    <span className="small">{tn("recipeBackup", { name: n.params?.name ?? "", file: n.params?.file ?? "" })}</span>
                  </div>
                ))}
              </div>
            </section>
          )}
        </div>
      </div>
    </div>
  );
}

/** Mientras llega todo (tablero Main › cargando): la fila de Continuar jugando y las dos columnas. */
function Loading() {
  const t = useTranslations("home");
  return (
    <div className="content" aria-busy="true" key="loading">
      <section className="sec">
        <div className="sec-h"><h2>{t("continue")}</h2></div>
        <div className="recent-list" style={{ overflow: "hidden" }}>
          {[1, 2, 3, 4, 5].map((i) => <div key={i} className="ot-game" aria-hidden="true"><div className="ot-cover ot-skel" /></div>)}
        </div>
      </section>
      <div className="home-cols">
        <div className="home-main"><NextCards count={2} /></div>
        <div className="home-side" />
      </div>
    </div>
  );
}
