"use client";

import { useQuery, type QueryClient } from "@tanstack/react-query";
import { request } from "@/lib/client";
import { useDescribeError, useDynamicT } from "@/lib/i18n";
import type { TaskInfo } from "@/lib/types";

/** Tareas de la cola (F1.2): lo pendiente y lo reciente; cada cambio llega por el evento "task". */
export const useTasks = () => useQuery({ queryKey: ["tasks"], queryFn: () => request<TaskInfo[]>("/api/tasks") }).data ?? [];

/** Pone al día la lista con una tarea que llegó por el evento "task" (la más nueva arriba). */
export function upsertTask(cache: QueryClient, task: TaskInfo) {
  cache.setQueryData<TaskInfo[]>(["tasks"], (list = []) =>
    [task, ...list.filter((t) => t.id !== task.id)].sort((a, b) => Number(b.id) - Number(a.id)));
}

/** Filas del panel de tareas (TaskRow del sistema de diseño). */
export function TaskRows() {
  const t = useDynamicT();
  const describe = useDescribeError();
  return useTasks().map((task) => {
    const kind = `tasks.kind.${task.kind}`;
    const progress = Number(task.progress);
    return (
      <div key={task.id} className={`ot-task${task.status === "error" ? " is-error" : ""}`}>
        <span className="ot-task-title">{t.has(kind) ? t(kind) : task.kind}</span>
        <span className="ot-task-state">{t(`tasks.status.${task.status}`, { progress })}</span>
        {task.status === "running" && <div className="ot-progress"><span style={{ width: `${progress}%` }} /></div>}
        {task.detail && <span className="ot-task-detail">{describe(task.detail)}</span>}
      </div>
    );
  });
}
