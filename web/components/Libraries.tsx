"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { useState } from "react";
import { request } from "@/lib/client";
import { gb, size } from "@/lib/format";
import { useDescribeError } from "@/lib/i18n";
import type { DiskItem, Drive, LibraryInfo, MigrationPlan, ToolInfo } from "@/lib/types";

/** Los discos y las ubicaciones de antes (#198, #201). La clave sigue siendo «libraries»: así se llaman en la base. */
export const librariesQuery = { queryKey: ["libraries"], queryFn: () => request<LibraryInfo[]>("/api/locations") };
export const toolsQuery = { queryKey: ["tools"], queryFn: () => request<ToolInfo[]>("/api/tools") };

/** Lo que guarda cada disco, en sus carpetas fijas y en este orden (#201). */
export const TYPES = ["game", "emulator", "port", "complement", "system"] as const;
export type LocationType = (typeof TYPES)[number];

const post = (body: unknown) => ({ method: "POST", body: JSON.stringify(body) });

/**
 * Ajustes › Almacenamiento (#201 a #205), como el de Steam: los discos arriba, el uso del elegido por tipo y lo que tiene, para
 * marcar y mover a otro disco. Solo se eligen discos; las carpetas las arma la app.
 */
export function StorageSettings() {
  const t = useTranslations("libraries");
  const describe = useDescribeError();
  const locations = useQuery({ ...librariesQuery, refetchInterval: (q) => (q.state.data?.some((l) => !l.connected) ? 10_000 : false) });
  const disks = (locations.data ?? []).filter((l) => l.type === "disk");
  const [picked, setPicked] = useState<string | null>(null);
  const [adding, setAdding] = useState(false);
  const current = disks.find((d) => d.id === picked) ?? disks.find((d) => d.default) ?? disks[0];

  return (
    <div className="settings-blocks">
      {locations.isError && <p className="error small" role="alert">{describe(locations.error)}</p>}
      <Migration />
      <div className="disk-picker" role="group" aria-label={t("disks")}>
        {disks.map((d) => <DiskCard key={d.id} disk={d} active={d.id === current?.id} onPick={() => { setPicked(d.id); setAdding(false); }} />)}
        <button className={`disk-card disk-add${adding ? " active" : ""}`} type="button" aria-pressed={adding} onClick={() => setAdding(!adding)}>
          <b>{t("addDisk")}</b>
        </button>
      </div>
      {locations.data && disks.length === 0 && !adding && <p className="missing small">{t("noDisks")}</p>}
      {adding && <AddDisk disks={disks} onDone={(id) => { setPicked(id); setAdding(false); }} />}
      {!adding && current && <DiskPanel key={current.id} disk={current} others={disks.filter((d) => d.id !== current.id && d.connected)} />}
    </div>
  );
}

function DiskCard({ disk, active, onPick }: { disk: LibraryInfo; active: boolean; onPick: () => void }) {
  const t = useTranslations("libraries");
  const locale = useLocale();
  return (
    <button className={`disk-card${active ? " active" : ""}`} type="button" aria-pressed={active} onClick={onPick}>
      <b>{disk.name}{disk.default && <span title={t("default")}> ★</span>}</b>
      <span className="small muted">
        {!disk.connected ? t("disconnected") : disk.free != null && disk.total != null ? t("free", { free: gb(disk.free, locale), total: gb(disk.total, locale) }) : disk.path}
      </span>
    </button>
  );
}

/** Lo que falta pasar al estándar (#159): las carpetas de antes y Herramientas de la carpeta de datos. */
function Migration() {
  const t = useTranslations("libraries");
  const describe = useDescribeError();
  const cache = useQueryClient();
  const plan = useQuery({ queryKey: ["libraries", "migration"], queryFn: () => request<MigrationPlan>("/api/disks/migration") });
  const run = useMutation({
    mutationFn: () => request<number>("/api/disks/migrate", post({})),
    onSuccess: () => cache.invalidateQueries({ queryKey: ["tasks"] }),
  });
  const p = plan.data;
  if (!p || (p.locations.length === 0 && !p.tools)) return null;
  return (
    <section className="panel" aria-labelledby="migration">
      <h2 id="migration">{t("migration.title")}</h2>
      <p className="small" style={{ margin: 0 }}>{t("migration.help")}</p>
      <ul className="detail-list small">
        {p.locations.map((l) => <li key={l.id}><span className="grow"><b>{l.name}</b> <span className="folder-path">{l.path}</span></span>
          <span className="muted">→ {t(`type.${l.type as LocationType}.title`)}</span></li>)}
        {p.tools && <li><span className="grow"><b>{t("migration.tools")}</b></span><span className="muted">→ {t("migration.toDefault")}</span></li>}
      </ul>
      {run.isSuccess ? <p className="small muted" role="status" style={{ margin: 0 }}>{t("migrationStarted")}</p> : (
        <div className="sentence">
          <button className="ot-btn ot-btn-primary ot-btn-sm" type="button" disabled={run.isPending} onClick={() => run.mutate()}>{t("migration.run")}</button>
        </div>
      )}
      {run.isError && <p className="error small" style={{ margin: 0 }}>{describe(run.error)}</p>}
    </section>
  );
}

/** Agregar un disco (#158): se elige la unidad y la app crea `Otteretro` con sus cinco carpetas. */
function AddDisk({ disks, onDone }: { disks: LibraryInfo[]; onDone: (id: string) => void }) {
  const t = useTranslations("libraries");
  const locale = useLocale();
  const describe = useDescribeError();
  const cache = useQueryClient();
  const drives = useQuery({ queryKey: ["drives"], queryFn: () => request<Drive[]>("/api/drives") });
  const free = (drives.data ?? []).filter((d) => !disks.some((k) => k.path.toLowerCase() === d.folder.toLowerCase()));
  const add = useMutation({
    mutationFn: (d: Drive) => request<LibraryInfo[]>("/api/disks", post({ path: d.folder, name: d.name.replace(/[\\/]+$/, "") || d.name })),
    onSuccess: (list, d) => {
      cache.setQueryData(["libraries"], list);
      cache.invalidateQueries({ queryKey: ["libraries", "migration"] });
      onDone(list.find((l) => l.path.toLowerCase() === d.folder.toLowerCase())?.id ?? "");
    },
  });
  return (
    <section className="panel" aria-labelledby="add-disk">
      <h2 id="add-disk">{t("addDisk")}</h2>
      <p className="small muted" style={{ margin: 0 }}>{t("addDiskHelp")}</p>
      {drives.data && free.length === 0 && <p className="missing small">{t("noDrives")}</p>}
      <ul className="detail-list">
        {free.map((d) => (
          <li key={d.name}>
            <span className="grow"><b>{d.name}</b> <span className="small muted">{t("free", { free: gb(d.free, locale), total: gb(d.total, locale) })}</span>
              <span className="folder-path">{d.folder}</span></span>
            <button className="ot-btn ot-btn-sm ot-btn-primary" type="button" disabled={add.isPending} onClick={() => add.mutate(d)}>{t("add")}</button>
          </li>
        ))}
      </ul>
      {add.isError && <p className="error small" style={{ margin: 0 }}>{describe(add.error)}</p>}
    </section>
  );
}

/** Un disco: su uso por tipo, lo que tiene (para marcar y mover a otro) y hacerlo predeterminado, reconectarlo o quitarlo. */
function DiskPanel({ disk, others }: { disk: LibraryInfo; others: LibraryInfo[] }) {
  const t = useTranslations("libraries");
  const locale = useLocale();
  const describe = useDescribeError();
  const cache = useQueryClient();
  const items = useQuery({ queryKey: ["libraries", "contents", disk.id], queryFn: () => request<DiskItem[]>(`/api/disks/${disk.id}/contents`), enabled: disk.connected });
  const [marked, setMarked] = useState<string[]>([]);
  const [to, setTo] = useState("");
  const [removing, setRemoving] = useState(false);
  const target = to || others[0]?.id;
  const done = () => {
    cache.invalidateQueries({ queryKey: ["tasks"] });
    cache.invalidateQueries({ queryKey: ["tools"] });
  };
  const action = useMutation({
    mutationFn: async (kind: "default" | "reconnect" | "remove" | "move") => {
      switch (kind) {
        case "default": return request<LibraryInfo[]>(`/api/disks/${disk.id}/default`, { method: "PUT" });
        case "reconnect": return request<LibraryInfo[]>(`/api/locations/${disk.id}/reconnect`, post({ path: null }));
        case "remove": return request<LibraryInfo[]>(`/api/locations/${disk.id}`, { method: "DELETE" });
        case "move": await request<number[]>(`/api/disks/${disk.id}/move`, post({ items: marked, toLocation: target })); setMarked([]); return null;
      }
    },
    onSuccess: (list) => { if (list) cache.setQueryData(["libraries"], list); setRemoving(false); done(); },
  });

  const all = items.data ?? [];
  const used = (type: string) => all.filter((i) => i.type === type).reduce((s, i) => s + Number(i.size), 0);
  const total = Number(disk.total ?? 0);
  const ours = TYPES.reduce((s, type) => s + used(type), 0);
  const other = Math.max(0, total - Number(disk.free ?? 0) - ours);
  const share = (n: number) => (total > 0 ? `${(n * 100) / total}%` : "0");
  const toggle = (relative: string) => setMarked((m) => (m.includes(relative) ? m.filter((x) => x !== relative) : [...m, relative]));

  return (
    <section className="panel" aria-labelledby="disk-title">
      <div className="version-row">
        <div className="grow">
          <h2 id="disk-title" style={{ margin: 0 }}>{disk.name}{disk.default && " ★"}</h2>
          <span className="folder-path">{disk.path}</span>
        </div>
        <span style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
          {disk.kind === "network" && <span className="ot-badge">{t("network")}</span>}
          <span className={`ot-badge ${disk.connected ? "ot-badge-have" : "ot-badge-danger"}`}>{disk.connected ? t("connected") : t("disconnected")}</span>
        </span>
      </div>
      <div className="sentence">
        {!disk.default && <button className="ot-btn ot-btn-sm" type="button" disabled={action.isPending} onClick={() => action.mutate("default")}>{t("makeDefault")}</button>}
        {!disk.connected && <button className="ot-btn ot-btn-sm ot-btn-primary" type="button" disabled={action.isPending} onClick={() => action.mutate("reconnect")}>{t("reconnect")}</button>}
        <button className="ot-btn ot-btn-sm ot-btn-ghost" type="button" onClick={() => setRemoving(true)}>{t("remove")}</button>
      </div>
      {disk.default && <p className="small muted" style={{ margin: 0 }}>{t("defaultHelp")}</p>}
      {removing && (
        <div className="form-block" role="alertdialog" aria-label={t("remove")}>
          <p className="small" style={{ margin: 0 }}>{t("removeHelp", { name: disk.name })}</p>
          <div style={{ display: "flex", gap: 8 }}>
            <button className="ot-btn" type="button" onClick={() => setRemoving(false)}>{t("cancel")}</button>
            <button className="ot-btn ot-btn-primary" type="button" disabled={action.isPending} onClick={() => action.mutate("remove")}>{t("removeConfirm")}</button>
          </div>
        </div>
      )}

      {disk.connected && total > 0 && (
        <>
          <div className="usage-bar" role="img" aria-label={t("usage")}>
            {TYPES.map((type) => <span key={type} className={`usage-${type}`} style={{ width: share(used(type)) }} />)}
            <span className="usage-other" style={{ width: share(other) }} />
          </div>
          <ul className="usage-legend small">
            {TYPES.map((type) => <li key={type}><i className={`usage-${type}`} />{t(`type.${type}.title`)}: {size(used(type), locale)}</li>)}
            <li><i className="usage-other" />{t("otherUse")}: {gb(other, locale)}</li>
            <li><i className="usage-free" />{t("freeSpace")}: {gb(disk.free ?? 0, locale)}</li>
          </ul>
        </>
      )}

      {items.isError && <p className="error small" style={{ margin: 0 }}>{describe(items.error)}</p>}
      {items.data && all.length === 0 && <p className="missing small">{t("diskEmpty")}</p>}
      {TYPES.filter((type) => all.some((i) => i.type === type)).map((type) => (
        <fieldset key={type} className="disk-items">
          <legend>{t(`type.${type}.title`)}</legend>
          {all.filter((i) => i.type === type).map((i) => (
            <label key={i.relative} className="disk-item small">
              <input type="checkbox" checked={marked.includes(i.relative)} onChange={() => toggle(i.relative)} />
              <span className="grow">{i.name} <span className="muted">· {i.group}</span></span>
              <span className="muted">{size(i.size, locale)}</span>
            </label>
          ))}
        </fieldset>
      ))}

      {all.length > 0 && (
        <div className="sentence">
          {others.length === 0 ? <span className="small muted">{t("noOtherDisk")}</span> : (
            <>
              <label className="small muted" htmlFor={`to-${disk.id}`}>{t("moveTo", { count: marked.length })}</label>
              <select id={`to-${disk.id}`} value={target} onChange={(e) => setTo(e.target.value)}>
                {others.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
              </select>
              <button className="ot-btn ot-btn-sm ot-btn-primary" type="button" disabled={marked.length === 0 || action.isPending} onClick={() => action.mutate("move")}>{t("move")}</button>
            </>
          )}
        </div>
      )}
      {action.isSuccess && action.variables === "move" && <p className="small muted" role="status" style={{ margin: 0 }}>{t("queued")}</p>}
      {action.isError && <p className="error small" style={{ margin: 0 }}>{describe(action.error)}</p>}
    </section>
  );
}
