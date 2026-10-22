import { renderToStaticMarkup } from "react-dom/server";
import { StaticRouter } from "react-router-dom";
import Navigation from "./components/Navigation";
import Footer from "./components/layout/Footer";
import HomePage from "./pages/HomePage";
import AboutPage from "./pages/AboutPage";
import MenuPage from "./pages/MenuPage";
import OrderPage from "./pages/OrderPage";
import { en } from "./i18n/strings/en";
import { getPublicPage, organization, publicPages, SITE_ORIGIN } from "./seo";

// These are the same page components used by the browser. Effects (API requests,
// browser storage, and staff sessions) do not run during static rendering.
const pages = {
  "/": HomePage,
  "/about": AboutPage,
  "/menu": MenuPage,
  "/order": OrderPage,
};

export const publicPaths = Object.keys(publicPages);
export const appPaths = ["/admin", "/admin-login", "/cash", "/order-status", "/order/confirmation", "/order/duplicate"];
export const siteOrigin = SITE_ORIGIN;

export function renderPage(pathname: string) {
  const match = getPublicPage(pathname);
  const page = match?.[1];
  const title = page?.title ?? en.meta.pageTitle;
  const description = page?.description ?? en.meta.description;
  const canonical = match ? `${SITE_ORIGIN}${match[0]}` : undefined;
  const head = renderToStaticMarkup(
    <>
      <title>{title}</title>
      <meta name="description" content={description} />
      <meta name="robots" content={page ? "index, follow" : "noindex, nofollow"} />
      <meta property="og:title" content={title} />
      <meta property="og:description" content={description} />
      <meta property="og:type" content="website" />
      <meta property="og:site_name" content={organization.name} />
      {canonical && <link rel="canonical" href={canonical} />}
      {canonical && <meta property="og:url" content={canonical} />}
      {match?.[0] === "/" && (
        <script id="organization-schema" type="application/ld+json"
          dangerouslySetInnerHTML={{ __html: JSON.stringify(organization).replaceAll("<", "\\u003c") }} />
      )}
    </>
  );
  const Page = match ? pages[match[0] as keyof typeof pages] : undefined;
  const body = Page ? renderToStaticMarkup(
    <StaticRouter location={pathname}>
      <div className="App">
        <Navigation />
        <main id="main-content" tabIndex={-1}>
          <Page />
          <noscript>
            <p>Enable JavaScript for current menu availability, prices, and online ordering.
              Follow <a href="https://www.instagram.com/roast66coffee/">Roast 66 Coffee on Instagram</a> for upcoming stops and event requests.</p>
          </noscript>
        </main>
        <Footer />
      </div>
    </StaticRouter>
  ) : "";
  return { head, body };
}
