import {
  Button,
  Card,
  CardContent,
  Grid,
  Stack,
  Typography,
} from "@mui/material";
import { Link as RouterLink } from "react-router-dom";

export function HomeView() {
  return (
    <Stack spacing={2.5} className="app-page">
      <Typography variant="h4">Tabletop banker companion</Typography>
      <Typography color="text.secondary">
        Start a game from a validated template, join by room code, and keep balances
        and ledger history visible for everyone at the table.
      </Typography>

      <Grid container spacing={2}>
        <Grid size={{ xs: 12, sm: 6 }}>
          <Card>
            <CardContent>
              <Stack spacing={1.5}>
                <Typography variant="h5">Host a new game</Typography>
                <Typography color="text.secondary">
                  Pick a template and configure a session.
                </Typography>
                <Button component={RouterLink} to="/templates" variant="contained">
                  Browse templates
                </Button>
              </Stack>
            </CardContent>
          </Card>
        </Grid>
        <Grid size={{ xs: 12, sm: 6 }}>
          <Card>
            <CardContent>
              <Stack spacing={1.5}>
                <Typography variant="h5">Join a room</Typography>
                <Typography color="text.secondary">
                  Enter a room code and reconnect details.
                </Typography>
                <Button component={RouterLink} to="/join" variant="outlined">
                  Join session
                </Button>
              </Stack>
            </CardContent>
          </Card>
        </Grid>
      </Grid>
    </Stack>
  );
}
