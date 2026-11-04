"use client";

import { useTranslations } from "next-intl";
import { useMutation } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useConsoleName } from "@/components/Catalog";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import type { MegapackPreview, PreviewItem } from "@/lib/types";

const PAGE = 200;
const post = (body: unknown) => ({ method: "POST", body: JSON.stringify(body) });

/** «Importar carpeta» (#193, #197): la carpeta, qué hay en ella, lo que se marca y el orden de regiones. */
export function ImportFolder({ onClose }: { onClose: () => void }) {
  const t = useTranslations("catalog.collection");
  const describe = useDescribeError();
  const consoleName = useConsoleName();
  const [path, setPath] = useState("");
  const [off, setOff] = useState<Set<string>>(new Set());
  const [consoleFilter, setConsoleFilter] = useState("");
  const [sagaFilter, setSagaFilter] = useState("");
  const [onlyCatalog, setOnlyCatalog] = useState(false);
  const [shown, setShown] = useState(PAGE);

  const preview = useMutation({
    mutationFn: () => request<MegapackPreview>("/api/collection/import/preview", post({ path })),
    onSuccess: (data) => setOff(new Set(data.items.filter((i) => !i.selected).map((i) => i.path))),
  });
  const regions = useMutation({
    mutationFn: (order: string[]) => request<void>("/api/collection/regions", { method: "PUT", body: JSON.stringify({ order }) }),
    onSuccess: () => preview.mutate(),
  });
  const run = useMutation({
    mutationFn: () => request<number>("/api/collection/import", post({ path, skip: [...off] })),
    onSuccess: onClose,
  });

  const items = useMemo(() => preview.data?.items ?? [], [preview.data]);
  const order = preview.data?.regions ?? [];
  const consoles = useMemo(() => [...new Set(items.map((i) => i.console ?? ""))], [items]);
  const sagas = useMemo(() => [...new Set(items.map((i) => i.saga).filter((s): s is string => !!s))].sort(), [items]);
  const visible = items.filter((i) => (!consoleFilter || (i.console ?? "") === consoleFilter) && (!sagaFilter || i.saga === sagaFilter) && (!onlyCatalog || i.gameId != null));
  const selected = items.length - off.size;

  const toggle = (item: PreviewItem) => setOff((old) => {
    const next = new Set(old);
    if (next.has(item.path)) next.delete(item.path); else next.add(item.path);
    return next;
  });
  const up = (index: number) => {
    const next = [...order];
    [next[index - 1], next[index]] = [next[index], next[index - 1]];
    regions.mutate(next);
  };

  return (
    <div className="panel" style={{ display: "grid", gap: 8 }}>
      <form style={{ display: "grid", gap: 8 }} onSubmit={(e) => { e.preventDefault(); preview.mutate(); }}>
        <label className="small muted" htmlFor="import-path">{t("importLabel")}</label>
        <input id="import-path" value={path} onChange={(e) => { setPath(e.target.value); preview.reset(); }} placeholder={t("importPlaceholder")} />
        <span className="small muted">{t("importHint")}</span>
        <div className="hero-actions">
          <button className="ot-btn ot-btn-primary ot-btn-sm" type="submit" disabled={!path.trim() || preview.isPending}>{t("importSee")}</button>
          <button className="ot-btn ot-btn-sm" type="button" onClick={onClose}>{t("importCancel")}</button>
        </div>
      </form>
      {preview.isError && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(preview.error)}</p>}
      {regions.isError && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(regions.error)}</p>}
      {preview.data && items.length === 0 && <p className="small muted" style={{ margin: 0 }}>{t("importEmpty")}</p>}
      {items.length > 0 && (
        <>
          <div className="hero-actions" role="group" aria-label={t("importRegions")}>
            <span className="small muted">{t("importRegions")}:</span>
            {order.map((r, i) => (
              <button key={r} className="ot-btn ot-btn-sm" type="button" disabled={i === 0 || regions.isPending || preview.isPending}
                aria-label={t("importUp", { region: t(`importRegion.${r as "es"}`) })} onClick={() => up(i)}>
                {i > 0 && "↑ "}{t(`importRegion.${r as "es"}`)}
              </button>
            ))}
          </div>
          <div className="hero-actions">
            <select className="ot-btn ot-btn-sm" aria-label={t("importConsole")} value={consoleFilter} onChange={(e) => setConsoleFilter(e.target.value)}>
              <option value="">{t("importConsole")}: {t("importAll")}</option>
              {consoles.map((c) => <option key={c} value={c}>{c ? consoleName(c) : t("importNoConsole")}</option>)}
            </select>
            <select className="ot-btn ot-btn-sm" aria-label={t("importSaga")} value={sagaFilter} onChange={(e) => setSagaFilter(e.target.value)}>
              <option value="">{t("importSaga")}: {t("importAll")}</option>
              {sagas.map((s) => <option key={s} value={s}>{s}</option>)}
            </select>
            <label className="small"><input type="checkbox" checked={onlyCatalog} onChange={(e) => setOnlyCatalog(e.target.checked)} /> {t("importOnlyCatalog")}</label>
          </div>
          <div className="table-wrap" style={{ padding: 0 }}>
            <table className="t">
              <tbody>
                {visible.slice(0, shown).map((i) => (
                  <tr key={i.path}>
                    <td><input type="checkbox" aria-label={i.name} checked={!off.has(i.path)} onChange={() => toggle(i)} /></td>
                    <td>{i.name}{i.game && i.game !== i.name && <span className="small muted" style={{ display: "block" }}>{i.game}</span>}</td>
                    <td>{i.console ? consoleName(i.console) : t("importNoConsole")}</td>
                    <td>{i.reason && <span className="ot-badge">{t(`importReason.${i.reason as "extra"}`)}</span>}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {visible.length > shown && (
            <button className="ot-btn ot-btn-sm" type="button" style={{ justifySelf: "start" }} onClick={() => setShown((n) => n + PAGE)}>{t("importMore", { count: Math.min(PAGE, visible.length - shown) })}</button>
          )}
          <div className="hero-actions">
            <button className="ot-btn ot-btn-primary ot-btn-sm" type="button" disabled={selected === 0 || run.isPending} onClick={() => run.mutate()}>{t("importRun", { count: selected })}</button>
            <span className="small muted">{t("importCount", { selected, total: items.length })}</span>
          </div>
          {run.isError && <p className="error small" role="alert" style={{ margin: 0 }}>{describe(run.error)}</p>}
        </>
      )}
    </div>
  );
}
