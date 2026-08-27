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
import PromotionPrice from "../common/PromotionPrice";

const categoryIds = ["specials", "drinks", "flavors"] as const;
type CategoryId = (typeof categoryIds)[number];

function getCategoryFromHash(): CategoryId {
  const category = window.location.hash.slice(1);
  return categoryIds.includes(category as CategoryId) ? (category as CategoryId) : "specials";
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
        id: "specials" as const,
        titleKey: "menu.categorySpecials" as const,
        items: menuItems.filter(
          (item) => item.isFeaturedOnHome || item.categoryType === CategoryType.SPECIALS
        ),
      },
      {
        id: "drinks" as const,
        titleKey: "menu.categoryDrinks" as const,
        items: menuItems.filter(
          (item) =>
            !item.isFeaturedOnHome &&
            (item.categoryType === CategoryType.COFFEE ||
              item.categoryType === CategoryType.DRINKS)
        ),
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
  const orderableSections = visibleSections.filter(({ id }) => id !== "flavors");
  const flavorSection = visibleSections.find(({ id }) => id === "flavors");

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
    <div className="min-h-screen p-4">
      <div className="w-full max-w-[1400px] mx-auto">
        <header className="rounded-2xl border border-[#d8c5b3] bg-[#fff9f2]/85 px-5 pb-12 pt-9 shadow-[0_12px_28px_rgba(74,51,38,0.1)] md:px-8 md:pt-11">
          <h2 className="text-3xl md:text-4xl font-bold mb-2 text-center text-[#4a3326] tracking-[0.01em]">
            {t("menu.pageTitle")}
          </h2>
          <p className="mb-7 text-center text-[0.98rem] leading-[1.6] text-[#6f5b4b]">
            {t("menu.pageSubtitle")}
          </p>
          <div className="flex items-center justify-center gap-3 text-sm uppercase font-semibold tracking-[0.08em] text-[#4d6f8a]">
            <span className="text-[#b59e8c]" aria-hidden="true">
              |
            </span>
            <span className="inline-flex items-center gap-1">
              <FaRoute aria-hidden="true" />
              {t("menu.badgeRouteInspired")}
            </span>
            <span className="text-[#b59e8c]" aria-hidden="true">
              |
            </span>
          </div>
        </header>

        {isLoading ? (
          <Loading />
        ) : (
          <>
            <nav
              aria-label={t("menu.categoryNavigation")}
              className="sticky top-3 z-30 mx-0 -mt-6 mb-8 overflow-x-auto rounded-xl border border-[#cdb9a7] bg-[#f4e9dd]/95 px-4 py-3 shadow-[0_8px_20px_rgba(74,51,38,0.14)] backdrop-blur-sm md:overflow-visible"
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
                          ? "border-[#c77e42] bg-[#c77e42] text-black hover:text-black"
                          : "border-[#cdb9a7] bg-[#fff9f2] text-[#4a3326] hover:border-[#a94727] hover:text-[#a94727]"
                      }`}
                    >
                      {t(titleKey)}
                    </a>
                  );
                })}
              </div>
            </nav>

            <div className="grid items-start gap-10 md:mx-4 xl:mx-0 xl:grid-cols-2 xl:gap-6">
              {orderableSections.map(({ id, titleKey, items }) => (
                <section
                  id={id}
                  key={id}
                  aria-labelledby={`${id}-heading`}
                  className={`scroll-mt-20 rounded-2xl border p-4 md:p-5 ${
                    id === "specials"
                      ? "border-[#c77e42] bg-gradient-to-br from-[#fff8ee] via-[#fff1df] to-[#f8e2cc] shadow-[0_12px_30px_rgba(166,75,42,0.12)]"
                      : "border-[#4a3326] bg-[#fffaf4] shadow-[0_10px_24px_rgba(74,51,38,0.1)]"
                  }`}
                >
                  <h3
                    id={`${id}-heading`}
                    className={`mb-4 border-b pb-2 text-2xl font-semibold text-[#4a3326] ${
                      id === "specials" ? "border-[#c77e42]" : "border-[#4a3326]"
                    }`}
                  >
                    <span className="inline-flex items-center gap-2">
                      {id === "specials" ? (
                        <FaStar className="text-[#c77e42]" aria-hidden />
                      ) : null}
                      {t(titleKey)}
                    </span>
                  </h3>

                  <div className="grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-2">
                    {items.map((item) => (
                      <Card
                        key={item.id}
                        tone={id === "specials" ? "special" : "drink"}
                        className="flex min-h-[19rem] h-full flex-col !shadow-none hover:!shadow-none"
                      >
                        <h4 className="mb-1 min-h-7 line-clamp-2 text-xl font-semibold text-[#4a3326]">
                          {item.name}
                        </h4>
                        <PromotionPrice item={item} className="text-[#a94727] font-semibold" />
                        <div className="min-h-[4.5rem]">
                          <p className="line-clamp-2 text-[#5b4940]">{item.description}</p>
                        </div>
                        {canOrderMenuItemDirectly(item) ? (
                          <div className="mt-auto pt-5">
                            <Button
                              type="button"
                              color={id === "specials" ? "yellow" : "green"}
                              className="flex min-h-14 w-full items-center justify-center"
                              onClick={() =>
                                navigate("/order", {
                                  state: { menuItemId: item.id },
                                })
                              }
                            >
                              {t("menu.orderItem", { itemName: item.name })}
                            </Button>
                          </div>
                        ) : null}
                      </Card>
                    ))}
                  </div>
                </section>
              ))}
            </div>

            {flavorSection ? (
              <section
                id={flavorSection.id}
                aria-labelledby="flavors-heading"
                className="mb-10 mt-10 scroll-mt-20 md:mx-4 xl:mx-8"
              >
                <h3
                  id="flavors-heading"
                  className="mb-4 border-b-2 border-[#99bfdd] pb-2 text-2xl font-semibold text-[#4a3326]"
                >
                  {t(flavorSection.titleKey)}
                </h3>
                <p className="mb-4 text-[0.98rem] leading-[1.6] text-[#6f5b4b]">
                  {t("menu.flavorsHelp")}
                </p>
                <div className="grid w-full grid-cols-1 gap-2 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
                  {flavorSection.items.map((item) => (
                    <div
                      key={item.id}
                      className="flex min-w-0 items-start justify-between gap-3 border-b-2 border-[#99bfdd] px-2 py-3"
                    >
                      <div className="min-w-0">
                        <h4 className="font-semibold text-[#4a3326]">{item.name}</h4>
                        <p className="mt-1 text-sm leading-snug text-[#898989]">
                          {item.description}
                        </p>
                      </div>
                      <PromotionPrice item={item} className="shrink-0 font-semibold text-[#a94727]" />
                    </div>
                  ))}
                </div>
              </section>
            ) : null}
          </>
        )}
      </div>
    </div>
  );
}

export default Menu;
