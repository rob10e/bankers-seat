import {
  Card,
  CardContent,
  FormControl,
  FormControlLabel,
  MenuItem,
  Radio,
  RadioGroup,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import {
  type ThemeModePreference,
  useAppSettingsStore,
} from "../../app/app-settings-store.ts";

export function SettingsView() {
  const themeMode = useAppSettingsStore((state) => state.themeMode);
  const setThemeMode = useAppSettingsStore((state) => state.setThemeMode);
  const snapshotRefreshMs = useAppSettingsStore((state) => state.snapshotRefreshMs);
  const setSnapshotRefreshMs = useAppSettingsStore(
    (state) => state.setSnapshotRefreshMs,
  );

  return (
    <Stack spacing={2.5} className="app-page">
      <Typography variant="h4">App settings</Typography>
      <Card>
        <CardContent>
          <Stack spacing={3}>
            <FormControl>
              <Typography variant="h6" sx={{ mb: 1 }}>
                Theme mode
              </Typography>
              <RadioGroup
                value={themeMode}
                onChange={(event) =>
                  setThemeMode(event.target.value as ThemeModePreference)
                }
              >
                <FormControlLabel value="system" control={<Radio />} label="System" />
                <FormControlLabel value="light" control={<Radio />} label="Light" />
                <FormControlLabel value="dark" control={<Radio />} label="Dark" />
              </RadioGroup>
            </FormControl>

            <FormControl>
              <TextField
                select
                label="Session refresh interval"
                value={snapshotRefreshMs}
                onChange={(event) => setSnapshotRefreshMs(Number(event.target.value))}
                helperText="How often the game workspace refreshes snapshot data."
              >
                <MenuItem value={2000}>2 seconds</MenuItem>
                <MenuItem value={3000}>3 seconds</MenuItem>
                <MenuItem value={5000}>5 seconds</MenuItem>
                <MenuItem value={10000}>10 seconds</MenuItem>
              </TextField>
            </FormControl>
          </Stack>
        </CardContent>
      </Card>
    </Stack>
  );
}
