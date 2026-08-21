import React, { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import axios from "../../axiosConfig";
import type { MenuItemDto } from "../../types/api";
import { useI18n } from "../../i18n/LanguageContext";
import Button from "../common/Button";

function FeaturedSpecials() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const [items, setItems] = useState<MenuItemDto[]>([]);

  useEffect(() => {
    let active = true;
    axios
      .get<MenuItemDto[]>("/menu")
      .then(({ data }) => {
        if (active) setItems(data.filter((item) => item.isFeaturedOnHome).slice(0, 3));
      })
      .catch(() => {
        if (active) setItems([]);
      });
    return () => {
      active = false;
    };
  }, []);

  return (
    <section aria-labelledby="featured-specials-title" className="py-12 sm:py-16">
      <div className="mb-7 max-w-2xl">
        <p className="text-xs font-bold uppercase tracking-[0.18em] text-[#a64b2a]">
          {t("home.featuredEyebrow")}
        </p>
        <h2 id="featured-specials-title" className="mt-2 text-3xl font-bold text-[#2c1d15] sm:text-4xl">
          {t("home.featuredTitle")}
        </h2>
        <p className="mt-3 leading-7 text-[#5b4940]">{t("home.featuredBody")}</p>
      </div>

      {items.length ? (
        <div className="grid gap-4 md:grid-cols-3">
          {items.map((item, index) => (
            <article key={item.id} className="r66-panel flex min-w-0 flex-col p-6">
              <span className="mb-4 text-xs font-black tracking-[0.16em] text-[#6c89a2]">
                {String(index + 1).padStart(2, "0")}
              </span>
              <h3 className="text-xl font-bold text-[#2c1d15]">{item.name}</h3>
              <p className="mt-2 flex-1 leading-6 text-[#5b4940]">{item.description}</p>
              <div className="mt-5 flex items-center justify-between gap-3">
                <span className="font-bold text-[#a64b2a]">${item.price.toFixed(2)}</span>
                <Button
                  type="button"
                  color="green"
                  onClick={() => navigate("/order", { state: { menuItemId: item.id } })}
                >
                  {t("menu.orderThisItem")}
                </Button>
              </div>
            </article>
          ))}
        </div>
      ) : (
        <p className="rounded-xl border border-dashed border-[#c9ab92] bg-[#fff9f2]/70 p-6 text-[#5b4940]">
          {t("home.featuredEmpty")}
        </p>
      )}

      <Link to="/menu#specials" className="mt-6 inline-flex min-h-11 items-center font-bold">
        {t("home.seeFullMenu")} →
      </Link>
    </section>
  );
}

export default FeaturedSpecials;
