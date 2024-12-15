// src/components/Customer/About.jsx
import React from 'react';
import Card from '../common/Card';

function About() {
  return (
    <div >
      <Card className="min-h-40" title="About Us">
        <p className="mb-4">
          Roast 66 Coffee is a unique coffee shop on wheels, bringing your favorite coffee directly to you! Whether you are at home, at work, or on the go, we can deliver freshly brewed coffee to your location, given enough time and depending on the drink.
        </p>
        <p>
          Our owner has always had a passion for cars and coffee. Combining these two loves, she created Roast 66 Coffee as a dream come true. Our coffee trailer roams the streets, serving up delicious beverages and bringing joy to coffee lovers everywhere. Come and experience the magic of cars and coffee with us!
        </p>
      </Card>
    </div>
  );
}

export default About;
