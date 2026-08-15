import classNames from "classnames";
import React, { type ReactNode } from "react";

type CardProps = {
  title?: string;
  children: ReactNode;
  className?: string;
  tone?: "default" | "special" | "drink";
};

const toneClasses = {
  default: "border-[#dccdbe] bg-[#fffaf3]/[0.92]",
  special: "border-[#c77e42] bg-[#fffaf4]",
  drink: "border-[#4a3326] bg-[#eadfd3]",
} as const;

const Card = ({ title, children, className, tone = "default" }: CardProps) => {
  return (
    <div
      className={classNames(
        "w-full rounded-[14px] border p-5 shadow-[0_10px_24px_rgba(54,33,19,0.12)] transition-[box-shadow,transform] duration-200 motion-safe:hover:-translate-y-px hover:shadow-[0_14px_30px_rgba(54,33,19,0.14)]",
        toneClasses[tone],
        className
      )}
    >
      {title ? (
        <h2 className="text-[1.35rem] font-bold mb-2 tracking-[0.01em] text-[#4a3326]">{title}</h2>
      ) : null}
      {children}
    </div>
  );
};

export default Card;
