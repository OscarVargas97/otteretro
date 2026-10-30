"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useFormatter, useTranslations } from "next-intl";
import { Suspense, useState } from "react";
import { GameTile, hours, useConsoles } from "@/components/Catalog";
import { Empty } from "@/components/Shell";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import type { CollectionPage } from "@/lib/types";

const FILTERS = ["all", "owned", "missing", "pc"] as const;
const SORTS = ["saga", "name", "year"] as const;

// La consola elegida va en la URL (/collection/?console=pc) para volver a ella desde la ficha.
export default function CollectionScreen() {
  return <Suspense><Collection /></Suspense>;
}

/** Colección (§4): por consola, con PC como una más. En PC, la tienda y las horas, y lo que aún no está en el catálogo. */
function Collection() {
  const t = useTranslations("catalog");
  const format = useFormatter();
  const describe = useDescribeError();
  const router = useRouter();
  const consoles = useConsoles();
  const [filter, setFilter] = useState<(typeof FILTERS)[number]>("all");
  const [sort, setSort] = useState<(typeof SORTS)[number]>("saga");
  const [view, setView] = useState<"grid" | "list">("grid");
  // Sin consola en la URL, la primera que tenga juegos.
  const selected = useSearchParams().get("console") ?? consoles.data?.find((c) => Number(c.games) > 0)?.id ?? consoles.data?.[0]?.id;
  const current = consoles.data?.find((c) => c.id === selected);
  const page = useQuery({
    queryKey: ["collection", selected, filter, sort],
    queryFn: () => request<CollectionPage>(`/api/collection/${selected}?${new URLSearchParams({ filter, sort })}`),
    enabled: !!selected,
    placeholderData: (previous) => previous,
  });

  const games = page.data?.games ?? [];
  const unlinked = page.data?.unlinked ?? [];
  const isPc = selected === "pc";

  return (
    <div className="collection">
      <nav className="collection-consoles" aria-label={t("collection.consoles")}>
        {consoles.data?.map((c) => (
          <button key={c.id} type="button" className="ot-nav-item" aria-current={c.id === selected ? "page" : undefined} onClick={() => router.replace(`/collection/?console=${c.id}`)}>
            <span>{c.name}</span>
            <span className="small muted tabular">{c.games}</span>
          </button>
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
            <select className="ot-btn" aria-label={t("collection.sortLabel")} value={sort} onChange={(e) => setSort(e.target.value as typeof sort)}>
              {SORTS.map((s) => <option key={s} value={s}>{t(`collection.sort.${s}`)}</option>)}
            </select>
            <div className="chips" role="group" aria-label={t("collection.view")}>
              <button className="chip" type="button" aria-pressed={view === "grid"} onClick={() => setView("grid")}>{t("collection.grid")}</button>
              <button className="chip" type="button" aria-pressed={view === "list"} onClick={() => setView("list")}>{t("collection.list")}</button>
            </div>
          </div>
        </div>
        {current && (
          <div className="small muted">
            {t("collection.summary", { console: current.name, games: t("games", { count: Number(current.games) }), owned: t("ownedCount", { count: Number(current.owned) }) })}
          </div>
        )}
        {page.isError && <p className="error small" role="alert">{describe(page.error)}</p>}
        {page.data && games.length === 0 && unlinked.length === 0 && (
          <Empty title={t("collection.emptyTitle")} text={t("collection.emptyText", { console: selected ?? "" })} />
        )}

        {view === "grid" ? (
          <div className="grid-cards">{games.map((g) => <GameTile key={g.id} game={g} />)}</div>
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
              </tbody>
            </table>
          </div>
        )}

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
