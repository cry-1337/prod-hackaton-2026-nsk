import type { Config } from "tailwindcss";

export default {
    content: ["./index.html", "./src/**/*.{ts,tsx}"],
    theme: {
        extend: {
            colors: {
                ink: "#18372b",
                muted: "#58675c",
                line: "#dce1d7",
                paper: "#fafbf7",
                leaf: "#286046",
                citrus: "#ff763e",
                sage: "#e2eedf"
            },
            fontFamily: {
                sans: ["Rubik Variable", "Arial", "sans-serif"],
                editorial: ["PT Serif", "Georgia", "serif"]
            }
        }
    }
} satisfies Config;
