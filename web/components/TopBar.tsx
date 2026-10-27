"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useEffect, useRef, useState } from "react";
import { eventsUrl, request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import { UpdateTask, useUpdate } from "./Update";
import { MoveTask, useMove } from "./DataFolder";
import { TaskRows, upsertTask, useTasks } from "./Tasks";
import { Empty, useAppState } from "./Shell";

/** Título de la pantalla, búsqueda global (Ctrl+K) y panel de tareas en segundo plano. */
export function TopBar({ title }: { title: string }) {
  const t = useTranslations("topBar");
  const [searching, setSearching] = useState(false);
  const [tasksOpen, setTasksOpen] = useState(false);
  const connected = useEvents();
  const fullscreen = !!useAppState().data?.fullscreen;
  const describe = useDescribeError();
  const goToSteam = useMutation({ mutationFn: () => request("/api/steam/big-picture", { method: "POST" }) });
  const update = useUpdate();
  const move = useMove();
  const tasks = useTasks();
  const moving = !!move?.running;
  const hasTasks = tasks.length > 0 || (!!update && ["Checking", "Downloading", "Ready", "Error"].includes(update.phase)) || (!!move && (move.running || ["done", "error"].includes(move.step)));
  const active = Number(update?.phase === "Checking" || update?.phase === "Downloading") + Number(moving)
    + tasks.filter((t) => t.status === "queued" || t.status === "running").length;
  const busy = active > 0;

  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        setSearching(true);
      }
      if (e.key === "Escape") {
        setSearching(false);
        setTasksOpen(false);
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);

  return (
    <header className="top">
      <h1>{title}</h1>
      <button className="search" type="button" onClick={() => setSearching(true)}>
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
          <circle cx="11" cy="11" r="7" />
          <path d="M20 20l-4-4" />
        </svg>
        <span className="search-text">{t("search")}</span>
        <span className="kbd">{t("shortcut")}</span>
      </button>
      {fullscreen && (
        <>
          <button className="ot-btn" type="button" disabled={goToSteam.isPending} onClick={() => goToSteam.mutate()}>{t("goToSteam")}</button>
          {goToSteam.error && <span className="error small" role="alert">{describe(goToSteam.error)}</span>}
        </>
      )}
      <button className="ot-btn" type="button" aria-expanded={tasksOpen} onClick={() => setTasksOpen(!tasksOpen)}>
        <span className="dot" data-connected={connected} data-busy={busy} />
        {t("tasks")}
        <span className="ot-count">{active}</span>
      </button>

      {tasksOpen && (
        <div className="ot-tasks tasks-panel" role="dialog" aria-label={t("tasksPanel")}>
          <UpdateTask />
          <MoveTask />
          <TaskRows />
          {!hasTasks && <Empty title={t("noTasksTitle")} text={t("noTasksText")} />}
          <p className="small muted tasks-panel-footer">{connected ? t("connected") : t("reconnecting")}</p>
        </div>
      )}

      {searching && <Search onClose={() => setSearching(false)} />}
    </header>
  );
}

function Search({ onClose }: { onClose: () => void }) {
  const t = useTranslations("topBar");
  const field = useRef<HTMLInputElement>(null);
  useEffect(() => field.current?.focus(), []);
  return (
    <div className="overlay" onClick={onClose}>
      <div className="search-dialog" role="dialog" aria-label={t("searchLabel")} onClick={(e) => e.stopPropagation()}>
        <label htmlFor="search" className="small muted">{t("searchLabel")}</label>
        <input id="search" ref={field} placeholder={t("searchPlaceholder")} />
        <p className="small muted">{t("searchLater")}</p>
      </div>
    </div>
  );
}

/** Conexión de Server-Sent Events con la app; el navegador reconecta solo. */
function useEvents() {
  const [connected, setConnected] = useState(false);
  const cache = useQueryClient();
  useEffect(() => {
    const source = new EventSource(eventsUrl());
    source.onopen = () => setConnected(true);
    source.onerror = () => setConnected(false);
    source.addEventListener("update", (e) => cache.setQueryData(["update"], JSON.parse(e.data)));
    source.addEventListener("move", (e) => cache.setQueryData(["move"], JSON.parse(e.data)));
    source.addEventListener("task", (e) => upsertTask(cache, JSON.parse(e.data)));
    // Al reconectar (por ejemplo, tras reiniciar la app) se vuelve a pedir la lista.
    source.addEventListener("open", () => cache.invalidateQueries({ queryKey: ["tasks"] }));
    return () => source.close();
  }, [cache]);
  return connected;
}
