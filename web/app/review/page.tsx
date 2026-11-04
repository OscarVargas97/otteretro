"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { useSearchParams } from "next/navigation";
import { Suspense, useState } from "react";
import { Empty } from "@/components/Shell";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import { Panels } from "@/components/Skeleton";
import { Tabs } from "@/components/Tabs";
import { usePaged } from "@/lib/paged";
import { reviewQuery, sagaQuery } from "@/lib/queries";
import { Cover, useConsoleName } from "@/components/Catalog";
import type { FileReview, ReviewPage, SearchResults } from "@/lib/types";

const TABS = ["doubtful", "unidentified"] as const;
type Tab = (typeof TABS)[number];

// Lo que trae cada resultado de Claude (F2.5): la forma depende del prompt.
type Source = { title: string; url: string };
type Route = { kind: "release" | "story" | "community"; rationale: string; steps: { gameId: number; versionId: number | null; reason: string }[]; sources: Source[] };
type Iconic = { iconic: boolean; gameIds: number[]; reason: string; sources: Source[] };
type Criterion = { score: number; text: string };
type Best = { bestVersionId: number; reason: string; versions: { versionId: number; content: Criterion; gameplay: Criterion; technical: Criterion; original: Criterion }[] };
type Kind = { kind: string; reason: string; before: string };
type Text = { text: string };

// La pestaña puede venir en la URL (/review/?tab=unidentified, desde el aviso de Inicio).
export default function ReviewScreen() {
  return <Suspense><Review /></Suspense>;
}

/** Revisar (§2.1, D3): lo de Claude por aprobar, los archivos dudosos o sin identificar (#181) y los aprendizajes. */
function Review() {
  const t = useTranslations("review");
  const describe = useDescribeError();
  const cache = useQueryClient();
  const asked = useSearchParams().get("tab");
  const [tab, setTab] = useState<Tab>(TABS.includes(asked as Tab) ? (asked as Tab) : "doubtful");
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

  if (page.isError) return <div className="content"><p className="error" role="alert">{describe(page.error)}</p></div>;
  if (!page.data) return <div className="content" aria-busy="true"><Panels count={3} /></div>;
  const { files } = page.data;
  const count = (k: Tab) => files.filter((f) => f.kind === k).length;

  return (
    <div className="content">
      <Tabs tabs={TABS.map((k) => ({ id: k, label: t(`tab.${k}`, { count: count(k) }) }))}
        current={tab} onSelect={(k) => setTab(k as Tab)} label={t("tabs")} panel="review-panel" />
      {act.error && <p className="error small" role="alert">{describe(act.error)}</p>}
      <div id="review-panel" role="tabpanel" aria-labelledby={`tab-${tab}`} style={{ display: "contents" }}>

      {(tab === "doubtful" || tab === "unidentified") && (count(tab) === 0
        ? <Empty title={t(`files.${tab}EmptyTitle`)} text={t(`files.${tab}EmptyText`)}><Link className="ot-btn" href="/collection/">{t("files.openCollection")}</Link></Empty>
        : files.filter((f) => f.kind === tab).map((f) => (
          <FileItem key={f.id} file={f} busy={act.isPending} resolve={(gameId) => act.mutate({ path: `/api/review/${f.id}/file`, body: { gameId } })} />
        )))}

      </div>
    </div>
  );
}

/** Un resultado por aprobar. Las rutas se pueden editar antes (F2.21): subir, bajar o quitar pasos. */
/** Un archivo en Dudosas o Sin identificar (#181): qué es, qué propuso Claude, confirmar o elegir el juego con el buscador. */
function FileItem({ file, busy, resolve }: { file: FileReview; busy: boolean; resolve: (gameId: number | null) => void }) {
  const t = useTranslations("review");
  const consoleName = useConsoleName();
  const [picking, setPicking] = useState(file.kind === "unidentified");
  const [query, setQuery] = useState("");
  const text = query.trim();
  const search = useQuery({
    queryKey: ["search", text],
    queryFn: () => request<SearchResults>(`/api/search?${new URLSearchParams({ q: text })}`),
    enabled: picking && text.length >= 2,
  });
  // Solo los juegos que salen en la consola del archivo.
  const games = (search.data?.games ?? []).filter((g) => g.platforms.includes(file.console)).slice(0, 8);
  return (
    <div className="panel" style={{ display: "grid", gap: 6 }}>
      <div><span className="folder-path">{file.path}</span>{file.component && <span className="ot-badge" style={{ marginLeft: 6 }}>{file.component}</span>}</div>
      <span className="small muted">{consoleName(file.console)}{file.titleId ? ` · ${file.titleId}` : ""}</span>
      {file.proposed
        ? <span className="small">{t("files.proposed", { name: file.proposed })}{file.reason && <span className="muted"> · {file.reason}</span>}</span>
        : file.reason && <span className="small muted">{t("files.noProposal", { reason: file.reason })}</span>}
      {file.gameId != null && <span className="small">{t("files.assigned")} <Link href={`/game/?id=${file.gameId}`}>{file.gameName}</Link></span>}
      <div className="hero-actions">
        {file.kind === "doubtful" && <button className="ot-btn ot-btn-primary ot-btn-sm" type="button" disabled={busy} onClick={() => resolve(null)}>{t("files.confirm")}</button>}
        {!picking && <button className="ot-btn ot-btn-sm" type="button" onClick={() => setPicking(true)}>{t("files.change")}</button>}
      </div>
      {picking && (
        <div style={{ display: "grid", gap: 6 }}>
          <input type="search" aria-label={t("files.search")} placeholder={t("files.search")} value={query} onChange={(e) => setQuery(e.target.value)} />
          {games.map((g) => (
            <button key={g.id} className="ot-btn ot-btn-sm" type="button" style={{ justifySelf: "start" }} disabled={busy} onClick={() => resolve(Number(g.id))}>
              {g.name}{g.year ? ` (${g.year})` : ""}
            </button>
          ))}
          {search.data && games.length === 0 && <span className="small muted">{t("files.noResults", { console: consoleName(file.console) })}</span>}
        </div>
      )}
    </div>
  );
}
