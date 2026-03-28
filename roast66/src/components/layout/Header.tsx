import React from "react";

type HeaderProps = {
  title: string;
  color?: string;
};

const Header = ({ title, color = "bg-[#4a3326]" }: HeaderProps) => {
  return (
    <header className={`${color} text-[#f7efe6] py-4 text-center border-b border-[#dccdbe]`}>
      <h1 className="text-3xl font-bold tracking-wide">{title}</h1>
    </header>
  );
};

export default Header;
