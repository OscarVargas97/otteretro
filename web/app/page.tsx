"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { Cover } from "@/components/Catalog";
import { request } from "@/lib/client";
import { useDynamicT } from "@/lib/i18n";
import type { ApiError, NextGame, ReviewPage } from "@/lib/types";

/** Inicio (§2): los avisos, el siguiente juego de cada ruta que sigues y lo que espera en Revisar. */
export default function Home() {
  const t = useTranslations("home");
  const tn = useTranslations("notices");
  const tc = useTranslations("catalog");
  const dt = useDynamicT();
  const notices = useQuery({ queryKey: ["notices"], queryFn: () => request<ApiError[]>("/api/notices") });
  const next = useQuery({ queryKey: ["home", "next"], queryFn: () => request<NextGame[]>("/api/home/next") });
  const review = useQuery({ queryKey: ["review"], queryFn: () => request<ReviewPage>("/api/review") });
  const pending = review.data?.pending.length ?? 0;

  return (
    <>
      {notices.data?.map((n) => (
        <div key={n.code + (n.params?.service ?? "")} className="notice" role="note" aria-label={tn("title")}>
          <span>{dt(n.code, { service: dt(`credentials.service.${n.params?.service}`) })}</span>
          <Link className="ot-btn" href="/settings/">{tn("openSettings")}</Link>
        </div>
      ))}
      <div className="content">
        <section className="sec">
          <div className="sec-h"><h2>{t("next")}</h2></div>
          {next.data && next.data.length === 0 && <p className="small muted">{t("nextEmpty")}</p>}
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
