import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { AppProviders } from "./app/app-providers.tsx";
import "./index.css";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <AppProviders />
  </StrictMode>
);
