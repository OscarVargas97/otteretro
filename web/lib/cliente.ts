// Conexión con la API local. La ventana abre la app con ?clave=… (y en desarrollo ?api=…);
// se guardan para la sesión porque la navegación interna ya no los lleva en la URL.

function leer(nombre: string, parametro: string): string {
  if (typeof window === "undefined") return "";
  const enUrl = new URLSearchParams(window.location.search).get(parametro);
  if (enUrl) sessionStorage.setItem(nombre, enUrl);
  return sessionStorage.getItem(nombre) ?? "";
}

const clave = () => leer("otteretro.clave", "clave");
const baseApi = () => leer("otteretro.api", "api");

export async function pedir<T>(ruta: string, init?: RequestInit): Promise<T> {
  const respuesta = await fetch(baseApi() + ruta, {
    ...init,
    headers: { "Content-Type": "application/json", "X-Otteretro-Clave": clave(), ...init?.headers },
  });
  if (!respuesta.ok) {
    const problema = await respuesta.json().catch(() => null);
    throw new Error(problema?.mensaje ?? `La app respondió con un error (${respuesta.status}).`);
  }
  return respuesta.json() as Promise<T>;
}

/** EventSource no admite cabeceras, así que la clave va en la URL. */
export const urlEventos = () => `${baseApi()}/api/eventos?clave=${encodeURIComponent(clave())}`;
