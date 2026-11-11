"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { useConsoleName } from "@/components/Catalog";
import { request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import type { PlayOrder, VersionPlay } from "@/lib/types";

type Criterion = "pc" | "port" | "perfect" | "playable";

/** «Cómo jugar» en la ficha (#398, §12.4): por versión, la opción elegida, su motivo y «Cambiar». El botón Jugar llega con F6.3. */
export function GamePlay({ gameId }: { gameId: number }) {
  const t = useTranslations("play");
  const versions = useQuery({ queryKey: ["play", gameId], queryFn: () => request<VersionPlay[]>(`/api/games/${gameId}/play`) });
  const shown = versions.data?.filter((v) => v.options.length > 0) ?? [];
  if (!shown.length) return null;
  return (
    <section className="sec">
      <div className="sec-h"><h2>{t("title")}</h2></div>
      {shown.map((v) => <VersionChoice key={String(v.versionId)} gameId={gameId} version={v} />)}
    </section>
  );
}

function VersionChoice({ gameId, version: v }: { gameId: number; version: VersionPlay }) {
  const t = useTranslations("play");
  const consoleName = useConsoleName();
  const describe = useDescribeError();
  const cache = useQueryClient();
  const [choosing, setChoosing] = useState(false);
  const url = `/api/games/${gameId}/play/${v.versionId}`;
  const change = useMutation({
    mutationFn: (key: string | null) => request<VersionPlay[]>(url, key === null ? { method: "DELETE" } : { method: "PUT", body: JSON.stringify({ key }) }),
    onSuccess: (data) => { cache.setQueryData(["play", gameId], data); setChoosing(false); },
  });
  const label = (o: VersionPlay["options"][number]) =>
    o.kind === "pc" ? t("pc", { platform: consoleName(o.name) }) : t(o.kind === "port" ? "port" : "emulator", { name: o.name });
  const chosen = v.options.find((o) => o.key === v.chosen);
  return (
    <div className="sentence small" role="group" aria-label={consoleName(v.platform)}>
      <span>{consoleName(v.platform)}:</span>
      {chosen && <b>{label(chosen)}</b>}
      {chosen && !chosen.installed && <span className="ot-badge">{t("notInstalled")}</span>}
      {v.reason && <span className="muted">{t(`reason.${v.reason as Criterion | "manual"}`)}</span>}
      {choosing
        ? (
          <select aria-label={t("chooseFor", { console: consoleName(v.platform) })} defaultValue="" disabled={change.isPending}
            onChange={(e) => { if (e.target.value) change.mutate(e.target.value); }}>
            <option value="" disabled>{t("change")}</option>
            {v.options.map((o) => <option key={o.key} value={o.key}>{label(o)}</option>)}
          </select>
        )
        : <button className="ot-btn ot-btn-sm" type="button" onClick={() => setChoosing(true)}>{t("change")}</button>}
      {v.reason === "manual" && (
        <button className="ot-btn ot-btn-sm" type="button" disabled={change.isPending} onClick={() => change.mutate(null)}>{t("reset")}</button>
      )}
      {change.isError && <span className="error" role="alert">{describe(change.error)}</span>}
    </div>
  );
}

/** Ajustes › Cómo jugar (#398, §8.3): el orden de los criterios de la regla automática. */
export function PlayOrderSettings() {
  const t = useTranslations("play");
  const describe = useDescribeError();
  const cache = useQueryClient();
  const order = useQuery({ queryKey: ["playOrder"], queryFn: () => request<PlayOrder>("/api/play/order") });
  const save = useMutation({
    mutationFn: (criteria: string[]) => request<PlayOrder>("/api/play/order", { method: "PUT", body: JSON.stringify({ criteria }) }),
    onSuccess: (data) => cache.setQueryData(["playOrder"], data),
  });
  const criteria = order.data?.criteria ?? [];
  const move = (i: number, to: number) => {
    const next = [...criteria];
    [next[i], next[to]] = [next[to], next[i]];
    save.mutate(next);
  };
  return (
    <section className="panel">
      <h2>{t("order")}</h2>
      <p className="small muted" style={{ margin: 0 }}>{t("orderHelp")}</p>
      <ol>
        {criteria.map((c, i) => (
          <li key={c} className="sentence">
            <span>{t(`criterion.${c as Criterion}`)}</span>
            <button className="ot-btn ot-btn-sm" type="button" disabled={i === 0 || save.isPending} aria-label={t("up", { criterion: t(`criterion.${c as Criterion}`) })}
              onClick={() => move(i, i - 1)}>↑</button>
            <button className="ot-btn ot-btn-sm" type="button" disabled={i === criteria.length - 1 || save.isPending} aria-label={t("down", { criterion: t(`criterion.${c as Criterion}`) })}
              onClick={() => move(i, i + 1)}>↓</button>
          </li>
        ))}
      </ol>
      {save.isError && <p className="error small" role="alert">{describe(save.error)}</p>}
    </section>
  );
}
