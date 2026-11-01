"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { Cover } from "@/components/Catalog";
import { Empty } from "@/components/Shell";
import { Panels, Tiles } from "@/components/Skeleton";
import { useDynamicT } from "@/lib/i18n";
import { nextQuery, noticesQuery, reviewQuery } from "@/lib/queries";

/** Inicio (§2): los avisos, el siguiente juego de cada ruta que sigues y lo que espera en Revisar. */
export default function Home() {
  const t = useTranslations("home");
  const tn = useTranslations("notices");
  const tc = useTranslations("catalog");
  const dt = useDynamicT();
  const notices = useQuery(noticesQuery);
  const next = useQuery(nextQuery);
  const review = useQuery(reviewQuery);
  const pending = review.data?.pending.length ?? 0;
  // Todo junto (#105): los avisos van arriba y, si llegaran después, empujarían el resto.
  if (notices.isPending || next.isPending || review.isPending)
    return <div className="content" aria-busy="true"><Panels count={1} /><div className="grid-cards"><Tiles kind="game" count={4} /></div></div>;

  return (
    <>
      {/* Un solo aviso con todos los servicios que no tienen clave (#169). */}
      {!!notices.data?.length && (
        <div className="notice" role="note" aria-label={tn("title")}>
          <span>{tn("missingKeys", { services: notices.data.map((n) => dt(`credentials.service.${n.params?.service}`)).join(", ") })}</span>
          <Link className="ot-btn" href="/settings/?s=credentials">{tn("openCredentials")}</Link>
        </div>
      )}
      <div className="content">
        <section className="sec">
          <div className="sec-h"><h2>{t("next")}</h2></div>
          {next.data && next.data.length === 0 && (
            <Empty title={t("nextEmptyTitle")} text={t("nextEmpty")}><Link className="ot-btn ot-btn-primary" href="/sagas/">{tc("goToSagas")}</Link></Empty>
          )}
          <div className="grid-cards">
            {next.data?.map((n) => (
              <Link key={n.sagaId} className="ot-game" href={`/game/?id=${n.step.gameId}`} style={{ textDecoration: "none", color: "var(--ink)", width: 180 }}>
                <Cover gameId={n.step.gameId} name={n.step.name} hasCover={n.step.hasCover} />
                <p className="ot-game-name" title={n.step.name}>{n.step.name}</p>
                <div className="ot-game-meta">
                  <span>{t("step", { saga: n.sagaName, route: tc(`route.${n.routeKind as "release"}`), done: Number(n.done), total: Number(n.total) })}</span>
                </div>
                <div className="ot-game-badges">
                  {n.step.owned ? <span className="ot-badge ot-badge-have">{tc("owned")}</span> : <span className="ot-badge">{tc("missing")}</span>}
                </div>
              </Link>
            ))}
          </div>
        </section>
        <section className="sec">
          <div className="sec-h"><h2>{t("review")}</h2></div>
          <div className="panel">
            {pending > 0 ? <Link href="/review/">{t("reviewCount", { count: pending })}</Link> : <span className="small muted">{t("reviewNone")}</span>}
          </div>
        </section>
      </div>
    </>
  );
}
