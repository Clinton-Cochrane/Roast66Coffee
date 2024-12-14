/* eslint-disable no-undef */
/** @type {import('tailwindcss').Config} */
/* eslint-env node */
// tailwind.config.js
module.exports = {
  content: [
    "./src/**/*.{js,jsx,ts,tsx}",
    "./public/index.html",
  ],
  theme: {
    extend: {
      colors: {
        primary: "#8B4513",     // Rich brown (Coffee)
        secondary: "#ad8453",   // Coffee with creamer (Tan)
        accent: "#5DADE2",      // Baby blue
        dark: "#333333",        // Dark gray for text
        light: "#AAAAAA",       // Light gray for subtle elements
      },
    },
  },
  plugins: [],
};
