import {
  Alert,
  Button,
  Card,
  CardActions,
  CardContent,
  Chip,
  CircularProgress,
  Grid,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import { useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { toTemplateKey } from "../../domain/template-catalog.ts";
import { useUiSessionStore } from "../session/ui-session-store.ts";
import { useTemplateCatalogQuery } from "./use-template-catalog-query.ts";

export function TemplateCatalogView() {
  const navigate = useNavigate();
  const setSelectedTemplateKey = useUiSessionStore((state) => state.setSelectedTemplateKey);
  const [searchTerm, setSearchTerm] = useState("");
  const catalogQuery = useTemplateCatalogQuery();

  const filteredEntries = useMemo(() => {
    if (!catalogQuery.data) {
      return [];
    }

    const normalizedSearch = searchTerm.trim().toLowerCase();
    if (normalizedSearch.length === 0) {
      return catalogQuery.data;
    }

    return catalogQuery.data.filter((entry) => {
      const text = `${entry.name} ${entry.editionName} ${entry.description} ${entry.tags.join(" ")}`;
      return text.toLowerCase().includes(normalizedSearch);
    });
  }, [catalogQuery.data, searchTerm]);

  if (catalogQuery.isPending) {
    return (
      <Stack spacing={1.5} sx={{ py: 6, alignItems: "center" }}>
        <CircularProgress />
        <Typography color="text.secondary">Loading validated templates…</Typography>
      </Stack>
    );
  }

  if (catalogQuery.isError) {
    return (
      <Alert severity="error">
        Could not load template catalog. Verify `/api/v1/templates` is available.
      </Alert>
    );
  }

  return (
    <Stack spacing={2.5} className="app-page">
      <Typography variant="h4">Template catalog</Typography>
      <TextField
        label="Search templates"
        value={searchTerm}
        onChange={(event) => setSearchTerm(event.target.value)}
        placeholder="Search by name, edition, or tag"
        fullWidth
      />

      <Grid container spacing={2}>
        {filteredEntries.map((entry) => (
          <Grid key={toTemplateKey(entry)} size={{ xs: 12 }}>
            <Card>
              <CardContent>
                <Stack spacing={1.3}>
                  <Stack
                    direction={{ xs: "column", sm: "row" }}
                    sx={{ justifyContent: "space-between", gap: 1 }}
                  >
                    <Typography variant="h5">{entry.name}</Typography>
                    <Chip
                      label={`${entry.minimumPlayers}-${entry.maximumPlayers} players`}
                      size="small"
                      color="primary"
                      variant="outlined"
                    />
                  </Stack>
                  <Typography color="text.secondary">
                    Edition: {entry.editionName} • Version {entry.templateVersion}
                  </Typography>
                  <Typography>{entry.description}</Typography>
                  <Stack direction="row" sx={{ gap: 0.8, flexWrap: "wrap" }}>
                    {entry.tags.map((tag) => (
                      <Chip key={tag} label={tag} size="small" variant="outlined" />
                    ))}
                  </Stack>
                </Stack>
              </CardContent>
              <CardActions>
                <Button
                  variant="contained"
                  onClick={() => {
                    setSelectedTemplateKey(toTemplateKey(entry));
                    navigate("/host/new");
                  }}
                >
                  Select template
                </Button>
              </CardActions>
            </Card>
          </Grid>
        ))}
      </Grid>
    </Stack>
  );
}
