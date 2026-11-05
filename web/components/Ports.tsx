"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useFormatter, useTranslations } from "next-intl";
import { useState } from "react";
import { useConsoleName } from "@/components/Catalog";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import { portNewsQuery } from "@/lib/queries";
import type { PortInfo } from "@/lib/types";

/** El estado tal cual lo publica el catálogo; 100 % jugable se distingue de todo lo demás (pedido de la persona en #286). */
function Status({ port }: { port: PortInfo }) {
  const t = useTranslations("ports");
  return (
    <span style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
      <span className={`ot-badge${port.fullyPlayable ? " ot-badge-have" : port.status === "no-port" ? "" : " ot-badge-review"}`}>
        {t(`status.${port.status as "complete"}`)}
      </span>
      {port.abandoned && <span className="ot-badge ot-badge-danger">{t("abandoned")}</span>}
      {port.delisted && <span className="ot-badge ot-badge-danger">{t("delisted")}</span>}
      {port.installed.length > 0 && <span className="ot-badge ot-badge-info">{t("installed", { versions: port.installed.join(", ") })}</span>}
    </span>
  );
}

/** Instalar (#287): un clic si tiene receta; si no, con la URL de su release (y su versión, si el archivo no la dice). */
function Install({ port }: { port: PortInfo }) {
  const t = useTranslations("ports");
  const describe = useDescribeError();
  const cache = useQueryClient();
  const [asking, setAsking] = useState(false);
  const [url, setUrl] = useState("");
  const [version, setVersion] = useState("");
  const install = useMutation({
    mutationFn: (body: { url?: string; version?: string }) => request<number>(`/api/ports/${port.id}/install`, { method: "POST", body: JSON.stringify(body) }),
    onSuccess: () => { setAsking(false); cache.invalidateQueries({ queryKey: ["tasks"] }); },
  });
  if (port.status === "no-port") return null;
  return (
    <div style={{ display: "grid", gap: 6 }}>
      <span className="hero-actions">
        {port.recipeId && <button type="button" className="ot-btn ot-btn-sm ot-btn-primary" disabled={install.isPending} onClick={() => install.mutate({})}>{t("install")}</button>}
        <button type="button" className="ot-btn ot-btn-sm" aria-expanded={asking} onClick={() => setAsking(!asking)}>{t("installFromUrl")}</button>
      </span>
      {asking && (
        <form className="hero-actions" onSubmit={(e) => { e.preventDefault(); install.mutate({ url, version: version || undefined }); }}>
          <input style={{ flex: 1 }} type="url" required placeholder="https://…" aria-label={t("url", { name: port.project })} value={url} onChange={(e) => setUrl(e.target.value)} />
          <input style={{ width: 110 }} placeholder={t("versionOptional")} aria-label={t("version")} value={version} onChange={(e) => setVersion(e.target.value)} />
          <button type="submit" className="ot-btn ot-btn-sm ot-btn-primary" disabled={install.isPending}>{t("installConfirm")}</button>
        </form>
      )}
      {install.isSuccess && <p className="small muted" role="status" style={{ margin: 0 }}>{t("queued")}</p>}
      {install.isError && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(install.error)}</p>}
    </div>
  );
}

function PortRow({ port, withGame }: { port: PortInfo; withGame?: boolean }) {
  const t = useTranslations("ports");
  const consoleName = useConsoleName();
  const format = useFormatter();
  return (
    <li style={{ display: "grid", gap: 6 }}>
      <div className="version-row">
        <span style={{ display: "grid" }}>
          <b>{withGame ? <Link href={`/game/?id=${port.gameId}`}>{port.gameName}</Link> : port.project}</b>
          <span className="small muted">
            {[withGame && port.project, consoleName(port.console), t(`kind.${port.kind as "decomp"}`), port.toolchain,
              t("added", { date: format.dateTime(new Date(port.added), { dateStyle: "medium", timeZone: "UTC" }) })].filter(Boolean).join(" · ")}
          </span>
        </span>
        <Status port={port} />
      </div>
      <span className="hero-actions small">
        <a href={port.link} target="_blank" rel="noreferrer">{t("open")}</a>
      </span>
      <Install port={port} />
    </li>
  );
}

/** Los ports del juego en su ficha: los 100 % jugables aparte de los demás. */
export function GamePorts({ gameId }: { gameId: number }) {
  const t = useTranslations("ports");
  const ports = useQuery({ queryKey: ["ports", "game", gameId], queryFn: () => request<PortInfo[]>(`/api/games/${gameId}/ports`) });
  if (!ports.data?.length) return null;
  const full = ports.data.filter((p) => p.fullyPlayable);
  const rest = ports.data.filter((p) => !p.fullyPlayable);
  return (
    <section className="sec" aria-label={t("title")}>
      <div className="sec-h"><h2>{t("title")}</h2></div>
      <div className="panel" style={{ display: "grid", gap: 12 }}>
        {full.length > 0 && <PortGroup title={t("fullyPlayable")} ports={full} />}
        {rest.length > 0 && <PortGroup title={t("others")} ports={rest} />}
      </div>
    </section>
  );
}

function PortGroup({ title, ports, withGame }: { title: string; ports: PortInfo[]; withGame?: boolean }) {
  return (
    <div style={{ display: "grid", gap: 6 }}>
      <h3 style={{ margin: 0 }}>{title}</h3>
      <ul className="detail-list">{ports.map((p) => <PortRow key={p.id} port={p} withGame={withGame} />)}</ul>
    </div>
  );
}

/** Inicio › Novedades (decisión 296): los ports nuevos de juegos que tienes o sigues, desde la última vez que los viste. */
export function PortNews() {
  const t = useTranslations("ports");
  const cache = useQueryClient();
  const news = useQuery(portNewsQuery);
  const seen = useMutation({
    mutationFn: () => request<void>("/api/ports/news/seen", { method: "POST" }),
    onSettled: () => cache.invalidateQueries({ queryKey: ["ports"] }),
  });
  if (!news.data?.length) return null;
  const full = news.data.filter((p) => p.fullyPlayable);
  const rest = news.data.filter((p) => !p.fullyPlayable);
  return (
    <div className="panel" style={{ display: "grid", gap: 12 }} aria-label={t("news")}>
      {full.length > 0 && <PortGroup title={t("newFullyPlayable")} ports={full} withGame />}
      {rest.length > 0 && <PortGroup title={t("newOthers")} ports={rest} withGame />}
      <button type="button" className="ot-btn ot-btn-sm" style={{ justifySelf: "start" }} disabled={seen.isPending} onClick={() => seen.mutate()}>{t("seen")}</button>
    </div>
  );
}
