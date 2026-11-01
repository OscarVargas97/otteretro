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
  await expect(notice).toContainText(en.credentials.service.igdb);
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
    ["review", en.review.pendingEmptyTitle],
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
  if (process.env.OTTERETRO_SHOT) await page.locator(".panel").first().screenshot({ path: process.env.OTTERETRO_SHOT });
  await page.unroute("**/api/downloads/links/check");
  await expectNoRawKeys();
  // Quitar los deja fuera para siempre.
  await page.reload();
  for (const name of ["abc123", "juego.part1.rar", "juego.part2.rar"])
    await page.getByRole("button", { name: en.downloads.removeLink.replace("{name}", name) }).click();
  await expect(page.getByText(en.downloads.captureEmpty)).toBeVisible();
});

test("Herramientas: pestañas por tipo y subir un archivo de sistema con versiones (#154, #199)", async () => {
  await page.goto(`${base()}/tools/`);
  for (const k of ["emulator", "port", "complement", "system"] as const) await expect(page.getByRole("tab", { name: en.tools.kind[k] })).toBeVisible();
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
  await page.goto(`${base()}/sagas/`);
  const tiles = page.locator("a.ot-saga");
  await expect(tiles).toHaveCount(60);
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
  many.exec("DELETE FROM saga WHERE id >= 1000");
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

  // Corregir el nombre a mano gana sobre IGDB; volver al de las fuentes lo deshace.
  await page.getByText(es.catalog.game.sources).click();
  await page.getByRole("button", { name: `${es.catalog.game.fix}: ${es.catalog.game.field.name_es}` }).click();
  await page.getByLabel(es.catalog.game.field.name_es).fill("La ocarina del tiempo");
  await page.getByRole("button", { name: es.catalog.game.save }).click();
  await expect(page.getByRole("heading", { level: 2, name: "La ocarina del tiempo" })).toBeVisible();
  await expectNoRawKeys();
  await expectTextFits();
  await page.screenshot({ path: "e2e-results/9-ficha-es.png", fullPage: true });
  await page.getByRole("button", { name: es.catalog.game.undoFix }).click();
  await expect(page.getByRole("heading", { level: 2, name: "Ocarina of Time" })).toBeVisible();

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

test("Claude: aprobar en Revisar, la ruta en la saga, la comparativa en la ficha y el siguiente juego en Inicio", async () => {
  // Lo que dejaría `claude cargar` (F2.5): las rutas de Zelda y la mejor versión de Ocarina, por aprobar.
  const db = await openDb();
  const criterion = { score: 4, text: "Bien" };
  const items = [
    ["saga", 1, { prompt: "ruta-saga", result: [
      { kind: "release", rationale: "Por fecha de salida.", steps: [{ gameId: 10, versionId: null, reason: "El primero en 3D" }, { gameId: 11, versionId: null, reason: "El último" }], sources: [] },
      { kind: "community", rationale: "Así se recomienda empezar.", steps: [{ gameId: 10, versionId: 101, reason: "El de 3DS se ve mejor" }, { gameId: 11, versionId: null, reason: "Después, el mundo abierto" }], sources: [{ title: "r/zelda", url: "https://reddit.com/r/zelda" }] },
    ] }],
    ["game", 10, { prompt: "mejor-version", result: { gameId: 10, bestVersionId: 101, reason: "Mejor hoy", versions: [
      { versionId: 100, content: criterion, gameplay: { score: 3, text: "Controles de su época" }, technical: criterion, original: { score: 5, text: "Es el original" } },
      { versionId: 101, content: criterion, gameplay: { score: 5, text: "Pantalla táctil para el inventario" }, technical: criterion, original: criterion },
    ] } }],
  ] as const;
  for (const [entity, id, data] of items)
    db.prepare("INSERT INTO review_item (kind, entity, entity_id, data, created_at) VALUES ('pending_approval', ?, ?, ?, '2026-10-31')").run(entity, id, JSON.stringify(data));
  db.close();

  await page.goto(`${base()}/review/`);
  await expect(page.getByText("Así se recomienda empezar.")).toBeVisible();
  // #171: la ruta por aprobar con sus portadas numeradas y la razón de cada paso.
  const preview = page.locator(".timeline.is-plain").first();
  await expect(preview.locator(".step-num").first()).toHaveText("1");
  await expect(preview).toContainText("Ocarina of Time");
  await expectNoRawKeys();
  await expectTextFits();
  await page.screenshot({ path: "e2e-results/11-revisar-es.png", fullPage: true });
  // F2.21: el contador de Revisar en la barra lateral, y la ruta de la comunidad editada antes de aprobarla.
  const reviewLink = page.getByRole("navigation", { name: es.nav.label }).getByRole("link", { name: new RegExp(es.nav.review) });
  await expect(reviewLink).toContainText("2");
  await page.getByRole("button", { name: es.review.edit }).click();
  await page.getByRole("button", { name: es.review.stepUp.replace("{step}", "2") }).nth(1).click();
  await page.getByRole("button", { name: es.review.approveEdited }).click();
  await expect(reviewLink).toContainText("1");
  await page.getByRole("button", { name: es.review.approve }).first().click();
  await expect(page.getByText(es.review.pendingEmptyTitle)).toBeVisible();
  await expect(reviewLink.locator(".ot-count")).toHaveCount(0);

  await page.goto(`${base()}/saga/?id=1`);
  await expect(page.getByText(es.catalog.saga.why)).toBeVisible();
  await expect(page.getByText("El de 3DS se ve mejor")).toBeVisible();
  // #170: la ruta se elige con pestañas junto al título; elegirla es seguirla.
  await page.getByRole("tab", { name: es.catalog.route.community }).click();
  await expect(page.getByRole("tab", { name: es.catalog.route.community })).toHaveAttribute("aria-selected", "true");
  // Editada: Breath of the Wild quedó primero. Los pasos llevan su número y lo jugado (Ocarina) va a color.
  await expect(page.locator(".timeline").first()).toContainText(/Breath of the Wild[\s\S]*Ocarina of Time/);
  await expect(page.locator(".timeline li.is-played")).toContainText("Ocarina of Time");
  await expect(page.locator(".timeline .step-num").first()).toHaveText("1");
  // «Pedir a Claude que la recalcule» está en el menú «⋯» del encabezado (no se pulsa: encolaría una llamada real).
  await page.getByRole("button", { name: es.catalog.saga.moreActions }).click();
  await expect(page.getByRole("menuitem", { name: es.catalog.saga.recalculate })).toBeVisible();
  await page.keyboard.press("Escape");
  // Las consolas de la tabla, abreviadas: Ocarina está en N64 y 3DS.
  await expect(page.getByRole("row", { name: /Ocarina of Time/ }).locator(".ot-console")).toHaveText(["N64", "3DS"]);
  await expectNoRawKeys();
  await expectTextFits();
  await page.screenshot({ path: "e2e-results/12-saga-ruta-es.png", fullPage: true });

  await page.goto(`${base()}/game/?id=10`);
  // #171: versiones y comparativa en una sola tabla, con barras de 5 y el mejor de cada columna resaltado.
  await expect(page.getByRole("heading", { level: 2, name: es.catalog.game.versions })).toBeVisible();
  await expect(page.getByText("Pantalla táctil para el inventario")).toBeVisible();
  const ds = page.getByRole("row", { name: /Nintendo 3DS/ });
  await expect(ds.getByRole("img", { name: "5/5" })).toHaveClass(/is-best/);
  await expect(page.getByRole("row", { name: /Nintendo 64/ }).getByRole("img", { name: "3/5" })).not.toHaveClass(/is-best/);
  await page.screenshot({ path: "e2e-results/13-comparativa-es.png", fullPage: true });

  // Ocarina ya está terminado (prueba anterior): lo siguiente es Breath of the Wild.
  await page.goto(`${base()}/`);
  // #170: tarjeta horizontal con la ruta, el avance y «Ver ficha»; Revisar solo si hay algo (aquí ya se aprobó todo).
  const card = page.locator(".next-card", { hasText: "Breath of the Wild" });
  await expect(card.getByRole("link", { name: `${es.home.viewGame}: Breath of the Wild` })).toHaveAttribute("href", "/game/?id=11");
  await expect(card).toContainText(es.catalog.ofTotal.replace("{done}", "1").replace("{total}", "2"));
  await expect(page.getByRole("heading", { level: 2, name: es.home.review })).toHaveCount(0);
  await page.screenshot({ path: "e2e-results/14-inicio-es.png", fullPage: true });

  // F2.21: terminar Breath of the Wild completa la ruta que sigues: la nutria lo celebra.
  await page.goto(`${base()}/game/?id=11`);
  await page.getByRole("radio", { name: es.catalog.status.finished }).click();
  await expect(page.getByRole("status").filter({ hasText: es.catalog.game.cheerRoute.replace("{saga}", "The Legend of Zelda") })).toBeVisible();
  await page.screenshot({ path: "e2e-results/14b-nutria-es.png", fullPage: true });

  // #95: el registro de uso va de a 10.
  const usageDb = await openDb();
  for (let i = 1; i <= 12; i++)
    usageDb.exec(`INSERT INTO claude_usage (prompt, model, duration_ms, result, input_tokens, output_tokens, cost_usd, created_at)
      VALUES ('uso-${i}', 'haiku', 1000, 'ok', 1, 1, 0, '2026-10-31T03:00:00Z')`);
  usageDb.close();
  await page.goto(`${base()}/settings/?s=claude`);
  await expect(page.getByRole("button", { name: es.settings.claude.runNow })).toBeVisible();
  // #110: tres bloques, el horario en una frase de una sola línea y el registro con su costo.
  for (const h of [es.settings.claude.schedule, es.settings.claude.now, es.settings.claude.use])
    await expect(page.locator("#settings-panel").getByRole("heading", { level: 2, name: h })).toBeVisible();
  const scheduleRow = page.locator(".sentence").filter({ has: page.getByLabel(es.settings.claude.from) });
  expect((await scheduleRow.boundingBox())!.height).toBeLessThan(48);
  await expect(page.getByRole("columnheader", { name: es.settings.claude.cost })).toBeVisible();
  const usageTable = page.locator("table").filter({ hasText: "uso-12" });
  await expect(usageTable.locator("tbody tr")).toHaveCount(10);
  // #172: el total de la página arriba del registro.
  await expect(page.locator(".usage-total")).toContainText(/10/);
  await page.getByRole("button", { name: es.settings.claude.next }).click();
  await expect(page.getByRole("cell", { name: "uso-1", exact: true })).toBeVisible();
  await page.getByRole("button", { name: es.settings.claude.previous }).click();
  await expect(page.getByRole("cell", { name: "uso-12" })).toBeVisible();
  await expectNoRawKeys();
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

test("Aprendizajes: cada cambio en claro, filtros y corregir con una nota para Claude", async () => {
  const db = await openDb();
  const content = {
    prompt: "depurar-sagas",
    change: { kind: "move", reason: "No es de Zelda.", gameIds: [11], toSagaId: null, newSagaName: null },
    before: { games: [{ item1: 11, item2: 1 }], createdSaga: null },
  };
  db.prepare("INSERT INTO learning (kind, content, created_at, task_id) VALUES ('rule', ?, '2026-10-31T15:00:00Z', 37)").run(JSON.stringify(content));
  db.close();
  await page.goto(`${base()}/review/`);
  await page.getByRole("tab", { name: /Aprendizajes/ }).click();
  await expect(page.getByRole("heading", { level: 2, name: "Tarea #37" })).toBeVisible();
  await expect(page.getByText(/Breath of the Wild: de The Legend of Zelda a ninguna saga/)).toBeVisible();
  await page.getByRole("button", { name: es.review.kindFilter.merge }).click();
  await expect(page.getByText(/ninguna saga/)).toHaveCount(0);
  await page.getByRole("button", { name: es.review.filterAll }).click();
  await page.getByRole("button", { name: es.review.correct }).click();
  await page.getByRole("textbox", { name: es.review.correct }).fill("Breath of the Wild sí es de Zelda");
  await page.getByRole("button", { name: es.review.send }).click();
  await expect(page.getByText("Tu corrección: Breath of the Wild sí es de Zelda")).toBeVisible();
  await expect(page.getByText(es.review.undone)).toBeVisible();
  await expectNoRawKeys();
  await page.screenshot({ path: "e2e-results/16-aprendizajes-es.png", fullPage: true });
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
  await page.goto(`${base()}/settings/?s=claude`);
  const more = page.getByRole("button", { name: new RegExp(es.tabs.more) });
  await expect(more).toBeVisible();
  await expect(page.locator("#tab-claude")).toHaveAttribute("aria-selected", "true");
  await noScroll();
  await more.click();
  await page.getByRole("menuitem").last().click();
  await expect(page.locator('#settings-panel')).toBeVisible();
  await noScroll();
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.goto(`${base()}/settings/`);
  await expect(more).toHaveCount(0);
  await expect(page.getByRole("tab")).toHaveCount(5);
  await noScroll();
  // #200: una sola columna centrada con ancho máximo.
  const content = (await page.locator(".content").boundingBox())!;
  const column = (await page.locator(".centered").boundingBox())!;
  expect(column.width).toBeLessThanOrEqual(881);
  expect(Math.abs((column.x - content.x) - (content.x + content.width - column.x - column.width))).toBeLessThan(40);
});

test("Ajustes: consolas habilitadas, al día y reevaluar icónicas", async () => {
  await page.goto(`${base()}/settings/?s=consoles`);
  await expect(page.getByRole("tabpanel", { name: es.settings.consoles.title })).toBeVisible();
  // #109: filas por empresa en una sola línea y la fecha de carga, a lo más, una vez.
  const heights = await page.locator(".console-row").evaluateAll((rows) => rows.map((r) => r.getBoundingClientRect().height));
  expect(heights.length).toBeGreaterThan(20);
  expect(Math.max(...heights)).toBeLessThan(48);
  expect(await page.getByText(/al día al/i).count()).toBeLessThanOrEqual(1);
  await expect(page.getByRole("heading", { level: 3, name: es.settings.consoles.group.nintendo })).toBeVisible();
  const vb = page.getByRole("switch", { name: /Virtual Boy/ });
  await expect(vb).toBeChecked();
  await vb.click(); // cambia al tocar (#107)
  await expect(vb).not.toBeChecked();
  await vb.click();
  await expect(vb).toBeChecked();
  // #94: la búsqueda en IGDB se cierra con el botón y con Escape.
  const add = page.getByRole("button", { name: es.settings.consoles.add });
  await add.click();
  await page.getByRole("button", { name: es.settings.consoles.close }).click();
  await expect(add).toBeVisible();
  await add.click();
  await page.getByRole("searchbox", { name: es.settings.consoles.search }).press("Escape");
  await expect(add).toBeVisible();
  await expect(page.getByRole("searchbox", { name: es.settings.consoles.search })).toHaveCount(0);
  await expect(page.getByRole("button", { name: es.settings.consoles.refreshNow })).toBeVisible();
  await expectNoRawKeys();
  await page.screenshot({ path: "e2e-results/17-consolas-es.png", fullPage: true });
  await page.getByRole("tab", { name: es.settings.claude.title }).click();
  await expect(page.getByRole("button", { name: es.settings.claude.iconic })).toBeVisible();
  await expectNoRawKeys();
  await page.screenshot({ path: "e2e-results/18-claude-es.png", fullPage: true });
});

test("Ajustes en una sola columna a cualquier ancho, y el menú se retrae, se recuerda y se expande", async () => {
  // #200 (reemplaza a #130 y #196): los bloques van en una sola columna, también con 1920 px.
  await page.setViewportSize({ width: 1920, height: 1080 });
  await page.goto(`${base()}/settings/?s=claude`);
  const blocks = page.locator("#settings-panel .settings-blocks > .panel");
  await expect(blocks).toHaveCount(3);
  const columns = async () => new Set(await Promise.all([0, 1, 2].map(async (i) => Math.round((await blocks.nth(i).boundingBox())!.x)))).size;
  expect(await columns()).toBe(1);
  await page.screenshot({ path: "e2e-results/19-claude-una-columna-es.png", fullPage: true });
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

test("Ajustes › Consolas: portadas con su total, bajarlas a pedido y «En curso»", async () => {
  // #132: sin clave de SteamGridDB, el bloque lleva a Cuentas y claves.
  await page.goto(`${base()}/settings/?s=consoles`);
  const block = page.getByRole("region", { name: es.settings.consoles.covers.title });
  await expect(block).toContainText(/\d+ de \d+ juegos con portada/);
  await expect(block.getByRole("link", { name: es.settings.consoles.covers.noKey })).toHaveAttribute("href", "/settings/?s=credentials");
  // Con clave (la descarga real se prueba en xUnit, sin red): el botón encola y queda «En curso».
  let queued = 0;
  await page.route("**/api/covers", (route) => {
    if (route.request().method() === "POST") queued++;
    return route.fulfill({ json: { have: 31967, total: 40210, running: queued > 0, hasKey: true } });
  });
  await page.reload();
  await expect(block).toContainText("31.967 de 40.210 juegos con portada");
  await block.getByRole("button", { name: es.settings.consoles.covers.download }).click();
  await expect(block.getByRole("button", { name: es.settings.consoles.covers.running })).toBeDisabled();
  expect(queued).toBe(1);
  await expectNoRawKeys();
  await page.unroute("**/api/covers");
});
