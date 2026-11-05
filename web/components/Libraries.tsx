"use client";

import { RecognizeSettings } from "@/components/Recognize";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { useEffect, useMemo, useRef, useState } from "react";
import { DataMoveStatus, UseOtherFolder, useMove, useMoveData } from "@/components/DataFolder";
import { request } from "@/lib/client";
import { gb, size } from "@/lib/format";
import { useDescribeError } from "@/lib/i18n";
import type { DiskItem, Drive, FolderInfo, DiskInfo, MigrationPlan, ToolInfo } from "@/lib/types";

/** Los discos y las ubicaciones de antes (#198, #201). La clave sigue siendo «libraries»: así se llaman en la base. */
export const librariesQuery = { queryKey: ["libraries"], queryFn: () => request<DiskInfo[]>("/api/locations") };
export const toolsQuery = { queryKey: ["tools"], queryFn: () => request<ToolInfo[]>("/api/tools") };
const contentsQuery = (id: string) => ({ queryKey: ["libraries", "contents", id], queryFn: () => request<DiskItem[]>(`/api/disks/${id}/contents`) });

/** Lo que guarda cada disco, en sus carpetas fijas y en este orden (#201). */
export const TYPES = ["game", "emulator", "port", "complement", "system"] as const;
export type LocationType = (typeof TYPES)[number];

const post = (body: unknown) => ({ method: "POST", body: JSON.stringify(body) });

/** La fila de los datos de la app en la tabla del disco ★ (#169): se marca y se mueve como lo demás. */
const DATA = ":data"; // «:» no va en una ruta de Windows: no choca con nada del disco

/**
 * Ajustes › Almacenamiento, con la distribución de Steam (#169): arriba el disco elegido con «+» y «⋯», al medio su uso por
 * tipo y abajo lo que tiene, para marcar y mover a otro disco. Solo se eligen discos; las carpetas las arma la app.
 */
export function StorageSettings() {
  const t = useTranslations("libraries");
  const describe = useDescribeError();
  const locale = useLocale();
  const locations = useQuery({ ...librariesQuery, refetchInterval: (q) => (q.state.data?.some((l) => !l.connected) ? 10_000 : false) });
  const disks = (locations.data ?? []).filter((l) => l.type === "disk");
  const [picked, setPicked] = useState<string | null>(null);
  const addDialog = useRef<HTMLDialogElement>(null);
  const current = disks.find((d) => d.id === picked) ?? disks.find((d) => d.default) ?? disks[0];
  // Lo del disco llega aparte y agrega filas y tramos al uso: hasta tenerlo, un esqueleto al final, para que nada salte al llegar (#278).
  const contents = useQuery({ ...contentsQuery(current?.id ?? ""), enabled: !!current?.connected });
  const label = (d: DiskInfo) => `${d.name}${d.default ? " ★" : ""} — ${!d.connected ? t("disconnected")
    : d.free != null && d.total != null ? t("free", { free: gb(d.free, locale), total: gb(d.total, locale) }) : d.path}`;

  return (
    <div className="settings-blocks">
      {locations.isError && <p className="error small" role="alert">{describe(locations.error)}</p>}
      <Migration />
      <div className="disk-select">
        <label className="sr-only" htmlFor="disk">{t("disk")}</label>
        <select id="disk" value={current?.id ?? ""} onChange={(e) => setPicked(e.target.value)} disabled={disks.length === 0}>
          {disks.map((d) => <option key={d.id} value={d.id}>{label(d)}</option>)}
        </select>
        <button className="ot-btn" type="button" aria-label={t("addDisk")} title={t("addDisk")} onClick={() => addDialog.current?.showModal()}>+</button>
        {current && <DiskMenu key={current.id} disk={current} onRemoved={() => setPicked(null)} />}
      </div>
      <dialog ref={addDialog} className="ot-dialog" aria-labelledby="add-disk">
        <AddDisk disks={disks} onDone={(id) => { setPicked(id); addDialog.current?.close(); }} onCancel={() => addDialog.current?.close()} />
      </dialog>
      <DataMoveStatus />
      {current?.connected && contents.isPending ? <div className="panel skeleton" style={{ height: 240 }} aria-busy="true" /> : (
        <>
          {current && <DiskPanel key={current.id} disk={current} others={disks.filter((d) => d.id !== current.id && d.connected)} />}
          <RecognizeSettings />
        </>
      )}
    </div>
  );
}

/** «⋯» del disco elegido (#169), como en Steam: abrir su carpeta, reconectar, usar otra carpeta de datos (★) o quitarlo. */
function DiskMenu({ disk, onRemoved }: { disk: DiskInfo; onRemoved: () => void }) {
  const t = useTranslations("libraries");
  const describe = useDescribeError();
  const cache = useQueryClient();
  const menu = useRef<HTMLDetailsElement>(null);
  const removeDialog = useRef<HTMLDialogElement>(null);
  const useDialog = useRef<HTMLDialogElement>(null);
  const action = useMutation({
    mutationFn: async (kind: "open" | "reconnect" | "remove") => {
      switch (kind) {
        case "open": await request(`/api/locations/${disk.id}/open`, { method: "POST" }); return null;
        case "reconnect": return request<DiskInfo[]>(`/api/locations/${disk.id}/reconnect`, post({ path: null }));
        case "remove": return request<DiskInfo[]>(`/api/locations/${disk.id}`, { method: "DELETE" });
      }
    },
    onSuccess: (list, kind) => {
      if (list) cache.setQueryData(["libraries"], list);
      if (kind === "remove") { removeDialog.current?.close(); onRemoved(); }
    },
  });
  const run = (f: () => void) => () => { if (menu.current) menu.current.open = false; f(); };
  return (
    <>
      <details ref={menu} className="ot-menu">
        <summary className="ot-btn" aria-label={t("moreActions")} title={t("moreActions")}>⋯</summary>
        <div className="ot-menu-list" role="menu">
          <button type="button" role="menuitem" disabled={!disk.connected} onClick={run(() => action.mutate("open"))}>{t("openFolder")}</button>
          {!disk.connected && <button type="button" role="menuitem" onClick={run(() => action.mutate("reconnect"))}>{t("reconnect")}</button>}
          {disk.default && <button type="button" role="menuitem" onClick={run(() => useDialog.current?.showModal())}>{t("useOtherData")}</button>}
          {!disk.default && <button type="button" role="menuitem" onClick={run(() => removeDialog.current?.showModal())}>{t("removeDisk")}</button>}
        </div>
      </details>
      {action.isError && action.variables !== "remove" && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(action.error)}</p>}
      <dialog ref={removeDialog} className="ot-dialog" aria-label={t("removeDisk")}>
        <section className="settings-blocks">
          <p className="small" style={{ margin: 0 }}>{t("removeHelp", { name: disk.name })}</p>
          {action.isError && action.variables === "remove" && <p className="error small" style={{ margin: 0 }}>{describe(action.error)}</p>}
          <div style={{ display: "flex", gap: 8 }}>
            <button className="ot-btn" type="button" onClick={() => removeDialog.current?.close()}>{t("cancel")}</button>
            <button className="ot-btn ot-btn-primary" type="button" disabled={action.isPending} onClick={() => action.mutate("remove")}>{t("removeConfirm")}</button>
          </div>
        </section>
      </dialog>
      {disk.default && (
        <dialog ref={useDialog} className="ot-dialog" aria-labelledby="use-folder">
          <UseOtherFolder onDone={() => useDialog.current?.close()} />
        </dialog>
      )}
    </>
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
function AddDisk({ disks, onDone, onCancel }: { disks: DiskInfo[]; onDone: (id: string) => void; onCancel: () => void }) {
  const t = useTranslations("libraries");
  const locale = useLocale();
  const describe = useDescribeError();
  const cache = useQueryClient();
  const drives = useQuery({ queryKey: ["drives"], queryFn: () => request<Drive[]>("/api/drives") });
  const free = (drives.data ?? []).filter((d) => !disks.some((k) => k.path.toLowerCase() === d.folder.toLowerCase()));
  const add = useMutation({
    mutationFn: (d: Drive) => request<DiskInfo[]>("/api/disks", post({ path: d.folder, name: d.name.replace(/[\\/]+$/, "") || d.name })),
    onSuccess: (list, d) => {
      cache.setQueryData(["libraries"], list);
      cache.invalidateQueries({ queryKey: ["libraries", "migration"] });
      onDone(list.find((l) => l.path.toLowerCase() === d.folder.toLowerCase())?.id ?? "");
    },
  });
  return (
    <section className="settings-blocks">
      <h2 id="add-disk" style={{ margin: 0 }}>{t("addDisk")}</h2>
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
      <div><button className="ot-btn" type="button" onClick={onCancel}>{t("cancel")}</button></div>
    </section>
  );
}

/**
 * El disco elegido, como en Steam (#169): su espacio libre, la barra de uso por tipo y lo que tiene, en grupos desplegables
 * (#171): tipo › consola o emulador › cada cosa. Se marca un tipo, una consola entera o cosas sueltas y se mueve a la misma ruta
 * de otro disco; una consola marcada entera va en una sola tarea. En el ★, la primera fila son los datos de la app, que se
 * mueven solos (con su progreso y el ★).
 */
function DiskPanel({ disk, others }: { disk: DiskInfo; others: DiskInfo[] }) {
  const t = useTranslations("libraries");
  const locale = useLocale();
  const describe = useDescribeError();
  const cache = useQueryClient();
  const items = useQuery({ ...contentsQuery(disk.id), enabled: disk.connected });
  const dataUse = Number(useQuery({ queryKey: ["folder"], queryFn: () => request<FolderInfo>("/api/data-folder"), enabled: disk.default }).data?.used ?? 0);
  const moving = useMove()?.running ?? false;
  const [marked, setMarked] = useState<Set<string>>(new Set());
  const [open, setOpen] = useState<Set<string>>(new Set());
  const [to, setTo] = useState("");
  const target = others.find((d) => d.id === to) ?? others[0];
  const all = items.data ?? [];
  const groups = useMemo(() => groupItems(all), [all]);

  const moveData = useMoveData();
  const move = useMutation({
    mutationFn: () => request<number[]>(`/api/disks/${disk.id}/move`, post({ items: payload(groups, all, marked), toLocation: target?.id })),
    onSuccess: () => {
      setMarked(new Set());
      cache.invalidateQueries({ queryKey: ["tasks"] });
      cache.invalidateQueries({ queryKey: ["tools"] });
    },
  });
  const submit = () => {
    if (marked.has(DATA)) moveData.mutate({ path: target!.path }, { onSuccess: () => setMarked(new Set()) });
    else move.mutate();
  };
  const error = move.error ?? moveData.error;

  const used = (type: string) => all.filter((i) => i.type === type).reduce((s, i) => s + Number(i.size), 0);
  const total = Number(disk.total ?? 0);
  const free = Number(disk.free ?? 0);
  const ours = TYPES.reduce((s, type) => s + used(type), 0) + (disk.default ? dataUse : 0);
  const other = Math.max(0, total - free - ours);
  const share = (n: number) => (total > 0 ? `${(n * 100) / total}%` : "0");
  const segments = [
    ...TYPES.map((type) => ({ key: type, label: t(`type.${type}.title`), value: used(type) })),
    ...(disk.default ? [{ key: "data", label: t("dataUse"), value: dataUse }] : []),
  ].filter((s) => s.value > 0);

  // Marcar un grupo marca todo lo de adentro; los datos se mueven solos: marcarlos desmarca lo demás, y al revés.
  const setMany = (ids: string[], on: boolean) => setMarked((m) => {
    const next = new Set(m);
    next.delete(DATA);
    for (const id of ids) if (on) next.add(id); else next.delete(id);
    return next;
  });
  const toggleData = () => setMarked((m) => (m.has(DATA) ? new Set() : new Set([DATA])));
  const toggleOpen = (key: string) => setOpen((o) => { const n = new Set(o); if (n.has(key)) n.delete(key); else n.add(key); return n; });
  const markedSize = all.filter((i) => marked.has(i.relative)).reduce((s, i) => s + Number(i.size), 0) + (marked.has(DATA) ? dataUse : 0);
  const count = marked.size;

  const row = (key: string, level: number, label: string, ids: string[], sizeOf: number, detail?: string, expandable = true) => {
    const on = ids.filter((id) => marked.has(id)).length;
    const expanded = open.has(key);
    return (
      <tr key={key} className={`level-${level}${on ? " is-marked" : ""}`}>
        <td><TriCheck label={label} checked={on > 0 && on === ids.length} partial={on > 0 && on < ids.length} onChange={(v) => setMany(ids, v)} /></td>
        <td>
          {expandable ? (
            <button className="tree-toggle" type="button" aria-expanded={expanded} onClick={() => toggleOpen(key)}>
              <span aria-hidden="true">{expanded ? "▾" : "▸"}</span> {label}
            </button>
          ) : label}
          {detail && <span className="muted small"> · {detail}</span>}
        </td>
        <td className="num">{size(sizeOf, locale)}</td>
      </tr>
    );
  };

  return (
    <section className="panel disk-panel" aria-labelledby="disk-title">
      <div className="version-row">
        <h2 id="disk-title" className="grow" style={{ margin: 0 }}>{disk.name}{disk.default && " ★"}</h2>
        {disk.kind === "network" && <span className="ot-badge">{t("network")}</span>}
        {!disk.connected && <span className="ot-badge ot-badge-danger">{t("disconnected")}</span>}
        {disk.connected && total > 0 && <span className="small muted">{t("free", { free: gb(free, locale), total: gb(total, locale) })}</span>}
      </div>

      {disk.connected && total > 0 && (
        <>
          <div className="usage-bar" role="img" aria-label={t("usage")}>
            {segments.map((s) => <span key={s.key} className={`usage-${s.key}`} style={{ width: share(s.value) }} />)}
            <span className="usage-other" style={{ width: share(other) }} />
          </div>
          <ul className="usage-legend small">
            {segments.map((s) => <li key={s.key}><i className={`usage-${s.key}`} />{s.label} <b>{size(s.value, locale)}</b></li>)}
            <li><i className="usage-other" />{t("otherUse")} <b>{gb(other, locale)}</b></li>
            <li><i className="usage-free" />{t("freeSpace")} <b>{gb(free, locale)}</b></li>
          </ul>
        </>
      )}

      {items.isError && <p className="error small" style={{ margin: 0 }}>{describe(items.error)}</p>}
      {disk.connected && (
        <div className="table-wrap">
          <table className="t disk-table">
            <thead>
              <tr><th aria-hidden="true" /><th>{t("colName")}</th><th className="num">{t("colSize")}</th></tr>
            </thead>
            <tbody>
              {disk.default && (
                <tr className={marked.has(DATA) ? "is-marked" : undefined}>
                  <td><input type="checkbox" aria-label={t("appData")} checked={marked.has(DATA)} onChange={toggleData} /></td>
                  <td>{t("appData")}<span className="muted small"> · {t("appType")}</span></td>
                  <td className="num">{size(dataUse, locale)}</td>
                </tr>
              )}
              {groups.flatMap((g) => [
                row(g.type, 0, t(`type.${g.type}.title`), g.ids, g.size, t("things", { count: g.ids.length })),
                ...(!open.has(g.type) ? [] : g.groups.flatMap((c) => [
                  row(`${g.type}|${c.name}`, 1, c.name, c.items.map((i) => i.relative), c.size, t("things", { count: c.items.length })),
                  ...(!open.has(`${g.type}|${c.name}`) ? [] : c.items.map((i) => row(`${g.type}|${c.name}|${i.relative}`, 2, i.name, [i.relative], Number(i.size), undefined, false))),
                ])),
              ])}
              {items.data && groups.length === 0 && !disk.default && <tr><td colSpan={3} className="missing small">{t("diskEmpty")}</td></tr>}
            </tbody>
          </table>
        </div>
      )}

      {(groups.length > 0 || disk.default) && (
        <div className="disk-actions">
          {others.length === 0 ? <span className="small muted">{t("noOtherDisk")}</span> : (
            <>
              <label className="small muted" htmlFor={`to-${disk.id}`}>{t("moveTo", { count, size: size(markedSize, locale) })}</label>
              <select id={`to-${disk.id}`} value={target?.id} onChange={(e) => setTo(e.target.value)}>
                {others.map((d) => <option key={d.id} value={d.id}>{d.name}</option>)}
              </select>
              <button className="ot-btn ot-btn-primary" type="button" disabled={count === 0 || move.isPending || moveData.isPending || moving} onClick={submit}>{t("move")}</button>
            </>
          )}
        </div>
      )}
      {move.isSuccess && <p className="small muted" role="status" style={{ margin: 0 }}>{t("queued")}</p>}
      {error && <p className="error small" style={{ margin: 0 }}>{describe(error)}</p>}
    </section>
  );
}

/** Casilla que también queda a medias (parte del grupo marcada). */
function TriCheck({ label, checked, partial, onChange }: { label: string; checked: boolean; partial: boolean; onChange: (on: boolean) => void }) {
  const ref = useRef<HTMLInputElement>(null);
  useEffect(() => { if (ref.current) ref.current.indeterminate = partial; }, [partial]);
  return <input ref={ref} type="checkbox" aria-label={label} checked={checked} aria-checked={partial ? "mixed" : checked} onChange={() => onChange(!checked)} />;
}

type Group = { name: string; items: DiskItem[]; size: number };
type TypeGroup = { type: LocationType; groups: Group[]; ids: string[]; size: number };

/** Lo del disco por tipo y, adentro, por consola o emulador (el `group` que manda el backend), cada uno por nombre. */
function groupItems(all: DiskItem[]): TypeGroup[] {
  const sum = (list: DiskItem[]) => list.reduce((s, i) => s + Number(i.size), 0);
  return TYPES.map((type) => {
    const ofType = all.filter((i) => i.type === type);
    const names = [...new Set(ofType.map((i) => i.group))].sort((a, b) => a.localeCompare(b));
    const groups = names.map((name) => {
      const list = ofType.filter((i) => i.group === name).sort((a, b) => a.name.localeCompare(b.name));
      return { name, items: list, size: sum(list) };
    });
    return { type, groups, ids: ofType.map((i) => i.relative), size: sum(ofType) };
  }).filter((g) => g.ids.length > 0);
}

/**
 * Qué se manda a mover (#171): un grupo marcado entero va como su carpeta (`Juegos\GBA`, una sola tarea) si esa carpeta es
 * solo suya; si no, o si está marcado a medias, cada cosa por separado. Siempre la misma ruta dentro del otro disco.
 */
function payload(groups: TypeGroup[], all: DiskItem[], marked: Set<string>): string[] {
  const parts = (p: string) => p.split(/[\\/]/);
  const out: string[] = [];
  for (const g of groups) for (const c of g.groups) {
    const ids = c.items.map((i) => i.relative);
    const chosen = ids.filter((id) => marked.has(id));
    if (chosen.length === 0) continue;
    // La carpeta común a todo el grupo (`Juegos\GBA`), si tiene al menos dos niveles y nada de otro grupo adentro.
    const common = ids.map(parts).reduce((a, b) => { let k = 0; while (k < a.length && a[k] === b[k]) k++; return a.slice(0, k); });
    const folder = common.join("\\");
    const onlyOurs = ids.length > 1 && common.length >= 2
      && all.every((i) => ids.includes(i.relative) || !parts(i.relative).join("\\").startsWith(`${folder}\\`));
    out.push(...(chosen.length === ids.length && onlyOurs ? [folder] : chosen));
  }
  return out;
}
