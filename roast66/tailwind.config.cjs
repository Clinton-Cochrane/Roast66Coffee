/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./index.html", "./src/**/*.{js,ts,jsx,tsx}"],
  theme: {
    extend: {
      colors: {
        primary: "#8B4513",
        secondary: "#ad8453",
        accent: "#5DADE2",
        dark: "#333333",
        light: "#AAAAAA",
      },
    },
  },
  plugins: [],
};
