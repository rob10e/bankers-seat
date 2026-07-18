import {
  Alert,
  Button,
  Card,
  CardContent,
  Divider,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { useMemo } from "react";
import { Link as RouterLink, useNavigate } from "react-router-dom";
import { toTemplateKey } from "../../domain/template-catalog.ts";
import { useUiSessionStore } from "../session/ui-session-store.ts";
import { useCreateSessionMutation } from "../session/use-session-mutations.ts";
import { useTemplateCatalogQuery } from "../templates/use-template-catalog-query.ts";

export function HostSetupView() {
  const navigate = useNavigate();
  const selectedTemplateKey = useUiSessionStore((state) => state.selectedTemplateKey);
  const setSelectedTemplateKey = useUiSessionStore((state) => state.setSelectedTemplateKey);
  const hostDisplayName = useUiSessionStore((state) => state.hostDisplayName);
  const setHostDisplayName = useUiSessionStore((state) => state.setHostDisplayName);
  const setActiveSession = useUiSessionStore((state) => state.setActiveSession);
  const createSessionMutation = useCreateSessionMutation();
  const catalogQuery = useTemplateCatalogQuery();

  const selectedEntry = useMemo(() => {
    if (!catalogQuery.data || catalogQuery.data.length === 0) {
      return null;
    }

    return (
      catalogQuery.data.find((entry) => toTemplateKey(entry) === selectedTemplateKey) ??
      catalogQuery.data[0]
    );
  }, [catalogQuery.data, selectedTemplateKey]);

  return (
    <Stack spacing={2.5} className="app-page">
      <Typography variant="h4">Host setup</Typography>
      {createSessionMutation.isError ? (
        <Alert severity="error">
          Could not create session. Verify the backend is running and template selection is valid.
        </Alert>
      ) : null}

      <Card>
        <CardContent>
          <Stack spacing={2}>
            <TextField
              select
              label="Template"
              value={selectedEntry ? toTemplateKey(selectedEntry) : ""}
              onChange={(event) => setSelectedTemplateKey(event.target.value)}
              fullWidth
            >
              {(catalogQuery.data ?? []).map((entry) => (
                <MenuItem key={toTemplateKey(entry)} value={toTemplateKey(entry)}>
                  {entry.name} ({entry.editionName})
                </MenuItem>
              ))}
            </TextField>

            <TextField
              label="Host display name"
              value={hostDisplayName}
              onChange={(event) => setHostDisplayName(event.target.value)}
              slotProps={{ htmlInput: { maxLength: 50 } }}
              fullWidth
            />

            <Divider />

            {selectedEntry ? (
              <Stack spacing={1}>
                <Typography variant="h5">{selectedEntry.name}</Typography>
                <Typography color="text.secondary">
                  Edition {selectedEntry.editionName} • Version {selectedEntry.templateVersion}
                </Typography>
                <Typography>
                  Player range: {selectedEntry.minimumPlayers} to {selectedEntry.maximumPlayers}
                </Typography>
                <Typography>{selectedEntry.description}</Typography>
              </Stack>
            ) : (
              <Typography color="text.secondary">
                Select a template to review setup details.
              </Typography>
            )}
          </Stack>
        </CardContent>
      </Card>

      <Stack direction={{ xs: "column", sm: "row" }} spacing={1.2}>
        <Button
          variant="contained"
          disabled={!selectedEntry || createSessionMutation.isPending}
          onClick={() => {
            if (!selectedEntry) {
              return;
            }

            void createSessionMutation
              .mutateAsync({
                templateId: selectedEntry.templateId,
                editionId: selectedEntry.editionId,
                templateVersion: selectedEntry.templateVersion,
                hostDisplayName,
                sessionOptions: {},
              })
              .then((response) => {
                setActiveSession({
                  sessionId: response.sessionId,
                  participantId: response.hostParticipantId,
                  reconnectCredential: response.reconnectCredential,
                  roomCode: response.roomCode,
                });
                navigate(`/game/${response.sessionId}`);
              });
          }}
        >
          {createSessionMutation.isPending ? "Creating room..." : "Create room"}
        </Button>
        <Button component={RouterLink} to="/templates" variant="outlined">
          Back to templates
        </Button>
      </Stack>
    </Stack>
  );
}
