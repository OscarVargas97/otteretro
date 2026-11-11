// Recorre la app como lo haría la persona, en español y en inglés (#140 a #142).
// Las pruebas van en orden y comparten la misma pestaña: cada una sigue donde quedó la anterior.
import { expect, test, type Page } from "@playwright/test";
import { join } from "node:path";
import { mkdirSync, writeFileSync } from "node:fs";
import zlib from "node:zlib";
import en from "../messages/en.json";
import es from "../messages/es.json";

const url = () => process.env.OTTERETRO_E2E_URL!;
const base = () => url().split("/?")[0];
const dataDir = (name: string) => join(process.env.OTTERETRO_E2E_DIR!, name);

test.describe.configure({ mode: "serial" });

let page: Page;
const problems: string[] = [];

test.beforeAll(async ({ browser }) => {
  page = await browser.newPage();
  // Un texto que falta en el catálogo o un error de React aparece en la consola: cualquiera hace fallar la prueba.
  page.on("console", (m) => m.type() === "error" && !m.text().startsWith("Failed to load resource") && problems.push(m.text()));
  page.on("pageerror", (e) => problems.push(e.message));
  // Las respuestas con error se anotan con su dirección (la consola solo dice "Failed to load resource").
  // Las de /api/ con 4xx son errores esperados de las pruebas (por ejemplo, una ruta incompleta).
  page.on("response", (r) => {
    const path = new URL(r.url()).pathname;
    if (r.status() >= 400 && !(path.startsWith("/api/") && r.status() < 500)) problems.push(`${r.status()} ${path}`);
  });
});

test.afterEach(() => {
  expect(problems, "errores en la consola").toEqual([]);
});

/** Ninguna clave sin traducir a la vista (next-intl muestra la ruta de la clave, por ejemplo "nav.home"). */
async function expectNoRawKeys() {
  const text = await page.locator("body").innerText();
  expect(text).not.toMatch(/\b(nav|topBar|comingSoon|welcome|settings|dataFolder|update|errors|move|credentials|catalog|notices|review|home|libraries|tools)\.[a-zA-Z]+/);
}

/** El buscador mide 320 px y su texto de ejemplo cabe entero (#20): se acorta, no se corta. */
async function expectSearchFits() {
  const search = page.locator(".search");
  expect((await search.boundingBox())?.width).toBe(320);
  const text = search.locator(".search-text");
  expect(await text.evaluate((e) => e.scrollWidth <= e.clientWidth), "el texto del buscador no cabe").toBe(true);
}

/** Ningún texto se sale de su botón, enlace o título (#20). Los cortes con «…» son a propósito y no cuentan. */
async function expectTextFits() {
  const overflowing = await page.locator(":is(button, a, h1, h2, h3, .ot-badge):not(.sr-only)").evaluateAll((els) =>
    els
      .filter((e) => {
        const s = getComputedStyle(e);
        return s.display !== "inline" && s.textOverflow !== "ellipsis" && (e.scrollWidth > e.clientWidth + 1 || e.scrollHeight > e.clientHeight + 1);
      })
      .map((e) => e.textContent?.trim()),
  );
  expect(overflowing, "textos que no caben").toEqual([]);
}

/** Un PNG de un solo color, para probar las imágenes sin depender de SteamGridDB. */
function solidPng(width: number, height: number, [r, g, b]: [number, number, number]) {
  const chunk = (type: string, data: Buffer) => {
    const body = Buffer.concat([Buffer.from(type), data]);
    const out = Buffer.alloc(body.length + 8);
    out.writeUInt32BE(data.length, 0);
    body.copy(out, 4);
    out.writeUInt32BE(zlib.crc32(body), body.length + 4);
    return out;
  };
  const header = Buffer.alloc(13);
  header.writeUInt32BE(width, 0); header.writeUInt32BE(height, 4); header[8] = 8; header[9] = 2;
  const row = Buffer.concat([Buffer.from([0]), Buffer.from(Array.from({ length: width }, () => [r, g, b]).flat())]);
  return Buffer.concat([Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]), chunk("IHDR", header),
    chunk("IDAT", zlib.deflateSync(Buffer.concat(Array.from({ length: height }, () => row)))), chunk("IEND", Buffer.alloc(0))]);
}

/** La base de la app con el catálogo adjunto, como la abre la app (#92): las tablas se nombran sin prefijo. */
async function openDb() {
  const { DatabaseSync } = await import("node:sqlite");
  const db = new DatabaseSync(join(dataDir("moved"), "data", "otteretro.db"));
  db.exec(`ATTACH DATABASE '${join(dataDir("moved"), "catalogo", "catalogo.db").replaceAll("'", "''")}' AS catalog`);
  return db;
}

test("sin carpeta de datos, abre el asistente en español", async () => {
  await page.goto(url());
  await expect(page).toHaveURL(/\/welcome\//);
  await expect(page.getByRole("button", { name: es.welcome.start })).toBeVisible();
  await expect(page.getByText(es.welcome.steps.dataFolder)).toBeVisible();
  await expectNoRawKeys();
});

test("cambiar a inglés en el asistente cambia la interfaz al momento", async () => {
  await page.getByRole("button", { name: en.languageName }).click();
  await expect(page.getByRole("button", { name: en.welcome.start })).toBeVisible();
  await expect(page.locator("html")).toHaveAttribute("lang", "en");
  await expectNoRawKeys();
  await page.screenshot({ path: "e2e-results/1-welcome-en.png" });
});

test("una ruta incompleta muestra el error del backend traducido", async () => {
  await page.getByRole("button", { name: en.welcome.start }).click();
  await expect(page.getByRole("heading", { name: en.welcome.chooseTitle })).toBeVisible();
  await page.getByLabel(en.welcome.otherPath).fill("relative\\folder");
  await page.getByRole("button", { name: /^Use / }).click();
  await expect(page.getByText(en.errors.path.notAbsolute)).toBeVisible();
  await page.screenshot({ path: "e2e-results/2-error-en.png" });
});

test("elegir la carpeta de datos lleva al paso de bibliotecas y, al saltarlo, al inicio", async () => {
  await page.getByLabel(en.welcome.otherPath).fill(dataDir("data"));
  await page.getByRole("button", { name: /^Use / }).click();
  // Paso 4 (F3): adoptar la estructura antigua, crear una biblioteca o saltar.
  await expect(page.getByRole("heading", { name: en.welcome.librariesTitle })).toBeVisible();
  await expectNoRawKeys();
  await page.getByRole("button", { name: en.welcome.skip }).click();
  await expect(page.getByRole("heading", { name: en.welcome.accountsTitle })).toBeVisible();
  await page.getByRole("button", { name: en.welcome.done }).click();
  await expect(page).toHaveURL(`${base()}/`);
  await expect(page.getByRole("heading", { level: 1, name: en.nav.home })).toBeVisible();
  await expect(page.getByRole("heading", { level: 2, name: en.home.next })).toBeVisible();
  // #169: un solo aviso con todos los servicios sin clave y el botón a Cuentas y claves.
  const notice = page.getByRole("note", { name: en.notices.title });
  await expect(notice).toHaveCount(1);
  await expect(notice).toContainText(en.credentials.service.steam);
  await expect(notice.getByRole("link", { name: en.notices.openCredentials })).toHaveAttribute("href", "/settings/?s=credentials");
  await expectNoRawKeys();
  await expectSearchFits();
  await expectTextFits();
  await page.screenshot({ path: "e2e-results/3-home-en.png" });
});

test("todas las pantallas se ven en inglés", async () => {
  // Las del catálogo, sin nada cargado, dicen cómo cargarlo; las demás, en qué fase llegan.
  const screens = [
    ["sagas", en.catalog.sagas.emptyTitle],
    ["collection", en.catalog.collection.emptyTitle],
    ["game", en.catalog.game.noneTitle],
    ["saga", en.catalog.saga.noneTitle],
    ["review", en.review.files.doubtfulEmptyTitle],
    ["tools", en.tools.emptyTitle],
    ...(["discover", "steam"] as const).map((s) => [s, en.comingSoon[s].title] as const),
  ] as const;
  // #169: las de fases futuras se ven atenuadas con su fase y no se abren desde el menú.
  await page.goto(`${base()}/`);
  for (const s of ["discover", "steam"] as const) {
    await expect(page.locator(".sidebar").getByRole("link", { name: en.nav[s] })).toHaveCount(0);
    await expect(page.locator(".sidebar .is-future", { hasText: en.nav[s] })).toHaveAttribute("aria-disabled", "true");
  }
  for (const [screen, title] of screens) {
    await page.goto(`${base()}/${screen}/`);
    await expect(page.getByRole("heading", { level: 1, name: en.nav[screen] })).toBeVisible();
    await expect(page.getByText(title)).toBeVisible();
    await expectNoRawKeys();
    await expectTextFits();
  }
});

/** Llama a la API desde la página, que tiene la clave de la sesión. */
const api = (method: string, path: string, body?: unknown) => page.evaluate(async ([method, path, body]) => {
  const r = await fetch(path as string, { method: method as string, body: body ? JSON.stringify(body) : undefined,
    headers: { "Content-Type": "application/json", "X-Otteretro-Key": sessionStorage.getItem("otteretro.key") ?? "" } });
  return { ok: r.ok, text: await r.text() };
}, [method, path, body] as const);

/** Agrega un disco por la API, con su carpeta Otteretro dentro de la carpeta de la prueba (el botón ofrece los discos reales). */
async function addDisk(name: string) {
  const path = join(dataDir(name), "Otteretro");
  const r = await api("POST", "/api/disks", { path, name });
  expect(r.ok, r.text).toBe(true);
  return path;
}

async function removeDisks() {
  for (const l of JSON.parse((await api("GET", "/api/locations")).text) as { id: string }[]) await api("DELETE", `/api/locations/${l.id}`);
}

test("Ajustes › Almacenamiento como Steam: discos, uso por tipo, mover lo marcado, ★ y quitar sin borrar (#161, #205)", async () => {
  const [a, b] = [await addDisk("Disco A"), await addDisk("Disco B")];
  const { existsSync } = await import("node:fs");
  // Cada disco trae sus cinco carpetas.
  for (const f of ["Juegos", "Emuladores", "Ports", "Complementos", "Sistema"]) expect(existsSync(join(a, f))).toBe(true);
  mkdirSync(join(a, "Juegos", "Switch", "Zelda"), { recursive: true });
  writeFileSync(join(a, "Juegos", "Switch", "Zelda", "juego.nsp"), "contenido");
  mkdirSync(join(a, "Juegos", "GBA"), { recursive: true });
  for (const rom of ["metroid.gba", "pokemon.gba"]) writeFileSync(join(a, "Juegos", "GBA", rom), "rom");
  await page.goto(`${base()}/settings/?s=storage`);
  // #208: el disco ★ es el de la carpeta de datos, se registró solo y se abre primero con sus datos.
  const picker = page.getByLabel(en.libraries.disk, { exact: true });
  await expect(picker.locator("option:checked")).toContainText("★");
  // #169: como Steam, los datos de la app son una fila más de la tabla.
  await expect(page.getByRole("checkbox", { name: en.libraries.appData })).toBeVisible();
  await expect(page.locator(".usage-legend").getByText(en.libraries.dataUse, { exact: false })).toBeVisible();
  await expectNoRawKeys();
  await expectTextFits();
  await page.screenshot({ path: "e2e-results/7-storage-en.png", fullPage: true });
  const pick = async (name: string) => picker.selectOption((await picker.locator("option", { hasText: name }).getAttribute("value"))!);
  await pick("Disco A");
  await expect(page.getByRole("heading", { level: 2, name: "Disco A" })).toBeVisible();
  await expect(page.getByText(en.libraries.otherUse, { exact: false })).toBeVisible();
  // #171: grupos desplegables, tipo › consola › cada cosa. Zelda suelto y toda GBA (una sola tarea), a la misma ruta.
  await page.locator(".disk-table").getByRole("button", { name: en.libraries.type.game.title }).click();
  await page.locator(".disk-table").getByRole("button", { name: "Switch" }).click();
  await page.getByRole("checkbox", { name: "Zelda" }).check();
  await page.getByRole("checkbox", { name: "GBA" }).check();
  await expect(page.getByRole("checkbox", { name: en.libraries.type.game.title })).toHaveAttribute("aria-checked", "true");
  await page.screenshot({ path: "e2e-results/7d-storage-tree-en.png" });
  await page.locator('select[id^="to-"]').selectOption({ label: "Disco B" });
  await page.locator(".panel").getByRole("button", { name: en.libraries.move, exact: true }).click();
  await expect(page.getByText(en.libraries.queued)).toBeVisible();
  await expect.poll(() => existsSync(join(b, "Juegos", "Switch", "Zelda", "juego.nsp")), { timeout: 15_000 }).toBe(true);
  await expect.poll(() => existsSync(join(b, "Juegos", "GBA", "pokemon.gba")), { timeout: 15_000 }).toBe(true);
  expect(existsSync(join(a, "Juegos", "GBA"))).toBe(false);
  // Quitar avisa que no borra nada; el marcador sigue en la carpeta.
  await pick("Disco B");
  await page.locator(".ot-menu summary").click();
  await page.screenshot({ path: "e2e-results/7c-disk-menu-en.png" });
  await page.getByRole("menuitem", { name: en.libraries.removeDisk }).click();
  const remove = page.getByRole("dialog", { name: en.libraries.removeDisk });
  await expect(remove.getByText(en.libraries.removeHelp.replace("{name}", "Disco B"))).toBeVisible();
  await remove.getByRole("button", { name: en.libraries.removeConfirm }).click();
  await expect(picker.locator("option", { hasText: "Disco B" })).toHaveCount(0);
  expect(existsSync(join(b, "otteretro-ubicacion.json"))).toBe(true);
  // «+» abre la ventanita con las unidades (sin tocarlas).
  await page.getByRole("button", { name: en.libraries.addDisk }).click();
  const add = page.getByRole("dialog", { name: en.libraries.addDisk });
  await expect(add.getByText(en.libraries.addDiskHelp)).toBeVisible();
  await page.screenshot({ path: "e2e-results/7b-add-disk-en.png" });
  await expectTextFits();
  await add.getByRole("button", { name: en.libraries.cancel }).click();
  await expect(add).toBeHidden();
  await page.getByRole("button", { name: en.topBar.tasks }).click();
  await page.getByRole("dialog", { name: en.topBar.tasksPanel }).getByRole("button", { name: en.tasks.clear }).click();
  await page.keyboard.press("Escape");
  await removeDisks();
});

test("Ajustes › Consolas: el detalle de una consola, su destino y mover sus juegos (#192, #195)", async () => {
  const [a, b] = [await addDisk("Disco C"), await addDisk("Disco D")];
  mkdirSync(join(a, "Juegos", "Switch", "Zelda"), { recursive: true });
  writeFileSync(join(a, "Juegos", "Switch", "Zelda", "juego.nsp"), "contenido");
  // Lista a la izquierda; al elegir Switch, su detalle dice dónde están sus juegos y qué le falta.
  await page.goto(`${base()}/settings/?s=consoles`);
  await expect(page.getByText(en.settings.consoles.detail.pick)).toBeVisible();
  await page.getByRole("button", { name: en.settings.consoles.detail.open.replace("{name}", "Switch"), exact: true }).click();
  await expect(page).toHaveURL(/c=switch/);
  const detail = page.locator(".console-detail");
  await expect(detail.getByText(en.settings.consoles.detail.noEmulators)).toBeVisible();
  await expect(detail.getByText(en.settings.consoles.detail.noSystem)).toBeVisible();
  await expectNoRawKeys();
  await expectTextFits();
  // El destino de los juegos nuevos se guarda.
  await detail.getByLabel(en.settings.consoles.detail.destination).selectOption({ label: "Disco D" });
  await page.reload();
  await expect.poll(() => page.locator(".console-detail").getByLabel(en.settings.consoles.detail.destination)
    .evaluate((s) => (s as HTMLSelectElement).selectedOptions[0].text)).toBe("Disco D");
  // #190: mover los juegos de la consola a otro disco.
  const loc = page.locator(".console-location", { hasText: en.settings.consoles.location.in.replace("{library}", "Disco C") });
  await loc.getByRole("button", { name: en.settings.consoles.location.move }).click();
  await loc.getByLabel(en.settings.consoles.location.to, { exact: true }).selectOption({ label: "Disco D" });
  await loc.getByRole("button", { name: en.settings.consoles.location.confirm, exact: true }).click();
  await expect(loc.getByText(en.settings.consoles.location.queued)).toBeVisible();
  const { existsSync } = await import("node:fs");
  await expect.poll(() => existsSync(join(b, "Juegos", "Switch", "Zelda", "juego.nsp")), { timeout: 15_000 }).toBe(true);
  await expect.poll(() => existsSync(join(a, "Juegos", "Switch")), { timeout: 15_000 }).toBe(false);
  // Se limpian la tarea y los discos para no dejar estado a las demás pruebas (quitar no borra archivos).
  await page.getByRole("button", { name: en.topBar.tasks }).click();
  // Al terminar una mudanza se escanea solo (#212): se limpia cuando también terminó eso.
  await expect(async () => {
    await page.getByRole("dialog", { name: en.topBar.tasksPanel }).getByRole("button", { name: en.tasks.clear }).click({ timeout: 1000 });
    await expect(page.getByText(en.topBar.noTasksTitle)).toBeVisible({ timeout: 1000 });
  }).toPass({ timeout: 15_000 });
  await page.keyboard.press("Escape");
  await removeDisks();
});

test("Descargas: el capturador guarda lo pegado, sobrevive a cambiar de pantalla y dice lo que respondió el sitio (#203, #204, #210)", async () => {
  await page.goto(`${base()}/downloads/?tab=links`);
  await expect(page.getByRole("tab", { name: en.downloads.tabLinks })).toHaveAttribute("aria-selected", "true");
  await expect(page.getByText(en.downloads.captureEmpty)).toBeVisible();
  // Se guarda sin consultar el sitio, y sigue ahí después de irse y volver.
  await page.getByLabel(en.downloads.links).fill("https://1fichier.com/?abc123&af=9 https://sitio.test/juego.part1.rar https://sitio.test/juego.part2.rar");
  await page.getByRole("button", { name: en.downloads.addLinks, exact: true }).click();
  await expect(page.getByRole("checkbox", { name: /^abc123/ })).toBeVisible();
  await expect(page.getByText(en.downloads.state.unchecked)).toHaveCount(3);
  await page.goto(`${base()}/tools/`);
  await page.goto(`${base()}/downloads/`);
  await expect(page.getByRole("tab", { name: en.downloads.tabLinksCount.replace("{count}", "3") })).toBeVisible();
  await page.getByRole("tab", { name: /Link grabber/ }).click();
  await expect(page.getByRole("checkbox", { name: /^abc123/ })).toBeVisible();
  await expect(page.getByRole("checkbox", { name: /^juego/ })).toBeVisible();
  await expect(page.getByText("juego.part2.rar")).toBeVisible();
  // Lo que respondió el sitio, no un «falta tu cuenta» cualquiera.
  await page.route("**/api/downloads/links/check", (route) => route.fulfill({ json: [
    { name: "abc123", size: 0, links: [{ id: 1, url: "https://1fichier.com/?abc123", name: "abc123", size: null, site: "1fichier", state: "blocked", detail: { code: "download.siteSaid", params: { site: "1fichier", message: "Resource not found #644" } }, gameId: null, status: "captured" }] },
  ] }));
  await page.getByRole("button", { name: en.downloads.check, exact: true }).click();
  await expect(page.getByText("Resource not found #644")).toBeVisible();
  await page.unroute("**/api/downloads/links/check");
  await expectNoRawKeys();
  // Cada descarga se despliega en sus enlaces, y dice cómo va cada uno.
  await page.route("**/api/downloads", (route) => route.fulfill({ json: [{ id: 999, name: "DKB ROM", disk: "D:", links: 3, done: 1, status: "paused", progress: 40, detail: null, createdAt: "2026-11-01T00:00:00Z",
    items: [{ url: "https://1fichier.com/?parte1", name: "DKB ROM.part1.rar", size: 5242880, check: "crc32" }, { url: "https://1fichier.com/?parte2", name: "DKB ROM.part2.rar", size: null, check: null }, { url: "https://1fichier.com/?parte3", name: "DKB ROM.part3.rar", size: null, check: null }] }] }));
  await page.goto(`${base()}/downloads/?tab=queue`);
  await expect(page.getByText("DKB ROM.part1.rar")).toBeHidden();
  await page.getByText("Show 3 links").click();
  const items = page.locator(".download-item");
  await expect(items).toHaveCount(3);
  await expect(items.nth(0).getByText(en.downloads.status.done)).toBeVisible();
  await expect(items.nth(1).getByText(en.downloads.status.paused)).toBeVisible();
  await expect(items.nth(2).getByText(en.downloads.itemPending)).toBeVisible();
  await expect(items.nth(0).getByText(en.downloads.verified.crc32)).toBeVisible();
  // Cuántas a la vez por sitio: sin cuenta premium, 1fichier solo deja 1.
  await page.getByText(en.downloads.parallelTitle).click();
  await expect(page.getByText(en.downloads.parallelFree)).toBeVisible();
  const direct = page.getByLabel(new RegExp(en.downloads.site.direct));
  await direct.selectOption("3");
  await page.reload();
  await page.getByText(en.downloads.parallelTitle).click();
  await expect(page.getByLabel(new RegExp(en.downloads.site.direct))).toHaveValue("3");
  if (process.env.OTTERETRO_SHOT) await page.locator(".panel").first().screenshot({ path: process.env.OTTERETRO_SHOT });
  await page.unroute("**/api/downloads");
  // Quitar los deja fuera para siempre.
  await page.goto(`${base()}/downloads/?tab=links`);
  for (const name of ["abc123", "juego.part1.rar", "juego.part2.rar"])
    await page.getByRole("button", { name: en.downloads.removeLink.replace("{name}", name) }).click();
  await expect(page.getByText(en.downloads.captureEmpty)).toBeVisible();
});

test("Herramientas: pestañas por tipo y subir un archivo de sistema con versiones (#154, #199)", async () => {
  await page.goto(`${base()}/tools/`);
  for (const k of ["emulator", "port", "complement", "tool", "system"] as const) await expect(page.getByRole("tab", { name: en.tools.kind[k] })).toBeVisible();
  await expect(page.getByRole("tab", { name: en.tools.kind.emulator })).toHaveAttribute("aria-selected", "true");
  await page.getByRole("tab", { name: en.tools.kind.system }).click();
  await expect(page).toHaveURL(/k=system/);
  await expect(page.getByText(en.tools.systemHelp)).toBeVisible();
  await expectNoRawKeys();
  await expectTextFits();
  await page.getByLabel(en.tools.systemConsole).selectOption("switch");
  await page.getByLabel(en.tools.systemUpload).setInputFiles({ name: "prueba.bin", mimeType: "application/octet-stream", buffer: Buffer.from("archivo de prueba") });
  await expect(page.getByText(/prueba\.bin/)).toBeVisible();
  await expectNoRawKeys();
});

test("Herramientas: un emulador sin receta desde su repo lista sus releases y la instala sin escribir la versión (#350)", async () => {
  await page.route("**/api/repos/releases?*", (r) => r.fulfill({ json: [
    { version: "5", channel: "dev", publishedAt: "2026-11-09T02:26:31Z", url: null, assets: [
      { name: "Fritchii-v5-debug-symbols-not-needed-to-play.zip", url: "https://github.com/Frizti/Fritchii/releases/download/fritchii-v5/Fritchii-v5-debug-symbols-not-needed-to-play.zip", windows: false },
      { name: "Fritchii-v5-Windows-x64.zip", url: "https://github.com/Frizti/Fritchii/releases/download/fritchii-v5/Fritchii-v5-Windows-x64.zip", windows: true },
    ] },
  ] }));
  let sent: Record<string, unknown> | null = null;
  await page.route("**/api/emulators/install", (r) => { sent = r.request().postDataJSON(); return r.fulfill({ json: 0 }); });
  await page.goto(`${base()}/tools/`);
  await page.getByRole("button", { name: en.tools.install.open.emulator }).click();
  await page.getByLabel(en.tools.install.what).selectOption(":other");
  await page.getByLabel(en.tools.install.name, { exact: true }).fill("Mi Fritchii");
  await page.getByLabel(en.tools.install.console).selectOption("switch2");
  await page.getByLabel(en.tools.install.fromKind.url).check();
  await page.getByLabel(en.tools.install.url, { exact: true }).fill("github.com/Frizti/Fritchii");
  await page.getByRole("button", { name: en.tools.install.findReleases }).click();
  // La versión sale del tag y, con un solo archivo de Windows, no hay nada que elegir.
  await expect(page.getByLabel(en.tools.install.version, { exact: true })).toHaveValue("dev|5");
  await expect(page.getByText("Fritchii-v5-Windows-x64.zip")).toBeVisible();
  await expectNoRawKeys();
  await page.locator("section[aria-labelledby=install-title]").getByRole("button", { name: en.tools.install.submit, exact: true }).click();
  await expect.poll(() => sent).toMatchObject({ emulator: "mi-fritchii", name: "Mi Fritchii", version: "5", repo: "github.com/Frizti/Fritchii", console: "switch2",
    url: "https://github.com/Frizti/Fritchii/releases/download/fritchii-v5/Fritchii-v5-Windows-x64.zip" });
  await page.unroute("**/api/repos/releases?*");
  await page.unroute("**/api/emulators/install");
});

test("Herramientas: instalar Steam ROM Manager desde una carpeta, su librería y los avisos que instalan (#296)", async () => {
  const src = dataDir("srm-a-mano");
  mkdirSync(src, { recursive: true });
  writeFileSync(join(src, "Steam-ROM-Manager.exe"), "srm");
  await addDisk("Disco Copia");
  // La pestaña Herramientas (decisión 311): instalar desde una carpeta de este equipo.
  await page.goto(`${base()}/tools/?k=tool`);
  await expect(page.getByRole("tab", { name: en.tools.kind.tool })).toHaveAttribute("aria-selected", "true");
  await page.getByRole("button", { name: en.tools.install.open.tool }).click();
  await page.getByLabel(en.tools.install.what).selectOption("steam-rom-manager");
  await page.getByLabel(en.tools.install.fromKind.path).check();
  await page.getByLabel(en.tools.install.path, { exact: true }).fill(src);
  await page.getByLabel(en.tools.install.version, { exact: true }).fill("2.6.2");
  await expectNoRawKeys();
  await expectTextFits();
  await page.locator("section[aria-labelledby=install-title]").getByRole("button", { name: en.tools.install.submit, exact: true }).click();
  await expect(page.getByText(en.tools.install.queued)).toBeVisible();
  // Al terminar: una sola tarjeta con lo de disco y lo registrado (#348); su origen, en el plegable.
  const panel = page.locator("#tools-panel");
  const card = panel.getByRole("region", { name: "Steam ROM Manager" });
  await expect(card).toBeVisible({ timeout: 15_000 });
  await expect(card).toHaveCount(1);
  await card.getByText(en.tools.library.details).click();
  await expect(card.getByText(en.tools.library.origin.upload, { exact: false })).toBeVisible();
  // Una herramienta no se "usa" ni se borra desde aquí: solo se abre su carpeta.
  await expect(card.getByRole("button", { name: en.tools.library.open })).toBeVisible();
  await expect(card.getByRole("button", { name: en.tools.library.use })).toHaveCount(0);
  await page.screenshot({ path: "e2e-results/8-tools-install-en.png", fullPage: true });

  // Ya no hay segunda copia: ni la opción en Ajustes › Almacenamiento ni insignias de copias (decisión 335).
  await expect(page.getByText(/En dos discos|On two disks|Una sola copia|Only one copy/)).toHaveCount(0);

  // Un aviso con su archivo: «Actualizar» lo instala al lado (con la API de instalar) y dice qué juegos lo usan.
  const many = ["A", "B", "C", "D", "E", "F", "G", "H"];
  await page.route("**/api/tools/updates", (route) => route.fulfill({ json: [{ recipeId: "steam-rom-manager", name: "Steam ROM Manager", kind: "tool", channel: "stable", version: "2.6.3",
    notes: null, url: "https://github.com/SteamGridDB/steam-rom-manager/releases", publishedAt: null, installed: ["2.6.2"], games: many, download: "https://example.com/srm-2.6.3.exe" }] }));
  let sent: unknown = null;
  await page.route("**/api/emulators/install", (route) => { sent = route.request().postDataJSON(); return route.fulfill({ json: 1 }); });
  // Va en su pestaña (decisión 320), con su contador, y en una fila que se abre (decisión 322).
  await page.goto(`${base()}/tools/`);
  await expect(page.getByText("Steam ROM Manager")).toHaveCount(0);
  await expect(page.getByRole("tab", { name: new RegExp(en.tools.kind.tool) }).locator(".ot-count")).toHaveText("1");
  await page.goto(`${base()}/tools/?k=tool`);
  const row = page.getByRole("button", { name: /Steam ROM Manager.*2\.6\.2 → 2\.6\.3/ });
  await expect(row).toHaveAttribute("aria-expanded", "false");
  await row.click();
  await page.screenshot({ path: "e2e-results/8-tools-updates-en.png" });
  await expect(page.getByText(en.tools.updates.gamesMore.replace("{list}", "A, B, C, D, E, F").replace(/\{count.*\}$/, "2 more"))).toBeVisible();
  await page.getByRole("button", { name: en.tools.updates.update, exact: true }).click();
  await expect(page.getByText(en.tools.updates.queued)).toBeVisible();
  expect(sent).toEqual({ emulator: "steam-rom-manager", version: "2.6.3", url: "https://example.com/srm-2.6.3.exe" });
  await expectNoRawKeys();
  await page.unroute("**/api/tools/updates");
  await page.unroute("**/api/emulators/install");
  // La tarjeta de un emulador (#329): «En uso», usar otra versión y borrar la que no se usa.
  const ver = (version: string, path: string | null, inUse: boolean) => ({ emulatorId: "eden", name: "Eden", kind: "emulator", version, origin: "recipe", url: null, file: null, sha256: null, installedAt: "2026-01-01T00:00:00Z", path, inUse });
  const dolphin = { ...ver("2512", "C:/d/2512", true), emulatorId: "dolphin", name: "Dolphin" };
  let list = [ver("1.0", "C:/e/1.0", true), ver("2.0", "C:/e/2.0", false), ver("0.9", null, false), dolphin];
  await page.route("**/api/emulators", (route) => route.fulfill({ json: list }));
  const bodies: Record<string, unknown> = {};
  for (const action of ["use", "remove"]) await page.route(`**/api/emulators/${action}`, (route) => {
    bodies[action] = route.request().postDataJSON();
    list = action === "use" ? [ver("1.0", "C:/e/1.0", false), ver("2.0", "C:/e/2.0", true), ver("0.9", null, false)] : list.filter((i) => i.version !== "1.0");
    return route.fulfill({ json: list });
  });
  await page.goto(`${base()}/tools/?k=emulator`);
  // Secciones por consola (decisiones 343 y 345): Dolphin sale en GameCube y en Wii; Eden, una vez, en Switch.
  for (const name of ["GameCube", "Wii"]) await expect(page.getByRole("heading", { name, exact: true })).toBeVisible();
  await expect(page.getByRole("region", { name: "Dolphin" })).toHaveCount(2);
  const eden = page.getByRole("region", { name: "Eden" });
  await expect(eden).toHaveCount(1);
  // La versión en uso a la vista y las demás plegadas (decisión 344).
  await expect(eden.getByText(en.tools.library.current.replace("{version}", "1.0"))).toBeVisible();
  await expect(eden.getByText(en.tools.library.archived)).toBeHidden();
  await eden.getByText(/2 more versions/).click();
  await expect(eden.getByText(en.tools.library.archived)).toBeVisible();
  await expectNoRawKeys();
  await expectTextFits();
  await page.screenshot({ path: "e2e-results/8-tools-cards-en.png", fullPage: true });
  await eden.getByRole("button", { name: en.tools.library.use }).click();
  await expect(eden.getByText(en.tools.library.current.replace("{version}", "2.0"))).toBeVisible();
  await expect(eden.locator("details .version-row", { hasText: "2.0" }).getByText(en.tools.library.inUse)).toBeVisible();
  expect(bodies.use).toEqual({ emulator: "eden", version: "2.0" });
  await eden.locator("details .version-row", { hasText: "1.0" }).getByRole("button", { name: en.tools.library.remove }).click();
  await eden.getByRole("button", { name: en.tools.library.removeConfirm, exact: true }).click();
  await expect(eden.getByText("1.0")).toHaveCount(0);
  expect(bodies.remove).toEqual({ emulator: "eden", version: "1.0" });
  for (const action of ["use", "remove"]) await page.unroute(`**/api/emulators/${action}`);
  await page.unroute("**/api/emulators");
  // Se limpian la instalación y la copia del panel de tareas, para las pruebas que siguen.
  await page.getByRole("button", { name: en.topBar.tasks }).click();
  await expect(async () => {
    await page.getByRole("dialog", { name: en.topBar.tasksPanel }).getByRole("button", { name: en.tasks.clear }).click({ timeout: 1000 });
    await expect(page.getByText(en.topBar.noTasksTitle)).toBeVisible({ timeout: 1000 });
  }).toPass({ timeout: 15_000 });
  await page.keyboard.press("Escape");
  await removeDisks();
});

test("el panel de tareas y la búsqueda están traducidos", async () => {
  // El botón no cambia de ancho con 0, 1 o varias tareas (#20): se prueba cambiando el número del contador.
  const tasks = page.getByRole("button", { name: en.topBar.tasks });
  const widths = await tasks.evaluate((b) => {
    const count = b.querySelector(".ot-count")!;
    const original = count.textContent;
    const w = ["0", "1", "8", "88"].map((n) => ((count.textContent = n), b.getBoundingClientRect().width));
    count.textContent = original;
    return w;
  });
  expect(new Set(widths).size, `anchos: ${widths.join(", ")}`).toBe(1);
  await tasks.click();
  await expect(page.getByText(en.topBar.noTasksTitle)).toBeVisible();
  await page.keyboard.press("Escape");
  await page.keyboard.press("Control+K");
  await expect(page.getByPlaceholder(en.topBar.searchPlaceholder)).toBeVisible();
  await expect(page.getByText(en.catalog.search.typeMore)).toBeVisible();
  await page.keyboard.type("zelda");
  await expect(page.getByText(en.catalog.search.noResults.replace("{query}", "zelda"))).toBeVisible();
  await page.keyboard.press("Escape");
});

test("Ajustes en inglés: carpeta, idioma y actualización", async () => {
  await page.goto(`${base()}/settings/`);
  await expect(page.getByRole("heading", { level: 1, name: en.nav.settings })).toBeVisible();
  // #97: pestañas, una sección a la vez; sin ?s= abre la primera.
  await expect(page.getByRole("tab", { name: en.settings.storage })).toHaveAttribute("aria-selected", "true");
  await expect(page.getByRole("checkbox", { name: en.libraries.appData })).toBeVisible();
  await expect(page.getByRole("button", { name: en.update.check })).toHaveCount(0);
  await page.getByRole("tab", { name: en.settings.storage }).focus();
  await page.keyboard.press("End");
  await expect(page).toHaveURL(/\?s=general$/);
  await expect(page.getByRole("tab", { name: en.settings.general })).toHaveAttribute("aria-selected", "true");
  await page.goBack();
  await expect(page.getByRole("checkbox", { name: en.libraries.appData })).toBeVisible();
  // #98: cambiar a una pestaña corta no cambia el ancho (la barra de scroll tiene su lugar reservado).
  const tabsWidth = async () => (await page.getByRole("tablist").boundingBox())!.width;
  const wide = await tabsWidth();
  await page.getByRole("tab", { name: en.settings.general }).click();
  await expect(page.getByRole("button", { name: en.languageName })).toHaveAttribute("aria-pressed", "true");
  expect(await tabsWidth()).toBe(wide);
  expect(await page.evaluate(() => getComputedStyle(document.documentElement).scrollbarGutter)).toBe("stable");
  // #111: ?s=about de antes lleva a General.
  await page.goto(`${base()}/settings/?s=about`);
  await expect(page.getByRole("tab", { name: en.settings.general })).toHaveAttribute("aria-selected", "true");
  // El botón no cambia de ancho mientras busca (#20).
  const check = page.getByRole("button", { name: en.update.check });
  const idle = (await check.boundingBox())!.width;
  // La región de estado ya existe antes de buscar: así el lector de pantalla anuncia lo que aparece adentro.
  await expect(page.locator('.version-row ~ [role="status"]')).toBeAttached();
  await check.click();
  await expect(check).toHaveAttribute("aria-busy", "true");
  await expect(page.getByRole("status").getByText(en.update.checkingDetail)).toBeVisible();
  expect((await check.boundingBox())!.width).toBe(idle);
  // Canal Estable por defecto (#180); fuera de la app instalada el motivo llega como código y se traduce.
  await expect(page.getByText(en.update.reason.notInstalled)).toBeVisible();
  expect((await check.boundingBox())!.width).toBe(idle);
  // #180: Estable, Pruebas u Otro origen; «Otro origen» pide una carpeta o un repo y se guarda al usarlo.
  const channels = page.getByRole("group", { name: en.update.channel.title });
  await expect(channels.getByRole("button", { name: en.update.channel.stable })).toHaveAttribute("aria-pressed", "true");
  await channels.getByRole("button", { name: en.update.channel.testing }).click();
  await expect(channels.getByRole("button", { name: en.update.channel.testing })).toHaveAttribute("aria-pressed", "true");
  await expect(page.getByText(en.update.channel.testingHint)).toBeVisible();
  await channels.getByRole("button", { name: en.update.channel.custom }).click();
  await page.getByLabel(en.update.channel.origin).fill(dataDir("paquetes"));
  await page.getByRole("button", { name: en.update.channel.use, exact: true }).click();
  await page.reload();
  await expect(page.getByRole("group", { name: en.update.channel.title }).getByRole("button", { name: en.update.channel.custom })).toHaveAttribute("aria-pressed", "true");
  await expect(page.getByLabel(en.update.channel.origin)).toHaveValue(dataDir("paquetes"));
  // #230: el catálogo tiene su pestaña: el que usas arriba y el origen al final. #217: un nombre que no es dueño/repo se rechaza sin tocar el origen.
  await page.goto(`${base()}/settings/?s=catalog`);
  await expect(page.getByRole("heading", { name: en.update.catalogVersion.inUse })).toBeVisible();
  await expect(page.getByText(en.update.catalogSource.official.replace("{repo}", "OscarVargas97/otteretro-catalogo"))).toBeVisible();
  await page.getByLabel(en.update.catalogSource.repo).fill("esto no es un repo");
  await page.getByRole("button", { name: en.update.catalogSource.use }).click();
  await expect(page.getByRole("alert").filter({ hasText: en.errors.catalog.sourceName })).toBeVisible();
  await expect(page.getByRole("button", { name: en.update.catalogSource.officialButton })).toHaveCount(0);
  await page.goto(`${base()}/settings/?s=general`);
  // #209: Local no pide nada: busca sus paquetes en tus discos y, sin paquetes, lo dice.
  const group = page.getByRole("group", { name: en.update.channel.title });
  await group.getByRole("button", { name: en.update.channel.local, exact: true }).click();
  await expect(group.getByRole("button", { name: en.update.channel.local, exact: true })).toHaveAttribute("aria-pressed", "true");
  await expect(page.getByText(en.update.channel.localNone)).toBeVisible();
  await expect(page.getByLabel(en.update.channel.origin)).toHaveCount(0);
  await group.getByRole("button", { name: en.update.channel.stable }).click();
  await expect(group.getByRole("button", { name: en.update.channel.stable })).toHaveAttribute("aria-pressed", "true");
  await group.getByRole("button", { name: en.update.channel.local, exact: true }).click();
  await expect(group.getByRole("button", { name: en.update.channel.local, exact: true })).toHaveAttribute("aria-pressed", "true");
  await group.getByRole("button", { name: en.update.channel.custom }).click();
  await expect(page.getByLabel(en.update.channel.origin)).toHaveValue(dataDir("paquetes"));
  await page.getByRole("group", { name: en.update.channel.title }).getByRole("button", { name: en.update.channel.stable }).click();
  await expect(page.getByRole("group", { name: en.update.channel.title }).getByRole("button", { name: en.update.channel.stable })).toHaveAttribute("aria-pressed", "true");
  await expectNoRawKeys();
  await expectTextFits();
  await page.screenshot({ path: "e2e-results/4-settings-en.png", fullPage: true });
});

test("llevar los datos a otro disco muestra el progreso y el resultado traducidos (#208)", async () => {
  // El disco de destino ya tiene un juego: se queda, y solo llegan los datos.
  const r = await api("POST", "/api/disks", { path: dataDir("moved"), name: "Moved" });
  expect(r.ok, r.text).toBe(true);
  mkdirSync(join(dataDir("moved"), "Juegos", "GBA"), { recursive: true });
  writeFileSync(join(dataDir("moved"), "Juegos", "GBA", "metroid.gba"), "juego");
  await page.getByRole("tab", { name: en.settings.storage }).click();
  // «⋯» del disco ★: abrir carpeta (no se toca: abriría el explorador) y usar otra carpeta de datos.
  await page.locator(".ot-menu summary").click();
  await expect(page.getByRole("menuitem", { name: en.libraries.openFolder })).toBeVisible();
  await expect(page.getByRole("menuitem", { name: en.libraries.useOtherData })).toBeVisible();
  await page.locator(".ot-menu summary").click();
  // #169: se marca la fila de los datos y se mueve como lo demás.
  await page.getByRole("checkbox", { name: en.libraries.appData }).check();
  await page.locator('select[id^="to-"]').selectOption({ label: "Moved" });
  await page.locator(".panel").getByRole("button", { name: en.libraries.move, exact: true }).click();
  await expect(page.locator(".result").getByText(en.dataFolder.moved, { exact: true })).toBeVisible({ timeout: 15_000 });
  await expect(page.getByText(dataDir("moved")).first()).toBeVisible();
  await page.getByRole("button", { name: en.dataFolder.dismiss }).click();
  await expect(page.locator(".result").getByText(en.dataFolder.moved, { exact: true })).toHaveCount(0);
  // Ahora el ★ es ese disco, y el juego que ya tenía sigue ahí.
  await expect(page.getByLabel(en.libraries.disk, { exact: true }).locator("option:checked")).toContainText("Moved ★");
  const { existsSync } = await import("node:fs");
  expect(existsSync(join(dataDir("moved"), "Juegos", "GBA", "metroid.gba"))).toBe(true);
  expect(existsSync(join(dataDir("data"), "data"))).toBe(false);
  await expectNoRawKeys();
});

test("volver a español en Ajustes cambia todo y se mantiene al recargar", async () => {
  await page.getByRole("tab", { name: en.settings.general }).click();
  await page.getByRole("button", { name: es.languageName }).click();
  await expect(page.getByRole("heading", { level: 1, name: es.nav.settings })).toBeVisible();
  await page.reload();
  await expect(page.getByRole("heading", { level: 1, name: es.nav.settings })).toBeVisible();
  await expect(page.getByRole("link", { name: es.nav.collection })).toBeVisible();
  await expectNoRawKeys();
  await expectSearchFits();
  await expectTextFits();
  await page.screenshot({ path: "e2e-results/5-settings-es.png", fullPage: true });
});

test("Herramientas en español: la barra de arriba en una línea y una tarjeta por emulador en cada consola (#348)", async () => {
  const one = (emulatorId: string, name: string) => ({ emulatorId, name, kind: "emulator", version: "1.0", origin: "recipe", url: "https://example.com/x.zip", file: null, sha256: null,
    installedAt: "2026-01-01T00:00:00Z", path: `C:/x/${emulatorId}`, inUse: true });
  await page.route("**/api/emulators", (route) => route.fulfill({ json: [one("dolphin", "Dolphin"), one("eden", "Eden")] }));
  await page.goto(`${base()}/tools/?k=emulator`);
  // «Instalar…», «Cargar perfil» y «Revisar ahora» en una sola barra, sin el panel «Versiones nuevas».
  const top = async (name: string) => (await page.getByRole("button", { name, exact: true }).boundingBox())!.y;
  const y = await top(es.tools.install.open.emulator);
  for (const name of [es.profiles.loadZip, es.tools.updates.checkNow]) expect(await top(name)).toBe(y);
  await expect(page.getByRole("region", { name: "Dolphin" })).toHaveCount(2);
  const eden = page.getByRole("region", { name: "Eden" });
  await expect(eden.getByText(es.tools.library.current.replace("{version}", "1.0"))).toBeVisible();
  await eden.getByText(es.tools.library.details).click();
  await expect(eden.getByText("https://example.com/x.zip")).toBeVisible();
  await expectNoRawKeys();
  await expectTextFits();
  await page.screenshot({ path: "e2e-results/8-tools-cards-es.png", fullPage: true });
  await page.unroute("**/api/emulators");
  await page.goto(`${base()}/settings/`);
});

test("Cuentas y claves: pegar una clave la deja guardada sin mostrarla, y borrarla la deja en falta", async () => {
  await page.getByRole("tab", { name: es.settings.credentials }).click();
  // Una fila por servicio de esta fase; lo de otras, en una línea gris plegada (#57, #111).
  const steam = `${es.credentials.configure}: ${es.credentials.service.steam}`;
  await expect(page.getByRole("button", { name: steam })).toBeVisible();
  await expect(page.getByRole("button", { name: `${es.credentials.configure}: ${es.credentials.service.github}` })).toBeHidden();
  await page.getByRole("button", { name: steam }).click();
  const dialog = page.getByRole("dialog", { name: es.credentials.service.steam });
  const row = dialog.locator(".cred-row").filter({ hasText: es.credentials.field.apiKey });
  await expect(row.getByText(es.credentials.missing, { exact: true })).toBeVisible();
  await row.getByRole("button", { name: `${es.credentials.paste}: ${es.credentials.field.apiKey}` }).click();
  await row.getByLabel(es.credentials.field.apiKey).fill("clave-e2e-123");
  await row.getByRole("button", { name: es.credentials.save }).click();
  await expect(row.getByText(es.credentials.saved, { exact: true })).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(dialog).toBeHidden();
  await page.reload();
  const line = page.locator(".cred-line").filter({ hasText: es.credentials.service.steam });
  await expect(line.locator(".ot-badge-have").getByText(es.credentials.saved, { exact: true })).toBeVisible();
  expect(await page.locator("body").innerText()).not.toContain("clave-e2e-123");
  await expectNoRawKeys();
  await expectTextFits();
  await page.screenshot({ path: "e2e-results/6-credentials-es.png", fullPage: true });
  await page.getByRole("button", { name: steam }).click();
  await row.getByRole("button", { name: `${es.credentials.delete}: ${es.credentials.field.apiKey}` }).click();
  await expect(row.getByText(es.credentials.missing, { exact: true })).toBeVisible();
  await page.screenshot({ path: "e2e-results/6b-credentials-dialog-es.png" });
  await page.getByRole("button", { name: es.credentials.close }).click();
});

test("con mando se recorre la app: foco, barra de botones, buscar con el teclado en pantalla y menú rápido", async ({ browser }) => {
  // Un mando de Xbox simulado: la app lo lee con navigator.getGamepads() en cada cuadro.
  const pad = await browser.newPage();
  await pad.addInitScript(() => {
    const state = {
      id: "Xbox Wireless Controller (STANDARD GAMEPAD Vendor: 045e)", index: 0, connected: true, mapping: "standard", timestamp: 0,
      axes: [0, 0, 0, 0], buttons: Array.from({ length: 17 }, () => ({ pressed: false, touched: false, value: 0 })),
    };
    (window as unknown as { pad: typeof state }).pad = state;
    navigator.getGamepads = () => [state as unknown as Gamepad];
  });
  const press = async (button: number) => {
    await pad.evaluate((b) => { (window as unknown as { pad: { buttons: { pressed: boolean }[] } }).pad.buttons[b].pressed = true; }, button);
    await pad.waitForTimeout(100);
    await pad.evaluate((b) => { (window as unknown as { pad: { buttons: { pressed: boolean }[] } }).pad.buttons[b].pressed = false; }, button);
    await pad.waitForTimeout(100);
  };
  const [A, B, X, , , , , , , MENU, , , , DOWN, , RIGHT, GUIDE] = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];

  await pad.goto(url());
  await expect(pad.getByRole("heading", { level: 1, name: es.nav.home })).toBeVisible();
  await press(DOWN);
  await expect(pad.locator("html")).toHaveAttribute("data-input", "pad");
  await expect(pad.locator(".ot-padbar")).toContainText(es.pad.choose);
  // Con la forma del mando conectado: A de Xbox para elegir.
  await expect(pad.locator(".ot-padbar .ot-glyph").first()).toHaveText("A");
  await expect(pad.locator(".is-focus")).toHaveCount(1);

  // X abre la búsqueda; A en el campo abre el teclado; se escribe «qw» y Menú lo deja listo.
  await press(X);
  const field = pad.locator("#search");
  await expect(field).toBeFocused();
  await press(A);
  await expect(pad.getByRole("dialog", { name: es.pad.keyboard })).toBeVisible();
  await press(A);
  await press(RIGHT);
  await press(A);
  await expect(pad.locator(".ot-osk-field")).toHaveText("qw");
  await press(MENU);
  await expect(pad.getByRole("dialog", { name: es.pad.keyboard })).toBeHidden();
  await expect(field).toHaveValue("qw");
  await press(B);
  await expect(field).toBeHidden();

  // Guía abre el menú rápido; B lo cierra.
  await press(GUIDE);
  await expect(pad.getByRole("dialog", { name: es.pad.quickMenu })).toBeVisible();
  await expect(pad.getByRole("button", { name: es.pad.goToSteam })).toBeFocused();
  await pad.screenshot({ path: "e2e-results/7-mando-menu-rapido.png" });
  await press(B);
  await expect(pad.getByRole("dialog", { name: es.pad.quickMenu })).toBeHidden();
  // #172: el menú rápido también lleva a Inicio, Buscar, Tareas y Ajustes; Tareas abre su panel.
  await press(GUIDE);
  const quick = pad.getByRole("dialog", { name: es.pad.quickMenu });
  for (const item of [es.pad.home, es.pad.search, es.pad.tasks, es.pad.settings]) await expect(quick.getByRole("button", { name: item })).toBeVisible();
  await quick.getByRole("button", { name: es.pad.tasks }).click();
  await expect(pad.getByRole("dialog", { name: es.topBar.tasksPanel })).toBeVisible();
  await pad.keyboard.press("Escape");

  // Con el ratón, la interfaz vuelve a su forma normal.
  await pad.mouse.move(400, 400);
  await expect(pad.locator("html")).toHaveAttribute("data-input", "mouse");
  await expect(pad.locator(".ot-padbar")).toHaveCount(0);
  await pad.close();
});

test("catálogo con datos: sagas, favorita, ficha con seguimiento y corrección a mano, colección de PC y búsqueda", async () => {
  // Un catálogo pequeño, como el que dejaría una carga de IGDB y de Steam.
  const db = await openDb();
  db.exec(`
    INSERT INTO saga (id, name, iconic) VALUES (1, 'The Legend of Zelda', 1), (2, 'Half-Life', 0);
    INSERT INTO game (id, saga_id, name_es, original_name, year) VALUES
      (10, 1, 'Ocarina of Time', 'Ocarina of Time', 1998), (11, 1, 'Breath of the Wild', 'Breath of the Wild', 2017), (20, 2, 'Half-Life', 'Half-Life', 1998);
    INSERT INTO version (id, game_id, platform, kind, year, steam_app_id) VALUES
      (100, 10, 'n64', 'original', 1998, NULL), (101, 10, '3ds', 'remake', 2011, NULL), (110, 11, 'switch', 'original', 2017, NULL), (200, 20, 'pc', 'original', 1998, 70);
    INSERT INTO field_value (entity, entity_id, field, value, source, manual) VALUES ('game', 10, 'name_es', 'Ocarina of Time', 'igdb', 0);
    INSERT INTO source (kind, store, store_id, name, playtime, last_played, version_id) VALUES
      ('store', 'steam', '70', 'Half-Life', 600, '2026-10-01T00:00:00Z', 200), ('store', 'steam', '999', 'Algo que IGDB no tiene', 5, NULL, NULL);
  `);
  db.close();

  // #96: con más de 60 sagas se dibujan de a 60 y al bajar aparecen las demás.
  const many = await openDb();
  many.exec(`WITH RECURSIVE n(i) AS (SELECT 1000 UNION ALL SELECT i + 1 FROM n WHERE i < 1099) INSERT INTO saga (id, name) SELECT i, 'Saga de prueba ' || i FROM n`);
  // #225: la app rehace Sagas cuando cambia la generación (al aplicar un catálogo); acá el catálogo se escribe directo, así que se sube a mano.
  many.exec("UPDATE cache_generation SET value = value + 1");
  await page.goto(`${base()}/sagas/`);
  const tiles = page.locator("a.ot-saga");
  // Lo guardado se sirve al instante y se rehace aparte: la siguiente carga ya trae las sagas nuevas.
  await expect(async () => {
    await page.reload();
    await expect(tiles).toHaveCount(60, { timeout: 2_000 });
  }).toPass({ timeout: 20_000 });
  // #170: el total en la barra de filtros, y la tarjeta con sus insignias y «lo que tienes».
  await expect(page.locator(".toolbar-total")).toContainText("102");
  const zelda = tiles.filter({ hasText: "The Legend of Zelda" });
  await expect(zelda.locator(".ot-badge-iconic")).toHaveText(es.catalog.iconic);
  const menuTop = async () => (await page.locator(".sidebar").boundingBox())!.y;
  const before = await menuTop();
  for (let i = 0; i < 5 && (await tiles.count()) < 102; i++) await page.mouse.wheel(0, 20000);
  await expect(tiles).toHaveCount(102);
  // #100: el menú lateral sigue en su lugar después de bajar.
  expect(await page.evaluate(() => window.scrollY)).toBeGreaterThan(500);
  expect(await menuTop()).toBe(before);
  // #169: la barra superior también queda arriba.
  expect((await page.locator(".top").boundingBox())!.y).toBe(0);
  // #128: con la lista en varios tramos, se abre una saga y, ya vieja (más de 10 s), pasar por «Sagas» del menú la pide de nuevo sin romper.
  await tiles.filter({ hasText: "Saga de prueba 1090" }).click();
  await expect(page.getByRole("heading", { level: 2, name: "Saga de prueba 1090" })).toBeVisible();
  await page.waitForTimeout(10_500);
  const sagasLink = page.locator(".sidebar").getByRole("link", { name: es.nav.sagas });
  const asked: string[] = [];
  page.on("request", (r) => { if (r.url().includes("/api/sagas?")) asked.push(new URL(r.url()).searchParams.get("offset")!); });
  await sagasLink.hover();
  await expect.poll(() => asked).toEqual(["0", "60"]); // los dos tramos, como la lista (antes, el segundo fallaba)
  await sagasLink.click();
  await expect(tiles).toHaveCount(102);
  await expect(page.locator("p.error")).toHaveCount(0);
  many.exec("DELETE FROM saga WHERE id >= 1000; UPDATE cache_generation SET value = value + 1");
  many.close();

  await page.goto(`${base()}/sagas/`);
  await page.getByRole("link", { name: /The Legend of Zelda/ }).click();
  await expect(page.getByRole("heading", { level: 2, name: "The Legend of Zelda" })).toBeVisible();
  const favorite = page.getByRole("button", { name: es.catalog.saga.favorite });
  await favorite.click();
  await expect(favorite).toHaveAttribute("aria-pressed", "true");
  await page.reload();
  await expect(page.getByRole("button", { name: es.catalog.saga.favorite })).toHaveAttribute("aria-pressed", "true");
  // #112: sin héroe, el fondo de siempre; con el héroe del juego más reciente (Breath of the Wild), la imagen y su acento.
  await expect(page.locator(".hero-art")).toHaveCount(0);
  mkdirSync(join(dataDir("moved"), "catalogo", "imagenes"), { recursive: true });
  writeFileSync(join(dataDir("moved"), "catalogo", "imagenes", "11-hero.png"), solidPng(96, 31, [40, 90, 200]));
  await page.reload();
  await expect(page.locator(".hero-art img.shown")).toBeVisible();
  await expect.poll(() => page.locator(".accented").evaluate((e) => getComputedStyle(e).getPropertyValue("--accent"))).toMatch(/^hsl\(22\d/);
  await expectNoRawKeys();
  await expectTextFits();
  await page.screenshot({ path: "e2e-results/8-saga-es.png", fullPage: true });
  await page.goto(`${base()}/saga/?id=2`);
  await expect(page.getByRole("heading", { level: 2, name: "Half-Life" })).toBeVisible();
  await expect(page.locator(".hero-art")).toHaveCount(0);
  await page.goBack();

  // #128: con la saga desplazada hacia abajo, abrir un juego empieza arriba y «Atrás» vuelve a la misma altura.
  await page.addStyleTag({ content: ".hero { margin-bottom: 3000px } .app-main { min-height: 6000px }" }); // páginas tan largas como las reales
  await page.getByRole("link", { name: "Ocarina of Time" }).scrollIntoViewIfNeeded();
  const deep = await page.evaluate(() => window.scrollY);
  expect(deep).toBeGreaterThan(1000);
  await page.route("**/api/games/10*", async (route) => { await new Promise((r) => setTimeout(r, 800)); await route.continue(); }); // con el catálogo real, la ficha tarda
  await page.getByRole("link", { name: "Ocarina of Time" }).click();
  await expect(page.getByRole("heading", { level: 2, name: "Ocarina of Time" })).toBeVisible();
  await page.unroute("**/api/games/10*");
  await expect.poll(() => page.evaluate(() => window.scrollY)).toBe(0);
  await page.goBack();
  await expect.poll(() => page.evaluate(() => window.scrollY)).toBeGreaterThan(deep - 200);
  await page.getByRole("link", { name: "Ocarina of Time" }).click();
  await expect(page.getByRole("heading", { level: 2, name: "Ocarina of Time" })).toBeVisible();
  // #169: el camino en la barra, con un enlace por parte, y la portada sin bajar con el color de su consola.
  const crumbs = page.getByRole("navigation", { name: es.topBar.crumbs });
  await expect(crumbs.getByRole("link", { name: es.nav.sagas })).toHaveAttribute("href", "/sagas/");
  await expect(crumbs.getByRole("link", { name: "The Legend of Zelda" })).toHaveAttribute("href", "/saga/?id=1");
  await expect(crumbs.getByText("Ocarina of Time")).toHaveAttribute("aria-current", "page");
  expect(await page.locator(".hero .ot-cover").evaluate((e) => getComputedStyle(e).backgroundImage)).toContain("linear-gradient");
  await page.getByRole("radio", { name: es.catalog.status.finished }).click();
  await expect(page.getByLabel(es.catalog.game.finished)).toBeVisible();
  await page.getByLabel(es.catalog.game.rating).selectOption("9");
  await expect(page.getByText(es.catalog.game.saved)).toBeVisible();
  await page.reload();
  await expect(page.getByRole("radio", { name: es.catalog.status.finished })).toHaveAttribute("aria-checked", "true");
  await expect(page.getByLabel(es.catalog.game.rating)).toHaveValue("9");

  // F2.18: «Lo tengo» por defecto y el número de cada consola sigue al filtro.
  await page.goto(`${base()}/collection/?console=n64`);
  await expect(page.getByRole("button", { name: es.catalog.collection.filter.owned })).toHaveAttribute("aria-pressed", "true");
  const n64 = page.getByRole("navigation", { name: es.catalog.collection.consoles }).getByRole("button", { name: /Nintendo 64/ });
  await expect(n64).toContainText("0");
  // #171: con «Lo tengo», solo las consolas con algo (y la elegida), agrupadas por empresa.
  const consoleNav = page.getByRole("navigation", { name: es.catalog.collection.consoles });
  await expect(consoleNav.getByRole("button", { name: /Switch/ })).toHaveCount(0);
  await page.getByRole("button", { name: es.catalog.collection.filter.all }).click();
  await expect(n64).toContainText("1");
  await expect(consoleNav.getByRole("button", { name: /Switch/ })).toBeVisible();
  await expect(consoleNav.locator(".console-nav-company").first()).toHaveText("Nintendo");

  await page.goto(`${base()}/collection/?console=pc`);
  await expect(page.getByRole("link", { name: /Half-Life/ })).toBeVisible();
  await expect(page.getByText("Algo que IGDB no tiene")).toBeVisible();
  await page.getByRole("button", { name: es.catalog.collection.list }).click();
  await expect(page.getByRole("cell", { name: "10 h" })).toBeVisible();
  await expectNoRawKeys();
  await expectTextFits();
  await page.screenshot({ path: "e2e-results/10-coleccion-pc-es.png", fullPage: true });

  await page.keyboard.press("Control+K");
  await page.keyboard.type("zelda");
  await expect(page.getByRole("link", { name: /The Legend of Zelda/ })).toBeVisible();
  await page.keyboard.press("Enter");
  await expect(page).toHaveURL(/\/saga\/\?id=1/);
});

test("la ficha muestra tus enlaces de ese juego, y se pueden quitar (#210)", async () => {
  await page.goto(`${base()}/game/?id=10`);
  await expect(page.getByRole("heading", { name: es.downloads.gameLinks })).toHaveCount(0);
  expect((await api("POST", "/api/downloads/links", { text: "https://sitio.test/fusion.zip", gameId: 10 })).ok).toBe(true);
  await page.reload();
  await expect(page.getByRole("heading", { name: es.downloads.gameLinks })).toBeVisible();
  await expect(page.getByText("fusion.zip")).toBeVisible();
  await page.getByRole("button", { name: es.downloads.removeLink.replace("{name}", "fusion.zip") }).click();
  await expect(page.getByRole("heading", { name: es.downloads.gameLinks })).toHaveCount(0);
});

test("la ficha muestra el emulador recomendado para el juego, con su fuente y «Instalar» si falta (#366)", async () => {
  await page.route("**/api/recipes", (route) => route.fulfill({ json: [{ id: "fork-prueba", name: "Fork de prueba", kind: "emulator", experimental: true, releases: true,
    consoles: ["n64"], baseFor: [], folder: "N64", game: null, forkOf: null, recommendedFor: [{ game: "Ocarina of Time", gameId: 10, source: "https://sitio.test/guia" }] }] }));
  await page.goto(`${base()}/game/?id=10`);
  await expect(page.getByText(es.profiles.recommended.replace("{emulator}", "Fork de prueba"))).toBeVisible();
  await expect(page.getByRole("link", { name: es.profiles.recommendedSource.replace("{source}", "sitio.test") })).toHaveAttribute("href", "https://sitio.test/guia");
  await expect(page.getByRole("button", { name: es.tools.forks.install })).toBeVisible();
  // Otro juego no lo muestra.
  await page.goto(`${base()}/game/?id=11`);
  await expect(page.getByText(es.profiles.recommended.replace("{emulator}", "Fork de prueba"))).toHaveCount(0);
  await page.unroute("**/api/recipes");
});

test("la ficha busca el mejor emulador, muestra lo local primero y deja elegir a mano o volver al del catálogo (#375)", async () => {
  const db = await openDb();
  db.exec(`
    INSERT INTO emulator_pick (game_id, console, recipe_id, repo, source, reason, origin, searched_at) VALUES
      (10, 'n64', 'rmg', NULL, 'https://sitio.test/rmg', 'va bien', 'claude', '2026-11-01T12:00:00Z'),
      (10, '3ds', NULL, 'https://sitio.test/sin-receta', 'https://sitio.test/fuente', 'otro', 'claude', '2026-11-01T12:00:00Z');
  `);
  db.close();
  // La búsqueda real llamaría a Claude: se simula la cola.
  let queued = false;
  await page.route("**/api/games/10/best-emulator", (route) => route.request().method() === "POST"
    ? (queued = true, route.fulfill({ json: 999 }))
    : route.fulfill({ json: { task: queued ? 999 : null, searchedAt: "2026-11-01T12:00:00Z" } }));
  await page.goto(`${base()}/game/?id=10`);
  const best = es.profiles.best;
  await expect(page.getByText(/Última búsqueda/)).toBeVisible();
  await page.getByRole("button", { name: best.search }).click();
  await expect(page.getByRole("status").filter({ hasText: best.queued })).toBeVisible();
  await expect(page.getByRole("button", { name: best.search })).toBeDisabled();

  const n64 = page.getByRole("group", { name: /64/ });
  await expect(n64.getByText(es.profiles.recommended.replace("{emulator}", "Rosalie's Mupen GUI"))).toBeVisible();
  await expect(n64.getByText(best.origin.claude)).toBeVisible();
  await expect(n64.getByRole("link", { name: es.profiles.recommendedSource.replace("{source}", "sitio.test") })).toHaveAttribute("href", "https://sitio.test/rmg");
  // Sin receta: su repo, sin «Instalar».
  const n3ds = page.getByRole("group", { name: /3DS/ });
  await expect(n3ds.getByRole("link", { name: best.repo })).toHaveAttribute("href", "https://sitio.test/sin-receta");
  await expect(n3ds.getByRole("button", { name: es.tools.forks.install })).toHaveCount(0);

  await n64.getByRole("button", { name: best.choose }).click();
  await n64.getByRole("combobox").selectOption({ label: "Rosalie's Mupen GUI" });
  await expect(n64.getByText(best.origin.manual)).toBeVisible();
  await n64.getByRole("button", { name: best.reset }).click();
  await expect(n64.getByText(best.none)).toBeVisible();
  await expect(n64.getByRole("button", { name: best.reset })).toHaveCount(0);
  await page.unroute("**/api/games/10/best-emulator");
  const check = await openDb();
  expect(check.prepare("SELECT console FROM emulator_pick WHERE game_id = 10").all()).toEqual([{ console: "3ds" }]);
  check.exec("DELETE FROM emulator_pick");
  check.close();
});

test("«Buscar el mejor emulador» encola solo los juegos elegidos y deja desmarcado lo ya buscado (#374)", async () => {
  const db = await openDb();
  db.exec("INSERT INTO emulator_search (game_id, searched_at) VALUES (11, '2026-11-01T12:00:00Z') ON CONFLICT DO NOTHING");
  db.close();
  let sent: unknown = null;
  await page.route("**/api/best-emulator", (route) => { sent = route.request().postDataJSON(); return route.fulfill({ json: [1] }); });
  await page.goto(`${base()}/saga/?id=1`);
  await page.getByRole("button", { name: es.catalog.bestEmulator.open }).click();
  const dialog = page.getByRole("dialog", { name: es.catalog.bestEmulator.title });
  await expect(dialog.getByRole("checkbox", { name: "The Legend of Zelda" })).toBeChecked();
  await expect(dialog.getByRole("checkbox", { name: /Ocarina of Time/ })).toBeChecked();
  const botw = dialog.getByRole("checkbox", { name: /Breath of the Wild/ });
  await expect(botw).not.toBeChecked();
  await expect(dialog.getByText(/buscado el/)).toBeVisible();
  await expect(dialog.getByRole("status")).toHaveText("1 juego elegido");
  await dialog.getByRole("button", { name: "Buscar en 1 juego" }).click();
  await expect(dialog).toBeHidden();
  expect(sent).toEqual({ gameIds: [10] });
  await page.unroute("**/api/best-emulator");
});

test("ports: Novedades con los nuevos de lo que sigues, la ficha separa los 100 % jugables e instalar pide la URL solo sin receta (#286, #287, #384)", async () => {
  // Sin ningún «Visto», Novedades muestra lo de los últimos 30 días.
  const daysAgo = (n: number) => new Date(Date.now() - n * 86_400_000).toISOString().slice(0, 10);
  const db = await openDb();
  db.exec(`
    INSERT INTO saga_user (saga_id, favorite, following) VALUES (1, 0, 1) ON CONFLICT (saga_id) DO UPDATE SET following = 1;
    INSERT INTO port (id, console, game, project, kind, status, delisted, link, source, added, game_id) VALUES
      (1, 'n64', 'Ocarina of Time', 'Ship of Harkinian', 'decomp', 'complete', 0, 'https://sitio.test/soh', 'recompendium', '${daysAgo(4)}', 10),
      (2, 'n64', 'Ocarina of Time', 'Viejo port', 'port', 'beta', 1, 'https://sitio.test/viejo', 'extra', '${daysAgo(20)}', 10),
      (3, 'gc', 'Ocarina of Time', 'Dusklight', 'decomp', 'playable', 0, 'https://github.com/TwilitRealm/dusklight', 'extra', '${daysAgo(40)}', 10);
  `);
  db.close();
  await page.goto(`${base()}/`);
  const news = page.getByLabel(es.ports.news);
  await expect(news.getByRole("heading", { name: es.ports.newFullyPlayable })).toBeVisible();
  await expect(news.getByRole("heading", { name: es.ports.newOthers })).toBeVisible();
  await expect(news.getByText(es.ports.delisted)).toBeVisible();
  await news.getByRole("button", { name: es.ports.seen }).click();
  await expect(page.getByLabel(es.ports.news)).toHaveCount(0);

  await page.goto(`${base()}/game/?id=10`);
  const ports = page.getByRole("region", { name: es.ports.title });
  await expect(ports.getByRole("heading", { name: es.ports.fullyPlayable })).toBeVisible();
  await expect(ports.getByText(es.ports.status.complete, { exact: true })).toBeVisible();
  await expect(ports.getByText(es.ports.added.replace("{date}", new Intl.DateTimeFormat("es", { dateStyle: "medium", timeZone: "UTC" }).format(new Date(daysAgo(4)))))).toBeVisible();
  await expect(ports.getByText(es.ports.status.beta, { exact: true })).toBeVisible();
  // Con receta (su «port» es el enlace), solo «Instalar»; sin receta, solo la URL (#384).
  await expect(ports.getByRole("button", { name: es.ports.install, exact: true })).toHaveCount(1);
  await expect(ports.getByRole("button", { name: es.ports.installFromUrl })).toHaveCount(2);
  // Sin receta: con la URL de su release; si el archivo no dice la versión, se pide.
  await ports.getByRole("button", { name: es.ports.installFromUrl }).first().click();
  await ports.getByLabel(es.ports.url.replace("{name}", "Ship of Harkinian")).fill("https://sitio.test/descarga");
  await ports.locator("form").getByRole("button", { name: es.ports.installConfirm, exact: true }).click();
  await expect(ports.getByRole("alert")).toHaveText(es.errors.install.noVersion);
  await expectTextFits();
});

test("la ficha: perfiles de cada versión, crear, activar y volver al por defecto (#269)", async () => {
  // Un emulador sin receta, base de Switch: sus perfiles no mueven archivos, así que no hace falta instalarlo.
  const db = await openDb();
  db.exec(`
    INSERT INTO emulator (id, kind, name) VALUES ('mi-emu', 'emulator', 'MiEmu');
    INSERT INTO profile (emulator_id, name, created_at) VALUES ('mi-emu', 'MiEmu', '');
    INSERT INTO console_emulator (console_id, emulator_id, is_base) VALUES ('switch', 'mi-emu', 1);
  `);
  db.close();
  await page.goto(`${base()}/game/?id=11`);
  await expect(page.getByRole("heading", { name: es.profiles.title })).toBeVisible();
  await expect(page.getByText(es.profiles.noneActive.replace("{name}", "MiEmu").replace("{emulator}", "MiEmu"))).toBeVisible();
  await page.getByRole("textbox", { name: es.profiles.newName }).fill("2x");
  await page.getByRole("button", { name: es.profiles.create }).click();
  await expect(page.getByRole("textbox", { name: es.profiles.rename.replace("{name}", "2x") })).toBeVisible();
  await page.getByRole("button", { name: es.profiles.activate }).click();
  await expect(page.getByText(es.profiles.active.replace("{name}", "2x").replace("{emulator}", "MiEmu"))).toBeVisible();
  await expectNoRawKeys();
  await page.screenshot({ path: "e2e-results/12-perfiles-es.png", fullPage: true });
  await page.getByRole("button", { name: es.profiles.activate }).click(); // la fila «sin perfil»
  await expect(page.getByText(es.profiles.noneActive.replace("{name}", "MiEmu").replace("{emulator}", "MiEmu"))).toBeVisible();

  // Compartir (#282): pide la ruta del .zip y avisa si su carpeta no existe; cargar, si el archivo no está.
  await page.getByRole("button", { name: es.profiles.shareOf.replace("{name}", "2x") }).click();
  await page.getByRole("textbox", { name: es.profiles.sharePath.replace("{name}", "2x") }).fill("C:\\no-existe\\2x.zip");
  await page.getByRole("button", { name: es.profiles.shareConfirm }).click();
  await expect(page.getByRole("alert").filter({ hasText: es.errors.profile.share.badPath })).toBeVisible();
  await page.getByLabel(es.profiles.loadZip).setInputFiles({ name: "perfil.zip", mimeType: "application/zip", buffer: Buffer.from("no es un zip") });
  await expect(page.getByRole("alert").filter({ hasText: es.errors.profile.import.badPackage })).toBeVisible();
  await expectNoRawKeys();
});

test("la ficha: cómo jugar elige por la regla, deja cambiar a mano y volver a la automática; Ajustes ordena los criterios (#398)", async () => {
  // Con la versión de Switch en la colección, su emulador (MiEmu, de la prueba anterior) es la opción.
  const db = await openDb();
  db.exec("INSERT INTO source (kind, relative_path, console, version_id) VALUES ('file', 'botw.nsp', 'switch', 110)");
  db.close();
  const p = es.play;
  await page.goto(`${base()}/game/?id=11`);
  const play = page.locator("section").filter({ has: page.getByRole("heading", { name: p.title }) });
  await expect(play.getByText(p.emulator.replace("{name}", "MiEmu"))).toBeVisible();
  await expect(play.getByText(p.reason.playable)).toBeVisible();
  await play.getByRole("button", { name: p.change }).click();
  await play.getByRole("combobox").selectOption({ label: p.emulator.replace("{name}", "MiEmu") });
  await expect(play.getByText(p.reason.manual)).toBeVisible();
  await play.getByRole("button", { name: p.reset }).click();
  await expect(play.getByText(p.reason.playable)).toBeVisible();
  await expect(play.getByRole("button", { name: p.reset })).toHaveCount(0);
  await expectNoRawKeys();

  await page.goto(`${base()}/settings/?s=play`);
  const first = p.criterion.pc;
  await page.getByRole("button", { name: p.down.replace("{criterion}", first) }).click();
  await expect(page.locator("ol li").first()).toContainText(p.criterion.port);
  await page.getByRole("button", { name: p.up.replace("{criterion}", first) }).click();
  await expect(page.locator("ol li").first()).toContainText(first);
  await expectNoRawKeys();

  const check = await openDb();
  check.exec("DELETE FROM source WHERE version_id = 110; DELETE FROM play_choice");
  check.close();
});

test("la ficha: ajustes del perfil por defecto de Eden según su receta y abrir el emulador (#276)", async () => {
  // Eden registrado sin instalar: muestra los ajustes de su receta, los guarda en el perfil y avisa que no hay versión instalada.
  const db = await openDb();
  db.exec(`
    INSERT OR IGNORE INTO emulator (id, kind, name) VALUES ('eden', 'emulator', 'Eden');
    INSERT INTO profile (emulator_id, name, created_at) SELECT 'eden', 'Eden', '' WHERE NOT EXISTS (SELECT 1 FROM profile WHERE emulator_id = 'eden' AND game_version_id IS NULL);
    INSERT OR IGNORE INTO console_emulator (console_id, emulator_id, is_base) VALUES ('switch', 'eden', 0);
  `);
  db.close();
  await page.goto(`${base()}/game/?id=11`);
  await page.getByText(es.profiles.defaults).click();
  await page.getByRole("button", { name: `${es.profiles.settings.open}: Eden` }).click();
  const form = page.getByRole("form", { name: es.profiles.settings.title.replace("{name}", "Eden") });
  await expect(form.getByText(es.profiles.settings.notInstalled)).toBeVisible();
  await form.getByLabel(es.profiles.settings.names.resolution).fill("3");
  await form.getByLabel(es.profiles.settings.names.speed).fill("200");
  await form.getByRole("button", { name: es.profiles.settings.save }).click();
  await expect(form.getByText(es.profiles.settings.saved)).toBeVisible();
  await expectNoRawKeys();
  await expectTextFits();
  await page.screenshot({ path: "e2e-results/13-ajustes-perfil-es.png", fullPage: true });
  await form.getByRole("button", { name: es.profiles.settings.openEmulator }).click();
  await expect(form.getByRole("alert")).toHaveText(es.errors.profile.notInstalled.replace("{emulator}", "eden"));

  await page.reload(); // lo guardado vuelve
  await page.getByText(es.profiles.defaults).click();
  await page.getByRole("button", { name: `${es.profiles.settings.open}: Eden` }).click();
  await expect(page.getByLabel(es.profiles.settings.names.resolution)).toHaveValue("3");
});

test("panel de tareas: terminadas por hora de término, registro al hacer clic y limpiar", async () => {
  const db = await openDb();
  const now = new Date();
  const at = (minutes: number) => new Date(now.getTime() - minutes * 60_000).toISOString();
  db.exec(`
    DELETE FROM task WHERE status NOT IN ('queued', 'running'); -- las de las pruebas anteriores (agregar un disco escanea, #212)
    INSERT INTO task (id, kind, status, progress, created_at, finished_at) VALUES
      (900, 'demo', 'done', 100, '${at(30)}', '${at(20)}'), (901, 'demo', 'error', 40, '${at(25)}', '${at(5)}');
    INSERT INTO task_log (task_id, at, level, message) VALUES
      (901, '${at(6)}', 'info', '{"code":"tasks.log.images","params":{"game":"Ocarina of Time","kinds":"cover","kb":"80"}}'),
      (901, '${at(5)}', 'error', '{"code":"tasks.failed","params":{"detail":"sin red"}}');
  `);
  db.close();
  await page.goto(`${base()}/`);
  await page.getByRole("button", { name: es.topBar.tasks }).click();
  const panel = page.getByRole("dialog", { name: es.topBar.tasksPanel });
  await expect(panel.locator("button.ot-task-title")).toHaveText([/#901/, /#900/]); // la que terminó después, arriba
  await panel.getByRole("button", { name: /#901/ }).click();
  await expect(panel.getByText("Ocarina of Time: bajó cover (80 KB).")).toBeVisible();
  // #172: estado como insignia de color, barra en cada tarea y ✕ para quitar una terminada.
  await expect(panel.locator(".ot-task", { hasText: "#901" }).locator(".ot-badge-danger")).toHaveText(es.tasks.status.error);
  await expect(panel.locator(".ot-task", { hasText: "#900" }).locator(".ot-badge-have")).toHaveText(es.tasks.status.done);
  await expect(panel.locator(".ot-task", { hasText: "#900" }).locator(".ot-progress")).toBeVisible();
  await expectNoRawKeys();
  await page.screenshot({ path: "e2e-results/15-registro-tarea-es.png" });
  await panel.locator(".ot-task", { hasText: "#900" }).getByRole("button", { name: es.tasks.remove.replace("{name}", es.tasks.kind.demo) }).click();
  await expect(panel.locator("button.ot-task-title")).toHaveText([/#901/]);
  await panel.getByRole("button", { name: es.tasks.clear }).click();
  await expect(panel.locator("button.ot-task-title")).toHaveCount(0);
});

test("panel de tareas: pausar, reanudar y cancelar (#146)", async () => {
  // En cola para dentro de un día: la cola no la toma sola mientras se prueba.
  const db = await openDb();
  db.exec(`INSERT INTO task (id, kind, status, progress, resume_at, created_at) VALUES
    (910, 'demo', 'queued', 30, '${new Date(Date.now() + 86_400_000).toISOString()}', '${new Date().toISOString()}')`);
  db.close();
  await page.goto(`${base()}/`);
  await page.getByRole("button", { name: es.topBar.tasks }).click();
  const row = page.getByRole("dialog", { name: es.topBar.tasksPanel }).locator(".ot-task", { hasText: "#910" });
  await row.getByRole("button", { name: es.tasks.pause }).click();
  await expect(row.locator(".ot-badge")).toHaveText(es.tasks.status.paused);
  await row.getByRole("button", { name: es.tasks.resume }).click();
  await expect(row.locator(".ot-badge")).toHaveText(es.tasks.status.queued);
  await row.getByRole("button", { name: es.tasks.cancel }).click();
  await expect(row.getByText(es.tasks.cancelConfirm)).toBeVisible();
  await expectNoRawKeys();
  await row.getByRole("button", { name: es.tasks.cancel }).click();
  await expect(row.locator(".ot-badge")).toHaveText(es.tasks.status.canceled);
  await expect(row.getByRole("button", { name: es.tasks.pause })).toHaveCount(0);
  await page.getByRole("dialog", { name: es.topBar.tasksPanel }).getByRole("button", { name: es.tasks.clear }).click();
  await page.keyboard.press("Escape");
});

test("los cambios se ven al tocar y, si el guardado falla, vuelven y avisan", async () => {
  // #107: el servidor retiene el guardado; la estrella ya cambió y el botón sigue activo. Después responde con error.
  await page.goto(`${base()}/saga/?id=1`);
  const fav = page.getByRole("button", { name: new RegExp(es.catalog.saga.favorite) });
  const before = (await fav.getAttribute("aria-pressed"))!;
  let release = () => {};
  const held = new Promise<void>((r) => (release = r));
  await page.route("**/api/sagas/1", async (route) => {
    if (route.request().method() !== "PUT") return route.fallback();
    await held;
    await route.fulfill({ status: 409, contentType: "application/json", body: "{}" });
  });
  await fav.click();
  await expect(fav).toHaveAttribute("aria-pressed", before === "true" ? "false" : "true");
  await expect(fav).toBeEnabled();
  release();
  await expect(fav).toHaveAttribute("aria-pressed", before);
  await expect(page.getByRole("alert").filter({ hasText: es.errors.http.replace("{status}", "409") })).toBeVisible();
  await page.unroute("**/api/sagas/1");
});

test("las pestañas nunca tienen barra: en una ventana angosta, lo que no cabe va a «Más ▾»", async () => {
  // #108: a ningún ancho hay barra ni segunda línea; la elegida siempre se ve.
  const noScroll = async () => {
    const row = page.locator('.content [role="tablist"]');
    const { scroll, client, height } = await row.evaluate((e) => ({ scroll: e.scrollWidth, client: e.clientWidth, height: e.getBoundingClientRect().height }));
    expect(scroll).toBeLessThanOrEqual(client);
    expect(height).toBeLessThan(50); // una sola línea
  };
  await page.setViewportSize({ width: 520, height: 900 });
  await page.goto(`${base()}/settings/?s=general`);
  const more = page.getByRole("button", { name: new RegExp(es.tabs.more) });
  await expect(more).toBeVisible();
  await expect(page.locator("#tab-general")).toHaveAttribute("aria-selected", "true");
  await noScroll();
  await more.click();
  await page.getByRole("menuitem").last().click();
  await expect(page.locator('#settings-panel')).toBeVisible();
  await noScroll();
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(`${base()}/settings/`);
  await expect(more).toHaveCount(0);
  await expect(page.getByRole("tab")).toHaveCount(6); // con Catálogo (#230), Cómo jugar (#398) y sin Claude (#251)
  await noScroll();
  // #200: una sola columna centrada con ancho máximo.
  const content = (await page.locator(".content").boundingBox())!;
  const column = (await page.locator(".centered").boundingBox())!;
  expect(column.width).toBeLessThanOrEqual(881);
  expect(Math.abs((column.x - content.x) - (content.x + content.width - column.x - column.width))).toBeLessThan(40);
});

test("Ajustes en una sola columna a cualquier ancho, y el menú se retrae, se recuerda y se expande", async () => {
  // #200 (reemplaza a #130 y #196): los bloques van en una sola columna, también con 1920 px.
  await page.setViewportSize({ width: 1920, height: 1080 });
  await page.goto(`${base()}/settings/?s=claude`); // la pestaña Claude ya no está (#251): lleva a Almacenamiento
  await expect(page.locator("#tab-storage")).toHaveAttribute("aria-selected", "true");
  await expect(page.getByRole("heading", { name: es.libraries.recognize.title })).toBeVisible();
  const blocks = page.locator("#settings-panel .settings-blocks > :is(.panel, .disk-select)");
  await expect(blocks.nth(1)).toBeVisible();
  const columns = async () => new Set(await Promise.all([0, 1].map(async (i) => Math.round((await blocks.nth(i).boundingBox())!.x)))).size;
  expect(await columns()).toBe(1);
  await page.screenshot({ path: "e2e-results/19-almacenamiento-una-columna-es.png", fullPage: true });
  await page.setViewportSize({ width: 900, height: 900 });
  expect(await columns()).toBe(1);
  await page.setViewportSize({ width: 1440, height: 900 });

  // #130: el menú se retrae a una franja de íconos con el nombre en el título, se recuerda al recargar y Ctrl+B lo expande.
  const sidebar = page.locator(".sidebar");
  const wide = (await sidebar.boundingBox())!.width;
  await page.getByRole("button", { name: es.nav.collapse }).click();
  await expect(sidebar).toHaveClass(/is-collapsed/);
  expect((await sidebar.boundingBox())!.width).toBeLessThan(80);
  await expect(sidebar.getByRole("link", { name: es.nav.sagas })).toHaveAttribute("title", es.nav.sagas);
  await expectTextFits();
  await page.screenshot({ path: "e2e-results/20-menu-retraido-es.png" });
  await page.reload();
  await expect(sidebar).toHaveClass(/is-collapsed/);
  await page.keyboard.press("Control+b");
  await expect(sidebar).not.toHaveClass(/is-collapsed/);
  expect((await sidebar.boundingBox())!.width).toBe(wide);
  await page.reload();
  await expect(sidebar).not.toHaveClass(/is-collapsed/);
});

test("Ajustes › Consolas: desactivar una consola la saca de lo que se muestra y activarla la devuelve", async () => {
  // #224: sin bajar nada; la elección es tuya y queda guardada.
  await page.goto(`${base()}/settings/?s=consoles`);
  await expect(page.getByText(es.settings.consoles.help)).toBeVisible();
  const nes = page.getByRole("switch", { name: es.settings.consoles.show.replace("{name}", "NES") });
  await expect(nes).toBeChecked();
  // Un clic y se espera: el interruptor es controlado y cambia cuando React vuelve a dibujar, no en el mismo instante del clic.
  await nes.click();
  await expect(nes).not.toBeChecked();
  await expect.poll(async () => JSON.parse((await api("GET", "/api/consoles")).text).some((c: { id: string }) => c.id === "nes")).toBe(false);
  await page.reload();
  await expect(nes).not.toBeChecked();
  await nes.click();
  await expect(nes).toBeChecked();
  await expectNoRawKeys();
});
