"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useFormatter, useTranslations } from "next-intl";
import { useState } from "react";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import { optimistic } from "@/lib/optimistic";
import type { ConsolesPage, PlatformInfo } from "@/lib/types";

/** Ajustes › Consolas (F2.13, F2.14): las consolas habilitadas, agregar otras de IGDB y cada cuánto se pone al día el catálogo. */
export function ConsoleSettings() {
  const t = useTranslations("settings.consoles");
  const describe = useDescribeError();
  const format = useFormatter();
  const cache = useQueryClient();
  const page = useQuery({ queryKey: ["consoles", "settings"], queryFn: () => request<ConsolesPage>("/api/consoles/settings") });
  const [adding, setAdding] = useState(false);
  const [query, setQuery] = useState("");
  const [days, setDays] = useState<number | null>(null);
  const igdb = useQuery({ queryKey: ["consoles", "igdb"], queryFn: () => request<PlatformInfo[]>("/api/consoles/igdb"), enabled: adding });
  const close = () => { setAdding(false); setQuery(""); }; // #94
  const done = (data: ConsolesPage) => {
    cache.setQueryData(["consoles", "settings"], data);
    cache.invalidateQueries({ queryKey: ["consoles", "igdb"] });
    cache.invalidateQueries({ queryKey: ["tasks"] });
  };
  const toggle = useMutation({
    mutationFn: ({ id, enabled }: { id: string; enabled: boolean }) =>
      request<ConsolesPage>(`/api/consoles/${id}`, { method: "PUT", body: JSON.stringify({ enabled }) }),
    ...optimistic(cache, ["consoles", "settings"], (d: ConsolesPage, v: { id: string; enabled: boolean }) =>
      ({ ...d, consoles: d.consoles.map((c) => (c.id === v.id ? { ...c, enabled: v.enabled } : c)) })),
    onSuccess: done,
  });
  const enable = useMutation({
    mutationFn: (igdbId: number) => request<ConsolesPage>("/api/consoles", { method: "POST", body: JSON.stringify({ igdbId }) }),
    onSuccess: done,
  });
  const saveDays = useMutation({
    mutationFn: (d: number) => request<ConsolesPage>("/api/catalog/refresh", { method: "PUT", body: JSON.stringify({ days: d }) }),
    onSuccess: done,
  });
  const refresh = useMutation({
    mutationFn: () => request<number>("/api/catalog/refresh", { method: "POST" }),
    onSuccess: () => cache.invalidateQueries({ queryKey: ["tasks"] }),
  });

  if (!page.data) return page.isError ? <p className="error" role="alert">{describe(page.error)}</p> : null;
  const p = page.data;
  const date = (iso: string) => format.dateTime(new Date(iso), { dateStyle: "medium" });
  const enabledIds = new Set(p.consoles.filter((c) => c.enabled).map((c) => c.id));
  const text = query.trim().toLowerCase();
  const error = toggle.error ?? enable.error ?? saveDays.error ?? refresh.error ?? igdb.error;

  return (
    <div className="panel" style={{ display: "grid", gap: 10 }}>
      <p className="small muted" style={{ margin: 0 }}>{t("help")}</p>
      <ul style={{ listStyle: "none", margin: 0, padding: 0, display: "grid", gap: 4, gridTemplateColumns: "repeat(auto-fill, minmax(260px, 1fr))" }}>
        {p.consoles.map((c) => (
          <li key={c.id}>
            <label className="small" style={{ display: "flex", gap: 6, alignItems: "baseline" }}>
              <input type="checkbox" checked={c.enabled} onChange={(e) => toggle.mutate({ id: c.id, enabled: e.target.checked })} />
              <span>
                <b>{c.name}</b>{" "}
                <span className="muted">· {t("games", { count: Number(c.games) })} · {c.refreshedAt ? t("refreshedAt", { at: date(c.refreshedAt) }) : t("never")}</span>
              </span>
            </label>
          </li>
        ))}
      </ul>
      {!adding ? (
        <div className="hero-actions"><button className="ot-btn" type="button" onClick={() => setAdding(true)}>{t("add")}</button></div>
      ) : (
        <div style={{ display: "grid", gap: 6 }}>
          <div style={{ display: "flex", gap: 6 }}>
            <input type="search" style={{ flex: 1 }} aria-label={t("search")} placeholder={t("search")} value={query} onChange={(e) => setQuery(e.target.value)}
              onKeyDown={(e) => { if (e.key === "Escape") close(); }} autoFocus />
            <button className="ot-btn" type="button" onClick={close}>{t("close")}</button>
          </div>
          <ul style={{ listStyle: "none", margin: 0, padding: 0, maxHeight: 260, overflowY: "auto", display: "grid", gap: 4 }}>
            {igdb.data?.filter((x) => !text || x.name.toLowerCase().includes(text) || x.abbreviation?.toLowerCase().includes(text)).map((x) => {
              const added = x.consoleId != null && enabledIds.has(x.consoleId);
              return (
                <li key={x.igdbId} className="small" style={{ display: "flex", gap: 8, alignItems: "center" }}>
                  <span style={{ flex: 1 }}>{x.name}{x.abbreviation ? <span className="muted"> · {x.abbreviation}</span> : null}</span>
                  {added ? <span className="muted">{t("already")}</span>
                    : <button className="ot-btn" type="button" disabled={enable.isPending} onClick={() => enable.mutate(Number(x.igdbId))}>{t("enable")}</button>}
                </li>
              );
            })}
          </ul>
        </div>
      )}
      <form className="chips" style={{ alignItems: "end" }} onSubmit={(e) => { e.preventDefault(); saveDays.mutate(days ?? Number(p.refreshDays)); }}>
        <label className="small" style={{ display: "grid", gap: 4 }}>
          {t("refreshEvery")}
          <input type="number" min={1} max={365} value={days ?? Number(p.refreshDays)} onChange={(e) => setDays(Number(e.target.value))} />
        </label>
        <button className="ot-btn" type="submit" disabled={saveDays.isPending}>{t("save")}</button>
        <button className="ot-btn" type="button" disabled={refresh.isPending} onClick={() => refresh.mutate()}>{t("refreshNow")}</button>
        {refresh.isSuccess && <span className="small muted">{t("queued")}</span>}
      </form>
      {p.lastRefresh && <p className="small muted" style={{ margin: 0 }}>{t("lastRefresh", { at: date(p.lastRefresh) })}</p>}
      {error && <p className="error small" role="alert">{describe(error)}</p>}
    </div>
  );
}
