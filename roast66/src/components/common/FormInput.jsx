import React from "react";
import PropTypes from "prop-types";

const FormInput = ({type = "text", placeholder, value, onChange, required = false}) => {
    return (
        <input type={type} placeholder={placeholder} value = {value} onChange={onChange} required = {required} className="w-full p-2 border rounded mb-2" />
    )
}

FormInput.propTypes = {
    type: PropTypes.string,
    placeholder: PropTypes.string,
    value: PropTypes.string,
    onChange: PropTypes.func.isRequired,
    required: PropTypes.bool,
}

export default FormInput;