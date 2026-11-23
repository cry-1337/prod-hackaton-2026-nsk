import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter } from "react-router-dom";
import "@fontsource-variable/rubik";
import "@fontsource/pt-serif/cyrillic-400-italic.css";
import "@fontsource/pt-serif/latin-400-italic.css";
import { inPractice } from "./practice";
import App from "./App";
import { queries } from "./data";
import "./index.css";

createRoot(document.getElementById("root")!).render(
    <StrictMode>
        <QueryClientProvider client={queries}>
            <BrowserRouter basename={inPractice() ? "/practice" : undefined} future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
                <App />
            </BrowserRouter>
        </QueryClientProvider>
    </StrictMode>
);
