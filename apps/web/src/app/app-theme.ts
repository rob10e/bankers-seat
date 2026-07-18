import { createTheme } from "@mui/material";

export const createAppTheme = (mode: "light" | "dark") => {
  return createTheme({
    palette: {
      mode,
      primary: {
        main: "#4F46E5",
      },
      background:
        mode === "light"
          ? {
              default: "#F8FAFC",
              paper: "#FFFFFF",
            }
          : {
              default: "#0F172A",
              paper: "#111827",
            },
    },
    shape: {
      borderRadius: 12,
    },
    typography: {
      fontFamily: '"Segoe UI", "Roboto", "Helvetica", "Arial", sans-serif',
      h4: {
        fontSize: "1.5rem",
        fontWeight: 700,
      },
      h5: {
        fontSize: "1.2rem",
        fontWeight: 700,
      },
    },
  });
};
