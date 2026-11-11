"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { useConsoleName } from "@/components/Catalog";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import { reviewQuery } from "@/lib/queries";
import type { FileReview, PackageReview, ReviewPage, SearchResults } from "@/lib/types";

/** Separa el nombre del archivo de su carpeta, con barras de Windows o de Linux. */
const split = (path: string) => {
  const i = Math.max(path.lastIndexOf("/"), path.lastIndexOf("\\"));
  return { name: path.slice(i + 1), folder: path.slice(0, Math.max(i, 0)) };
};

/**
 * «N archivos por revisar» en Colección (#415, antes la pantalla Revisar): los archivos dudosos o sin identificar (#181)
 * y los paquetes sin juegos (#310). Sin nada por revisar no se ve; «Revisar» despliega la lista ahí mismo.
 */
export function FilesToReview({ initiallyOpen }: { initiallyOpen: boolean }) {
  const t = useTranslations("review");
  const describe = useDescribeError();
  const cache = useQueryClient();
  const [open, setOpen] = useState(initiallyOpen);
  const page = useQuery(reviewQuery);
  const act = useMutation({
    mutationFn: ({ path, body }: { path: string; body?: unknown }) =>
      request<ReviewPage>(path, { method: "POST", body: body === undefined ? undefined : JSON.stringify(body) }),
    onSuccess: (data) => {
      cache.setQueryData(["review"], data);
      cache.invalidateQueries({ queryKey: ["review", "count"] });
      cache.invalidateQueries({ queryKey: ["saga"] });
      cache.invalidateQueries({ queryKey: ["home"] });
      cache.invalidateQueries({ queryKey: ["notices"] });
      cache.invalidateQueries({ queryKey: ["collection"] });
      cache.invalidateQueries({ queryKey: ["consoles"] });
      cache.invalidateQueries({ queryKey: ["game"] });
    },
  });

  if (!page.data) return null;
  const { files, packages } = page.data;
  const count = files.length + packages.length;
  if (count === 0) return null;

  return (
    <>
      <div className="panel" role="note" aria-label={t("notice", { count })}
        style={{ background: "var(--review-soft)", flexDirection: "row", flexWrap: "wrap", alignItems: "center", gap: "8px 16px" }}>
        <span className="ot-badge ot-badge-review">{count}</span>
        <span style={{ flex: "1 1 320px" }}><b>{t("notice", { count })}</b> <span className="small muted">{t("hint")}</span></span>
        <button className="ot-btn" type="button" aria-expanded={open} aria-controls="review-list" onClick={() => setOpen((v) => !v)}>{t(open ? "hide" : "open")}</button>
      </div>
      {act.error && <p className="error small" role="alert">{describe(act.error)}</p>}
      {open && (
        <div id="review-list" className="panel table-wrap" style={{ padding: 0 }}>
          <table className="t">
            <tbody>
              {files.map((f) => (
                <FileRow key={f.id} file={f} busy={act.isPending} resolve={(gameId) => act.mutate({ path: `/api/review/${f.id}/file`, body: { gameId } })} />
              ))}
              {packages.map((p) => (
                <PackageRow key={p.id} item={p} busy={act.isPending} retry={() => act.mutate({ path: `/api/review/${p.id}/retry` })} />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}

/** Un paquete de `Entrada\` sin juegos (#310): su nombre, lo que traía y «Reintentar», que lo vuelve a importar. */
function PackageRow({ item, busy, retry }: { item: PackageReview; busy: boolean; retry: () => void }) {
  const t = useTranslations("review");
  return (
    <tr>
      <td style={{ minWidth: 0 }}>
        <b>{item.name}</b>
        <div className="small muted">{item.path}</div>
        <details>
          <summary className="small">{t("packages.files", { count: item.files.length })}</summary>
          <ul className="small muted">{item.files.map((f) => <li key={f}>{f}</li>)}</ul>
        </details>
      </td>
      <td><span className="ot-badge">{t("packages.noGames")}</span></td>
      <td className="small">{t("packages.hint")}</td>
      <td style={{ textAlign: "right", whiteSpace: "nowrap" }}>
        <button className="ot-btn" type="button" disabled={busy} onClick={retry}>{t("packages.retry")}</button>
      </td>
    </tr>
  );
}

/** Un archivo dudoso o sin identificar (#181): qué es, qué propuso Claude, confirmar o elegir el juego con el buscador. */
function FileRow({ file, busy, resolve }: { file: FileReview; busy: boolean; resolve: (gameId: number | null) => void }) {
  const t = useTranslations("review");
  const consoleName = useConsoleName();
  const [picking, setPicking] = useState(false);
  const [query, setQuery] = useState("");
  const text = query.trim();
  const search = useQuery({
    queryKey: ["search", text],
    queryFn: () => request<SearchResults>(`/api/search?${new URLSearchParams({ q: text })}`),
    enabled: picking && text.length >= 2,
  });
  // Solo los juegos que salen en la consola del archivo.
  const games = (search.data?.games ?? []).filter((g) => g.platforms.includes(file.console)).slice(0, 8);
  const { name, folder } = split(file.path);
  const doubtful = file.kind === "doubtful";
  return (
    <>
      <tr>
        <td style={{ minWidth: 0 }}>
          <b>{name}</b>{file.component && <span className="ot-badge" style={{ marginLeft: 6 }}>{file.component}</span>}
          <div className="small muted">{folder} · {consoleName(file.console)}{file.titleId ? ` · ${file.titleId}` : ""}</div>
        </td>
        <td><span className={doubtful ? "ot-badge ot-badge-review" : "ot-badge"}>{t(`kind.${doubtful ? "doubtful" : "unidentified"}`)}</span></td>
        <td className="small">
          {file.proposed
            ? <>{t("files.proposed", { name: file.proposed })}{file.reason && <span className="muted"> · {file.reason}</span>}</>
            : file.reason ? <span className="muted">{t("files.noProposal", { reason: file.reason })}</span> : <span className="muted">{t("files.none")}</span>}
          {file.gameId != null && <div>{t("files.assigned")} <Link href={`/game/?id=${file.gameId}`}>{file.gameName}</Link></div>}
        </td>
        <td style={{ textAlign: "right", whiteSpace: "nowrap" }}>
          {doubtful && <button className="ot-btn ot-btn-primary" type="button" disabled={busy} onClick={() => resolve(null)}>{t("files.confirm")}</button>}{" "}
          <button className="ot-btn" type="button" aria-expanded={picking} onClick={() => setPicking((v) => !v)}>{t(doubtful ? "files.change" : "files.search")}</button>
        </td>
      </tr>
      {picking && (
        <tr>
          <td colSpan={4}>
            <div style={{ display: "grid", gap: 6 }}>
              <input type="search" aria-label={t("files.search")} placeholder={t("files.search")} value={query} onChange={(e) => setQuery(e.target.value)} />
              {games.map((g) => (
                <button key={g.id} className="ot-btn ot-btn-sm" type="button" style={{ justifySelf: "start" }} disabled={busy} onClick={() => resolve(Number(g.id))}>
                  {g.name}{g.year ? ` (${g.year})` : ""}
                </button>
              ))}
              {search.data && games.length === 0 && <span className="small muted">{t("files.noResults", { console: consoleName(file.console) })}</span>}
            </div>
          </td>
        </tr>
      )}
    </>
  );
}
