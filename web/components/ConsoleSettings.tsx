"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { librariesQuery } from "@/components/Libraries";
import { useState } from "react";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import type { ConsoleDetail, ConsoleFolder, ConsoleInfo, ConsolesPage, DiskInfo } from "@/lib/types";

const GROUPS = ["nintendo", "playstation", "xbox", "pc", "other"] as const;
const groupOf = (c: ConsoleInfo): (typeof GROUPS)[number] =>
  c.id === "pc" || c.id === "pcvr" ? "pc" : c.company === "Nintendo" ? "nintendo" : c.company === "Sony" ? "playstation" : c.company === "Microsoft" ? "xbox" : "other";

/**
 * Ajustes › Consolas: por empresa, cada consola con su detalle al lado (`?c=`) y si la app la muestra (#224).
 */
export function ConsoleSettings() {
  const t = useTranslations("settings.consoles");
  const describe = useDescribeError();
  const cache = useQueryClient();
  const page = useQuery({ queryKey: ["consoles", "settings"], queryFn: () => request<ConsolesPage>("/api/consoles/settings") });

  // Mostrar u ocultar una consola solo cambia lo que se ve (#224): Sagas, Colección y la búsqueda se recalculan.
  const visible = useMutation({
    mutationFn: ({ id, value }: { id: string; value: boolean }) =>
      request<ConsolesPage>(`/api/consoles/${id}/visible`, { method: "PUT", body: JSON.stringify({ visible: value }) }),
    // Cambia al instante (el servidor rehace Sagas antes de responder, #225); si falla, vuelve como estaba.
    onMutate: ({ id, value }) => {
      const before = cache.getQueryData<ConsolesPage>(["consoles", "settings"]);
      if (before) cache.setQueryData(["consoles", "settings"], { ...before, consoles: before.consoles.map((c) => (c.id === id ? { ...c, visible: value } : c)) });
      return { before };
    },
    onError: (_error, _vars, context) => { if (context?.before) cache.setQueryData(["consoles", "settings"], context.before); },
    onSuccess: (data) => {
      cache.setQueryData(["consoles", "settings"], data);
      for (const key of ["sagas", "saga", "collection", "consoles", "search", "home"]) cache.invalidateQueries({ queryKey: [key] });
    },
  });

  const router = useRouter();
  const chosen = useSearchParams().get("c");

  if (!page.data) return page.isError ? <p className="error" role="alert">{describe(page.error)}</p> : null;
  const p = page.data;
  const status = (c?: ConsoleInfo) => c && <span className="small muted tabular">{t("games", { count: Number(c.games) })}</span>;

  return (
    <div className="settings-blocks">
      <p className="small muted" style={{ margin: 0 }}>{t("help")}</p>
      <section className="panel">
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
                      <li key={c.id} className="console-row" aria-current={c.id === chosen}>
                        <button type="button" className="link-button console-name" aria-label={t("detail.open", { name: c.name })}
                          onClick={() => router.push(`/settings/?s=consoles&c=${c.id}`)}>
                          <b>{c.name}</b>
                        </button>
                        <span className="small muted tabular console-games">{Number(c.games)}</span>
                        <input type="checkbox" role="switch" aria-label={t("show", { name: c.name })} checked={c.visible}
                          onChange={(e) => visible.mutate({ id: c.id, value: e.target.checked })} />
                      </li>
                    ))}
                  </ul>
                </div>
              );
            })}
          </div>
          {visible.error && <p className="error small" role="alert">{describe(visible.error)}</p>}
          {chosen ? <ConsoleDetailPanel id={chosen} status={status(p.consoles.find((x) => x.id === chosen))} />
            : <p className="missing small">{t("detail.pick")}</p>}
        </div>
      </section>
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
function GameFolder({ folder, locations }: { folder: ConsoleFolder; locations: DiskInfo[] }) {
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
