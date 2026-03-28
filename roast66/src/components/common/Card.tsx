import classNames from "classnames";
import React, { type ReactNode } from "react";

type CardProps = {
  title?: string;
  children: ReactNode;
  className?: string;
};

const Card = ({ title, children, className }: CardProps) => {
  return (
    <div className={classNames("r66-panel p-5 w-full", className)}>
      {title ? (
        <h2 className="text-[1.35rem] font-bold mb-2 tracking-[0.01em] text-[#4a3326]">{title}</h2>
      ) : null}
      {children}
    </div>
  );
};

export default Card;
