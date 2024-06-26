const colors = require('tailwindcss/colors');

module.exports = {
  content: [
    './www/*.html',
    './www/**/*.html',
    './www/templates/*.vue',
    './www/**/*.vue',
  ],
  theme: {
    extend: {
      colors: {
        white: "#ffffff",
        blue: colors.blue,
        green: colors.emerald,
        'duration-0': "rgb(69, 159, 246)",
        'duration-1': "rgb(139, 93, 246)",
        'duration-2': "rgb(246, 168, 35)",
        'duration-3': "rgb(115, 128, 140)",
        goldenstar: "rgb(255, 215, 0)",
        'yellow-700': "rgb(251 191 36)", 
        shortdarker: "rgb(48, 111, 171)",
        'rarity-0': 'rgb(156 163 175)',
        'rarity-1': "#6ab6ff",
        'rarity-2': '#c03fe2',
        'rarity-3': '#eeab42',
        dubcap: 'rgb(173, 10, 198)',
        dubcapdarker: 'rgb(120, 7, 138)',
        selectedmission: 'rgb(10, 173, 82)',
        selectedmissiondarker: 'rgb(7, 120, 56)',
        buggedcap: 'rgb(198, 10, 10)',
        buggedcapdarker: 'rgb(138, 7, 7)',
      },
      zIndex: {
        '3': '3',
        '_20': '-20',
      },
      height: {
        'stretch': 'stretch',
        '1rem': '1rem',
        'half': '50%',
        'half-vh': '50vh',
        '9/10': '90%',
      },
      minHeight: {
        '6': '1.5rem',
        '7': '1.75rem',
      },
      maxHeight: {
        '6': '1.5rem',
        '9': '2.25rem',
        '40': '10rem',
        '50': '12.5rem',
        '6/10': '60%',
        '60vh': '60vh',
        '80vh': '80vh',
        '90vh': '90vh',
      },
      width: {
        'onedig': '1rem',
        'twodig': '1.25rem',
        'threedig': '1.6rem',
        'fourdig': '2rem',
        'fivedig': '2.35rem',
        'sixdig': '2.7rem',
        'sevendig': '3rem',
        '9/10': '90%',
      },
      minWidth: {
        '6': '1.5rem',
        '15vw': '15vw',
        '30vw': '30vw',
        '50vw': '50vw',
        '70vw': '70vw',
      },
      maxWidth: {
        '1/3': '33.333333%',
        'half': '50%',
        '6': '1.5rem',
        '5rem': '5rem',
        '10rem': '10rem',
        '28vw': '28vw',
        '30vw': '30vw',
        '50vw': '50vw',
        '70vw': '70vw',
        '90vw': '90vw',
        '9/10': '90%',
        '32': '8rem',
      },
      lineHeight: {
        '1rem': '1rem'
      },
      spacing: {
        '0i': '0 !important',
        '0_25rem': '0.25rem',
        '0_5rem': '0.5rem',
        '0_75rem': '0.75rem',
        '1rem': '1rem',
        '2rem': '2rem',
        '3rem': '3rem',
        '7rem': '7rem',
        '70vw': '70vw',
        '1/4': '25%',
        '1/3': '33.333333%',
      },
      margin: {
        '0i': '0 !important',
        '0_25rem': '0.25rem !important',
        '0_75rem': '0.75rem',
        '0_5rem': '0.5rem',
        '1_5rem': '1.5rem',
        '2rem': '2rem',
        '2_5rem': '2.5rem',
      },
      padding: {
        '0': '0rem',
        '0_5rem': '0.5rem',
        '1rem:' : '1rem',
        '2rem': '2rem',
        'select': '2.5rem',
        '7rem': '7rem',
        '6': '24px',
      },
      fontSize: {
        'star': '1.4rem',
        'xl': ['1.125rem', {
          lineHeight: '1.75rem',
        }],
      },
      backgroundColor: {
        'white': '#ffffff',
        'dark': '#393b40',
        'darkerthandark': '#33353a',
        'darker': '#242629',
        'darkerer': "#1c1d1f",
        'darkest': '#151617',
        'darkester': '#0e0f10',

        'dark_tab': "#323633",
        'darker_tab': "#262927",
        'dark_tab_hover': "#3a3d3a",
        'darker_tab_hover': "#2e312e",
        'privacy_blue': "#276ec8",
        'data_loss_red': "#820808",

        'upgrade_green': '#1c802e',
        'upgrade_green_hover': '#155e22',
        'rarity-0': '#443e45'
      },
      backgroundImage: {
        'rarity-1': 'radial-gradient(#a8dfff,#8dd5ff,#3a9dfc)',
        'rarity-2': 'radial-gradient(#ce81f7,#b958ed,#8819c2)',
        'rarity-3': 'radial-gradient(#fcdd6a, #ffdb58, #e09143)',
      },
      borderColor: {
        "dark_tab": "#111211",
        'red-700': "rgb(185 28 28)",
        'yellow-700': "rgb(251 191 36)",
        'rare': '#6ab6ff',
        'epic': '#c03fe2',
        'legendary': '#eeab42',
        'tutorial': "rgb(115, 128, 140)",
        'short': "rgb(69, 159, 246)",
        'standard': "rgb(139, 93, 246)",
        'extended': "rgb(246, 168, 35)",
        'upgrade_green': "#155e22",
        'upgrade_green_hover': "#114f1c",
      },
    },
  },
  plugins: [require('@tailwindcss/forms')],
};
