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

function Menu() {
  const navigate = useNavigate();
  const { t } = useI18n();
  const [menuItems, setMenuItems] = useState<MenuItemDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);

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
        titleKey: "menu.categoryDrinks" as const,
        items: menuItems.filter(
          (item) =>
            item.categoryType === CategoryType.COFFEE || item.categoryType === CategoryType.DRINKS
        ),
      },
      {
        titleKey: "menu.categorySpecials" as const,
        items: menuItems.filter((item) => item.categoryType === CategoryType.SPECIALS),
      },
      {
        titleKey: "menu.categoryFlavors" as const,
        items: menuItems.filter((item) => item.categoryType === CategoryType.FLAVORS),
      },
    ],
    [menuItems]
  );

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
          sections.map(
            ({ titleKey, items }) =>
              items.length > 0 && (
                <div key={titleKey} className="mb-8">
                  <h3 className="text-2xl font-semibold mb-4 text-[#4a3326] border-b border-[#ddcdbf] pb-2">
                    {t(titleKey)}
                  </h3>
                  <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                    {items.map((item) => (
                      <Card key={item.id} className="h-full flex flex-col">
                        <h3 className="text-xl font-semibold mb-2 text-[#4a3326]">{item.name}</h3>
                        <p className="text-[#a64b2a] font-semibold">${item.price.toFixed(2)}</p>
                        <p className="text-[#5b4940]">{item.description}</p>
                        {canOrderMenuItemDirectly(item) ? (
                          <div className="mt-auto pt-4">
                            <Button
                              type="button"
                              variant="link"
                              color="green"
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
                </div>
              )
          )
        )}
      </div>
    </div>
  );
}

export default Menu;
