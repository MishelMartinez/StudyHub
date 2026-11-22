/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        blush: '#F2AEB4',
        rose: { DEFAULT: '#D95F98', dark: '#B9467C' },
        night: { DEFAULT: '#221A40', soft: '#3A2F63' },
        amber: { DEFAULT: '#F29422' },
        coral: { DEFAULT: '#D9483B' },
      },
      fontFamily: { sans: ['Inter', 'system-ui', 'Segoe UI', 'Roboto', 'sans-serif'] },
    },
  },
  plugins: [],
}
