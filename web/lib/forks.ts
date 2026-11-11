/** Ids de las tarjetas que no salen sueltas porque van dentro de la de su original (#358): un fork solo se anida si su original está en la lista. */
export function nestedForkIds(cards: { id: string }[], forkOf: (id: string) => string | null | undefined): Set<string> {
  const ids = new Set(cards.map((c) => c.id));
  return new Set(cards.filter((c) => { const o = forkOf(c.id); return !!o && o !== c.id && ids.has(o); }).map((c) => c.id));
}
