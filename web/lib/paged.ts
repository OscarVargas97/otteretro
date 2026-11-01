import { keepPreviousData, useInfiniteQuery } from "@tanstack/react-query";
import { useEffect, useRef } from "react";
import { request } from "@/lib/client";

/**
 * Lista larga por tramos (#177): pide el primer tramo, y el siguiente cuando el marcador `end` (un div al final de la lista)
 * queda a menos de 2.500 px de la vista. `pick` saca de cada respuesta sus elementos y el total.
 */
export function usePaged<P, T>(key: unknown[], url: (offset: number) => string, pick: (page: P) => { items: T[]; total: number }, enabled = true) {
  const query = useInfiniteQuery({
    queryKey: key,
    enabled,
    queryFn: ({ pageParam }) => request<P>(url(pageParam)),
    initialPageParam: 0,
    getNextPageParam: (last, all) => {
      const loaded = all.reduce((n, p) => n + pick(p).items.length, 0);
      return loaded < pick(last).total ? loaded : undefined;
    },
    placeholderData: keepPreviousData,
  });
  const items = query.data?.pages.flatMap((p) => pick(p).items) ?? [];
  const total = query.data ? pick(query.data.pages[0]).total : 0;
  const end = useRef<HTMLDivElement>(null);
  const { hasNextPage, isFetchingNextPage, fetchNextPage } = query;
  useEffect(() => {
    const node = end.current;
    if (!node || !hasNextPage || isFetchingNextPage) return;
    const watch = new IntersectionObserver(([e]) => { if (e.isIntersecting) fetchNextPage(); }, { rootMargin: "0px 0px 2500px 0px" });
    watch.observe(node);
    return () => watch.disconnect();
  }, [hasNextPage, isFetchingNextPage, fetchNextPage, items.length]);
  return { ...query, items, total, end };
}
