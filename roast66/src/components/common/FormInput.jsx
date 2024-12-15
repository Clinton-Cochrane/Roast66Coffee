import React from "react";
import PropTypes from "prop-types";

const FormInput = ({
  type = "text",
  name,
  placeholder,
  value,
  onChange,
  required = false,
}) => {
  return (
    <input
      type={type}
      name={name}
      placeholder={placeholder}
      value={value}
      onChange={onChange}
      required={required}
      className="w-full p-2 border rounded mb-2"
    />
  );
};

FormInput.propTypes = {
  type: PropTypes.string,
  name: PropTypes.string.isRequired, 
  placeholder: PropTypes.string,
  value: PropTypes.oneOfType([PropTypes.string, PropTypes.number]).isRequired,
  onChange: PropTypes.func.isRequired,
  required: PropTypes.bool,
};

export default FormInput;
