import React, { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import axios from "../../axiosConfig";
import type { MenuItemDto } from "../../types/api";
import { useI18n } from "../../i18n/LanguageContext";
import logo from "../../logo-sign.svg";
import PromotionPrice from "../common/PromotionPrice";

const MAX_SPECIALS = 3;

function FeaturedSpecials() {
  const { t } = useI18n();
  const [specials, setSpecials] = useState<MenuItemDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    let active = true;

    axios
      .get<MenuItemDto[]>("/menu")
      .then(({ data }) => {
        if (active) {
          setSpecials(
            data.filter((item) => item.isFeaturedOnHome).slice(0, MAX_SPECIALS)
          );
        }
      })
      .catch(() => {
        if (active) setSpecials([]);
      })
      .finally(() => {
        if (active) setIsLoading(false);
      });

    return () => {
      active = false;
    };
  }, []);

  return (
    <section className="r66-specials" aria-labelledby="daily-specials-title">
      <div className="r66-specials-inner">
        <header className="r66-specials-heading">
          <div className="r66-specials-title-row">
            <img src={logo} alt="" aria-hidden="true" />
            <h2 id="daily-specials-title">{t("home.specialsTitle")}</h2>
            <img src={logo} alt="" aria-hidden="true" />
          </div>
          <p>{t("home.specialsTagline")}</p>
        </header>

        {isLoading ? (
          <div className="r66-specials-grid" aria-label={t("home.specialsLoading")}>
            {Array.from({ length: MAX_SPECIALS }, (_, index) => (
              <div className="r66-special-card r66-special-card-loading" key={index} aria-hidden="true" />
            ))}
          </div>
        ) : specials.length > 0 ? (
          <div className="r66-specials-grid">
            {specials.map((special, index) => (
              <article className="r66-special-card" key={special.id}>
                <span className="r66-special-number" aria-hidden="true">
                  {String(index + 1).padStart(2, "0")}
                </span>
                <div className="r66-special-copy">
                  <h3>{special.name}</h3>
                  <p>{special.description}</p>
                </div>
                <PromotionPrice item={special} className="r66-special-price" />
              </article>
            ))}
          </div>
        ) : (
          <p className="r66-specials-empty">{t("home.specialsEmpty")}</p>
        )}

        <Link className="r66-specials-menu-link" to="/menu#specials">
          {t("home.seeFullMenu")} <span aria-hidden="true">→</span>
        </Link>
      </div>
    </section>
  );
}

export default FeaturedSpecials;
