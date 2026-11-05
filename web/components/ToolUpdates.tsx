"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useFormatter, useTranslations } from "next-intl";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import { toolUpdatesQuery } from "@/lib/queries";
import type { ToolUpdate } from "@/lib/types";
import { useState } from "react";

/** Cuántos juegos se nombran en el aviso; del resto, cuántos son. */
const GAMES = 6;
const id = (u: ToolUpdate) => `${u.recipeId}-${u.channel}-${u.version}`;

/** Arriba de Herramientas: cada versión nueva con sus notas, quién la usa, Actualizar (o Instalar, si no la tienes) y «Omitir esta versión», más «Revisar ahora». */
export function ToolUpdates() {
  const t = useTranslations("tools.updates");
  const format = useFormatter();
  const describe = useDescribeError();
  const cache = useQueryClient();
  const updates = useQuery(toolUpdatesQuery);
  const check = useMutation({
    mutationFn: () => request<number>("/api/tools/updates/check", { method: "POST" }),
    onSuccess: () => cache.invalidateQueries({ queryKey: ["tasks"] }),
  });
  const skip = useMutation({
    mutationFn: (u: ToolUpdate) => request<void>(`/api/tools/updates/${encodeURIComponent(u.recipeId)}/${u.channel}/skip`, { method: "POST" }),
    onSettled: () => { cache.invalidateQueries({ queryKey: ["toolUpdates"] }); cache.invalidateQueries({ queryKey: ["notices"] }); },
  });
  // «Actualizar» e «Instalar»: la versión del aviso, desde su archivo, en una tarea (#296).
  const [queued, setQueued] = useState<Set<string>>(new Set());
  const install = useMutation({
    mutationFn: (u: ToolUpdate) => request<number>("/api/emulators/install", { method: "POST", body: JSON.stringify({ emulator: u.recipeId, version: u.version, url: u.download }) }),
    onSuccess: (_, u) => { setQueued((q) => new Set(q).add(id(u))); cache.invalidateQueries({ queryKey: ["tasks"] }); },
  });
  const list = updates.data ?? [];
  return (
    <section className="panel" style={{ display: "grid", gap: 8 }} aria-labelledby="tool-updates">
      <div className="version-row">
        <h3 id="tool-updates" style={{ margin: 0 }}>
          {t("title")} {list.length > 0 && <span className="ot-count" aria-label={t("count", { count: list.length })}>{list.length}</span>}
        </h3>
        <button className="ot-btn ot-btn-sm" type="button" disabled={check.isPending} onClick={() => check.mutate()}>{t("checkNow")}</button>
      </div>
      {check.isSuccess && <p className="small muted" role="status" style={{ margin: 0 }}>{t("checking")}</p>}
      {(updates.isError || check.isError || skip.isError) && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(updates.error ?? check.error ?? skip.error)}</p>}
      {updates.isPending && <div className="skeleton" style={{ height: 40 }} aria-busy="true" />}
      {updates.data?.length === 0 && <p className="small muted" style={{ margin: 0 }}>{t("none")}</p>}
      {list.map((u) => (
        <article key={`${u.recipeId}-${u.channel}`} className="tool-detail" style={{ display: "grid", gap: 6 }}>
          <div className="version-row">
            <b>{u.name}</b>
            <span style={{ display: "flex", gap: 6 }}>
              <span className="ot-badge ot-badge-info">{u.version}</span>
              {u.channel === "dev" && <span className="ot-badge ot-badge-review">{t("dev")}</span>}
            </span>
          </div>
          <dl className="facts small">
            <dt>{t("installed")}</dt><dd>{u.installed.length > 0 ? u.installed.join(", ") : <span className="muted">{t("notInstalled")}</span>}</dd>
            {u.publishedAt && <><dt>{t("published")}</dt><dd>{format.dateTime(new Date(u.publishedAt), { dateStyle: "medium" })}</dd></>}
            <dt>{t("games")}</dt><dd>{u.games.length === 0 ? <span className="muted">{t("gamesUnknown")}</span>
              : u.games.length <= GAMES ? format.list(u.games) : t("gamesMore", { list: u.games.slice(0, GAMES).join(", "), count: u.games.length - GAMES })}</dd>
          </dl>
          {u.notes && (
            <details className="small">
              <summary>{t("notes")}</summary>
              <p style={{ whiteSpace: "pre-wrap", margin: "6px 0 0" }}>{u.notes}</p>
            </details>
          )}
          {u.installed.length > 0 && <p className="small muted" style={{ margin: 0 }}>{t("besideOld")}</p>}
          <div className="hero-actions">
            {/* Con el archivo de la versión se instala al lado de la vieja (#296); si la fuente no lo dio, se abre su página para bajarlo. */}
            {u.download ? (
              <button className="ot-btn ot-btn-primary ot-btn-sm" type="button" disabled={install.isPending || queued.has(id(u))} onClick={() => install.mutate(u)}>
                {t(u.installed.length > 0 ? "update" : "install")}
              </button>
            ) : u.url && <a className="ot-btn ot-btn-primary ot-btn-sm" href={u.url} target="_blank" rel="noreferrer">{t(u.installed.length > 0 ? "update" : "install")}</a>}
            <button className="ot-btn ot-btn-sm" type="button" disabled={skip.isPending} onClick={() => skip.mutate(u)}>{t("skip")}</button>
          </div>
          {queued.has(id(u)) && <p className="small muted" role="status" style={{ margin: 0 }}>{t("queued")}</p>}
          {install.isError && install.variables === u && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(install.error)}</p>}
        </article>
      ))}
    </section>
  );
}
