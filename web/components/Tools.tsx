"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { useState } from "react";
import { librariesQuery, toolsQuery } from "@/components/Libraries";
import { Empty } from "@/components/Shell";
import { SideNav } from "@/components/SideNav";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import type { ToolInfo } from "@/lib/types";

const KINDS = ["emulator", "port", "complement", "system"] as const;
type Kind = (typeof KINDS)[number];

/** Herramientas (§5.1, #193, #194, #197): el inventario por tipo, con lista y detalle. Instalar y actualizar llegan en la F5. */
export function ToolsPage() {
  const t = useTranslations("tools");
  const router = useRouter();
  const asked = useSearchParams().get("k");
  const kind: Kind = KINDS.find((k) => k === asked) ?? "emulator";
  return (
    <SideNav groups={[{ items: KINDS.map((k) => ({ id: k, label: t(`kind.${k}`) })) }]} current={kind} label={t("sections")}
      panel="tools-panel" onSelect={(k) => router.push(`/tools/?k=${k}`)}>
      {kind === "system" ? <SystemHelp /> : <ToolList kind={kind} />}
    </SideNav>
  );
}

function SystemHelp() {
  const t = useTranslations("tools");
  return (
    <section className="panel">
      <p className="small" style={{ margin: 0 }}>{t("systemHelp")}</p>
      <Link className="ot-btn" href="/settings/?s=storage">{t("openStorage")}</Link>
    </section>
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

/** El detalle de lo instalado: consola, juego que cubre, dónde está, y moverlo a otra ubicación de su tipo (#198). */
function ToolDetail({ tool }: { tool: ToolInfo }) {
  const t = useTranslations("tools");
  const describe = useDescribeError();
  const cache = useQueryClient();
  const locations = (useQuery(librariesQuery).data ?? []).filter((l) => l.type === tool.kind && l.connected);
  const where = locations.find((l) => l.id === tool.locationId)?.name;
  const targets = [{ id: "", name: t("dataFolder") }, ...locations].filter((l) => (l.id || null) !== (tool.locationId ?? null));
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
