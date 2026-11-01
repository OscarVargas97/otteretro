"use client";

import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useFormatter, useTranslations } from "next-intl";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Suspense, useState } from "react";
import { GameTile, hours, useConsoles } from "@/components/Catalog";
import { Empty } from "@/components/Shell";
import { useDescribeError } from "@/lib/i18n";
import { Rows, Tiles } from "@/components/Skeleton";
import { request } from "@/lib/client";
import { usePaged } from "@/lib/paged";
import type { CollectionPage, ConsoleCount } from "@/lib/types";

const FILTERS = ["all", "owned", "missing", "pc"] as const;
const SORTS = ["saga", "name", "year"] as const;

// La consola elegida va en la URL (/collection/?console=pc) para volver a ella desde la ficha.
export default function CollectionScreen() {
  return <Suspense><Collection /></Suspense>;
}

/** El número de cada consola sigue al filtro elegido (F2.18). */
function countFor(c: ConsoleCount, filter: (typeof FILTERS)[number]) {
  const games = Number(c.games), owned = Number(c.owned);
  return { all: games, owned, missing: games - owned, pc: Number(c.withPc) }[filter];
}

/** Colección (§4): por consola, con PC como una más. En PC, la tienda y las horas, y lo que aún no está en el catálogo. */
function Collection() {
  const t = useTranslations("catalog");
  const format = useFormatter();
  const describe = useDescribeError();
  const router = useRouter();
  const consoles = useConsoles();
  const [filter, setFilter] = useState<(typeof FILTERS)[number]>("owned"); // lo tuyo primero (F2.18)
  const [sort, setSort] = useState<(typeof SORTS)[number]>("saga");
  const [view, setView] = useState<"grid" | "list">("grid");
  const cache = useQueryClient();
  // «Escanear» (#178): la tarea que reconoce tus archivos; al terminar, el panel de tareas refresca Colección.
  const scan = useMutation({
    mutationFn: () => request<number>("/api/collection/scan", { method: "POST" }),
    onSuccess: () => cache.invalidateQueries({ queryKey: ["tasks"] }),
  });
  // «Verificar» (#179): el hash de lo que tienes de esta consola contra No-Intro o Redump.
  const verify = useMutation({
    mutationFn: () => request<number>("/api/collection/verify", { method: "POST", body: JSON.stringify({ console: selected }) }),
    onSuccess: () => cache.invalidateQueries({ queryKey: ["tasks"] }),
  });
  // Sin consola en la URL, la primera que tenga juegos.
  const selected = useSearchParams().get("console") ?? consoles.data?.find((c) => Number(c.games) > 0)?.id ?? consoles.data?.[0]?.id;
  const current = consoles.data?.find((c) => c.id === selected);
  // Por tramos al bajar (#177): PC tiene 20.000 juegos. Lo de Steam sin IGDB llega con el primero.
  const page = usePaged(["collection", selected, filter, sort], (offset) => `/api/collection/${selected}?${new URLSearchParams({ filter, sort, offset: String(offset) })}`,
    (p: CollectionPage) => ({ items: p.games, total: Number(p.total) }), !!selected);

  const games = page.items;
  const unlinked = page.data?.pages[0].unlinked ?? [];
  const isPc = selected === "pc";
  const shown = (consoles.data ?? []).filter((c) => countFor(c, filter) > 0 || c.id === selected);
  const groups = [...Map.groupBy(shown, (c) => c.company ?? "")];

  return (
    <div className="collection">
      {/* Solo las consolas con algo según el filtro (y la elegida), agrupadas por empresa (#171). */}
      <nav className="collection-consoles" aria-label={t("collection.consoles")}>
        {groups.map(([company, list]) => (
          <div key={company} className="console-nav-group">
            <span className="small muted console-nav-company">{company || t("collection.otherCompany")}</span>
            {list.map((c) => (
              <button key={c.id} type="button" className="ot-nav-item" aria-current={c.id === selected ? "page" : undefined} onClick={() => router.replace(`/collection/?console=${c.id}`)}>
                <span>{c.name}</span>
                <span className="small muted tabular">{countFor(c, filter)}</span>
              </button>
            ))}
          </div>
        ))}
      </nav>
      <div className="content collection-main">
        <div className="toolbar">
          <div className="chips" role="group" aria-label={t("collection.filters")}>
            {FILTERS.filter((f) => !(isPc && f === "pc")).map((f) => (
              <button key={f} className="chip" type="button" aria-pressed={filter === f} onClick={() => setFilter(f)}>{t(`collection.filter.${f}`)}</button>
            ))}
          </div>
          <div className="toolbar-end">
            <button className="ot-btn" type="button" title={t("collection.scanHelp")} disabled={scan.isPending} onClick={() => scan.mutate()}>{t("collection.scan")}</button>
            {current?.hasDat && Number(current.owned) > 0 && (
              <button className="ot-btn" type="button" title={t("collection.verifyHelp")} disabled={verify.isPending} onClick={() => verify.mutate()}>{t("collection.verify")}</button>
            )}
            <select className="ot-btn" aria-label={t("collection.sortLabel")} value={sort} onChange={(e) => setSort(e.target.value as typeof sort)}>
              {SORTS.map((s) => <option key={s} value={s}>{t(`collection.sort.${s}`)}</option>)}
            </select>
            {/* Cuadrícula/Lista como control con íconos (#171). */}
            <div className="segmented" role="group" aria-label={t("collection.view")}>
              <button type="button" aria-pressed={view === "grid"} aria-label={t("collection.grid")} title={t("collection.grid")} onClick={() => setView("grid")}>
                <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true"><rect x="1" y="1" width="6" height="6" rx="1" /><rect x="9" y="1" width="6" height="6" rx="1" /><rect x="1" y="9" width="6" height="6" rx="1" /><rect x="9" y="9" width="6" height="6" rx="1" /></svg>
              </button>
              <button type="button" aria-pressed={view === "list"} aria-label={t("collection.list")} title={t("collection.list")} onClick={() => setView("list")}>
                <svg width="16" height="16" viewBox="0 0 16 16" fill="currentColor" aria-hidden="true"><rect x="1" y="2" width="14" height="2" rx="1" /><rect x="1" y="7" width="14" height="2" rx="1" /><rect x="1" y="12" width="14" height="2" rx="1" /></svg>
              </button>
            </div>
          </div>
        </div>
        {current && (
          <div className="small muted">
            {t("collection.summary", { console: current.name, games: t("games", { count: Number(current.games) }), owned: t("ownedCount", { count: Number(current.owned) }) })}
          </div>
        )}
        {scan.isSuccess && <p className="small muted" role="status" style={{ margin: 0 }}>{t("collection.scanQueued")}</p>}
        {scan.isError && <p className="error small" role="alert">{describe(scan.error)}</p>}
        {verify.isSuccess && <p className="small muted" role="status" style={{ margin: 0 }}>{t("collection.verifyQueued")}</p>}
        {verify.isError && <p className="error small" role="alert">{describe(verify.error)}</p>}
        {page.isError && <p className="error small" role="alert">{describe(page.error)}</p>}
        {page.data && games.length === 0 && unlinked.length === 0 && (
          <Empty title={t("collection.emptyTitle")} text={t("collection.emptyText", { console: selected ?? "" })}><Link className="ot-btn" href="/settings/?s=consoles">{t("openConsoles")}</Link></Empty>
        )}

        {view === "grid" ? (
          <div className="grid-cards">
            {games.map((g) => <GameTile key={g.id} game={g} />)}
            {(!page.data || page.hasNextPage) && <Tiles kind="game" count={page.data ? 6 : 12} />}
          </div>
        ) : games.length > 0 && (
          <div className="panel table-wrap" style={{ padding: 0 }}>
            <table className="t">
              <thead><tr><th>{t("collection.name")}</th><th>{t("collection.saga")}</th><th>{t("collection.year")}</th><th>{t("game.have")}</th>{isPc && <th>{t("collection.hours")}</th>}</tr></thead>
              <tbody>
                {games.map((g) => (
                  <tr key={g.id}>
                    <td><Link href={`/game/?id=${g.id}`}>{g.name}</Link></td>
                    <td>{g.sagaId ? <Link href={`/saga/?id=${g.sagaId}`}>{g.sagaName}</Link> : <span className="muted">{t("emulation")}</span>}</td>
                    <td>{g.year ?? "—"}</td>
                    <td>{g.owned ? <span className="ot-badge ot-badge-have">{g.stores.length ? g.stores.map((s) => t(`store.${s as "steam"}`)).join(" · ") : t("owned")}</span> : <span className="ot-badge">{t("missing")}</span>}</td>
                    {isPc && <td className="tabular">{hours(g.playtime)}</td>}
                  </tr>
                ))}
                {page.hasNextPage && <Rows cols={isPc ? 5 : 4} count={4} />}
              </tbody>
            </table>
          </div>
        )}
        <div ref={page.end} aria-hidden="true" />

        {unlinked.length > 0 && (
          <section className="sec">
            <div className="sec-h"><h2>{t("collection.unlinked")}</h2><span className="small muted">{t("games", { count: unlinked.length })}</span></div>
            <p className="small muted" style={{ margin: 0 }}>{t("collection.unlinkedHint")}</p>
            <div className="panel table-wrap" style={{ padding: 0 }}>
              <table className="t">
                <thead><tr><th>{t("collection.name")}</th><th>{t("collection.store")}</th><th>{t("collection.hours")}</th><th>{t("collection.lastPlayed")}</th></tr></thead>
                <tbody>
                  {unlinked.map((u, i) => (
                    <tr key={i}>
                      <td>{u.name}</td>
                      <td>{t(`store.${u.store as "steam"}`)}</td>
                      <td className="tabular">{hours(u.playtime)}</td>
                      <td>{u.lastPlayed ? format.dateTime(new Date(u.lastPlayed), { dateStyle: "medium" }) : "—"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        )}
      </div>
    </div>
  );
}
