"use client";

import { useQuery } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { Empty } from "@/components/Shell";
import { Panels } from "@/components/Skeleton";
import { SteamAccount, SteamGroups, steamExportQuery, steamQuery, useExport } from "@/components/Steam";
import { useDescribeError } from "@/lib/i18n";
import type { SteamEntry } from "@/lib/types";

/** Steam (§10, pantallas.md §9, #402): la cuenta, lo marcado por saga con lo que cambia desde la última exportación y «Exportar».
 *  Lo que se puede sumar va plegado; sin Steam ROM Manager, una línea a Herramientas. */
export default function Steam() {
  const t = useTranslations("steam");
  const describe = useDescribeError();
  const status = useQuery(steamQuery);
  const changes = useQuery(steamExportQuery);
  const { run, task, busy } = useExport();

  const error = status.error ?? changes.error;
  if (error) return <div className="content"><p className="error" role="alert">{describe(error)}</p></div>;
  if (!status.data || !changes.data) return <div className="content" aria-busy="true"><Panels count={2} /></div>;

  const s = status.data;
  const c = changes.data;
  const has = (list: SteamEntry[]) => (e: SteamEntry) => list.some((x) => String(x.versionId) === String(e.versionId));
  const isAdded = has(c.added);
  const isChanged = has(c.changed);
  const pending = c.added.length + c.changed.length + c.removed.length;

  if (c.entries.length === 0 && c.removed.length === 0 && s.available.length === 0)
    return <div className="content"><Empty title={t("emptyTitle")} text={t("emptyText")} /><div className="panel small"><SteamAccount status={s} /></div></div>;

  return (
    <div className="content">
      <section className="sec">
        <div className="sec-h">
          <h2>{t("title")}</h2>
          <button className="ot-btn ot-btn-primary" type="button" disabled={!s.account || pending === 0 || busy} onClick={() => run.mutate()}>
            {busy ? t("exporting") : t("export")}
          </button>
        </div>
        <div className="panel">
          <SteamAccount status={s} />
          <span className="small">
            {pending === 0 ? t("upToDate") : [
              c.added.length > 0 && t("added", { count: c.added.length }),
              c.removed.length > 0 && t("removed", { count: c.removed.length }),
              c.changed.length > 0 && t("changed", { count: c.changed.length }),
            ].filter(Boolean).join(" · ")}
            {pending > 0 && <span className="muted"> · {t("closeSteam")}</span>}
          </span>
          {run.error && <p className="error" role="alert">{describe(run.error)}</p>}
          {task?.detail && <span className="small" role="status">{describe(task.detail)}</span>}
        </div>
        {c.entries.length === 0 && <p className="small muted">{t("noneMarked")}</p>}
        <SteamGroups entries={c.entries} marked
          badge={(e) => isAdded(e) ? <span className="ot-badge ot-badge-have">{t("new")}</span> : isChanged(e) ? <span className="ot-badge ot-badge-info">{t("changes")}</span> : null} />
        {c.removed.length > 0 && (
          <div className="panel small">
            <span><span className="ot-badge ot-badge-danger">{t("goes")}</span> {c.removed.map((e) => e.title).join(" · ")}</span>
          </div>
        )}
        {s.available.length > 0 && (
          <details className="fold">
            <summary>{t("more", { count: s.available.length })}</summary>
            <div className="sec" style={{ paddingBlock: 16 }}><SteamGroups entries={s.available} marked={false} /></div>
          </details>
        )}
      </section>
    </div>
  );
}
