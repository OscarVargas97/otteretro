"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { Suspense } from "react";
import { Platforms, useConsoles, useIdParam } from "@/components/Catalog";
import { Empty } from "@/components/Shell";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import type { SagaDetail } from "@/lib/types";

// El detalle de una saga se abre como /saga/?id=… (0 = Emulación).
export default function SagaPage() {
  return <Suspense><Saga /></Suspense>;
}

/** Detalle de saga (§3.2) sin rutas (F2): cabecera con favorita y seguir, y todos sus juegos. */
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
        {!emulation && <p className="small muted">{t("saga.routesLater")}</p>}
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
