import {
  AppBar,
  Button,
  BottomNavigation,
  BottomNavigationAction,
  Box,
  Chip,
  Container,
  Stack,
  Tab,
  Tabs,
  useMediaQuery,
  useTheme,
  Toolbar,
  Typography,
} from "@mui/material";
import type { ReactNode } from "react";
import { useLocation, useNavigate } from "react-router-dom";

interface AppShellProps {
  readonly children: ReactNode;
}

type NavRoute = {
  readonly label: string;
  readonly value: string;
  readonly icon: ReactNode;
};

const navRoutes: readonly NavRoute[] = [
  { label: "Home", value: "/", icon: <span aria-hidden="true">⌂</span> },
  { label: "Templates", value: "/templates", icon: <span aria-hidden="true">▦</span> },
  { label: "Join", value: "/join", icon: <span aria-hidden="true">↪</span> },
  { label: "Game", value: "/game/demo", icon: <span aria-hidden="true">▶</span> },
  { label: "Settings", value: "/settings", icon: <span aria-hidden="true">⚙</span> },
];

const toNavValue = (pathname: string): string => {
  if (pathname.startsWith("/templates")) {
    return "/templates";
  }
  if (pathname.startsWith("/join")) {
    return "/join";
  }
  if (pathname.startsWith("/game")) {
    return "/game/demo";
  }
  if (pathname.startsWith("/settings")) {
    return "/settings";
  }
  return "/";
};

export function AppShell({ children }: AppShellProps) {
  const theme = useTheme();
  const isDesktop = useMediaQuery(theme.breakpoints.up("md"));
  const navigate = useNavigate();
  const location = useLocation();
  const selectedNavValue = toNavValue(location.pathname);

  return (
    <Box
      sx={{
        minHeight: "100vh",
        backgroundColor: "background.default",
        pb: isDesktop ? 0 : 8,
      }}
    >
      <AppBar position="sticky" color="inherit" elevation={0}>
        <Toolbar
          sx={{
            borderBottom: 1,
            borderColor: "divider",
            justifyContent: "space-between",
            gap: 2,
          }}
        >
          <Stack spacing={0.4}>
            <Typography variant="h6" sx={{ fontWeight: 700 }}>
              Banker&apos;s Seat
            </Typography>
            <Typography variant="caption" color="text.secondary">
              Server-authoritative session companion
            </Typography>
          </Stack>

          {isDesktop ? (
            <Tabs
              value={selectedNavValue}
              onChange={(_, value: string) => navigate(value)}
              aria-label="Main navigation"
            >
              {navRoutes.map((route) => (
                <Tab key={route.value} value={route.value} label={route.label} />
              ))}
            </Tabs>
          ) : null}

          <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
            <Chip label="Planning UI" size="small" color="primary" variant="outlined" />
            {isDesktop ? (
              <Button
                variant="outlined"
                size="small"
                onClick={() => navigate("/settings")}
              >
                Settings
              </Button>
            ) : null}
          </Stack>
        </Toolbar>
      </AppBar>

      <Container maxWidth="md" sx={{ py: 2.5 }}>
        {children}
      </Container>

      {!isDesktop ? (
        <Box
          sx={{
            position: "fixed",
            left: 0,
            right: 0,
            bottom: 0,
            borderTop: 1,
            borderColor: "divider",
            bgcolor: "background.paper",
          }}
        >
          <BottomNavigation
            value={selectedNavValue}
            onChange={(_, value: string) => navigate(value)}
            showLabels
          >
            {navRoutes.map((route) => (
              <BottomNavigationAction
                key={route.value}
                value={route.value}
                icon={route.icon}
                label={route.label}
              />
            ))}
          </BottomNavigation>
        </Box>
      ) : null}
    </Box>
  );
}
