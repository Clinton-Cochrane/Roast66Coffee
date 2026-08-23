import React from "react";
import { useI18n } from "../../i18n/LanguageContext";
import HomePhoto from "./HomePhoto";

function About() {
  const { t } = useI18n();

  return (
    <section
      id="home-story"
      aria-labelledby="home-story-title"
      className="grid scroll-mt-6 overflow-hidden rounded-2xl border border-[#dccdbe] bg-[#fff9f2]/90 shadow-[0_10px_24px_rgba(54,33,19,0.1)] md:grid-cols-2"
    >
      <HomePhoto
        name="story"
        alt={t("home.storyImageAlt")}
        pendingLabel={t("home.storyImagePending")}
        width={1200}
        height={900}
        sizes="(min-width: 768px) 42vw, 100vw"
        className="md:min-h-full"
      />
      <div className="flex flex-col justify-center p-7 sm:p-10">
        <p className="text-xs font-bold uppercase tracking-[0.18em] text-[#a64b2a]">
          {t("home.heroEyebrow")}
        </p>
        <h2 id="home-story-title" className="mt-2 text-3xl font-bold text-[#2c1d15]">
          {t("home.aboutTitle")}
        </h2>
        <p className="mt-4 text-lg leading-8 text-[#5b4940]">{t("home.aboutBodyOne")}</p>
      </div>
    </section>
  );
}

export default About;
