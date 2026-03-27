import React from "react";
import PropTypes from "prop-types";

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
}) => {
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

FormInput.propTypes = {
  id: PropTypes.string,
  label: PropTypes.string,
  type: PropTypes.string,
  name: PropTypes.string,
  placeholder: PropTypes.string,
  value: PropTypes.oneOfType([PropTypes.string, PropTypes.number]).isRequired,
  onChange: PropTypes.func.isRequired,
  required: PropTypes.bool,
  className: PropTypes.string,
};

export default FormInput;
