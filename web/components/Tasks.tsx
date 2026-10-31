"use client";

import { useMutation, useQuery, useQueryClient, type QueryClient } from "@tanstack/react-query";
import { useFormatter } from "next-intl";
import { useState } from "react";
import { request } from "@/lib/client";
import { useDescribeError, useDynamicT } from "@/lib/i18n";
import type { TaskInfo, TaskLogEntry } from "@/lib/types";

/** Tareas de la cola (F1.2): lo pendiente y lo reciente; cada cambio llega por el evento "task". */
export const useTasks = () => useQuery({ queryKey: ["tasks"], queryFn: () => request<TaskInfo[]>("/api/tasks") }).data ?? [];

// El orden del panel (F2.11), el mismo que da la API: lo que corre, la cola y lo terminado por hora de término.
const rank = (t: TaskInfo) => (t.status === "running" ? 0 : t.status === "queued" ? 1 : 2);
const byPanelOrder = (a: TaskInfo, b: TaskInfo) =>
  rank(a) - rank(b) || (b.finishedAt ?? "").localeCompare(a.finishedAt ?? "") || Number(a.id) - Number(b.id);

/** Pone al día la lista con una tarea que llegó por el evento "task". */
export function upsertTask(cache: QueryClient, task: TaskInfo) {
  cache.setQueryData<TaskInfo[]>(["tasks"], (list = []) => [task, ...list.filter((t) => t.id !== task.id)].sort(byPanelOrder));
}

/** Filas del panel de tareas (TaskRow del sistema de diseño). Al hacer clic se abre su registro (F2.10). */
export function TaskRows() {
  const t = useDynamicT();
  const describe = useDescribeError();
  const [open, setOpen] = useState<number | null>(null);
  return useTasks().map((task) => {
    const kind = `tasks.kind.${task.kind}`;
    const progress = Number(task.progress);
    const id = Number(task.id);
    return (
      <div key={task.id} className={`ot-task${task.status === "error" ? " is-error" : ""}`}>
        <button type="button" className="ot-task-title" aria-expanded={open === id} onClick={() => setOpen(open === id ? null : id)}
          style={{ all: "unset", cursor: "pointer", fontWeight: "inherit" }}>
          {t.has(kind) ? t(kind) : task.kind} <span className="small muted">#{id}</span>
        </button>
        <span className="ot-task-state">{t(`tasks.status.${task.status}`, { progress })}</span>
        {task.status === "running" && <div className="ot-progress"><span style={{ width: `${progress}%` }} /></div>}
        {task.detail && <span className="ot-task-detail">{describe(task.detail)}</span>}
        {open === id && <TaskLog id={id} live={task.status === "running"} />}
      </div>
    );
  });
}

/** El registro de una tarea, lo más nuevo arriba; mientras corre se pone al día solo. */
function TaskLog({ id, live }: { id: number; live: boolean }) {
  const t = useDynamicT();
  const describe = useDescribeError();
  const format = useFormatter();
  const log = useQuery({ queryKey: ["tasks", id, "log"], queryFn: () => request<TaskLogEntry[]>(`/api/tasks/${id}/log`), refetchInterval: live ? 3_000 : false });
  if (!log.data) return null;
  if (log.data.length === 0) return <p className="small muted" style={{ margin: 0 }}>{t("tasks.logEmpty")}</p>;
  return (
    <ol className="small" aria-label={t("tasks.log")} style={{ margin: 0, paddingLeft: 0, listStyle: "none", maxHeight: 240, overflowY: "auto" }}>
      {log.data.map((l) => (
        <li key={l.id} className={l.level === "error" ? "error" : l.level === "warn" ? "muted" : undefined}>
          <span className="muted">{format.dateTime(new Date(l.at), { timeStyle: "medium" })}</span> {describe(l.message)}
        </li>
      ))}
    </ol>
  );
}

/** «Limpiar terminadas» (F2.11): borra las tareas hechas o con error y su registro. */
export function ClearTasks() {
  const t = useDynamicT();
  const cache = useQueryClient();
  const tasks = useTasks();
  const clear = useMutation({
    mutationFn: () => request<number>("/api/tasks/finished", { method: "DELETE" }),
    onSuccess: () => cache.invalidateQueries({ queryKey: ["tasks"] }),
  });
  if (!tasks.some((x) => x.status === "done" || x.status === "error")) return null;
  return <button className="ot-btn" type="button" disabled={clear.isPending} onClick={() => clear.mutate()}>{t("tasks.clear")}</button>;
}
