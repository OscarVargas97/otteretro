"use client";

import type { ReactNode } from "react";

export type SideGroup = { label?: string; items: { id: string; label: string }[] };

/**
 * Lista y detalle (#196, #197): las secciones agrupadas en una columna a la izquierda y una sola columna de contenido con
 * ancho máximo; en una pantalla angosta la lista pasa arriba. Es una lista de pestañas vertical: flechas, Inicio y Fin
 * mueven entre ellas. Cada pestaña lleva el id `tab-<id>` y controla `panel`.
 */
export function SideNav({ groups, current, onSelect, label, panel, children }:
  { groups: SideGroup[]; current: string; onSelect: (id: string) => void; label: string; panel: string; children: ReactNode }) {
  const all = groups.flatMap((g) => g.items);
  const onKeyDown = (e: React.KeyboardEvent) => {
    const i = all.findIndex((x) => x.id === current);
    const to = { ArrowDown: i + 1, ArrowRight: i + 1, ArrowUp: i - 1, ArrowLeft: i - 1, Home: 0, End: all.length - 1 }[e.key];
    if (to === undefined || !(e.target as HTMLElement).matches('[role="tab"]')) return;
    e.preventDefault();
    const next = all[(to + all.length) % all.length];
    onSelect(next.id);
    document.getElementById(`tab-${next.id}`)?.focus();
  };
  return (
    <div className="side-layout">
      <nav className="side-nav" role="tablist" aria-orientation="vertical" aria-label={label} onKeyDown={onKeyDown}>
        {groups.map((g, i) => (
          <div key={g.label ?? i} className="side-group" role="presentation">
            {g.label && <span className="side-group-label small muted" role="presentation">{g.label}</span>}
            {g.items.map((x) => (
              <button key={x.id} id={`tab-${x.id}`} type="button" className="side-item" role="tab" aria-selected={x.id === current}
                aria-controls={panel} tabIndex={x.id === current ? 0 : -1} onClick={() => onSelect(x.id)}>
                {x.label}
              </button>
            ))}
          </div>
        ))}
      </nav>
      <section className="side-detail" id={panel} role="tabpanel" aria-labelledby={`tab-${current}`}>{children}</section>
    </div>
  );
}
