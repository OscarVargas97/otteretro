"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useFormatter, useTranslations } from "next-intl";
import { Cover, Progress } from "@/components/Catalog";
import { PortNews } from "@/components/Ports";
import { Empty } from "@/components/Shell";
import { NextCards } from "@/components/Skeleton";
import { useDynamicT } from "@/lib/i18n";
import { nextQuery, noticesQuery, portNewsQuery, reviewQuery } from "@/lib/queries";

/** Inicio (§2): los avisos, el siguiente juego de cada ruta que sigues y lo que espera en Revisar. */
export default function Home() {
  const t = useTranslations("home");
  const tn = useTranslations("notices");
  const tc = useTranslations("catalog");
  const dt = useDynamicT();
  const format = useFormatter();
  const notices = useQuery(noticesQuery);
  const next = useQuery(nextQuery);
  const review = useQuery(reviewQuery);
  const ports = useQuery(portNewsQuery);
  const pending = review.data?.files.length ?? 0;
  const missing = notices.data?.filter((n) => n.code === "notices.missingKey") ?? [];
  const unidentified = notices.data?.find((n) => n.code === "notices.unidentified");
  const news = notices.data?.filter((n) => n.code === "notices.toolUpdate") ?? [];
  const recipeBackups = notices.data?.filter((n) => n.code === "notices.recipeBackup") ?? [];
  // Todo junto (#105): los avisos van arriba y, si llegaran después, empujarían el resto.
  if (notices.isPending || next.isPending || review.isPending || ports.isPending)
    return <div className="content" aria-busy="true"><section className="sec"><div className="sec-h"><h2>{t("next")}</h2></div><NextCards count={2} /></section></div>;

  return (
    <>
      {/* Un solo aviso con todos los servicios que no tienen clave (#169). */}
      {missing.length > 0 && (
        <div className="notice" role="note" aria-label={tn("title")}>
          <span>{tn("missingKeys", { services: format.list(missing.map((n) => dt(`credentials.service.${n.params?.service}`))) })}</span>
          <Link className="ot-btn" href="/settings/?s=credentials">{tn("openCredentials")}</Link>
        </div>
      )}
      {/* Lo que ni el escaneo ni Claude reconocieron (#180). */}
      {unidentified && (
        <div className="notice" role="note" aria-label={tn("title")}>
          <span>{tn("unidentified", { count: Number(unidentified.params?.count) })}</span>
          <Link className="ot-btn" href="/review/?tab=unidentified">{tn("openReview")}</Link>
        </div>
      )}
      {/* Recetas locales que chocaron con la del catálogo: ganó la publicada y la local quedó respaldada (#357). */}
      {recipeBackups.map((n) => (
        <div key={n.params?.file} className="notice" role="note" aria-label={tn("title")}>
          <span>{tn("recipeBackup", { name: n.params?.name ?? "", file: n.params?.file ?? "" })}</span>
        </div>
      ))}
      <div className="content">
        <section className="sec">
          <div className="sec-h"><h2>{t("next")}</h2></div>
          {next.data && next.data.length === 0 && (
            <Empty title={t("nextEmptyTitle")} text={t("nextEmpty")}><Link className="ot-btn ot-btn-primary" href="/sagas/">{tc("goToSagas")}</Link></Empty>
          )}
          {/* Tarjeta horizontal (#170): portada chica, saga, ruta, avance y «Ver ficha». */}
          <div className="next-list">
            {next.data?.map((n) => (
              <article key={n.sagaId} className="next-card">
                <Cover gameId={n.step.gameId} name={n.step.name} hasCover={n.step.hasCover} platform={n.step.versionPlatform} />
                <div className="next-body">
                  <h3 className="ot-game-name" title={n.step.name}>{n.step.name}</h3>
                  <span className="small muted">{t("routeOf", { saga: n.sagaName, route: tc(`route.${n.routeKind as "release"}`) })}</span>
                  <Progress done={Number(n.done)} total={Number(n.total)} label={t("progress", { done: Number(n.done), total: Number(n.total) })} />
                </div>
                {n.step.owned ? <span className="ot-badge ot-badge-have">{tc("owned")}</span> : <span className="ot-badge">{tc("missing")}</span>}
                <Link className="ot-btn ot-btn-sm" href={`/game/?id=${n.step.gameId}`} aria-label={`${t("viewGame")}: ${n.step.name}`}>{t("viewGame")}</Link>
              </article>
            ))}
          </div>
        </section>
        {/* Novedades: versiones nuevas de lo que tienes instalado (decisión 294) y ports nuevos de juegos que tienes o sigues (296). */}
        {(news.length > 0 || (ports.data?.length ?? 0) > 0) && (
          <section className="sec">
            <div className="sec-h"><h2>{t("news")}</h2></div>
            {news.length > 0 && (
              <div className="panel" style={{ display: "grid", gap: 6 }}>
                {news.map((n) => (
                  <span key={`${n.params?.name}-${n.params?.channel}`} className="small">
                    {tn("toolUpdate", { name: n.params?.name ?? "", version: n.params?.version ?? "", channel: n.params?.channel ?? "" })}
                  </span>
                ))}
                <Link className="ot-btn" href="/tools/" style={{ alignSelf: "flex-start" }}>{tn("openTools")}</Link>
              </div>
            )}
            <PortNews />
          </section>
        )}
        {/* Solo si hay algo por aprobar (#170). */}
        {pending > 0 && (
          <section className="sec">
            <div className="sec-h"><h2>{t("review")}</h2></div>
            <div className="panel">
              <Link className="ot-btn" href="/review/" style={{ alignSelf: "flex-start" }}>{t("reviewCount", { count: pending })}</Link>
            </div>
          </section>
        )}
      </div>
    </>
  );
}
