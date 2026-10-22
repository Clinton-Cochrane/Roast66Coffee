# Search indexing and SEO verification

## Observed production baseline

On October 22, 2026, `https://roast66coffee.com/` returned a permanent redirect
to `https://www.roast66coffee.com/`. The destination returned HTTP 200 with an
empty React root, a generic description, and “You need to enable JavaScript to
run this app.” `/sitemap.xml` returned that same HTML document. `robots.txt`
allowed crawling but did not advertise a sitemap. These observations establish
delivery problems; they do not establish Google's indexing status. Gemini's
answer and a `site:` query are not substitutes for Search Console inspection.

## Hosting prerequisite: keep public HTML available during API idle time

The application is decoupled: a React/Vite frontend and a separate .NET API.
Both repository Blueprints specify a Render static frontend. Live inspection in
the user-confirmed **My Workspace** instead found two legacy Free Docker web
services: `Roast66Coffee-frontend` (`srv-ctdvec2lqhvc73daf1d0`) and
`Roast66Coffee-backend` (`srv-ctdrt0lumphs7397tahg`). Both track `main`. Before the cutover, public DNS
mapped `www.roast66coffee.com` to `roast66coffee-frontend.onrender.com`. This
confirmed that the public frontend was exposed to Free web-service idle behavior;
it does not establish Google's last observed crawl response.

Render documents that Free web services sleep after 15 minutes without inbound
traffic and take about one minute to resume. More directly relevant to SEO,
Render automatically returns a disallow-all `robots.txt` while a Free web
service is asleep; a robots request does not wake it. Build-time HTML cannot
override that platform response. A successful robots response while awake
does not prove that the domain stays crawlable while idle.

Use a Render Static Site for the generated frontend so its HTML, assets,
robots.txt, and sitemap are served from the CDN independently of API requests.
The API's compute plan is a separate decision for ordering responsiveness.
Upgrading the workspace plan alone does not remove Free compute limitations.
See [Render's Free service limits](https://render.com/docs/free) and
[Static Sites](https://render.com/docs/static-sites).

Before any domain cutover:

1. Confirm the live frontend service type, compute plan, deployment branch,
   custom domains, and API origin in the user-selected workspace.
2. If it is a sleeping web service, prepare the static frontend using the
   reviewed release commit, the Blueprint build command, `roast66/dist`, the
   public-route rewrites, and the correct API URL and security headers.
3. Verify the static site's temporary URL with JavaScript disabled and enabled,
   including menu API access, CORS, public HTML, sitemap, and private noindex.
4. Move the custom domains to the verified static site, preserve the observed
   apex-to-www redirect, and repeat the live checks below. Keep the previous
   service available for rollback until the cutover is verified.

Do not treat either the current Blueprints or the passing Nginx smoke tests as
proof that production already uses a static CDN. Do not close the indexing goal
until live hosting and Google's actual indexing have been verified.

### Live static frontend

A separate static site, `roast66coffee-static` (`srv-dbd3vssipgvc73e2fic0`), was
created in My Workspace and deployed on October 22, 2026. Both production
domains now belong to this static site, and automatic deploys are off. The old
frontend service remains available for rollback. The backend's allowed-origin
configuration was expanded as recorded below. Its Render URL is
<https://roast66coffee-static.onrender.com>, and its
[Render settings](https://dashboard.render.com/static/srv-dbd3vssipgvc73e2fic0/settings)
show the active domain associations.

The source is `bug/334-crawlable-public-pages`, build command
`cd roast66 && npm ci --include=dev && npm run build`, publish path `roast66/dist`,
Node version `24.20.0`, API URL `https://roast66coffee.onrender.com/api`, and
`VITE_USE_STATIC_MENU=false`. Online payments remain disabled. After PR #335 is
reviewed and merged, move the static site's source to the reviewed release
branch/commit using the intended manual release workflow.

Public HTTP checks verified all four public documents, matching canonical tags,
robots.txt, an XML sitemap, and noindex entry documents for the six private
routes. The static deployment running commit `a437ba3d` is live.

On October 22, the API's `AllowedOrigins` was updated to append
`https://roast66coffee-static.onrender.com`, preserving its existing old-frontend,
apex, and www origins. Configuration deploy `dep-dbd54067bikc73f4mcdg` became live
on the unchanged backend commit `5772849a`. HTTP checks returned 51 menu items
and matching CORS headers for both the www and preview origins. Browser checks
confirmed 51 rendered item headings on the preview and successful selection of
a drink into the order builder with quantity one. The browser's Spanish UI
preference was preserved, while SEO metadata remained English. No real order
was submitted. After cutover, the production menu also rendered all 51 items,
and selecting RUSTEZ opened the order builder with one item and quantity one.
No production order was submitted.

On October 22, authenticated Chrome sessions were used for the Render domain
move and Zoho DNS update because the connector does not expose custom-domain
editing. Only the website's `www` CNAME was changed, from
`roast66coffee-frontend.onrender.com` to `roast66coffee-static.onrender.com`.
The apex A record remains `216.24.57.1`, matching Render's displayed instructions.
Zoho mail records were preserved. Removing the old service's `www` association
also removed its paired apex association. Adding `www.roast66coffee.com` to the
static site recreated the apex-to-www redirect. Both domains show Verified and
Certificate Issued in Render. Google Public DNS returned the new CNAME.

Production HTTPS checks returned 200 for all four public pages with rendered
headings, matching www canonicals, and index/follow directives. `robots.txt`
advertises the XML sitemap. All six private entry documents returned noindex.
The apex redirects to the canonical www homepage. Browser checks confirmed
live API menu access and the order-builder flow described above.

Google Search Console ownership of the `roast66coffee.com` Domain property was
verified for the signed-in account using Google's exact TXT token, added as a
second apex TXT value alongside the existing SPF record. Keep that verification
record in DNS. The submitted `https://www.roast66coffee.com/sitemap.xml` reports
**Success**, last read October 22, 2026, with **4 discovered pages**.

Homepage URL Inspection reported **Discovered – currently not indexed**, with
no last crawl or Google-selected canonical. The live test on October 22, 2026
at 11:37 Pacific reported **URL is available to Google** and **Page can be
indexed**: crawl allowed, successful page fetch, indexing allowed, and the
correct `https://www.roast66coffee.com/` user-declared canonical. Google's tested
HTML contained the business heading, service cities, and canonical metadata.
The homepage indexing request was accepted into Google's priority crawl queue.
This confirms delivery and request acceptance, not actual indexing. Recheck the
Google Index result after Google crawls the queued URL.

## Build and hosting behavior

`npm run build` builds the browser app, renders the existing public React page
components in Node, and writes `index.html`, `about.html`, `menu.html`, and
`order.html`. It also writes noindex entry documents for every staff, tracking,
and order-result route, including nested routes. Render can serve these clean
URLs directly without a catch-all rewrite. The Node bundle stays outside `dist`
and is removed afterward.
The browser replaces the static markup using the existing `createRoot` flow,
so language preferences, live menu data, and ordering retain their behavior.
The build makes no API requests and embeds no live prices or order data.

Each public response includes a distinct English title and description, an
index/follow directive, canonical URL, Open Graph tags, and crawlable links.
The homepage includes Organization JSON-LD with the canonical URL, Instagram
profile, and service cities. It does not claim a permanent street address or
opening hours. This is a mobile business; confirm any future business details
with the owner before adding LocalBusiness data.

Menu availability, individual menu rows, prices, and ordering still require
JavaScript and the API. The generated pages describe the business and provide
navigation and an Instagram contact link without JavaScript. Home's sample
fallback specials are excluded from generated HTML. This does not turn the
live API menu into a static snapshot or guarantee individual menu-item indexing.

`app.html` is a separate noindex/nofollow document with no public canonical.
Nginx serves it for staff, order tracking, confirmation, and unknown routes.
Missing scripts, styles, images, and crawl files return 404. Render static-site
Blueprints rewrite public routes to their respective documents and use
`app.html` as the final fallback. Those rules provide a noindex fallback for
unknown routes; a static site without them returns 404 for unknown routes.
The explicit entry documents preserve all known routes in either case.
A Docker frontend uses `public/nginx.conf` automatically.
Vite preview is useful for public pages but does not reproduce Nginx's private
route fallback; use Nginx for the browser SEO checks.

`robots.txt` allows crawling, including crawling the noindex directives, and
advertises `https://www.roast66coffee.com/sitemap.xml`. The generated sitemap
contains exactly the four public canonical URLs. It uses no fabricated
last-modified timestamps or search-engine submission pings.

## Verification

The frontend CI job runs `npm run test:seo` after building. Those focused checks
inspect the actual generated documents without executing JavaScript, validate
the sitemap, ensure referenced assets exist, and verify the noindex fallback.
`src/App.test.tsx` covers client navigation and adoption of generated metadata.
The release smoke script serves the built frontend through Nginx, then runs
the live-menu smoke and `e2e/seo.spec.ts`. The SEO browser checks cover visible
content without JavaScript, client navigation, private routes, and missing files.

Ready-to-run local OpenCode prompt, from the repository root:

```sh
opencode run 'Validate issue #334 on the current topic branch. Read /home/workstation/Storage/code/AGENTS.md first. Do not edit files, switch branches, commit, push, merge, or deploy. In roast66 run npm ci, npm test, npm run lint, npm run build, and npm run test:seo. From the repository root run scripts/ci/verify-hosting-config.sh. Then run scripts/ci/render-smoke.sh "$PWD" "$PWD" to verify the built menu and SEO browser checks through Nginx; this needs Docker and Playwright Chromium (install with cd roast66 && npx playwright install chromium if absent). Report command results and failures. Do not mark Google indexing complete from local tests.'
```

After deployment, inspect the actual responses before asking Google to recrawl:

```sh
curl -I https://roast66coffee.com/
curl -fsS https://www.roast66coffee.com/
curl -fsS https://www.roast66coffee.com/about
curl -fsS https://www.roast66coffee.com/menu
curl -fsS https://www.roast66coffee.com/order
curl -fsS https://www.roast66coffee.com/robots.txt
curl -fsS https://www.roast66coffee.com/sitemap.xml
curl -fsS https://www.roast66coffee.com/admin
curl -I https://www.roast66coffee.com/missing.xml
```

Verify that public responses contain the actual page text and matching
canonicals with no `noindex` tag or `X-Robots-Tag` header, `/admin` has noindex
in its initial response, and the sitemap has an XML content type. Check that
the canonical host redirects consistently and that navigation and assets load.
Inspect all four public pages in a browser with JavaScript disabled and enabled.
Do not treat a green build as evidence that production has this revision.

## Google Search Console completion gate

An owner of the business/domain must use their existing verified Search Console
property or verify the `roast66coffee.com` Domain property using Google's exact
DNS TXT token. Never invent a verification token. Domain properties cover both
the apex and `www` hostnames.

1. Submit `https://www.roast66coffee.com/sitemap.xml` in Sitemaps and verify
   successful processing and discovery of the public URLs.
2. Inspect `https://www.roast66coffee.com/` with URL Inspection, run Test Live URL,
   and inspect the tested HTML/rendered result. Confirm it is accessible and
   indexable and contains the business content.
3. Request indexing of the homepage and inspect `/about`, `/menu`, and `/order`.
   Requesting indexing does not prove that indexing has happened.
4. Recheck URL Inspection after Google crawls the new deployment. Record the
   last crawl, “URL is on Google” status, and Google's selected canonical.
5. Verify a Google search result links to the actual domain for the business.
   Review Page Indexing reasons for any excluded public pages and fix genuine
   exclusions. Use Search Console as the authoritative per-URL evidence.

Keep the overall indexing goal open until production delivery and actual Google
indexing have been verified. Indexing is Google's decision, and a sitemap or
an indexing request cannot guarantee inclusion, timing, or ranking.

References:

- [Google JavaScript SEO basics](https://developers.google.com/search/docs/crawling-indexing/javascript/javascript-seo-basics)
- [Build and submit a sitemap](https://developers.google.com/search/docs/crawling-indexing/sitemaps/build-sitemap)
- [Request recrawling](https://developers.google.com/search/docs/crawling-indexing/ask-google-to-recrawl)
- [Render redirects and rewrites](https://render.com/docs/redirects-rewrites)
