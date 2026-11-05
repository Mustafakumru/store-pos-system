/** @type {import('tailwindcss').Config} */
module.exports = {
    content: [
        "./src/**/*.{html,ts}",
    ],
    theme: {
        extend: {},
    },
    // PrimeNG kendi prefix'lerini kullandığı için çakışma olmasın
    corePlugins: {
        preflight: false
    },
    plugins: [],
}