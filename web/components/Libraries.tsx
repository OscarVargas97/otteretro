"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { useState } from "react";
import { request } from "@/lib/client";
import { gb } from "@/lib/format";
import { useDescribeError } from "@/lib/i18n";
import type { AdoptResult, LibraryInfo, ToolInfo } from "@/lib/types";

/** Las ubicaciones (#198, #199). La clave sigue siendo «libraries»: así se llaman en la base. */
export const librariesQuery = { queryKey: ["libraries"], queryFn: () => request<LibraryInfo[]>("/api/locations") };
export const toolsQuery = { queryKey: ["tools"], queryFn: () => request<ToolInfo[]>("/api/tools") };

/** Los tipos de ubicación, en el orden de Almacenamiento (#198). */
export const TYPES = ["game", "emulator", "port", "complement", "system"] as const;
export type LocationType = (typeof TYPES)[number];
const TOOL_TYPES: string[] = ["emulator", "port", "complement"];

const post = (body: unknown) => ({ method: "POST", body: JSON.stringify(body) });

/** Ajustes › Almacenamiento (D8, §5.3, #198): un apartado por tipo con sus ubicaciones y lo que se puede hacer con cada una. */
export function StorageSettings() {
  const describe = useDescribeError();
  const libraries = useQuery({ ...librariesQuery, refetchInterval: (q) => (q.state.data?.some((l) => !l.connected) ? 10_000 : false) });
  return (
    <div className="settings-blocks">
      {libraries.isError && <p className="error small" role="alert">{describe(libraries.error)}</p>}
      {libraries.data && TYPES.map((type) => <TypeSection key={type} type={type} rows={libraries.data.filter((l) => l.type === type)} />)}
    </div>
  );
}

/** Un tipo: su explicación, sus ubicaciones (en las herramientas, primero la de la carpeta de datos), crear y adoptar. */
function TypeSection({ type, rows }: { type: LocationType; rows: LibraryInfo[] }) {
  const t = useTranslations("libraries");
  const describe = useDescribeError();
  const cache = useQueryClient();
  const [mode, setMode] = useState<"create" | "adopt" | null>(null);
  const [path, setPath] = useState("");
  const [name, setName] = useState("");
  const [started, setStarted] = useState(false);
  const action = useMutation({
    mutationFn: () => mode === "create"
      ? request<LibraryInfo[]>("/api/locations", post({ path, name: name || null, type }))
      : request<AdoptResult>("/api/locations/adopt", post({ path, name: name || null, type })).then((r) => { setStarted(r.task != null); return r.libraries; }),
    onSuccess: (list) => {
      cache.setQueryData(["libraries"], list);
      cache.invalidateQueries({ queryKey: ["tasks"] });
      cache.invalidateQueries({ queryKey: ["tools"] });
      setMode(null);
    },
  });
  const open = (m: "create" | "adopt") => { setMode(m); setPath(""); setName(""); action.reset(); };
  const empty = t(`type.${type}.empty`);

  return (
    <section className="panel" aria-labelledby={`type-${type}`}>
      <h2 id={`type-${type}`}>{t(`type.${type}.title`)}</h2>
      <p className="small muted" style={{ margin: 0 }}>{t(`type.${type}.help`)}</p>
      {TOOL_TYPES.includes(type) && (
        <div className="location-row">
          <div className="grow"><b>{t("dataFolder")}</b><span className="small muted">{t("dataFolderHelp")}</span></div>
        </div>
      )}
      {rows.length === 0 && empty && !mode && <p className="missing small">{empty}</p>}
      {rows.map((l) => <LibraryRow key={l.id} library={l} />)}
      {started && <p className="small muted" role="status" style={{ margin: 0 }}>{t("migrationStarted")}</p>}

      {!mode ? (
        <div className="sentence">
          <button className={`ot-btn ot-btn-sm${type === "game" && rows.length === 0 ? " ot-btn-primary" : ""}`} type="button" onClick={() => open("adopt")}>{t("adopt")}</button>
          <button className="ot-btn ot-btn-sm" type="button" onClick={() => open("create")}>{t("create")}</button>
        </div>
      ) : (
        <div className="form-block">
          <p className="small" style={{ margin: 0 }}>{mode === "create" ? t("createHelp") : type === "game" ? t("adoptHelp") : t("adoptOtherHelp")}</p>
          <label className="small muted" htmlFor={`path-${type}`}>{t("path")}</label>
          <input id={`path-${type}`} value={path} onChange={(e) => setPath(e.target.value)} placeholder={t("pathPlaceholder")} />
          <label className="small muted" htmlFor={`name-${type}`}>{t("name")}</label>
          <input id={`name-${type}`} value={name} onChange={(e) => setName(e.target.value)} placeholder={t("namePlaceholder")} />
          {action.isError && <p className="error small" style={{ margin: 0 }}>{describe(action.error)}</p>}
          <div style={{ display: "flex", gap: 8 }}>
            <button className="ot-btn" type="button" onClick={() => setMode(null)}>{t("cancel")}</button>
            <button className="ot-btn ot-btn-primary" type="button" disabled={!path || action.isPending} onClick={() => action.mutate()}>
              {mode === "create" ? t("create") : t("adopt")}
            </button>
          </div>
        </div>
      )}
    </section>
  );
}

/** Una ubicación: nombre, ruta, espacio y estado, con renombrar, mover, migrar, reconectar y quitar. */
function LibraryRow({ library: l }: { library: LibraryInfo }) {
  const t = useTranslations("libraries");
  const locale = useLocale();
  const describe = useDescribeError();
  const cache = useQueryClient();
  const [mode, setMode] = useState<"rename" | "move" | "reconnect" | "remove" | null>(null);
  const [value, setValue] = useState("");
  const [queued, setQueued] = useState(false);

  const run = useMutation({
    mutationFn: async (kind: "rename" | "move" | "reconnect" | "remove" | "migrate") => {
      const base = `/api/locations/${l.id}`;
      switch (kind) {
        case "rename": return request<LibraryInfo[]>(base, { method: "PUT", body: JSON.stringify({ name: value }) });
        case "remove": return request<LibraryInfo[]>(base, { method: "DELETE" });
        case "reconnect": return request<LibraryInfo[]>(`${base}/reconnect`, post({ path: value || null }));
        case "move": await request<number>(`${base}/move`, post({ path: value })); return null;
        case "migrate": await request<number>(`${base}/migrate`, post({})); return null;
      }
    },
    onSuccess: (list) => {
      if (list) cache.setQueryData(["libraries"], list);
      else setQueued(true);
      cache.invalidateQueries({ queryKey: ["tasks"] });
      cache.invalidateQueries({ queryKey: ["tools"] });
      setMode(null);
    },
  });
  const open = (m: typeof mode, initial = "") => { setMode(m); setValue(initial); run.reset(); };

  return (
    <div className="location-row">
      <div className="version-row">
        <div className="grow">
          <b>{l.name}</b>
          <span className="folder-path">{l.path}</span>
          {l.free != null && l.total != null && <span className="small muted">{t("free", { free: gb(l.free, locale), total: gb(l.total, locale) })}</span>}
        </div>
        <span style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
          {l.kind === "network" && <span className="ot-badge">{t("network")}</span>}
          <span className={`ot-badge ${l.connected ? "ot-badge-have" : "ot-badge-danger"}`}>{l.connected ? t("connected") : t("disconnected")}</span>
          {l.legacy && <span className="ot-badge ot-badge-info">{t("legacy")}</span>}
        </span>
      </div>
      {l.legacy && <p className="small" style={{ margin: 0 }}>{t("legacyHelp")}</p>}
      {queued && <p className="small muted" role="status" style={{ margin: 0 }}>{t("queued")}</p>}

      {!mode && (
        <div className="sentence">
          {l.legacy && <button className="ot-btn ot-btn-primary ot-btn-sm" type="button" disabled={run.isPending} onClick={() => run.mutate("migrate")}>{t("migrate")}</button>}
          {!l.connected && <button className="ot-btn ot-btn-primary ot-btn-sm" type="button" onClick={() => open("reconnect")}>{t("reconnect")}</button>}
          <button className="ot-btn ot-btn-sm" type="button" onClick={() => open("rename", l.name)}>{t("rename")}</button>
          {l.connected && <button className="ot-btn ot-btn-sm" type="button" onClick={() => open("move")}>{t("move")}</button>}
          <button className="ot-btn ot-btn-sm ot-btn-ghost" type="button" onClick={() => open("remove")}>{t("remove")}</button>
        </div>
      )}

      {mode && mode !== "remove" && (
        <div className="form-block">
          <p className="small" style={{ margin: 0 }}>{t(`${mode}Help`)}</p>
          <input aria-label={t(mode)} value={value} onChange={(e) => setValue(e.target.value)}
            placeholder={mode === "rename" ? t("namePlaceholder") : t("pathPlaceholder")} />
          <div style={{ display: "flex", gap: 8 }}>
            <button className="ot-btn" type="button" onClick={() => setMode(null)}>{t("cancel")}</button>
            <button className="ot-btn ot-btn-primary" type="button" disabled={(mode !== "reconnect" && !value) || run.isPending} onClick={() => run.mutate(mode)}>{t(mode)}</button>
          </div>
        </div>
      )}
      {mode === "remove" && (
        <div className="form-block" role="alertdialog" aria-label={t("remove")}>
          <p className="small" style={{ margin: 0 }}>{t("removeHelp", { name: l.name })}</p>
          <div style={{ display: "flex", gap: 8 }}>
            <button className="ot-btn" type="button" onClick={() => setMode(null)}>{t("cancel")}</button>
            <button className="ot-btn ot-btn-primary" type="button" disabled={run.isPending} onClick={() => run.mutate("remove")}>{t("removeConfirm")}</button>
          </div>
        </div>
      )}
      {run.isError && <p className="error small" style={{ margin: 0 }}>{describe(run.error)}</p>}
    </div>
  );
}
