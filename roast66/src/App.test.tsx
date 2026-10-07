import React, { StrictMode } from "react";
import { beforeEach, afterEach, describe, it, expect, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { Link } from "react-router-dom";
import App from "./App";
import { LanguageProvider, useI18n } from "./i18n/LanguageContext";

vi.mock("./pages/HomePage", () => ({
  default: () => <div>Home Page</div>,
}));
vi.mock("./pages/AboutPage", () => ({
  default: () => <div>About Page</div>,
}));
vi.mock("./pages/MenuPage", () => ({
  default: () => <div>Menu Page</div>,
}));
vi.mock("./pages/OrderPage", () => ({
  default: () => <div>Order Page</div>,
}));
vi.mock("./pages/OrderConfirmationPage", () => ({
  default: () => <div>Order Confirmation</div>,
}));
vi.mock("./pages/DuplicateOrderPage", () => ({
  default: () => <div>Duplicate Order</div>,
}));
vi.mock("./pages/OrderStatusPage", () => ({
  default: () => <div>Order Status</div>,
}));
vi.mock("./components/Admin/AdminGate", () => ({
  default: () => <div>Admin Gate</div>,
}));
vi.mock("./components/Admin/CashGate", () => ({
  default: () => <div>Cash Gate</div>,
}));
vi.mock("./components/Navigation", () => ({
  default: function Navigation() {
    const { setLocale, t } = useI18n();
    return (
      <nav>
        {["/", "/about", "/menu", "/order", "/admin"].map((path) => (
          <Link key={path} to={path}>{path}</Link>
        ))}
        <button onClick={() => setLocale("es")}>Switch language</button>
        <span>{t("nav.menu")}</span>
      </nav>
    );
  },
}));
vi.mock("./components/layout/Footer", () => ({
  default: () => <footer>Footer</footer>,
}));
vi.mock("react-toastify", () => ({
  ToastContainer: () => null,
  toast: {},
}));

const originalHead = document.head.innerHTML;
const originalLanguage = document.documentElement.lang;

beforeEach(() => {
  window.history.replaceState({}, "", "/");
  window.localStorage.clear();
  document.head.innerHTML = '<title>Roast 66 Coffee</title><meta name="description" content="Roast 66 Coffee — order drinks, track your order, and visit our shop.">';
});

afterEach(() => {
  document.head.innerHTML = originalHead;
  document.documentElement.lang = originalLanguage;
  window.localStorage.clear();
});

describe("App", () => {
  it("renders navigation and home page", async () => {
    render(<App />);
    expect(screen.getByRole("navigation")).toBeInTheDocument();
    expect(await screen.findByText("Home Page")).toBeInTheDocument();
  });
});

const publicPages = [
  {
    path: "/",
    title: "Roast 66 Coffee | Mobile Coffee Trailer",
    description: "Roast 66 Coffee is a mobile coffee trailer serving handcrafted drinks in the Brentwood, Oakley, and Antioch area of California. Follow us for our next stop.",
  },
  {
    path: "/about",
    title: "Our Story | Roast 66 Coffee",
    description: "Meet Roast 66 Coffee, an owner-run mobile coffee trailer built from a love of cars, coffee, and community. Learn our story and connect with us for your next event.",
  },
  {
    path: "/menu",
    title: "Coffee & Drink Menu | Roast 66 Coffee",
    description: "Explore the current Roast 66 Coffee menu, including handcrafted coffee, specialty drinks, and flavors for your next visit to our mobile coffee trailer.",
  },
  {
    path: "/order",
    title: "Order Online | Roast 66 Coffee",
    description: "Order online from Roast 66 Coffee. Choose your drinks, customize your coffee, and place your order with our mobile coffee trailer.",
  },
];

function meta(selector: string) {
  return document.head.querySelector(selector)?.getAttribute("content");
}

function expectPublicMetadata(page: typeof publicPages[number]) {
  expect(document.title).toBe(page.title);
  expect(meta('meta[name="description"]')).toBe(page.description);
  expect(meta('meta[name="robots"]')).toBe("index, follow");
  expect(meta('meta[property="og:title"]')).toBe(page.title);
  expect(meta('meta[property="og:description"]')).toBe(page.description);
  expect(meta('meta[property="og:type"]')).toBe("website");
  expect(meta('meta[property="og:site_name"]')).toBe("Roast 66 Coffee");
}

function renderApp(path = "/") {
  window.history.replaceState({}, "", path);
  render(<StrictMode><LanguageProvider><App /></LanguageProvider></StrictMode>);
}

describe("route metadata", () => {
  it.each(publicPages)("sets distinct English metadata for $path", async (page) => {
    renderApp(page.path);
    await waitFor(() => expectPublicMetadata(page));
    expect(document.head.querySelectorAll('meta[name="description"]')).toHaveLength(1);
    expect(document.head.querySelector('link[rel="canonical"], meta[property="og:url"], meta[property="og:image"], link[hreflang]')).toBeNull();
  });

  it.each(["/admin", "/admin-login", "/cash", "/order-status", "/order/confirmation", "/order/duplicate"])("marks %s noindex, nofollow", async (path) => {
    renderApp(path);
    await waitFor(() => expect(meta('meta[name="robots"]')).toBe("noindex, nofollow"));
    expect(document.head.querySelector('script[type="application/ld+json"]')).toBeNull();
  });

  it("keeps the admin-login redirect noindex", async () => {
    renderApp("/admin-login");
    await waitFor(() => expect(window.location.pathname).toBe("/admin"));
    expect(meta('meta[name="robots"]')).toBe("noindex, nofollow");
  });

  it("replaces stale metadata and homepage JSON-LD during navigation", async () => {
    renderApp();
    await waitFor(() => expectPublicMetadata(publicPages[0]));
    for (const page of publicPages.slice(1)) {
      fireEvent.click(screen.getByRole("link", { name: page.path }));
      await waitFor(() => expectPublicMetadata(page));
      expect(document.head.querySelector('script[type="application/ld+json"]')).toBeNull();
    }
    fireEvent.click(screen.getByRole("link", { name: "/admin" }));
    await waitFor(() => expect(meta('meta[name="robots"]')).toBe("noindex, nofollow"));
    expect(document.title).toBe("Roast 66 Coffee");
    fireEvent.click(screen.getByRole("link", { name: "/" }));
    await waitFor(() => expectPublicMetadata(publicPages[0]));
    expect(document.head.querySelectorAll('script[type="application/ld+json"]')).toHaveLength(1);
    expect(document.head.querySelectorAll('meta[property="og:title"]')).toHaveLength(1);
    expect(document.head.querySelectorAll('meta[name="robots"]')).toHaveLength(1);
  });

  it("adds truthful Organization data on Home without storefront information", async () => {
    renderApp();
    await waitFor(() => expect(document.head.querySelector('script[type="application/ld+json"]')).not.toBeNull());
    const script = document.head.querySelector('script[type="application/ld+json"]');
    expect(JSON.parse(script?.textContent ?? "")).toEqual({
      "@context": "https://schema.org",
      "@type": "Organization",
      name: "Roast 66 Coffee",
      description: publicPages[0].description,
      sameAs: ["https://www.instagram.com/roast66coffee/"],
      areaServed: [
        { "@type": "City", name: "Brentwood, California" },
        { "@type": "City", name: "Oakley, California" },
        { "@type": "City", name: "Antioch, California" },
      ],
    });
  });

  it("keeps SEO English while preserving language switching", async () => {
    renderApp("/about");
    await waitFor(() => expectPublicMetadata(publicPages[1]));
    fireEvent.click(screen.getByRole("button", { name: "Switch language" }));
    await waitFor(() => expect(document.documentElement.lang).toBe("es"));
    expect(screen.getByText("Menú")).toBeInTheDocument();
    expect(window.localStorage.getItem("roast66_locale")).toBe("es");
    expectPublicMetadata(publicPages[1]);
  });

  it("matches public paths like the router with trailing slashes, case, and query strings", async () => {
    renderApp("/MENU/?category=coffee");
    await waitFor(() => expectPublicMetadata(publicPages[2]));
    expect(window.location.pathname).toBe("/MENU/");
  });
});
