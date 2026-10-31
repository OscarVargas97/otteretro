"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useFormatter, useTranslations } from "next-intl";
import { useState } from "react";
import { Empty } from "@/components/Shell";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import type { ReviewItem, ReviewPage } from "@/lib/types";

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
  const format = useFormatter();
  const cache = useQueryClient();
  const [tab, setTab] = useState<Tab>("pending");
  const page = useQuery({ queryKey: ["review"], queryFn: () => request<ReviewPage>("/api/review") });
  const act = useMutation({
    mutationFn: (path: string) => request<ReviewPage>(path, { method: "POST" }),
    onSuccess: (data) => {
      cache.setQueryData(["review"], data);
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
          <section key={item.id} className="panel">
            <div className="sec-h">
              <h2>{item.entity === "saga" ? <Link href={`/saga/?id=${item.entityId}`}>{item.title}</Link> : item.title}</h2>
              <span className="ot-badge ot-badge-info">{t(`prompt.${item.prompt as "ruta-saga"}`)}</span>
            </div>
            <Result item={item} />
            <div className="hero-actions">
              <button className="ot-btn ot-btn-primary" type="button" disabled={act.isPending} onClick={() => act.mutate(`/api/review/${item.id}/approve`)}>{t("approve")}</button>
              <button className="ot-btn" type="button" disabled={act.isPending} onClick={() => act.mutate(`/api/review/${item.id}/reject`)}>{t("reject")}</button>
            </div>
          </section>
        )))}

      {tab === "learnings" && (learnings.length === 0
        ? <Empty title={t("learningsEmptyTitle")} text={t("learningsEmptyText")} />
        : (
          <div className="panel table-wrap" style={{ padding: 0 }}>
            <table className="t">
              <thead><tr><th>{t("change")}</th><th>{t("reason")}</th><th>{t("date")}</th><th /></tr></thead>
              <tbody>
                {learnings.map((l) => (
                  <tr key={l.id} className={l.undone ? "is-unplayable" : undefined}>
                    <td>{t(`learning.${l.change as "move"}`)} <span className="small muted">#{l.id}</span></td>
                    <td>{l.reason ?? "—"}</td>
                    <td className="small">{format.dateTime(new Date(l.createdAt), { dateStyle: "medium" })}</td>
                    <td>
                      {l.undone ? <span className="ot-badge">{t("undone")}</span>
                        : <button className="ot-btn" type="button" disabled={act.isPending} onClick={() => act.mutate(`/api/learnings/${l.id}/undo`)}>{t("undo")}</button>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ))}
    </div>
  );
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
