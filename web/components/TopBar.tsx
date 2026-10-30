"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useTranslations } from "next-intl";
import { useDeferredValue, useEffect, useRef, useState } from "react";
import { eventsUrl, request } from "@/lib/client";
import { useDescribeError } from "@/lib/i18n";
import type { SearchResults } from "@/lib/types";
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

/** Búsqueda global (§0.6): sagas y juegos por nombre, en español u original. Enter abre el primero. */
function Search({ onClose }: { onClose: () => void }) {
  const t = useTranslations("topBar");
  const tc = useTranslations("catalog");
  const router = useRouter();
  const field = useRef<HTMLInputElement>(null);
  const [query, setQuery] = useState("");
  const text = useDeferredValue(query.trim());
  const results = useQuery({
    queryKey: ["search", text],
    queryFn: () => request<SearchResults>(`/api/search?q=${encodeURIComponent(text)}`),
    enabled: text.length >= 2,
    placeholderData: (previous) => previous,
  });
  useEffect(() => field.current?.focus(), []);
  const links = [
    ...(results.data?.sagas ?? []).map((s) => ({ href: `/saga/?id=${s.id}`, name: s.name, meta: tc("games", { count: Number(s.games) }), group: "sagas" as const })),
    ...(results.data?.games ?? []).map((g) => ({ href: `/game/?id=${g.id}`, name: g.name, meta: [g.sagaName, g.year].filter(Boolean).join(" · "), group: "games" as const })),
  ];
  const open = (href: string) => {
    onClose();
    router.push(href);
  };

  return (
    <div className="overlay" onClick={onClose}>
      <div className="search-dialog" role="dialog" aria-label={t("searchLabel")} onClick={(e) => e.stopPropagation()}>
        <label htmlFor="search" className="small muted">{t("searchLabel")}</label>
        <input id="search" ref={field} placeholder={t("searchPlaceholder")} value={query} autoComplete="off"
          onChange={(e) => setQuery(e.target.value)}
          onKeyDown={(e) => { if (e.key === "Enter" && links[0]) open(links[0].href); }} />
        {text.length < 2 ? (
          <p className="small muted">{tc("search.typeMore")}</p>
        ) : results.data && links.length === 0 ? (
          <p className="small muted">{tc("search.noResults", { query: text })}</p>
        ) : (
          (["sagas", "games"] as const).map((group) => links.some((l) => l.group === group) && (
            <div key={group} className="search-group">
              <h3 className="small muted">{tc(`search.${group}`)}</h3>
              {links.filter((l) => l.group === group).map((l) => (
                <Link key={l.href} className="search-result" href={l.href} onClick={(e) => { e.preventDefault(); open(l.href); }}>
                  <span>{l.name}</span>
                  <span className="small muted">{l.meta}</span>
                </Link>
              ))}
            </div>
          ))
        )}
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
