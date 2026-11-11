"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useTranslations } from "next-intl";
import type { ReactNode } from "react";
import { request } from "@/lib/client";
import type { SteamChanges, SteamEntry, SteamStatus } from "@/lib/types";
import { useTasks } from "./Tasks";

/** La cuenta, Steam ROM Manager y lo que se puede marcar; y lo marcado con sus diferencias (#401, #402). Ambas bajo ["steam"]. */
export const steamQuery = { queryKey: ["steam"], queryFn: () => request<SteamStatus>("/api/steam") };
export const steamExportQuery = { queryKey: ["steam", "export"], queryFn: () => request<SteamChanges>("/api/steam/export") };

const same = (a: SteamEntry) => (b: SteamEntry) => String(a.versionId) === String(b.versionId);

/** Marcar o desmarcar una versión: pasa en el acto de una lista a la otra y el servidor recalcula las diferencias por detrás. */
function useMark() {
  const cache = useQueryClient();
  return useMutation({
    mutationFn: ({ entry, on }: { entry: SteamEntry; on: boolean }) =>
      request<void>(`/api/versions/${entry.versionId}/steam`, { method: "PUT", body: JSON.stringify({ export: on }) }),
    onMutate: ({ entry, on }) => {
      cache.setQueryData<SteamStatus>(["steam"], (s) => s && { ...s, available: on ? s.available.filter((e) => !same(entry)(e)) : [...s.available, entry] });
      cache.setQueryData<SteamChanges>(["steam", "export"], (c) => c && { ...c, entries: on ? [...c.entries, entry] : c.entries.filter((e) => !same(entry)(e)) });
    },
    // Si falla, la lectura de nuevo deja todo como está guardado.
    onSettled: () => cache.invalidateQueries({ queryKey: ["steam"] }),
  });
}

/** Lo de `entries` agrupado por saga (su categoría en Steam), con una casilla por juego. */
export function SteamGroups({ entries, marked, badge }: { entries: SteamEntry[]; marked: boolean; badge?: (e: SteamEntry) => ReactNode }) {
  const mark = useMark();
  const groups = Map.groupBy([...entries].sort((a, b) => a.category.localeCompare(b.category) || a.title.localeCompare(b.title)), (e) => e.category);
  return [...groups].map(([category, list]) => (
    <div key={category} className="panel" role="group" aria-label={category}>
      <h3 style={{ margin: 0 }}>{category} <span className="small muted">{list.length}</span></h3>
      {list.map((e) => (
        <label key={e.versionId} className="version-row">
          <span><input type="checkbox" checked={marked} onChange={() => mark.mutate({ entry: e, on: !marked })} /> {e.title}</span>
          {badge?.(e)}
        </label>
      ))}
    </div>
  ));
}

/** La cuenta detectada, o qué hacer si no hay Steam; y una línea a Herramientas si falta Steam ROM Manager. */
export function SteamAccount({ status }: { status: SteamStatus }) {
  const t = useTranslations("steam");
  return (
    <>
      <span>{status.account ? t("account", { account: status.account }) : t("noAccount")}</span>
      {!status.srm && <span className="small muted">{t.rich("noSrm", { link: (chunks) => <Link href="/tools/?k=tool">{chunks}</Link> })}</span>}
    </>
  );
}

/** Exportar: encola la tarea y sigue su estado por el panel de tareas (llega por SSE). */
export function useExport() {
  const cache = useQueryClient();
  const run = useMutation({
    mutationFn: () => request<number>("/api/steam/export", { method: "POST" }),
    onSuccess: () => cache.invalidateQueries({ queryKey: ["tasks"] }),
  });
  const task = useTasks().find((x) => String(x.id) === String(run.data));
  const busy = run.isPending || (!!task && (task.status === "queued" || task.status === "running"));
  return { run, task, busy };
}
