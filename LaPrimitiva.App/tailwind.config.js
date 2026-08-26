/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ["./Components/**/*.{razor,html,cs}"],
  theme: {
    extend: {
      fontFamily: {
        sans: ["Poppins", "system-ui", "sans-serif"]
      }
    }
  },
  plugins: []
};
