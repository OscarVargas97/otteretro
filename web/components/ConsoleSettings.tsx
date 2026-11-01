"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useFormatter, useTranslations } from "next-intl";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { librariesQuery } from "@/components/Libraries";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import { optimistic } from "@/lib/optimistic";
import type { ConsoleDetail, ConsoleFolder, ConsoleInfo, ConsolesPage, CoversInfo, LibraryInfo, PlatformInfo } from "@/lib/types";

const GROUPS = ["nintendo", "playstation", "xbox", "pc", "other"] as const;
const groupOf = (c: ConsoleInfo): (typeof GROUPS)[number] =>
  c.id === "pc" || c.id === "pcvr" ? "pc" : c.company === "Nintendo" ? "nintendo" : c.company === "Sony" ? "playstation" : c.company === "Microsoft" ? "xbox" : "other";
const day = (iso: string) => iso.slice(0, 10);

/**
 * Ajustes › Consolas (F2.13, F2.14, #109, #192): arriba cada cuánto se pone al día el catálogo; abajo la lista de consolas
 * por empresa y, al lado, el detalle de la elegida (`?c=`). La fecha de carga va una sola vez arriba y una fila solo lleva
 * insignia si difiere.
 */
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
    onSuccess: (data) => { done(data); setDays(null); },
  });
  const refresh = useMutation({
    mutationFn: () => request<number>("/api/catalog/refresh", { method: "POST" }),
    onSuccess: () => cache.invalidateQueries({ queryKey: ["tasks"] }),
  });

  // Portadas a pedido (#132): mientras hay una descarga, el total se refresca solo.
  const covers = useQuery({ queryKey: ["covers"], queryFn: () => request<CoversInfo>("/api/covers"), refetchInterval: (q) => (q.state.data?.running ? 5_000 : false) });
  const downloadCovers = useMutation({
    mutationFn: () => request<CoversInfo>("/api/covers", { method: "POST" }),
    onSuccess: (data) => { cache.setQueryData(["covers"], data); cache.invalidateQueries({ queryKey: ["tasks"] }); },
  });

  const router = useRouter();
  const chosen = useSearchParams().get("c");

  if (!page.data) return page.isError ? <p className="error" role="alert">{describe(page.error)}</p> : null;
  const p = page.data;
  const date = (iso: string) => format.dateTime(new Date(iso), { day: "numeric", month: "short" });
  const enabledIds = new Set(p.consoles.filter((c) => c.enabled).map((c) => c.id));
  const text = query.trim().toLowerCase();
  const error = toggle.error ?? enable.error ?? saveDays.error ?? refresh.error ?? igdb.error;

  // La fecha de carga más común entre las habilitadas va arriba; una fila solo dice algo si difiere.
  const loaded = p.consoles.filter((c) => c.enabled && c.refreshedAt).map((c) => day(c.refreshedAt!));
  const common = loaded.sort((a, b) => loaded.filter((x) => x === b).length - loaded.filter((x) => x === a).length)[0];
  const allSame = !!common && p.consoles.every((c) => !c.enabled || (c.refreshedAt && day(c.refreshedAt) === common && c.loading == null));
  const badge = (c: ConsoleInfo) => {
    if (!c.enabled) return null;
    if (c.loading != null) return <span className="ot-badge ot-badge-info">{t("loading", { progress: Number(c.loading) })}</span>;
    if (!c.refreshedAt) return <span className="ot-badge">{Number(c.games) > 0 ? t("partial") : t("notLoaded")}</span>;
    if (day(c.refreshedAt) !== common) return <span className="ot-badge">{t("upToDateAt", { at: date(c.refreshedAt) })}</span>;
    return null;
  };

  // En la lista va solo el número de juegos; el estado de carga, en el detalle (#192).
  const status = (c?: ConsoleInfo) => c && <>{badge(c)}<span className="small muted tabular">{t("games", { count: Number(c.games) })}</span></>;

  const last = p.lastRefresh ? new Date(p.lastRefresh) : null;
  const next = last ? new Date(last.getTime() + Number(p.refreshDays) * 86_400_000) : null;
  const soon = next && next.getTime() - Date.now() < 7 * 86_400_000;
  const commitDays = () => { if (days != null && days !== Number(p.refreshDays) && days >= 1) saveDays.mutate(days); };

  return (
    <div className="settings-blocks">
      <section className="panel">
        <form className="sentence" onSubmit={(e) => { e.preventDefault(); commitDays(); }}>
          <label htmlFor="refresh-days">{t("every")}</label>
          <input id="refresh-days" type="number" min={1} max={365} style={{ width: 72 }} value={days ?? Number(p.refreshDays)}
            onChange={(e) => setDays(Number(e.target.value))} onBlur={commitDays} />
          <span>{t("days")}</span>
          {last && <span className="small muted">· {t("lastAgo", { ago: format.relativeTime(last, new Date()) })}</span>}
          {next && <span className="small muted">· {t("nextOn", { when: format.dateTime(next, soon ? { weekday: "long" } : { day: "numeric", month: "short" }) })}</span>}
          <button className="ot-btn" type="button" style={{ marginLeft: "auto" }} disabled={refresh.isPending} onClick={() => refresh.mutate()}>{t("refreshNow")}</button>
        </form>
        {refresh.isSuccess && <span className="small muted">{t("queued")}</span>}
      </section>

      {covers.data && (
        <section className="panel" aria-labelledby="covers-title">
          <div className="version-row">
            <div>
              <h2 id="covers-title">{t("covers.title")}</h2>
              <span className="small muted tabular">{t("covers.count", { have: Number(covers.data.have), total: Number(covers.data.total) })}</span>
            </div>
            {!covers.data.hasKey ? <Link className="ot-btn" href="/settings/?s=credentials">{t("covers.noKey")}</Link>
              : <button className="ot-btn" type="button" disabled={covers.data.running || downloadCovers.isPending} onClick={() => downloadCovers.mutate()}>
                  {covers.data.running ? t("covers.running") : t("covers.download")}
                </button>}
          </div>
          {downloadCovers.error && <p className="error small" role="alert">{describe(downloadCovers.error)}</p>}
        </section>
      )}

      <section className="panel">
        <div className="version-row">
          <span className="small muted">{common ? (allSame ? t("allUpToDate", { at: date(common) }) : t("mostUpToDate", { at: date(common) })) : t("help")}</span>
          {!adding && <button className="ot-btn" type="button" onClick={() => setAdding(true)}>{t("add")}</button>}
        </div>
        {adding && (
          <div style={{ display: "grid", gap: 6 }}>
            <div style={{ display: "flex", gap: 6 }}>
              <input type="search" style={{ flex: 1 }} aria-label={t("search")} placeholder={t("search")} value={query} onChange={(e) => setQuery(e.target.value)}
                onKeyDown={(e) => { if (e.key === "Escape") close(); }} autoFocus />
              <button className="ot-btn" type="button" onClick={close}>{t("close")}</button>
            </div>
            <ul className="console-list" style={{ maxHeight: 260, overflowY: "auto" }}>
              {igdb.data?.filter((x) => !text || x.name.toLowerCase().includes(text) || x.abbreviation?.toLowerCase().includes(text)).map((x) => {
                const added = x.consoleId != null && enabledIds.has(x.consoleId);
                return (
                  <li key={x.igdbId} className="console-row">
                    <span className="console-name">{x.name}{x.abbreviation ? <span className="muted"> · {x.abbreviation}</span> : null}</span>
                    {added ? <span className="small muted">{t("already")}</span>
                      : <button className="ot-btn ot-btn-sm" type="button" disabled={enable.isPending} onClick={() => enable.mutate(Number(x.igdbId))}>{t("enable")}</button>}
                  </li>
                );
              })}
            </ul>
          </div>
        )}
        <div className="console-split">
          <div className="console-list">
            {GROUPS.map((g) => {
              const rows = p.consoles.filter((c) => groupOf(c) === g);
              if (rows.length === 0) return null;
              return (
                <div key={g}>
                  <h3 className="small muted console-group">{t(`group.${g}`)}</h3>
                  <ul className="console-list">
                    {rows.map((c) => (
                      <li key={c.id} className={`console-row${c.enabled ? "" : " muted"}`} aria-current={c.id === chosen}>
                        <input type="checkbox" role="switch" aria-label={c.name} checked={c.enabled} onChange={(e) => toggle.mutate({ id: c.id, enabled: e.target.checked })} />
                        <button type="button" className="link-button console-name" aria-label={t("detail.open", { name: c.name })}
                          onClick={() => router.push(`/settings/?s=consoles&c=${c.id}`)}>
                          <b>{c.name}</b>
                        </button>
                        <span className="small muted tabular console-games">{Number(c.games)}</span>
                      </li>
                    ))}
                  </ul>
                </div>
              );
            })}
          </div>
          {chosen ? <ConsoleDetailPanel id={chosen} status={status(p.consoles.find((x) => x.id === chosen))} />
            : <p className="missing small">{t("detail.pick")}</p>}
        </div>
      </section>
      {error && <p className="error small" role="alert">{describe(error)}</p>}
    </div>
  );
}

const detailKey = (id: string) => ["consoles", "detail", id];

/**
 * Todo lo de una consola junto (#192): sus juegos (en qué ubicación, a cuál van los nuevos y moverlos), sus emuladores,
 * ports con el juego que cubren, complementos y sistema. Lo que falta se dice.
 */
function ConsoleDetailPanel({ id, status }: { id: string; status: React.ReactNode }) {
  const t = useTranslations("settings.consoles.detail");
  const describe = useDescribeError();
  const cache = useQueryClient();
  const detail = useQuery({ queryKey: detailKey(id), queryFn: () => request<ConsoleDetail>(`/api/consoles/${id}/detail`) });
  const locations = useQuery(librariesQuery).data ?? [];
  const games = locations.filter((l) => (l.type === "disk" || l.type === "game") && l.connected);
  const destination = useMutation({
    mutationFn: (locationId: string | null) => request<ConsoleDetail>(`/api/consoles/${id}/destination`, { method: "PUT", body: JSON.stringify({ locationId }) }),
    onSuccess: (data) => cache.setQueryData(detailKey(id), data),
  });
  if (detail.isError) return <p className="error small" role="alert">{describe(detail.error)}</p>;
  if (!detail.data) return <div className="panel skeleton" style={{ height: 160 }} aria-busy="true" />;
  const d = detail.data;
  const where = (l?: string | null) => (l ? t("inLocation", { location: locations.find((x) => x.id === l)?.name ?? l }) : t("inDataFolder"));
  const tools = (list: typeof d.emulators, empty: string) => list.length === 0 ? <p className="missing small">{empty}</p> : (
    <ul className="detail-list">
      {list.map((x) => (
        <li key={x.path}>
          <span className="grow"><b>{x.name}</b><span className="small muted">{x.game ? t("covers", { game: x.game }) : where(x.locationId)}</span></span>
          <span className="ot-badge">{x.version}</span>
        </li>
      ))}
    </ul>
  );

  return (
    <div className="console-detail settings-blocks" aria-label={d.name}>
      <div className="sentence"><h2>{d.name}</h2>{status}</div>
      <section aria-labelledby="detail-games">
        <h3 id="detail-games" className="small muted console-group">{t("games")}</h3>
        {d.games.length === 0 ? <p className="missing small">{t("noGames")}</p> : (
          <ul className="detail-list">{d.games.map((f) => <GameFolder key={`${f.libraryId}/${f.relative}`} folder={f} locations={games} />)}</ul>
        )}
        <div className="sentence small">
          <label htmlFor={`destination-${id}`}>{t("destination")}</label>
          <select id={`destination-${id}`} value={d.destination ?? ""} disabled={destination.isPending}
            onChange={(e) => destination.mutate(e.target.value || null)}>
            <option value="">{t("noDestination")}</option>
            {games.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
          </select>
        </div>
        {destination.isError && <p className="error small" style={{ margin: 0 }}>{describe(destination.error)}</p>}
      </section>
      <section aria-labelledby="detail-emulators">
        <h3 id="detail-emulators" className="small muted console-group">{t("emulators")}</h3>
        {tools(d.emulators, t("noEmulators"))}
      </section>
      <section aria-labelledby="detail-ports">
        <h3 id="detail-ports" className="small muted console-group">{t("ports")}</h3>
        {tools(d.ports, t("noPorts"))}
      </section>
      {d.complements.length > 0 && (
        <section aria-labelledby="detail-complements">
          <h3 id="detail-complements" className="small muted console-group">{t("complements")}</h3>
          {tools(d.complements, "")}
        </section>
      )}
      <section aria-labelledby="detail-system">
        <h3 id="detail-system" className="small muted console-group">{t("system")}</h3>
        {d.system.length === 0 ? <p className="missing small">{t("noSystem")}</p> : (
          <ul className="detail-list">
            {d.system.map((f) => <li key={`${f.libraryId}/${f.relative}`}><span className="grow">{where(f.libraryId)}</span></li>)}
          </ul>
        )}
      </section>
    </div>
  );
}

/** La carpeta de la consola en una ubicación de juegos, y moverla a otra (#190). */
function GameFolder({ folder, locations }: { folder: ConsoleFolder; locations: LibraryInfo[] }) {
  const t = useTranslations("settings.consoles.location");
  const describe = useDescribeError();
  const cache = useQueryClient();
  const others = locations.filter((l) => l.id !== folder.libraryId);
  const [open, setOpen] = useState(false);
  const [to, setTo] = useState("");
  const move = useMutation({
    mutationFn: () => request<number>(`/api/locations/${folder.libraryId}/move-folder`,
      { method: "POST", body: JSON.stringify({ relative: folder.relative, toLibrary: to || others[0]?.id }) }),
    onSuccess: () => { setOpen(false); cache.invalidateQueries({ queryKey: ["tasks"] }); },
  });
  const name = locations.find((l) => l.id === folder.libraryId)?.name ?? folder.libraryId;
  return (
    <li className="console-location small">
      <span className="grow">{t("in", { library: name })}</span>
      {move.isSuccess && <span className="muted" role="status">{t("queued")}</span>}
      {!open && others.length > 0 && <button className="ot-btn ot-btn-sm ot-btn-ghost" type="button" onClick={() => { setOpen(true); setTo(others[0].id); move.reset(); }}>{t("move")}</button>}
      {open && (
        <span style={{ display: "inline-flex", gap: 6, alignItems: "center", flexWrap: "wrap" }}>
          <label className="muted" htmlFor={`to-${folder.libraryId}-${folder.relative}`}>{t("to")}</label>
          <select id={`to-${folder.libraryId}-${folder.relative}`} value={to} onChange={(e) => setTo(e.target.value)}>
            {others.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
          </select>
          <button className="ot-btn ot-btn-sm" type="button" onClick={() => setOpen(false)}>{t("cancel")}</button>
          <button className="ot-btn ot-btn-sm ot-btn-primary" type="button" disabled={move.isPending} onClick={() => move.mutate()}>{t("confirm")}</button>
        </span>
      )}
      {move.isError && <p className="error small" style={{ margin: 0 }}>{describe(move.error)}</p>}
    </li>
  );
}
