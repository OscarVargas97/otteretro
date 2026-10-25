// Recorre la app como lo haría la persona, en español y en inglés (#140 a #142).
// Las pruebas van en orden y comparten la misma pestaña: cada una sigue donde quedó la anterior.
import { expect, test, type Page } from "@playwright/test";
import { join } from "node:path";
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
  expect(text).not.toMatch(/\b(nav|topBar|comingSoon|welcome|settings|dataFolder|update|errors|move|credentials)\.[a-zA-Z]+/);
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
  const overflowing = await page.locator("button, a, h1, h2, h3, .ot-badge").evaluateAll((els) =>
    els
      .filter((e) => {
        const s = getComputedStyle(e);
        return s.display !== "inline" && s.textOverflow !== "ellipsis" && (e.scrollWidth > e.clientWidth + 1 || e.scrollHeight > e.clientHeight + 1);
      })
      .map((e) => e.textContent?.trim()),
  );
  expect(overflowing, "textos que no caben").toEqual([]);
}

test("sin carpeta de datos, abre el asistente en español", async () => {
  await page.goto(url());
  await expect(page).toHaveURL(/\/welcome\//);
  await expect(page.getByRole("button", { name: es.welcome.start })).toBeVisible();
  await expect(page.getByText(es.welcome.steps.dataFolder)).toBeVisible();
  await expectNoRawKeys();
});

test("cambiar a inglés en el asistente cambia la interfaz al momento", async () => {
  await page.getByLabel(es.settings.language).selectOption("en");
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

test("elegir la carpeta de datos lleva al inicio", async () => {
  await page.getByLabel(en.welcome.otherPath).fill(dataDir("data"));
  await page.getByRole("button", { name: /^Use / }).click();
  await expect(page).toHaveURL(`${base()}/`);
  await expect(page.getByRole("heading", { level: 1, name: en.nav.home })).toBeVisible();
  await expect(page.getByText(en.comingSoon.home.title)).toBeVisible();
  await expectNoRawKeys();
  await expectSearchFits();
  await expectTextFits();
  await page.screenshot({ path: "e2e-results/3-home-en.png" });
});

test("todas las pantallas se ven en inglés", async () => {
  const screens = ["sagas", "collection", "game", "saga", "discover", "review", "downloads", "tools", "steam"] as const;
  for (const screen of screens) {
    await page.goto(`${base()}/${screen}/`);
    await expect(page.getByRole("heading", { level: 1, name: en.nav[screen] })).toBeVisible();
    await expect(page.getByText(en.comingSoon[screen].title)).toBeVisible();
    await expectNoRawKeys();
    await expectTextFits();
  }
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
  await page.keyboard.press("Escape");
});

test("Ajustes en inglés: carpeta, idioma y actualización", async () => {
  await page.goto(`${base()}/settings/`);
  await expect(page.getByRole("heading", { level: 1, name: en.nav.settings })).toBeVisible();
  await expect(page.getByText(dataDir("data"))).toBeVisible();
  // El botón no cambia de ancho mientras busca (#20).
  const check = page.getByRole("button", { name: en.update.check });
  const idle = (await check.boundingBox())!.width;
  // La región de estado ya existe antes de buscar: así el lector de pantalla anuncia lo que aparece adentro.
  await expect(page.locator('.version-row ~ [role="status"]')).toBeAttached();
  await check.click();
  await expect(check).toHaveAttribute("aria-busy", "true");
  await expect(page.getByRole("status").getByText(en.update.checkingDetail)).toBeVisible();
  expect((await check.boundingBox())!.width).toBe(idle);
  // Sin origen de actualizaciones: el motivo llega como código y se traduce.
  await expect(page.getByText(en.update.reason.noSource)).toBeVisible();
  expect((await check.boundingBox())!.width).toBe(idle);
  await expectNoRawKeys();
  await expectTextFits();
  await page.screenshot({ path: "e2e-results/4-settings-en.png", fullPage: true });
});

test("mover la carpeta de datos muestra el progreso y el resultado traducidos", async () => {
  await page.getByRole("button", { name: en.dataFolder.moveButton }).click();
  await page.getByLabel(en.dataFolder.folderLabel).fill(dataDir("moved"));
  await page.getByRole("button", { name: en.dataFolder.move, exact: true }).click();
  await expect(page.getByText(en.dataFolder.moved, { exact: true })).toBeVisible({ timeout: 15_000 });
  await expect(page.getByText(dataDir("moved")).first()).toBeVisible();
  await expectNoRawKeys();
});

test("volver a español en Ajustes cambia todo y se mantiene al recargar", async () => {
  await page.getByLabel(en.settings.language).selectOption("es");
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
  const row = page.locator(".cred-row").filter({ hasText: es.credentials.field.apiKey }).first();
  await expect(row.getByText(es.credentials.missing, { exact: true })).toBeVisible();
  await row.getByRole("button", { name: `${es.credentials.paste}: ${es.credentials.field.apiKey}` }).click();
  await row.getByLabel(es.credentials.field.apiKey).fill("clave-e2e-123");
  await row.getByRole("button", { name: es.credentials.save }).click();
  await expect(row.getByText(es.credentials.saved, { exact: true })).toBeVisible();
  await page.reload();
  await expect(row.getByText(es.credentials.saved, { exact: true })).toBeVisible();
  expect(await page.locator("body").innerText()).not.toContain("clave-e2e-123");
  await expectNoRawKeys();
  await expectTextFits();
  await page.screenshot({ path: "e2e-results/6-credentials-es.png", fullPage: true });
  await row.getByRole("button", { name: `${es.credentials.delete}: ${es.credentials.field.apiKey}` }).click();
  await expect(row.getByText(es.credentials.missing, { exact: true })).toBeVisible();
});
