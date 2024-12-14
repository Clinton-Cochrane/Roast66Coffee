// src/components/Customer/Welcome.jsx
import React from 'react';
import Card from '../common/Card';

function Welcome() {
  return (
    <div className="min-h-screen bg-gray-100 flex items-center justify-center p-6">
      <Card title="Welcome to Roast 66">
        <p className="text-lg text-gray-700">Your favorite coffee shop</p>
      </Card>
    </div>
  );
}

export default Welcome;
