/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./index.html", "./src/**/*.{js,ts,jsx,tsx}"],
  theme: {
    extend: {
      colors: {
        primary: "#a94727",
        secondary: "#4a3326",
        accent: "#4d6f8a",
        dark: "#2c1d1c",
        light: "#f4ece1",
      },
      fontFamily: {
        sans: ['"Source Sans 3 Variable"', '"Segoe UI"', "Arial", "sans-serif"],
        display: ['"Barlow Condensed"', '"Arial Narrow"', "sans-serif"],
      },
    },
  },
  plugins: [],
};
