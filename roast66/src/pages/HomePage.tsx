import React from "react";
import Welcome from "../components/Customer/Welcome";
import FeaturedSpecials from "../components/Customer/FeaturedSpecials";
import HomeMarketingLocation from "../components/Customer/HomeMarketingLocation";

function HomePage() {
  return (
    <div className="flex flex-col items-center">
      <Welcome />
      <FeaturedSpecials />
      <div className="r66-home-connect-wrap">
        <HomeMarketingLocation />
      </div>
    </div>
  );
}

export default HomePage;
