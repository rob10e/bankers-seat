import { Button, Box, Alert, Snackbar } from '@mui/material';
import CloudDownloadIcon from '@mui/icons-material/CloudDownload';
import { usePwaInstall } from './use-pwa';

/**
 * Install prompt component - displays when app can be installed
 */
export function InstallPrompt() {
  const { canInstall, isInstalled, install } = usePwaInstall();

  if (!canInstall || isInstalled) {
    return null;
  }

  return (
    <Snackbar
      open={canInstall}
      anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      autoHideDuration={null}
    >
      <Alert severity="info" sx={{ width: '100%', gap: 1 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <CloudDownloadIcon />
          <span>Install Banker's Seat for quick access</span>
        </Box>
        <Box sx={{ mt: 1 }}>
          <Button
            variant="contained"
            size="small"
            onClick={install}
            sx={{ mr: 1 }}
          >
            Install
          </Button>
          <Button
            variant="text"
            size="small"
            onClick={() => {}}
          >
            Dismiss
          </Button>
        </Box>
      </Alert>
    </Snackbar>
  );
}
