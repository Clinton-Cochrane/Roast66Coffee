import React from "react";
import About from "../components/Customer/About";
import Welcome from "../components/Customer/Welcome";
import Location from "../components/Customer/Location";

function HomePage() {
  return (
    <main className="p-6 space-y-6">
      <Welcome />
      <About />
      <Location />
    </main>
  );
}

export default HomePage;
