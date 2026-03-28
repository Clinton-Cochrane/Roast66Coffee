import React, { type ChangeEvent, type InputHTMLAttributes } from "react";

export type FormInputProps = {
  id?: string;
  label?: string;
  type?: string;
  name?: string;
  placeholder?: string;
  value: string | number;
  onChange: (e: ChangeEvent<HTMLInputElement>) => void;
  required?: boolean;
  className?: string;
} & Omit<InputHTMLAttributes<HTMLInputElement>, "value" | "onChange">;

const FormInput = ({
  id,
  label,
  type = "text",
  name,
  placeholder,
  value,
  onChange,
  required = false,
  className = "",
  ...rest
}: FormInputProps) => {
  const inputId = id || name;

  return (
    <div className="mb-2">
      {label ? (
        <label htmlFor={inputId} className="block text-sm font-semibold text-[#4a3326] mb-1">
          {label}
        </label>
      ) : null}
      <input
        id={inputId}
        type={type}
        name={name}
        placeholder={placeholder}
        value={value}
        onChange={onChange}
        required={required}
        className={`w-full p-2 border border-[#cbb8a8] rounded-md bg-[#fffaf3] text-[#2f2621] placeholder:text-[#8b7768] focus:outline-none focus:ring-2 focus:ring-[#99bfdd] ${className}`.trim()}
        {...rest}
      />
    </div>
  );
};

export default FormInput;
