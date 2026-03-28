import React from "react";
import Welcome from "../components/Customer/Welcome";
import About from "../components/Customer/About";
import Location from "../components/Customer/Location";

function HomePage() {
  return (
    <main className="flex flex-col items-center px-3 py-6">
      <div className="w-full max-w-3xl">
        <Welcome />
      </div>
      <div className="mt-4 w-full max-w-3xl">
        <About />
      </div>
      <div className="mt-4 w-full max-w-3xl">
        <Location />
      </div>
    </main>
  );
}

export default HomePage;
