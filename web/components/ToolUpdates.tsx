"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useFormatter, useTranslations } from "next-intl";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import { toolUpdatesQuery } from "@/lib/queries";
import type { ToolUpdate } from "@/lib/types";
import { useState } from "react";

/** Cuántos juegos se nombran en el aviso abierto; del resto, cuántos son. */
const GAMES = 6;

/**
 * La parte de los avisos en la barra de una pestaña de Herramientas (#348): cuántas versiones nuevas hay y «Revisar ahora».
 * Cada aviso va en la tarjeta de lo suyo, con {@link UpdateRow}.
 */
export function UpdatesStatus({ kind }: { kind: string }) {
  const t = useTranslations("tools.updates");
  const describe = useDescribeError();
  const cache = useQueryClient();
  const updates = useQuery(toolUpdatesQuery);
  const check = useMutation({
    mutationFn: () => request<number>("/api/tools/updates/check", { method: "POST" }),
    onSuccess: () => cache.invalidateQueries({ queryKey: ["tasks"] }),
  });
  const count = (updates.data ?? []).filter((u) => u.kind === kind).length;
  return (
    <span className="sentence">
      {updates.isSuccess && (count === 0 ? <span className="small muted" title={t("none")}>{t("upToDate")}</span> : <span className="small">{t("count", { count })}</span>)}
      <button className="ot-btn ot-btn-sm" type="button" disabled={check.isPending} onClick={() => check.mutate()}>{t("checkNow")}</button>
      {check.isSuccess && <span className="small muted" role="status">{t("checking")}</span>}
      {(updates.isError || check.isError) && <span className="error small" role="alert">{describe(updates.error ?? check.error)}</span>}
    </span>
  );
}

/**
 * Un aviso de versión nueva (decisiones 320 y 322), en la tarjeta de su emulador, port o herramienta: «2509 → 2512 · La usan
 * 14 juegos · Actualizar · ⋯», que al tocarla abre las notas y los juegos.
 */
export function UpdateRow({ u }: { u: ToolUpdate }) {
  const t = useTranslations("tools.updates");
  const format = useFormatter();
  const describe = useDescribeError();
  const cache = useQueryClient();
  const skip = useMutation({
    mutationFn: () => request<void>(`/api/tools/updates/${encodeURIComponent(u.recipeId)}/${u.channel}/skip`, { method: "POST" }),
    onSettled: () => { cache.invalidateQueries({ queryKey: ["toolUpdates"] }); cache.invalidateQueries({ queryKey: ["notices"] }); },
  });
  // «Actualizar»: la versión del aviso, desde su archivo y en una tarea (#296): al lado de la vieja, o en su lugar si es un port (decisión 325).
  const install = useMutation({
    mutationFn: () => request<number>("/api/emulators/install", { method: "POST", body: JSON.stringify({ emulator: u.recipeId, version: u.version, url: u.download }) }),
    onSuccess: () => cache.invalidateQueries({ queryKey: ["tasks"] }),
  });
  const [shown, setShown] = useState(false);
  const port = u.kind === "port";
  return (
    <article className="update-row">
      <div className="version-row">
        <button type="button" className="update-row-toggle" aria-expanded={shown} onClick={() => setShown(!shown)}>
          <b>{u.name}</b>
          <span className="small">{u.installed.at(-1)} → {u.version}</span>
          {u.channel === "dev" && <span className="ot-badge ot-badge-review">{t("dev")}</span>}
          <span className="small muted">{t("gamesCount", { count: u.games.length })}</span>
        </button>
        <span style={{ display: "flex", gap: 6, alignItems: "center" }}>
          {/* Con el archivo de la versión se instala al lado de la vieja (#296); si la fuente no lo dio, se abre su página para bajarlo. */}
          {u.download ? (
            <button className="ot-btn ot-btn-primary ot-btn-sm" type="button" disabled={install.isPending || install.isSuccess} onClick={() => install.mutate()}>{t("update")}</button>
          ) : u.url && <a className="ot-btn ot-btn-primary ot-btn-sm" href={u.url} target="_blank" rel="noreferrer">{t("update")}</a>}
          <details className="ot-menu ot-menu-sm">
            <summary className="ot-btn ot-btn-sm" aria-label={t("more", { name: u.name })} title={t("more", { name: u.name })}>⋯</summary>
            <div className="ot-menu-list" role="menu">
              <button type="button" role="menuitem" disabled={skip.isPending} onClick={() => skip.mutate()}>{t("skip")}</button>
            </div>
          </details>
        </span>
      </div>
      {shown && (
        <div className="small" style={{ display: "grid", gap: 6 }}>
          <dl className="facts">
            <dt>{t("installed")}</dt><dd>{u.installed.join(", ")}</dd>
            {u.publishedAt && <><dt>{t("published")}</dt><dd>{format.dateTime(new Date(u.publishedAt), { dateStyle: "medium" })}</dd></>}
            <dt>{t("games")}</dt><dd>{u.games.length === 0 ? <span className="muted">{t("gamesUnknown")}</span>
              : u.games.length <= GAMES ? format.list(u.games) : t("gamesMore", { list: u.games.slice(0, GAMES).join(", "), count: u.games.length - GAMES })}</dd>
          </dl>
          {u.notes && <p style={{ whiteSpace: "pre-wrap", margin: 0 }}>{u.notes}</p>}
          <p className="muted" style={{ margin: 0 }}>{t(port ? "replaces" : "besideOld")}</p>
        </div>
      )}
      {install.isSuccess && <p className="small muted" role="status" style={{ margin: 0 }}>{t(port ? "queuedReplace" : "queued")}</p>}
      {(install.isError || skip.isError) && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(install.error ?? skip.error)}</p>}
    </article>
  );
}
