// src/components/Customer/Welcome.jsx
import React from 'react';
import Card from '../common/Card';
import logo from "../../logo.png"; // Adjust path if necessary


function Welcome() {
  return (
    <div className=" bg-gray-100 flex items-center justify-center">
      <Card title="Welcome to Roast 66">
        <img src={logo} alt="Roast 66 Coffee Logo" className='w-64 mx-auto my-4'/>
        <p className="text-lg text-gray-700">Your favorite coffee shop</p>
      </Card>
    </div>
  );
}

export default Welcome;
