const colors = require('tailwindcss/colors');
module.exports = {
  important: '#body',
  purge: {
    enabled: false,
    content: [
      './../../Assets/Lava/**/*.lava',
      './../../Layouts/*.aspx',
      './../../Layouts/Site.Master'
    ]
  },
  prefix: 'tw-',
  darkMode: false, // or 'media' or 'class'
  theme: {
    screens: {
      xs: "320px",
      sm: "481px",
      md: "769px",
      lg: "993px",
      xl: "1201px"
    },
    fontSize: {
      'xs':   '1.75rem',
      'sm':   '1.875rem',
      'tiny': '1.875rem',
      'base': '2rem',
      'lg':   '2.125rem',
      'xl':   '2.25rem',
      '2xl':  '2.5rem',
      '3xl':  '2.875rem',
      '4xl':  '3.25rem',
      '5xl':  '4rem',
      '6xl':  '5rem',
      '7xl':  '6rem',
    },
    extend: {
        minWidth: {
          '0': '0',
          '50': '50px',
          '100': '100px',
          '250': '250px',
          '500': '500px',
        },
        minHeight: {
          '0': '0',
          '50': '50px',
          '100': '100px',
          '250': '250px',
          '500': '500px',
        },
        height: theme => ({
          "screen-90": "90vh",
          "screen-80": "80vh",
          "screen-75": "75vh",
          "screen/2": "50vh",
          "screen/3": "calc(100vh / 3)",
          "screen/4": "calc(100vh / 4)",
          "screen/5": "calc(100vh / 5)",
        }),
        colors: {
          'c-primary': 'rgba(var(--primary))',
          'c-primary-100': 'rgba(var(--primary),0.1)',
          'c-primary-200': 'rgba(var(--primary),0.2)',
          'c-primary-300': 'rgba(var(--primary),0.3)',
          'c-primary-400': 'rgba(var(--primary),0.4)',
          'c-primary-500': 'rgba(var(--primary),0.5)',
          'c-primary-600': 'rgba(var(--primary),0.6)',
          'c-primary-700': 'rgba(var(--primary),0.7)',
          'c-primary-800': 'rgba(var(--primary),0.8)',
          'c-primary-900': 'rgba(var(--primary),0.9)',
          'c-secondary': 'rgba(var(--secondary))',
          'c-secondary-100': 'rgba(var(--secondary),0.1)',
          'c-secondary-200': 'rgba(var(--secondary),0.2)',
          'c-secondary-300': 'rgba(var(--secondary),0.3)',
          'c-secondary-400': 'rgba(var(--secondary),0.4)',
          'c-secondary-500': 'rgba(var(--secondary),0.5)',
          'c-secondary-600': 'rgba(var(--secondary),0.6)',
          'c-secondary-700': 'rgba(var(--secondary),0.7)',
          'c-secondary-800': 'rgba(var(--secondary),0.8)',
          'c-secondary-900': 'rgba(var(--secondary),0.9)',
        }
    },
  },
  variants: {
    extend: {},
  },
  plugins: [
    require("tailwindcss-debug-screens"),
    function ({ addUtilities, theme, variants }) {
      const fn = (prefix, key, prop, opacity) => {
        const t = theme(prop);

        addUtilities(Object.keys(t)
          .reduce((_o, _k) => ({
            ..._o,
            ...Object.keys(t[_k])
              .filter(x => /^rgb\(.*\)$/i.test(t[_k][x]))
              .reduce((o, k) => ({
                ...o,
                [`.${prefix}-${_k}-${k}`]: {
                  [opacity]: '1',
                  [key]: t[_k][k].replace(/^rgb\((.*)\)$/i, `rgba($1, var(${opacity})) !important`)
                }
              }), {})
          }), {}), {
          respectImportant: false,
          variants: variants(prop)
        });
      }

      // add more utils here...
      fn('bg', 'background-color', 'backgroundColor', '--tw-bg-opacity');
      fn('text', 'color', 'textColor', '--tw-text-opacity');
    }
  ],
}
