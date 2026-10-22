import { readFile, writeFile, rm, mkdir } from "node:fs/promises";
import { resolve, dirname } from "node:path";
import { pathToFileURL } from "node:url";
import { build } from "vite";

// Build-only Node bundle. It is never copied into the public website.
const serverOutput = "node_modules/.cache/prerender";
await build({
  build: {
    ssr: "src/prerender.tsx",
    outDir: serverOutput,
    emptyOutDir: true,
    copyPublicDir: false,
  },
});

try {
  const { renderPage, publicPaths, appPaths, siteOrigin } = await import(
    pathToFileURL(resolve(serverOutput, "prerender.js")).href
  );
  const template = await readFile("dist/index.html", "utf8");
  const manifest = JSON.parse(await readFile("dist/.vite/manifest.json", "utf8"));
  if (!template.includes("<!--page-head-->") || !template.includes("<!--app-html-->")) {
    throw new Error("The HTML template is missing prerender placeholders.");
  }
  function pageStyles(entry, visited = new Set()) {
    if (visited.has(entry.file)) return [];
    visited.add(entry.file);
    return [
      ...(entry.imports ?? []).flatMap((key) => pageStyles(manifest[key], visited)),
      ...(entry.css ?? []),
    ];
  }
  function documentFor(pathname) {
    const { head, body } = renderPage(pathname);
    const pageNames = { "/": "HomePage", "/about": "AboutPage", "/menu": "MenuPage", "/order": "OrderPage" };
    // A shared page (OrderPage is also used by CashGate) can be emitted as a
    // named common chunk instead of retaining its source filename as the key.
    const entry = pageNames[pathname]
      ? Object.values(manifest).find((chunk) => chunk.name === pageNames[pathname])
      : undefined;
    if (pageNames[pathname] && !entry) throw new Error(`Missing client chunk for ${pathname}`);
    const styles = [...new Set(entry ? pageStyles(entry) : [])]
      .filter((file) => !template.includes(`href="/${file}"`))
      .map((file) => `<link rel="stylesheet" href="/${file}">`).join("");
    // Function replacements preserve literal dollar sequences in rendered text.
    return template.replace("<!--page-head-->", () => head)
      .replace("<!--app-html-->", () => body)
      .replace("</head>", () => styles + "</head>");
  }
  // Clean .html entry documents also work on a CDN without SPA rewrites.
  for (const path of [...publicPaths, ...appPaths]) {
    const file = path === "/" ? "index.html" : `${path.slice(1)}.html`;
    await mkdir(dirname(resolve("dist", file)), { recursive: true });
    await writeFile(resolve("dist", file), documentFor(path));
  }
  // Staff pages, order tracking and unknown paths must not inherit Home's SEO.
  await writeFile("dist/app.html", documentFor("/app"));
  const urls = publicPaths.map((path) => `  <url><loc>${siteOrigin}${path}</loc></url>`).join("\n");
  await writeFile("dist/sitemap.xml", `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${urls}\n</urlset>\n`);
} finally {
  await rm(serverOutput, { recursive: true, force: true });
}
