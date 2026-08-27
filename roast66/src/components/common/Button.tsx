import React, { type ReactNode } from "react";

const colorClasses = {
  blue: "bg-[#4d6f8a] text-white hover:bg-[#3d5b73] hover:text-white",
  green: "bg-[#4a3326] text-white hover:bg-[#2c1d15] hover:text-white",
  red: "bg-[#a94727] text-white hover:bg-[#893b1f] hover:text-white",
  yellow: "bg-[#c77e42] text-black hover:bg-[#aa6935] hover:text-black",
  gray: "bg-[#7b6d62] text-white hover:bg-[#66584f] hover:text-white",
} as const;

const linkTextClasses = {
  blue: "text-[#4d6f8a] hover:text-[#3d5b73]",
  green: "text-[#4a3326] hover:text-[#2c1d15]",
  red: "text-[#a94727] hover:text-[#893b1f]",
  yellow: "text-[#c77e42] hover:text-[#aa6935]",
  gray: "text-[#7b6d62] hover:text-[#66584f]",
} as const;

type ColorKey = keyof typeof colorClasses;

const solidButtonClasses =
  "py-2 px-4 rounded-md font-semibold tracking-wide shadow-[0_2px_0_rgba(0,0,0,0.16)] transition-[background-color,color,box-shadow,transform] duration-150 motion-safe:hover:-translate-y-[1px] hover:shadow-[0_5px_14px_rgba(74,51,38,0.24)] active:translate-y-0 active:shadow-[0_1px_0_rgba(0,0,0,0.16)] focus:outline-none focus:ring-2 focus:ring-[#99bfdd] focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60 disabled:hover:translate-y-0 disabled:hover:shadow-[0_2px_0_rgba(0,0,0,0.16)]";

const linkButtonClasses =
  "bg-transparent py-0.5 px-0 rounded-sm font-semibold tracking-wide underline underline-offset-[0.2em] shadow-none transition-colors duration-150 focus:outline-none focus:ring-2 focus:ring-[#99bfdd] focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-60";

type ButtonProps = {
  children: ReactNode;
  onClick?: () => void;
  type?: "button" | "submit" | "reset";
  color?: ColorKey;
  variant?: "solid" | "link";
  disabled?: boolean;
  className?: string;
};

const Button = ({
  children,
  onClick,
  type = "button",
  color = "blue",
  variant = "solid",
  disabled = false,
  className = "",
}: ButtonProps) => {
  const isLink = variant === "link";
  const colorStyles = isLink ? linkTextClasses[color] : colorClasses[color];
  const shapeStyles = isLink ? linkButtonClasses : solidButtonClasses;

  return (
    <button
      type={type}
      onClick={onClick}
      className={`${colorStyles} ${shapeStyles} ${className}`.trim()}
      disabled={disabled}
    >
      {children}
    </button>
  );
};

export default Button;
