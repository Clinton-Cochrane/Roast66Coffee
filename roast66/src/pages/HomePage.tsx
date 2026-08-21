import React from "react";
import Welcome from "../components/Customer/Welcome";
import About from "../components/Customer/About";
import Location from "../components/Customer/Location";
import FeaturedSpecials from "../components/Customer/FeaturedSpecials";

function HomePage() {
  return (
    <div className="flex flex-col items-center">
      <Welcome />
      <div className="w-full max-w-6xl px-3 pb-6 sm:px-6">
        <FeaturedSpecials />
        <About />
        <div className="mt-6">
          <Location />
        </div>
      </div>
    </div>
  );
}

export default HomePage;
