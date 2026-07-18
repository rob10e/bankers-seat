import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ThemeProvider } from "@mui/material";
import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { App } from "../App.tsx";
import { createAppTheme } from "./app-theme.ts";

describe("App", () => {
  it("renders home view content", () => {
    const queryClient = new QueryClient();

    render(
      <ThemeProvider theme={createAppTheme("light")}>
        <QueryClientProvider client={queryClient}>
          <MemoryRouter initialEntries={["/"]}>
            <App />
          </MemoryRouter>
        </QueryClientProvider>
      </ThemeProvider>,
    );

    expect(screen.getByText("Banker's Seat")).toBeInTheDocument();
    expect(screen.getByText("Host a new game")).toBeInTheDocument();
    expect(screen.getByText("Join a room")).toBeInTheDocument();
  });
});
