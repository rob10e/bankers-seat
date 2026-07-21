import { Box, Alert, AlertTitle, Button, Snackbar } from '@mui/material';
import WifiOffIcon from '@mui/icons-material/WifiOff';
import UpdateIcon from '@mui/icons-material/Update';
import { usePwaStatus } from './use-pwa';

/**
 * Offline indicator - shows when connection is lost
 */
export function OfflineIndicator() {
  const { isOnline } = usePwaStatus();

  if (isOnline) {
    return null;
  }

  return (
    <Box
      sx={{
        position: 'fixed',
        top: 0,
        left: 0,
        right: 0,
        zIndex: 1400,
        display: 'flex',
        alignItems: 'center',
        gap: 1,
        px: 2,
        py: 1,
        backgroundColor: '#ff9800',
        color: 'white',
        fontSize: '0.875rem',
      }}
    >
      <WifiOffIcon sx={{ fontSize: '1.2rem' }} />
      <span>You are offline. Some features may be limited.</span>
    </Box>
  );
}

/**
 * Update available prompt - notifies when a new version is ready
 */
export function UpdatePrompt() {
  const { updateAvailable, applyUpdate } = usePwaStatus();

  return (
    <Snackbar
      open={updateAvailable}
      anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      autoHideDuration={null}
    >
      <Alert severity="warning" sx={{ width: '100%', display: 'flex', gap: 1 }}>
        <UpdateIcon />
        <Box sx={{ flex: 1 }}>
          <AlertTitle>Update available</AlertTitle>
          <span>A new version of Banker's Seat is ready.</span>
        </Box>
        <Button
          variant="contained"
          size="small"
          onClick={applyUpdate}
          sx={{ ml: 2 }}
        >
          Update
        </Button>
      </Alert>
    </Snackbar>
  );
}
