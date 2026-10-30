"use client";

import { useMutation } from "@tanstack/react-query";
import { useTranslations } from "next-intl";
import { useRouter } from "next/navigation";
import { useCallback, useEffect, useRef, useState } from "react";
import { request } from "@/lib/client";
import { nearest, type Direction } from "@/lib/spatial";

// Mando (pantallas.md §0.7, F1.1): la misma interfaz, con foco espacial, barra de botones, menú rápido (Guía)
// y teclado en pantalla. Con mando, <html data-input="pad">; al mover el ratón, "mouse".

const FOCUSABLE = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

/** Botones del mapeo estándar de la Gamepad API, por posición. */
const BUTTON = { south: 0, east: 1, west: 2, north: 3, menu: 9, up: 12, down: 13, left: 14, right: 15, guide: 16 } as const;
type Action = keyof typeof BUTTON;

type Family = "xbox" | "playstation" | "switch";
/** El glifo de cada posición según el mando conectado (la función va por posición: abajo elige, derecha vuelve). */
const GLYPHS: Record<Family, Record<"south" | "east" | "west" | "north", string>> = {
  xbox: { south: "A", east: "B", west: "X", north: "Y" },
  playstation: { south: "✕", east: "○", west: "□", north: "△" },
  switch: { south: "B", east: "A", west: "Y", north: "X" },
};

function familyOf(id: string): Family {
  const s = id.toLowerCase();
  // Xbox primero: «Xbox Wireless Controller» también dice «wireless controller», como el de PlayStation.
  if (/045e|xbox|xinput/.test(s)) return "xbox";
  if (/054c|playstation|dualsense|dualshock|wireless controller/.test(s)) return "playstation";
  if (/057e|nintendo|pro controller|joy-con/.test(s)) return "switch";
  return "xbox";
}

const isTextField = (e: Element | null): e is HTMLInputElement | HTMLTextAreaElement =>
  e instanceof HTMLTextAreaElement || (e instanceof HTMLInputElement && ["text", "search", "password", "email", "url", ""].includes(e.type));

const visible = (e: HTMLElement) => {
  const r = e.getBoundingClientRect();
  return r.width > 0 && r.height > 0 && getComputedStyle(e).visibility !== "hidden";
};

/** Lo que se recorre: el último diálogo abierto, o la pantalla entera. */
function scope(): ParentNode {
  const dialogs = document.querySelectorAll('[role="dialog"]');
  return dialogs[dialogs.length - 1] ?? document;
}

/** Marca el foco con .is-focus (el anillo grande del mando) y lo trae a la vista. */
function focus(e: HTMLElement) {
  document.querySelectorAll(".is-focus").forEach((x) => x.classList.remove("is-focus"));
  e.focus({ preventScroll: true });
  e.classList.add("is-focus");
  e.scrollIntoView({ block: "nearest", inline: "nearest" });
}

function move(dir: Direction) {
  const items = [...scope().querySelectorAll<HTMLElement>(FOCUSABLE)].filter(visible);
  const current = document.activeElement as HTMLElement | null;
  if (!current || !items.includes(current)) {
    if (items[0]) focus(items[0]);
    return;
  }
  const others = items.filter((e) => e !== current);
  const i = nearest(current.getBoundingClientRect(), others.map((e) => e.getBoundingClientRect()), dir);
  if (i >= 0) focus(others[i]);
}

/** Cambia el valor de un campo de React desde fuera (el teclado en pantalla). */
function setFieldValue(field: HTMLInputElement | HTMLTextAreaElement, value: string) {
  const proto = field instanceof HTMLTextAreaElement ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
  Object.getOwnPropertyDescriptor(proto, "value")!.set!.call(field, value);
  field.dispatchEvent(new Event("input", { bubbles: true }));
}

const pressKey = (key: string, ctrlKey = false) => window.dispatchEvent(new KeyboardEvent("keydown", { key, ctrlKey, bubbles: true }));

/** Lee el mando en cada cuadro y avisa cada pulsación (las direcciones se repiten si se mantienen). */
function usePad(onPress: (a: Action) => void, onFamily: (f: Family) => void) {
  const press = useRef(onPress);
  const family = useRef(onFamily);
  useEffect(() => {
    press.current = onPress;
    family.current = onFamily;
  });
  useEffect(() => {
    const held = new Map<Action, number>();
    let primed = false; // lo que ya estaba apretado al aparecer (el A que abrió el teclado) no cuenta hasta soltarlo
    let frame = 0;
    const tick = (now: number) => {
      const pad = [...(navigator.getGamepads?.() ?? [])].find((p) => p?.connected);
      if (pad) {
        family.current(familyOf(pad.id));
        const [x = 0, y = 0] = pad.axes;
        const down = (a: Action) =>
          !!pad.buttons[BUTTON[a]]?.pressed ||
          (a === "left" && x < -0.5) || (a === "right" && x > 0.5) || (a === "up" && y < -0.5) || (a === "down" && y > 0.5);
        for (const a of Object.keys(BUTTON) as Action[]) {
          if (!down(a)) {
            held.delete(a);
            continue;
          }
          const since = held.get(a);
          if (!primed) {
            held.set(a, Infinity);
            continue;
          }
          const repeats = a === "up" || a === "down" || a === "left" || a === "right";
          if (since === undefined) {
            held.set(a, now + 400);
            press.current(a);
          } else if (repeats && now >= since) {
            held.set(a, now + 120);
            press.current(a);
          }
        }
        primed = true;
      }
      frame = requestAnimationFrame(tick);
    };
    frame = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(frame);
  }, []);
}

export function Gamepad() {
  const t = useTranslations("pad");
  const router = useRouter();
  const [pad, setPad] = useState(false);
  const [family, setFamily] = useState<Family>("xbox");
  const [quick, setQuick] = useState(false);
  const [osk, setOsk] = useState<HTMLInputElement | HTMLTextAreaElement | null>(null);
  const goToSteam = useMutation({ mutationFn: () => request("/api/steam/big-picture", { method: "POST" }) });

  useEffect(() => {
    document.documentElement.dataset.input = pad ? "pad" : "mouse";
  }, [pad]);

  // El ratón devuelve la interfaz a su forma normal; con mando, el foco que llega por otros medios también se marca.
  useEffect(() => {
    const toMouse = () => {
      setPad(false);
      document.querySelectorAll(".is-focus").forEach((x) => x.classList.remove("is-focus"));
    };
    const onFocus = (e: FocusEvent) => {
      if (document.documentElement.dataset.input !== "pad" || !(e.target instanceof HTMLElement)) return;
      document.querySelectorAll(".is-focus").forEach((x) => x.classList.remove("is-focus"));
      e.target.classList.add("is-focus");
    };
    window.addEventListener("mousemove", toMouse);
    window.addEventListener("mousedown", toMouse);
    document.addEventListener("focusin", onFocus);
    return () => {
      window.removeEventListener("mousemove", toMouse);
      window.removeEventListener("mousedown", toMouse);
      document.removeEventListener("focusin", onFocus);
    };
  }, []);

  const onPress = useCallback(
    (a: Action) => {
      setPad(true);
      if (a === "guide") return setQuick((q) => !q);
      if (a === "up" || a === "down" || a === "left" || a === "right") return move(a);
      if (osk) return; // el teclado en pantalla atiende sus propios botones
      const active = document.activeElement;
      if (a === "south") {
        if (isTextField(active)) setOsk(active);
        else if (active instanceof HTMLSelectElement) {
          // Un <select> no se abre con un clic simulado: A pasa a la opción siguiente.
          active.selectedIndex = (active.selectedIndex + 1) % active.options.length;
          active.dispatchEvent(new Event("change", { bubbles: true }));
        }
        else if (active instanceof HTMLElement && active !== document.body) active.click();
        else move("down");
      } else if (a === "east") {
        if (quick) setQuick(false);
        else if (document.querySelector('[role="dialog"]')) pressKey("Escape");
        else router.back();
      } else if (a === "west") pressKey("k", true);
    },
    [osk, quick, router],
  );
  usePad(onPress, setFamily);

  const glyph = GLYPHS[family];
  const dialog = pad && typeof document !== "undefined" && !!document.querySelector('[role="dialog"]');
  return (
    <>
      {pad && !osk && !quick && (
        <div className="ot-padbar pad-bar" aria-hidden="true">
          <Hint glyph={glyph.south} label={t("choose")} />
          <Hint glyph={glyph.east} label={dialog ? t("close") : t("back")} />
          <Hint glyph={glyph.west} label={t("search")} />
        </div>
      )}
      {quick && (
        <div className="ot-scrim pad-layer" onClick={() => setQuick(false)}>
          <div className="ot-quick" role="dialog" aria-modal="true" aria-label={t("quickMenu")} onClick={(e) => e.stopPropagation()}>
            <div className="ot-quick-head">
              <img src="/nutria.png" alt="" />
              <div className="ot-quick-now"><strong>Otteretro</strong><span>{t("noGame")}</span></div>
            </div>
            <button className="ot-quick-item" type="button" autoFocus onClick={() => { goToSteam.mutate(); setQuick(false); }}>
              {t("goToSteam")}
            </button>
            <div className="ot-quick-foot">
              <span><Glyph text={glyph.south} />{t("choose")}</span>
              <span><Glyph text={glyph.east} />{t("close")}</span>
            </div>
          </div>
        </div>
      )}
      {osk && <Keyboard field={osk} glyph={glyph} onClose={() => { setOsk(null); focus(osk); }} />}
    </>
  );
}

const Glyph = ({ text, pill }: { text: string; pill?: boolean }) => <span className={`ot-glyph${pill ? " ot-glyph-pill" : ""}`}>{text}</span>;
const Hint = ({ glyph, label }: { glyph: string; label: string }) => <span className="ot-padbar-hint"><Glyph text={glyph} />{label}</span>;

const ROWS = ["qwertyuiop", "asdfghjklñ", "zxcvbnm,.-"];

/** Teclado en pantalla de Otteretro (fuera de la sesión de Steam): B cerrar, X borrar, Y espacio, Menú listo. */
function Keyboard({ field, glyph, onClose }: { field: HTMLInputElement | HTMLTextAreaElement; glyph: Record<string, string>; onClose: () => void }) {
  const t = useTranslations("pad");
  const [text, setText] = useState(field.value);
  const [caps, setCaps] = useState(false);
  const first = useRef<HTMLButtonElement>(null);
  const type = useCallback((value: string) => {
    setText(value);
    setFieldValue(field, value);
  }, [field]);
  useEffect(() => {
    if (first.current) focus(first.current);
  }, []);
  const done = useCallback(() => {
    onClose();
    field.form?.requestSubmit();
  }, [field, onClose]);

  usePad((a) => {
    if (a === "east") onClose();
    else if (a === "west") type(text.slice(0, -1));
    else if (a === "north") type(text + " ");
    else if (a === "menu") done();
    else if (a === "south" && document.activeElement instanceof HTMLElement) document.activeElement.click();
  }, () => {});

  return (
    <div className="ot-scrim pad-layer pad-osk" onClick={onClose}>
      <div className="ot-osk" role="dialog" aria-modal="true" aria-label={t("keyboard")} onClick={(e) => e.stopPropagation()}>
        <div className="ot-osk-field" data-placeholder={field.placeholder}>{text}</div>
        <div className="ot-osk-keys">
          {ROWS.flatMap((row, r) => [...row].map((k, i) => {
            const key = caps ? k.toUpperCase() : k;
            return <button key={k} ref={r === 0 && i === 0 ? first : undefined} className="ot-key" type="button" onClick={() => type(text + key)}>{key}</button>;
          }))}
          <button className={`ot-key ot-key-wide${caps ? " is-on" : ""}`} type="button" aria-pressed={caps} onClick={() => setCaps(!caps)}>{t("shift")}</button>
          <button className="ot-key ot-key-space" type="button" onClick={() => type(text + " ")}>{t("space")}</button>
          <button className="ot-key ot-key-wide" type="button" onClick={() => type(text.slice(0, -1))}>{t("delete")}</button>
          <button className="ot-key ot-key-done" type="button" onClick={done}>{t("done")}</button>
        </div>
        <div className="ot-osk-foot">
          <span><Glyph text={glyph.east} />{t("close")}</span>
          <span><Glyph text={glyph.west} />{t("delete")}</span>
          <span><Glyph text={glyph.north} />{t("space")}</span>
          <span><Glyph text={t("menu")} pill />{t("done")}</span>
        </div>
      </div>
    </div>
  );
}
