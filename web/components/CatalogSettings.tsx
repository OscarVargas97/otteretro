"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useFormatter, useTranslations } from "next-intl";
import { useState } from "react";
import { useTasks } from "@/components/Tasks";
import { request } from "@/lib/client";
import { useDescribeError, useDynamicT } from "@/lib/i18n";
import type { CatalogSource, CatalogVersion, CatalogVersions } from "@/lib/types";

const CHANGES = ["merged", "removed", "renamed", "changed", "added", "image"] as const;

/** La fecha que lleva el nombre de una versión (`catalogo-AAAAMMDD-HHMM-…`), para cuando GitHub no la da. */
function tagDate(tag: string) {
  const m = /^catalogo-(\d{4})(\d{2})(\d{2})/.exec(tag);
  return m ? new Date(Date.UTC(Number(m[1]), Number(m[2]) - 1, Number(m[3]))) : null;
}

/**
 * Ajustes › Catálogo (#230): el catálogo en uso arriba, con sus totales; las versiones publicadas para elegir a cuál ir (#228), con lo
 * que pasa con tus datos antes de aceptar (#222, #223); y al final, de qué repo sale (oficial o un fork, #217).
 */
export function CatalogSettings() {
  const t = useTranslations("update.catalogVersion");
  const describe = useDescribeError();
  const dt = useDynamicT();
  const format = useFormatter();
  const cache = useQueryClient();
  const checking = useTasks().find((x) => x.kind === "catalog.check" && (x.status === "queued" || x.status === "running"));
  const info = useQuery({
    queryKey: ["catalog", "version"],
    queryFn: () => request<CatalogVersion>("/api/catalog/version"),
    refetchInterval: checking ? 2_000 : false,
  });
  const source = useQuery({ queryKey: ["catalog", "source"], queryFn: () => request<CatalogSource>("/api/catalog/source") });
  const versions = useQuery({ queryKey: ["catalog", "versions"], queryFn: () => request<CatalogVersions>("/api/catalog/versions"), staleTime: 5 * 60_000 });
  const done = (data: CatalogVersion) => {
    cache.setQueryData(["catalog", "version"], data);
    for (const key of ["sagas", "saga", "collection", "consoles", "search", "home", "game"]) cache.invalidateQueries({ queryKey: [key] });
    cache.invalidateQueries({ queryKey: ["catalog", "versions"] });
  };
  const check = useMutation({
    mutationFn: (tag?: string) => request<number>(`/api/catalog/version/check${tag ? `?tag=${encodeURIComponent(tag)}` : ""}`, { method: "POST" }),
    onSuccess: () => cache.invalidateQueries({ queryKey: ["tasks"] }),
  });
  const accept = useMutation({ mutationFn: () => request<CatalogVersion>("/api/catalog/version/accept", { method: "POST" }), onSuccess: done });
  const reject = useMutation({ mutationFn: () => request<CatalogVersion>("/api/catalog/version/reject", { method: "POST" }), onSuccess: done });
  if (!info.data) return null;

  const { pending, totals } = info.data;
  // La lista identifica tu versión por su hash si no se sabía su nombre (#230): aparece sin esperar a recargar.
  const current = info.data.current ?? versions.data?.current ?? null;
  const busy = accept.isPending || reject.isPending || !!checking || check.isPending;
  const error = check.error ?? accept.error ?? reject.error ?? versions.error;
  const day = (d: Date | null) => (d ? format.dateTime(d, { day: "numeric", month: "short", year: "numeric" }) : "");
  const summary = (counts: Record<string, number | string>) =>
    CHANGES.filter((c) => Number(counts[c] ?? 0) > 0).map((c) => t(`count.${c}`, { count: Number(counts[c]) })).join(" · ");
  const currentInfo = versions.data?.versions.find((v) => v.current);
  const latest = versions.data?.versions.find((v) => v.available);

  return (
    <div className="settings-blocks">
      <section className="panel" aria-labelledby="catalog-current">
        <h2 id="catalog-current">{t("inUse")}</h2>
        <p style={{ margin: 0 }}>
          <b className="tabular">{current ?? t("unknownVersion")}</b>
          {current && <span className="muted"> · {day(currentInfo?.publishedAt ? new Date(currentInfo.publishedAt) : tagDate(current))}</span>}
          {source.data && <span className="muted"> · {source.data.official ? t("official") : t("forkOf", { repo: source.data.repo })}</span>}
        </p>
        {totals && <p className="small muted tabular" style={{ margin: 0 }}>{t("totals", { sagas: Number(totals.sagas), games: Number(totals.games), covers: Number(totals.covers) })}</p>}
        {latest && <p className="small" style={{ margin: 0 }}>{latest.current ? t("upToDate") : t("newer", { version: latest.tag })}</p>}
      </section>

      {pending && (
        <section className="panel" aria-label={t("pendingTitle", { version: pending.to })}>
          <h2 style={{ margin: 0 }}>{t("pendingTitle", { version: pending.to })}</h2>
          <p className="small muted" style={{ margin: 0 }}>{summary(pending.counts) || t("nothing")}</p>
          {!pending.fromHistory && <p className="small" role="note" style={{ margin: 0 }}>{t("ownComparison")}</p>}
          {pending.mismatch.length > 0 && (
            <details role="alert">
              <summary className="small">{t("mismatch", { count: pending.mismatch.length })}</summary>
              <ul className="small tabular">{pending.mismatch.map((m) => <li key={m}>{m}</li>)}</ul>
            </details>
          )}
          {pending.items.length > 0 && (
            <ul className="small" style={{ maxHeight: 320, overflow: "auto", margin: 0 }}>
              {pending.items.map((c) => (
                <li key={`${c.kind}-${c.id}-${c.change}`}>
                  {dt(`update.catalogVersion.item.${c.change}`, { kind: dt(`update.catalogVersion.kind.${c.kind}`), name: c.name, from: c.from ?? "", into: c.intoName ?? "" })}
                  {Number(c.yours) > 0 && <b> · {t("yours", { count: Number(c.yours) })}</b>}
                </li>
              ))}
            </ul>
          )}
          <div className="sentence">
            <button className="ot-btn ot-btn-primary" type="button" disabled={busy} aria-busy={accept.isPending} onClick={() => accept.mutate()}>{t("accept")}</button>
            <button className="ot-btn" type="button" disabled={busy} aria-busy={reject.isPending} onClick={() => reject.mutate()}>{t("reject")}</button>
          </div>
        </section>
      )}

      <section className="panel" aria-labelledby="catalog-versions">
        <div className="version-row">
          <h2 id="catalog-versions" style={{ margin: 0 }}>{t("versions")}</h2>
          {checking && <span className="small muted" role="status"><span className="spinner" aria-hidden="true" /> {t("checking")}</span>}
        </div>
        {versions.isPending && <p className="small muted">{t("loadingVersions")}</p>}
        {versions.data && (
          <ul aria-label={t("versions")} style={{ listStyle: "none", padding: 0, margin: 0, display: "grid", gap: 6 }}>
            {versions.data.versions.map((v) => {
              const older = !!current && v.tag < current;
              const text = summary(v.counts);
              return (
                <li key={v.tag} className="version-row" style={{ alignItems: "start", gap: 8 }}>
                  <span aria-hidden="true" style={{ width: "1em" }}>{v.current ? "●" : "○"}</span>
                  <span className="small" style={{ flex: 1 }}>
                    <b className="tabular">{v.tag}</b>
                    <span className="muted"> · {day(v.publishedAt ? new Date(v.publishedAt) : tagDate(v.tag))}</span>
                    {v.current && <span className="ot-badge ot-badge-have" style={{ marginLeft: 6 }}>{t("yoursBadge")}</span>}
                    <br />
                    {v.changes.length > 0 ? (
                      <details>
                        <summary className="muted">{text}</summary>
                        <ul style={{ margin: 0 }}>
                          {v.changes.map((c) => <li key={`${c.kind}-${c.id}-${c.change}`}>{dt(`update.catalogVersion.item.${c.change}`, { kind: dt(`update.catalogVersion.kind.${c.kind}`), name: c.name ?? "", from: c.from ?? "", into: "" })}</li>)}
                        </ul>
                      </details>
                    ) : <span className="muted">{text || t("noHistory")}</span>}
                  </span>
                  {!v.current && (v.reason
                    ? <span className="small muted">{t(`reason.${v.reason as "newer" | "older"}`)}</span>
                    : v.available && v.compatible
                    ? <button className="ot-btn" type="button" disabled={busy} onClick={() => check.mutate(v.tag)}>{older ? t("goBack") : t("goTo")}</button>
                    : <span className="small muted">{t("notAvailable")}</span>)}
                </li>
              );
            })}
          </ul>
        )}
        {error && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(error)}</p>}
      </section>

      <CatalogSource />
    </div>
  );
}

/** De qué repo sale el catálogo: el oficial o un fork, con su aviso y la vuelta al oficial (#217). Cambiarlo solo guarda el origen (259). */
function CatalogSource() {
  const t = useTranslations("update.catalogSource");
  const describe = useDescribeError();
  const cache = useQueryClient();
  const info = useQuery({ queryKey: ["catalog", "source"], queryFn: () => request<CatalogSource>("/api/catalog/source") });
  const [repo, setRepo] = useState("");
  const save = useMutation({
    mutationFn: (value: string | null) => request<CatalogSource>("/api/catalog/source", { method: "PUT", body: JSON.stringify({ repo: value }) }),
    onSuccess: (data) => {
      cache.setQueryData(["catalog", "source"], data);
      cache.invalidateQueries({ queryKey: ["catalog"] });
      setRepo("");
    },
  });
  if (!info.data) return null;
  return (
    <section className="panel" aria-labelledby="catalog-source">
      <h2 id="catalog-source">{t("title")}</h2>
      <p className="small" role={info.data.official ? undefined : "note"} style={{ margin: 0, fontWeight: info.data.official ? undefined : 600 }}>
        {t(info.data.official ? "official" : "fork", { repo: info.data.repo })}
      </p>
      <p className="small muted" style={{ margin: 0 }}>{t("hint")}</p>
      <form className="sentence" onSubmit={(e) => { e.preventDefault(); if (repo.trim()) save.mutate(repo.trim()); }}>
        <input style={{ flex: 1 }} aria-label={t("repo")} placeholder={t("placeholder")} value={repo} onChange={(e) => setRepo(e.target.value)} />
        <button className="ot-btn" type="submit" disabled={save.isPending || !repo.trim()} aria-busy={save.isPending}>{t("use")}</button>
        {!info.data.official && (
          <button className="ot-btn" type="button" disabled={save.isPending} onClick={() => save.mutate(null)}>{t("officialButton")}</button>
        )}
      </form>
      {save.error && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(save.error)}</p>}
    </section>
  );
}
