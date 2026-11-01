"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useLocale, useTranslations } from "next-intl";
import { useState } from "react";
import { Empty } from "@/components/Shell";
import { request } from "@/lib/client";
import { gb } from "@/lib/format";
import { useDescribeError } from "@/lib/i18n";
import type { AdoptResult, LibraryInfo, ToolInfo } from "@/lib/types";

export const librariesQuery = { queryKey: ["libraries"], queryFn: () => request<LibraryInfo[]>("/api/libraries") };
export const toolsQuery = { queryKey: ["tools"], queryFn: () => request<ToolInfo[]>("/api/tools") };

const post = (body: unknown) => ({ method: "POST", body: JSON.stringify(body) });

/** Ajustes › Bibliotecas (D8, §5.3): la lista con su estado y lo que se puede hacer con cada una. */
export function LibrariesSettings() {
  const t = useTranslations("libraries");
  const describe = useDescribeError();
  const cache = useQueryClient();
  const libraries = useQuery({ ...librariesQuery, refetchInterval: (q) => (q.state.data?.some((l) => !l.connected) ? 10_000 : false) });
  const [mode, setMode] = useState<"create" | "adopt" | null>(null);
  const [path, setPath] = useState("");
  const [name, setName] = useState("");
  const [started, setStarted] = useState(false);

  const action = useMutation({
    mutationFn: () => mode === "create"
      ? request<LibraryInfo[]>("/api/libraries", post({ path, name: name || null }))
      : request<AdoptResult>("/api/libraries/adopt", post({ path, name: name || null })).then((r) => { setStarted(r.task != null); return r.libraries; }),
    onSuccess: (list) => {
      cache.setQueryData(["libraries"], list);
      cache.invalidateQueries({ queryKey: ["tasks"] });
      setMode(null);
    },
  });

  return (
    <div className="settings-blocks">
      {libraries.data?.length === 0 && !mode && <Empty title={t("emptyTitle")} text={t("emptyText")} />}
      {libraries.isError && <p className="error small" role="alert">{describe(libraries.error)}</p>}
      {libraries.data?.map((l) => <LibraryRow key={l.id} library={l} />)}
      {started && <p className="small muted" role="status" style={{ margin: 0 }}>{t("migrationStarted")}</p>}

      {!mode ? (
        <div className="sentence">
          <button className="ot-btn ot-btn-primary" type="button" onClick={() => { setMode("adopt"); setPath(""); setName(""); action.reset(); }}>{t("adopt")}</button>
          <button className="ot-btn" type="button" onClick={() => { setMode("create"); setPath(""); setName(""); action.reset(); }}>{t("create")}</button>
        </div>
      ) : (
        <section className="panel form-block">
          <h2>{mode === "create" ? t("create") : t("adopt")}</h2>
          <p className="small" style={{ margin: 0 }}>{mode === "create" ? t("createHelp") : t("adoptHelp")}</p>
          <label className="small muted" htmlFor="library-path">{t("path")}</label>
          <input id="library-path" value={path} onChange={(e) => setPath(e.target.value)} placeholder={t("pathPlaceholder")} />
          <label className="small muted" htmlFor="library-name">{t("name")}</label>
          <input id="library-name" value={name} onChange={(e) => setName(e.target.value)} placeholder={t("namePlaceholder")} />
          {action.isError && <p className="error small" style={{ margin: 0 }}>{describe(action.error)}</p>}
          <div style={{ display: "flex", gap: 8 }}>
            <button className="ot-btn" type="button" onClick={() => setMode(null)}>{t("cancel")}</button>
            <button className="ot-btn ot-btn-primary" type="button" disabled={!path || action.isPending} onClick={() => action.mutate()}>
              {mode === "create" ? t("create") : t("adopt")}
            </button>
          </div>
        </section>
      )}
    </div>
  );
}

/** Una biblioteca: nombre, ruta, espacio y estado, con renombrar, mover, migrar, reconectar y quitar. */
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
      const base = `/api/libraries/${l.id}`;
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
      setMode(null);
    },
  });
  const open = (m: typeof mode, initial = "") => { setMode(m); setValue(initial); run.reset(); };

  return (
    <section className="panel">
      <div className="version-row">
        <div style={{ display: "flex", flexDirection: "column", minWidth: 0 }}>
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
    </section>
  );
}

/** Herramientas: lo instalado, agrupado en emuladores, ports y complementos, con su versión (§5.1). */
export function ToolsList() {
  const t = useTranslations("tools");
  const describe = useDescribeError();
  const tools = useQuery(toolsQuery);
  if (tools.isError) return <p className="error small" role="alert">{describe(tools.error)}</p>;
  if (!tools.data) return <div className="panel skeleton" style={{ height: 160 }} aria-busy="true" />;
  if (tools.data.length === 0) return <Empty title={t("emptyTitle")} text={t("emptyText")} />;
  return (
    <div className="settings-blocks">
      {(["emulator", "port", "complement"] as const).map((kind) => {
        const rows = tools.data.filter((x) => x.kind === kind);
        if (rows.length === 0) return null;
        return (
          <section key={kind} className="panel">
            <h2>{t(`kind.${kind}`)}</h2>
            {rows.map((x) => (
              <div key={x.path} className="version-row">
                <div style={{ display: "flex", flexDirection: "column", minWidth: 0 }}>
                  <b>{x.name}</b>
                  <span className="small muted">{x.folder}</span>
                </div>
                <span style={{ display: "flex", gap: 6 }}>
                  <span className="ot-badge">{x.version}</span>
                  {!x.recipeId && <span className="ot-badge ot-badge-info" title={t("noRecipeHelp")}>{t("noRecipe")}</span>}
                </span>
              </div>
            ))}
          </section>
        );
      })}
    </div>
  );
}
