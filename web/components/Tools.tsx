"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useFormatter } from "next-intl";
import { useRouter, useSearchParams } from "next/navigation";
import { useRef, useState } from "react";
import { useConsoles } from "@/components/Catalog";
import { librariesQuery, toolsQuery } from "@/components/Libraries";
import { Empty } from "@/components/Shell";
import { Tabs } from "@/components/Tabs";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import type { SystemConsole, ToolInfo } from "@/lib/types";

const KINDS = ["emulator", "port", "complement", "system"] as const;
type Kind = (typeof KINDS)[number];

/** Herramientas (§5.1, #193, #194, #200): el inventario por tipo, en pestañas y una sola columna centrada. Instalar y actualizar llegan en la F5. */
export function ToolsPage() {
  const t = useTranslations("tools");
  const router = useRouter();
  const asked = useSearchParams().get("k");
  const kind: Kind = KINDS.find((k) => k === asked) ?? "emulator";
  return (
    <div className="centered">
      <Tabs tabs={KINDS.map((k) => ({ id: k, label: t(`kind.${k}`) }))} current={kind} label={t("sections")} panel="tools-panel"
        onSelect={(k) => router.push(`/tools/?k=${k}`)} />
      <section className="sec" id="tools-panel" role="tabpanel" aria-labelledby={`tab-${kind}`}>
        {kind === "system" ? <SystemPanel /> : <ToolList kind={kind} />}
      </section>
    </div>
  );
}

/** BIOS, keys y firmware (§8.5, #199): lo que tienes por consola, con versiones, y subir otro. Otra versión nunca reemplaza la anterior. */
function SystemPanel() {
  const t = useTranslations("tools");
  const format = useFormatter();
  const describe = useDescribeError();
  const cache = useQueryClient();
  const consoles = useConsoles().data ?? [];
  const list = useQuery({ queryKey: ["system"], queryFn: () => request<SystemConsole[]>("/api/system") });
  const [console_, setConsole] = useState("");
  const input = useRef<HTMLInputElement>(null);
  const upload = useMutation({
    mutationFn: (file: File) => request<void>(`/api/system/${encodeURIComponent(console_)}?${new URLSearchParams({ name: file.name })}`,
      { method: "POST", body: file, headers: { "Content-Type": "application/octet-stream" } }),
    onSettled: () => {
      if (input.current) input.current.value = "";
      cache.invalidateQueries({ queryKey: ["system"] });
    },
  });
  return (
    <div style={{ display: "grid", gap: 12 }}>
      <section className="panel" style={{ display: "grid", gap: 8 }}>
        <p className="small" style={{ margin: 0 }}>{t("systemHelp")}</p>
        <div className="hero-actions">
          <select className="ot-btn ot-btn-sm" aria-label={t("systemConsole")} value={console_} onChange={(e) => setConsole(e.target.value)}>
            <option value="">{t("systemConsole")}</option>
            {consoles.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
          <input ref={input} type="file" aria-label={t("systemUpload")} disabled={!console_ || upload.isPending}
            onChange={(e) => { const file = e.target.files?.[0]; if (file) upload.mutate(file); }} />
        </div>
        {upload.isPending && <p className="small muted" role="status" style={{ margin: 0 }}>{t("systemUploading")}</p>}
        {upload.isError && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(upload.error)}</p>}
      </section>
      {list.isError && <p className="error small" role="alert">{describe(list.error)}</p>}
      {list.data?.length === 0 && <Empty title={t("systemEmptyTitle")} text={t("systemEmptyText")} />}
      {list.data?.map((c) => (
        <section className="panel" key={c.id} style={{ display: "grid", gap: 6 }}>
          <h3 style={{ margin: 0 }}>{c.name}</h3>
          {c.missing.map((m) => <span key={m} className="ot-badge ot-badge-review" style={{ justifySelf: "start" }}>{t("systemMissing", { name: m })}</span>)}
          {c.files.map((f) => (
            <div key={f.path} className="version-row small">
              <span className="folder-path">{f.name}{f.version && ` · ${f.version}`}</span>
              <span className="muted">{f.disk} · {format.dateTime(new Date(f.modified), { dateStyle: "medium" })}</span>
            </div>
          ))}
        </section>
      ))}
    </div>
  );
}

function ToolList({ kind }: { kind: Exclude<Kind, "system"> }) {
  const t = useTranslations("tools");
  const describe = useDescribeError();
  const tools = useQuery(toolsQuery);
  const [open, setOpen] = useState<string | null>(null);
  if (tools.isError) return <p className="error small" role="alert">{describe(tools.error)}</p>;
  if (!tools.data) return <div className="panel skeleton" style={{ height: 160 }} aria-busy="true" />;
  if (tools.data.length === 0) return <Empty title={t("emptyTitle")} text={t("emptyText")} />;
  const rows = tools.data.filter((x) => x.kind === kind);
  if (rows.length === 0) return <p className="missing small">{t("none")}</p>;
  return (
    <section className="panel">
      <ul className="detail-list">
        {rows.map((x) => (
          <li key={x.path}>
            <div className="version-row" style={{ flex: 1 }}>
              <button type="button" className="link-button grow" aria-expanded={open === x.path} onClick={() => setOpen(open === x.path ? null : x.path)}>
                <b>{x.name}</b>
                <span className="small muted">{x.game ? `${x.folder} · ${x.game}` : x.folder}</span>
              </button>
              <span style={{ display: "flex", gap: 6 }}>
                <span className="ot-badge">{x.version}</span>
                {!x.recipeId && <span className="ot-badge ot-badge-info" title={t("noRecipeHelp")}>{t("noRecipe")}</span>}
              </span>
            </div>
            {open === x.path && <ToolDetail tool={x} />}
          </li>
        ))}
      </ul>
    </section>
  );
}

/** El detalle de lo instalado: consola, juego que cubre, en qué disco está, y moverlo a otro (#198, #205). */
function ToolDetail({ tool }: { tool: ToolInfo }) {
  const t = useTranslations("tools");
  const describe = useDescribeError();
  const cache = useQueryClient();
  const locations = (useQuery(librariesQuery).data ?? []).filter((l) => (l.type === "disk" || l.type === tool.kind) && l.connected);
  const where = locations.find((l) => l.id === tool.locationId)?.name;
  const targets = locations.filter((l) => l.type === "disk" && l.id !== tool.locationId);
  const [moving, setMoving] = useState(false);
  const [to, setTo] = useState("");
  const move = useMutation({
    mutationFn: () => request<number>("/api/tools/move", { method: "POST", body: JSON.stringify({ path: tool.path, toLocation: to || null }) }),
    onSuccess: () => { setMoving(false); cache.invalidateQueries({ queryKey: ["tasks"] }); },
  });
  return (
    <div className="tool-detail">
      <dl className="facts small">
        <dt>{t("console")}</dt><dd>{tool.folder}</dd>
        {tool.game && <><dt>{t("covers")}</dt><dd>{tool.game}</dd></>}
        <dt>{t("version")}</dt><dd>{tool.version}</dd>
        <dt>{t("location")}</dt><dd>{where ?? t("dataFolder")}</dd>
        <dt>{t("path")}</dt><dd className="folder-path">{tool.path}</dd>
      </dl>
      {move.isSuccess && <p className="small muted" role="status" style={{ margin: 0 }}>{t("queued")}</p>}
      {!moving && targets.length > 0 && (
        <button className="ot-btn ot-btn-sm" type="button" onClick={() => { setMoving(true); setTo(targets[0].id); move.reset(); }}>{t("move")}</button>
      )}
      {moving && (
        <span className="sentence">
          <label className="small muted" htmlFor={`move-${tool.path}`}>{t("to")}</label>
          <select id={`move-${tool.path}`} value={to} onChange={(e) => setTo(e.target.value)}>
            {targets.map((l) => <option key={l.id} value={l.id}>{l.name}</option>)}
          </select>
          <button className="ot-btn ot-btn-sm" type="button" onClick={() => setMoving(false)}>{t("cancel")}</button>
          <button className="ot-btn ot-btn-sm ot-btn-primary" type="button" disabled={move.isPending} onClick={() => move.mutate()}>{t("confirm")}</button>
        </span>
      )}
      {move.isError && <p className="error small" style={{ margin: 0 }}>{describe(move.error)}</p>}
    </div>
  );
}
