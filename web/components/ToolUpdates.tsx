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
 * El aviso de arriba de Herramientas (#416, #432): cuántas versiones nuevas hay y de qué, «Actualizar todo» y «Revisar ahora»;
 * sin ninguna, «Al día». Está siempre, con el mismo alto: que llegue el aviso no empuja la lista (riesgo de #423).
 */
export function UpdatesNotice() {
  const t = useTranslations("tools.updates");
  const format = useFormatter();
  const describe = useDescribeError();
  const cache = useQueryClient();
  const updates = useQuery(toolUpdatesQuery);
  const check = useMutation({
    mutationFn: () => request<number>("/api/tools/updates/check", { method: "POST" }),
    onSuccess: () => cache.invalidateQueries({ queryKey: ["tasks"] }),
  });
  const list = updates.data ?? [];
  const names = [...new Set(list.map((u) => u.name))];
  // Una por receta (la estable si también hay de desarrollo) y solo las que traen su archivo: las demás se bajan desde su página.
  const all = list.filter((u) => u.download && !(u.channel === "dev" && list.some((o) => o.recipeId === u.recipeId && o.channel === "stable" && o.download)));
  const updateAll = useMutation({
    mutationFn: () => Promise.all(all.map((u) => request<number>("/api/emulators/install", { method: "POST", body: JSON.stringify({ emulator: u.recipeId, version: u.version, url: u.download }) }))),
    onSettled: () => cache.invalidateQueries({ queryKey: ["tasks"] }),
  });
  return (
    <div className="panel version-row tools-notice" data-new={names.length > 0}>
      <span className="grow" role="status">
        {names.length > 0 ? <span><b>{t("banner", { count: list.length, names: format.list(names) })}</b> <span className="small muted">· {t("bannerBeside")}</span></span>
          : updates.isSuccess && <span className="small muted" title={t("none")}>{t("upToDate")}</span>}
        {check.isSuccess && <span className="small muted">{t("checking")}</span>}
        {updateAll.isSuccess && <span className="small muted">{t("queuedAll")}</span>}
        {(updates.isError || check.isError || updateAll.isError) && <span className="error small" role="alert">{describe(updates.error ?? check.error ?? updateAll.error)}</span>}
      </span>
      <span className="sentence">
        {all.length > 0 && <button className="ot-btn" type="button" disabled={updateAll.isPending || updateAll.isSuccess} onClick={() => updateAll.mutate()}>{t("updateAll")}</button>}
        <button className="ot-btn ot-btn-ghost" type="button" disabled={check.isPending} onClick={() => check.mutate()}>{t("checkNow")}</button>
      </span>
    </div>
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
