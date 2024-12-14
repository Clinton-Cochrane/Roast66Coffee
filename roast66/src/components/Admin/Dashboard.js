import React from 'react';
import Header from '../components/layout/Header';
import Card from '../components/common/Card';

function Dashboard() {
  return (
    <div className="min-h-screen bg-gray-100 p-6">
      <Header title="Admin Dashboard" />
      <Card>
        <p>Welcome to the admin panel</p>
      </Card>
    </div>
  );
}

export default Dashboard;
