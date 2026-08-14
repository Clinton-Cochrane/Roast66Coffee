import React, { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import axios from "../../axiosConfig";
import Card from "../common/Card";
import Button from "../common/Button";
import CategoryType from "../../constants/categories";
import Loading from "../common/Loading";
import { useI18n } from "../../i18n/LanguageContext";
import { canOrderMenuItemDirectly } from "../../utils/canOrderMenuItemDirectly";
import { FaRoute, FaStar } from "react-icons/fa";
import type { MenuItemDto } from "../../types/api";

const categoryIds = ["drinks", "specials", "flavors"] as const;
type CategoryId = (typeof categoryIds)[number];

function getCategoryFromHash(): CategoryId {
  const category = window.location.hash.slice(1);
  return categoryIds.includes(category as CategoryId) ? (category as CategoryId) : "drinks";
}

function Menu() {
  const navigate = useNavigate();
  const { t } = useI18n();
  const [menuItems, setMenuItems] = useState<MenuItemDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [activeCategory, setActiveCategory] = useState<CategoryId>(getCategoryFromHash);

  useEffect(() => {
    axios
      .get<MenuItemDto[]>("/menu")
      .then((response) => {
        setMenuItems(response.data);
      })
      .catch((error: unknown) => {
        console.error("Error fetching menu items:", error);
      })
      .finally(() => {
        setIsLoading(false);
      });
  }, []);

  const sections = useMemo(
    () => [
      {
        id: "drinks" as const,
        titleKey: "menu.categoryDrinks" as const,
        items: menuItems.filter(
          (item) =>
            item.categoryType === CategoryType.COFFEE || item.categoryType === CategoryType.DRINKS
        ),
      },
      {
        id: "specials" as const,
        titleKey: "menu.categorySpecials" as const,
        items: menuItems.filter((item) => item.categoryType === CategoryType.SPECIALS),
      },
      {
        id: "flavors" as const,
        titleKey: "menu.categoryFlavors" as const,
        items: menuItems.filter((item) => item.categoryType === CategoryType.FLAVORS),
      },
    ],
    [menuItems]
  );

  const visibleSections = sections.filter(({ items }) => items.length > 0);

  useEffect(() => {
    if (isLoading || !window.location.hash) return;

    const category = getCategoryFromHash();
    const section = document.getElementById(category);
    if (section) {
      setActiveCategory(category);
      section.scrollIntoView?.();
    }
  }, [isLoading]);

  return (
    <div className="min-h-screen p-6">
      <div className="w-full max-w-5xl mx-auto">
        <h2 className="text-3xl md:text-4xl font-bold mb-2 text-center text-[#4a3326] tracking-[0.01em]">
          {t("menu.pageTitle")}
        </h2>
        <p className="text-center r66-subtitle mb-8">{t("menu.pageSubtitle")}</p>
        <div className="flex items-center justify-center gap-3 mb-8 text-xs uppercase font-semibold tracking-[0.08em] text-[#6c89a2]">
          <span className="inline-flex items-center gap-1">
            <FaRoute />
            {t("menu.badgeRouteInspired")}
          </span>
          <span className="text-[#b59e8c]">|</span>
          <span className="inline-flex items-center gap-1">
            <FaStar />
            {t("menu.badgeHouseFavorites")}
          </span>
        </div>

        {isLoading ? (
          <Loading />
        ) : (
          <>
            <nav
              aria-label={t("menu.categoryNavigation")}
              className="sticky top-0 z-10 -mx-6 mb-8 overflow-x-auto border-y border-[#ddcdbf] bg-[#f4ece1]/95 px-6 py-3 backdrop-blur-sm md:static md:mx-0 md:overflow-visible md:rounded-xl md:border"
            >
              <div className="flex min-w-max items-center justify-center gap-2">
                {visibleSections.map(({ id, titleKey }) => {
                  const isActive = activeCategory === id;
                  return (
                    <a
                      key={id}
                      href={`#${id}`}
                      aria-current={isActive ? "location" : undefined}
                      onClick={() => setActiveCategory(id)}
                      className={`inline-flex min-h-11 items-center rounded-full border px-5 py-2 font-semibold no-underline transition-colors focus-visible:outline-none ${
                        isActive
                          ? "border-[#a64b2a] bg-[#a64b2a] text-white hover:text-white"
                          : "border-[#cdb9a7] bg-[#fff9f2] text-[#4a3326] hover:border-[#a64b2a] hover:text-[#a64b2a]"
                      }`}
                    >
                      {t(titleKey)}
                    </a>
                  );
                })}
              </div>
            </nav>

            {visibleSections.map(({ id, titleKey, items }) => (
              <section
                id={id}
                key={id}
                aria-labelledby={`${id}-heading`}
                className={`mb-10 scroll-mt-20 ${
                  id === "specials"
                    ? "rounded-2xl border border-[#c77e42]/60 bg-[#fff4e8]/70 p-4 md:p-6"
                    : ""
                }`}
              >
                <h3
                  id={`${id}-heading`}
                  className={`text-2xl font-semibold mb-4 text-[#4a3326] border-b pb-2 ${
                    id === "specials" ? "border-[#c77e42]" : "border-[#ddcdbf]"
                  }`}
                >
                  {t(titleKey)}
                </h3>

                {id === "flavors" ? (
                  <>
                    <p className="r66-subtitle mb-4">{t("menu.flavorsHelp")}</p>
                    <div className="grid grid-cols-1 gap-2 sm:grid-cols-2">
                      {items.map((item) => (
                        <div
                          key={item.id}
                          className="flex min-w-0 items-center justify-between gap-3 rounded-lg border border-[#ddcdbf] bg-[#fff9f2]/85 px-4 py-3"
                        >
                          <span className="min-w-0 font-semibold text-[#4a3326]">{item.name}</span>
                          <span className="shrink-0 font-semibold text-[#a64b2a]">
                            ${item.price.toFixed(2)}
                          </span>
                        </div>
                      ))}
                    </div>
                  </>
                ) : (
                  <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                    {items.map((item) => (
                      <Card
                        key={item.id}
                        className={`h-full flex flex-col ${
                          id === "specials" ? "border-[#c77e42] bg-[#fffaf4]" : ""
                        }`}
                      >
                        <h4 className="text-xl font-semibold mb-2 text-[#4a3326]">{item.name}</h4>
                        <p className="text-[#a64b2a] font-semibold">${item.price.toFixed(2)}</p>
                        <p className="text-[#5b4940]">{item.description}</p>
                        {canOrderMenuItemDirectly(item) ? (
                          <div className="mt-auto pt-5">
                            <Button
                              type="button"
                              color={id === "specials" ? "yellow" : "green"}
                              className="min-h-11 w-full"
                              onClick={() =>
                                navigate("/order", {
                                  state: { menuItemId: item.id },
                                })
                              }
                            >
                              {t("menu.orderThisItem")}
                            </Button>
                          </div>
                        ) : null}
                      </Card>
                    ))}
                  </div>
                )}
              </section>
            ))}
          </>
        )}
      </div>
    </div>
  );
}

export default Menu;
