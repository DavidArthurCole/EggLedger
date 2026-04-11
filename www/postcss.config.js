const path = require('node:path');

module.exports = {
  plugins: {
    'tailwindcss/nesting': {},
    tailwindcss: { config: path.resolve(__dirname, 'tailwind.config.js') },
    autoprefixer: {},
  },
};
