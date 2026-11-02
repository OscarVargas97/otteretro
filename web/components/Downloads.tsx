"use client";

import { useFormatter } from "next-intl";
import { librariesQuery } from "@/components/Libraries";
import type { DiskInfo } from "@/lib/types";

export { librariesQuery };

/** Un disco para elegir destino: su nombre y los GB libres (§9.2). */
export function useLibraryHint() {
  const format = useFormatter();
  return (d: DiskInfo) => `${d.name}${d.default ? " ★" : ""}${d.free != null ? ` — ${format.number(Number(d.free) / 1073741824, { maximumFractionDigits: 0 })} GB` : ""}`;
}
