"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { Empty } from "@/components/Shell";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import type { LearningItem, ReviewItem, ReviewPage } from "@/lib/types";

type Tab = "pending" | "learnings";

// Lo que trae cada resultado de Claude (F2.5): la forma depende del prompt.
type Source = { title: string; url: string };
type Route = { kind: "release" | "story" | "community"; rationale: string; steps: { gameId: number; versionId: number | null; reason: string }[]; sources: Source[] };
type Iconic = { iconic: boolean; gameIds: number[]; reason: string; sources: Source[] };
type Criterion = { score: number; text: string };
type Best = { bestVersionId: number; reason: string; versions: { versionId: number; content: Criterion; gameplay: Criterion; technical: Criterion; original: Criterion }[] };
type Kind = { kind: string; reason: string; before: string };
type Text = { text: string };

/** Revisar (§2.1, D3): los resultados de Claude por aprobar y los aprendizajes, que se pueden deshacer. */
export default function Review() {
  const t = useTranslations("review");
  const describe = useDescribeError();
  const cache = useQueryClient();
  const [tab, setTab] = useState<Tab>("pending");
  const page = useQuery({ queryKey: ["review"], queryFn: () => request<ReviewPage>("/api/review") });
  const act = useMutation({
    mutationFn: ({ path, body }: { path: string; body?: unknown }) =>
      request<ReviewPage>(path, { method: "POST", body: body === undefined ? undefined : JSON.stringify(body) }),
    onSuccess: (data) => {
      cache.setQueryData(["review"], data);
      cache.invalidateQueries({ queryKey: ["review", "count"] });
      cache.invalidateQueries({ queryKey: ["saga"] });
      cache.invalidateQueries({ queryKey: ["home"] });
    },
  });

  if (page.isError) return <div className="content"><p className="error" role="alert">{describe(page.error)}</p></div>;
  if (!page.data) return <div className="content"><p className="muted">{t("loading")}</p></div>;
  const { pending, learnings } = page.data;

  return (
    <div className="content">
      <div className="chips" role="tablist">
        {(["pending", "learnings"] as const).map((k) => (
          <button key={k} className="chip" type="button" role="tab" aria-selected={tab === k} aria-pressed={tab === k} onClick={() => setTab(k)}>
            {t(`tab.${k}`, { count: k === "pending" ? pending.length : learnings.filter((l) => !l.undone).length })}
          </button>
        ))}
      </div>
      {act.error && <p className="error small" role="alert">{describe(act.error)}</p>}

      {tab === "pending" && (pending.length === 0
        ? <Empty title={t("pendingEmptyTitle")} text={t("pendingEmptyText")} />
        : pending.map((item) => (
          <Pending key={item.id} item={item} busy={act.isPending}
            approve={(routes) => act.mutate(routes ? { path: `/api/review/${item.id}/approve-edited`, body: { routes } } : { path: `/api/review/${item.id}/approve` })}
            reject={() => act.mutate({ path: `/api/review/${item.id}/reject` })} />
        )))}

      {tab === "learnings" && (learnings.length === 0
        ? <Empty title={t("learningsEmptyTitle")} text={t("learningsEmptyText")} />
        : <Learnings learnings={learnings} busy={act.isPending} undo={(id) => act.mutate({ path: `/api/learnings/${id}/undo` })}
            onCorrected={(data) => cache.setQueryData(["review"], data)} />)}
    </div>
  );
}

/** Un resultado por aprobar. Las rutas se pueden editar antes (F2.21): subir, bajar o quitar pasos. */
function Pending({ item, busy, approve, reject }: { item: ReviewItem; busy: boolean; approve: (routes?: Route[]) => void; reject: () => void }) {
  const t = useTranslations("review");
  const [routes, setRoutes] = useState<Route[] | null>(null);
  const editable = item.prompt === "ruta-saga";
  return (
    <section className="panel">
      <div className="sec-h">
        <h2>{item.entity === "saga" ? <Link href={`/saga/?id=${item.entityId}`}>{item.title}</Link> : item.title}</h2>
        <span className="ot-badge ot-badge-info">{t(`prompt.${item.prompt as "ruta-saga"}`)}</span>
      </div>
      {routes ? <RouteEditor routes={routes} onChange={setRoutes} /> : <Result item={item} />}
      <div className="hero-actions">
        <button className="ot-btn ot-btn-primary" type="button" disabled={busy} onClick={() => approve(routes ?? undefined)}>{routes ? t("approveEdited") : t("approve")}</button>
        {editable && !routes && <button className="ot-btn" type="button" onClick={() => setRoutes(JSON.parse(item.result) as Route[])}>{t("edit")}</button>}
        {routes && <button className="ot-btn" type="button" onClick={() => setRoutes(null)}>{t("cancel")}</button>}
        <button className="ot-btn" type="button" disabled={busy} onClick={reject}>{t("reject")}</button>
      </div>
    </section>
  );
}

function RouteEditor({ routes, onChange }: { routes: Route[]; onChange: (routes: Route[]) => void }) {
  const t = useTranslations("review");
  const tc = useTranslations("catalog");
  const update = (r: number, steps: Route["steps"]) => onChange(routes.map((x, i) => (i === r ? { ...x, steps } : x)));
  const move = (r: number, from: number, to: number) => {
    const steps = [...routes[r].steps];
    steps.splice(to, 0, ...steps.splice(from, 1));
    update(r, steps);
  };
  return <>{routes.map((route, r) => (
    <div key={route.kind}>
      <b>{tc(`route.${route.kind}`)}</b>
      <ol className="small" style={{ margin: "4px 0" }}>
        {route.steps.map((s, i) => (
          <li key={s.gameId} style={{ display: "flex", gap: 6, alignItems: "center" }}>
            <span style={{ flex: 1 }}>{s.reason}</span>
            <button className="ot-btn" type="button" aria-label={t("stepUp", { step: i + 1 })} disabled={i === 0} onClick={() => move(r, i, i - 1)}>↑</button>
            <button className="ot-btn" type="button" aria-label={t("stepDown", { step: i + 1 })} disabled={i === route.steps.length - 1} onClick={() => move(r, i, i + 1)}>↓</button>
            <button className="ot-btn" type="button" aria-label={t("stepRemove", { step: i + 1 })} onClick={() => update(r, route.steps.filter((_, j) => j !== i))}>×</button>
          </li>
        ))}
      </ol>
    </div>
  ))}</>;
}

/** El resultado en palabras, según el prompt. */
function Result({ item }: { item: ReviewItem }) {
  const t = useTranslations("review");
  const tc = useTranslations("catalog");
  const value: unknown = JSON.parse(item.result);
  const sources = (list: Source[]) => list.length > 0 && (
    <p className="small muted">{t("sources")}: {list.map((s, i) => <span key={s.url}>{i > 0 && " · "}<a href={s.url} target="_blank" rel="noreferrer">{s.title}</a></span>)}</p>
  );
  switch (item.prompt) {
    case "ruta-saga":
      return <>{(value as Route[]).map((r) => (
        <div key={r.kind}>
          <b>{tc(`route.${r.kind}`)}</b> · <span className="small">{r.rationale}</span>
          <ol className="small" style={{ margin: "4px 0" }}>{r.steps.map((s, i) => <li key={i}>{s.reason}</li>)}</ol>
          {sources(r.sources)}
        </div>
      ))}</>;
    case "iconico": {
      const v = value as Iconic;
      return <><p>{v.iconic ? t("iconicYes") : t("iconicNo")} · {v.reason}</p>{sources(v.sources)}</>;
    }
    case "mejor-version": {
      const v = value as Best;
      return <p>{t("bestVersion", { count: v.versions.length })} · {v.reason}</p>;
    }
    case "clasificar-version": {
      const v = value as Kind;
      return <p>{tc(`kind.${v.before as "original"}`)} → <b>{tc(`kind.${v.kind as "original"}`)}</b> · {v.reason}</p>;
    }
    case "traducir-descripcion":
      return <p className="small">{(value as Text).text}</p>;
    default:
      return null;
  }
}

/** Aprendizajes (F2.12): cada cambio en claro, con filtros, agrupado por tarea; se deshace o se corrige con una nota para Claude. */
function Learnings({ learnings, busy, undo, onCorrected }: {
  learnings: LearningItem[]; busy: boolean; undo: (id: number) => void; onCorrected: (page: ReviewPage) => void;
}) {
  const t = useTranslations("review");
  const [kind, setKind] = useState<string | null>(null);
  const [query, setQuery] = useState("");
  const text = query.trim().toLowerCase();
  const shown = learnings.filter((l) => (!kind || l.change === kind)
    && (!text || [l.sagaName, l.to, l.kept, l.nameBefore, l.nameAfter, ...l.games, ...l.from].some((x) => x?.toLowerCase().includes(text))));
  const groups = Map.groupBy(shown, (l) => (l.taskId == null ? null : Number(l.taskId)));
  return (
    <>
      <div className="chips" style={{ alignItems: "center" }}>
        {([null, "move", "merge", "rename"] as const).map((k) => (
          <button key={k ?? "all"} className="chip" type="button" aria-pressed={kind === k} onClick={() => setKind(k)}>
            {k ? t(`kindFilter.${k}`) : t("filterAll")}
          </button>
        ))}
        <input type="search" aria-label={t("search")} placeholder={t("search")} value={query} onChange={(e) => setQuery(e.target.value)} />
      </div>
      {[...groups].map(([task, items]) => (
        <section key={task ?? "none"} className="sec">
          <div className="sec-h"><h2>{task == null ? t("noTask") : t("task", { id: task })}</h2><span className="small muted">{t("count", { count: items.length })}</span></div>
          {items.map((l) => <Learning key={l.id} l={l} busy={busy} undo={undo} onCorrected={onCorrected} />)}
        </section>
      ))}
    </>
  );
}

function Learning({ l, busy, undo, onCorrected }: { l: LearningItem; busy: boolean; undo: (id: number) => void; onCorrected: (page: ReviewPage) => void }) {
  const t = useTranslations("review");
  const describe = useDescribeError();
  const [note, setNote] = useState<string | null>(null);
  const correct = useMutation({
    mutationFn: () => request<ReviewPage>(`/api/learnings/${l.id}/correct`, { method: "POST", body: JSON.stringify({ note }) }),
    onSuccess: (data) => { setNote(null); onCorrected(data); },
  });
  const list = (names: string[]) => names.map((n) => n || t("loose")).join(", ");
  const what = l.change === "move"
    ? t("moved", { games: list(l.games), from: list(l.from), to: l.toKind === "new" ? t("toNew", { name: l.to ?? "" }) : l.toKind === "none" ? t("toNone") : l.to ?? "" })
    : l.change === "merge" ? t("merged", { games: list(l.games), kept: l.kept ?? "" })
      : l.change === "rename" ? t("renamed", { before: l.nameBefore ?? "?", after: l.nameAfter ?? "" })
        : t(`learning.${l.change as "move"}`);
  return (
    <div className={`panel${l.undone ? " is-unplayable" : ""}`} style={{ display: "grid", gap: 6 }}>
      <div><b>{what}</b> <span className="small muted">#{l.id}</span>{l.undone && <> <span className="ot-badge">{t("undone")}</span></>}</div>
      {l.reason && <span className="small muted">{l.reason}</span>}
      {l.rules.map((r, i) => <span key={i} className="small">{t("rule", { text: r })}</span>)}
      {note !== null ? (
        <form style={{ display: "grid", gap: 6 }} onSubmit={(e) => { e.preventDefault(); correct.mutate(); }}>
          <textarea rows={2} maxLength={500} aria-label={t("correct")} placeholder={t("correctPlaceholder")} value={note} onChange={(e) => setNote(e.target.value)} />
          <div className="hero-actions">
            <button className="ot-btn ot-btn-primary" type="submit" disabled={correct.isPending || note.trim() === ""}>{t("send")}</button>
            <button className="ot-btn" type="button" onClick={() => setNote(null)}>{t("cancel")}</button>
          </div>
          {correct.error && <p className="error small" role="alert">{describe(correct.error)}</p>}
        </form>
      ) : (
        <div className="hero-actions">
          {l.sagaId != null && <Link className="ot-btn" href={`/saga/?id=${l.sagaId}`}>{t("openSaga")}</Link>}
          {!l.undone && <button className="ot-btn" type="button" disabled={busy} onClick={() => undo(Number(l.id))}>{t("undo")}</button>}
          <button className="ot-btn" type="button" onClick={() => setNote("")}>{t("correct")}</button>
        </div>
      )}
    </div>
  );
}
