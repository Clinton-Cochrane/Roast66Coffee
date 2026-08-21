import React, { useState } from "react";
import { FaCamera } from "react-icons/fa";

type HomePhotoProps = {
  name: "hero" | "story";
  alt: string;
  pendingLabel: string;
  width: number;
  height: number;
  sizes: string;
  className?: string;
  tone?: "dark" | "light";
};

function HomePhoto({
  name,
  alt,
  pendingLabel,
  width,
  height,
  sizes,
  className = "",
  tone = "dark",
}: HomePhotoProps) {
  const [loaded, setLoaded] = useState(false);

  return (
    <div
      className={`relative isolate overflow-hidden ${
        tone === "light" ? "bg-[#d8cec3]" : "bg-[#3b281f]"
      } ${className}`}
      style={{ aspectRatio: `${width} / ${height}` }}
    >
      <div
        className={`absolute inset-0 flex flex-col items-center justify-center gap-3 px-6 text-center ${
          tone === "light" ? "text-[#4a3326]" : "text-[#fff9f2]"
        }`}
      >
        <FaCamera
          className={`text-3xl ${tone === "light" ? "text-[#a64b2a]" : "text-[#99bfdd]"}`}
          aria-hidden="true"
        />
        <span className="min-w-0 max-w-full break-words text-sm font-bold uppercase tracking-[0.14em]">
          {pendingLabel}
        </span>
      </div>
      <picture>
        <source
          type="image/avif"
          srcSet={`/images/home/${name}-640.avif 640w, /images/home/${name}-960.avif 960w, /images/home/${name}-1440.avif 1440w`}
          sizes={sizes}
        />
        <source
          type="image/webp"
          srcSet={`/images/home/${name}-640.webp 640w, /images/home/${name}-960.webp 960w, /images/home/${name}-1440.webp 1440w`}
          sizes={sizes}
        />
        <img
          src={`/images/home/${name}-960.jpg`}
          alt={alt}
          width={width}
          height={height}
          loading={name === "hero" ? "eager" : "lazy"}
          decoding="async"
          onLoad={() => setLoaded(true)}
          onError={(event) => {
            event.currentTarget.hidden = true;
          }}
          className={`absolute inset-0 h-full w-full object-cover transition-opacity duration-300 ${
            loaded ? "opacity-100" : "opacity-0"
          }`}
        />
      </picture>
    </div>
  );
}

export default HomePhoto;
