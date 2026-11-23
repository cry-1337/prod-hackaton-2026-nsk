import { defineConfig, loadEnv } from "vite";
import react from "@vitejs/plugin-react-swc";

export default defineConfig(({ mode }) => {
    const { API_PROXY_TARGET } = loadEnv(mode, process.cwd(), "API_");
    const proxy = { "/api": { target: API_PROXY_TARGET || "http://127.0.0.1:5080", changeOrigin: true, ws: true } };

    return {
        plugins: [react()],
        server: { host: "0.0.0.0", proxy },
        preview: { proxy }
    };
});
