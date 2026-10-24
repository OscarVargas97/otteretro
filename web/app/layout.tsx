import type { Metadata } from "next";
import type { ReactNode } from "react";
import { Atkinson_Hyperlegible, Bricolage_Grotesque, Silkscreen } from "next/font/google";
import { Marco } from "@/componentes/Marco";
import "./estilos/tokens.css";
import "./estilos/componentes.css";
import "./estilos/app.css";

// Las fuentes se incluyen en la app al compilar (funcionan sin internet) y definen los tokens del sistema de diseño.
const display = Bricolage_Grotesque({ subsets: ["latin"], weight: ["600", "700"], variable: "--font-display" });
const sans = Atkinson_Hyperlegible({ subsets: ["latin"], weight: ["400", "700"], variable: "--font-sans" });
const pixel = Silkscreen({ subsets: ["latin"], weight: "400", variable: "--font-pixel" });

export const metadata: Metadata = { title: "Otteretro" };

export default function Layout({ children }: { children: ReactNode }) {
  return (
    <html lang="es" className={`${display.variable} ${sans.variable} ${pixel.variable}`}>
      <body>
        <Marco>{children}</Marco>
      </body>
    </html>
  );
}
