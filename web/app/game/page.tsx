"use client";

import { ComingSoon } from "@/components/Shell";

// La ficha de un juego se abre como /game/?id=…
export default function Game() {
  return <ComingSoon screen="game" phase={1} />;
}
