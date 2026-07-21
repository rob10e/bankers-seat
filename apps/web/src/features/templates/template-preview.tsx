import {
  Alert,
  Box,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Divider,
  Grid,
  Stack,
  Typography,
} from "@mui/material";
import MoneyIcon from "@mui/icons-material/Money";
import PeopleIcon from "@mui/icons-material/People";
import ExtensionIcon from "@mui/icons-material/Extension";
import { useTemplatePreviewQuery, type TemplatePreviewData } from "./use-template-preview-query.ts";

export interface TemplatePreviewProps {
  templateId: string;
  editionId: string;
  version: string;
}

function PreviewSection({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}) {
  return (
    <Box sx={{ mb: 3 }}>
      <Typography variant="subtitle1" sx={{ fontWeight: 600, mb: 1.5 }}>
        {title}
      </Typography>
      {children}
    </Box>
  );
}

function CurrencyDisplay({ currency }: { currency?: TemplatePreviewData["currency"] }) {
  if (!currency) return null;

  return (
    <Stack spacing={1}>
      <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
        <MoneyIcon fontSize="small" color="primary" />
        <Typography variant="body2">
          <strong>Symbol:</strong> {currency.symbol} ({currency.code})
        </Typography>
      </Stack>
      <Typography variant="body2">
        <strong>Name:</strong> {currency.name}
      </Typography>
    </Stack>
  );
}

function BankDisplay({ bank }: { bank?: TemplatePreviewData["bank"] }) {
  if (!bank) return null;

  return (
    <Stack spacing={1}>
      <Typography variant="body2">
        <strong>Starting Balance:</strong> {bank.startingPlayerBalance}{" "}
        {bank.startingPlayerBalance === 1 ? "unit" : "units"}
      </Typography>
      <Typography variant="body2">
        <strong>Bank Mode:</strong>{" "}
        <Chip
          label={bank.bankMode}
          size="small"
          variant="outlined"
          sx={{ ml: 0.5 }}
        />
      </Typography>
      <Typography variant="body2">
        <strong>Overdraft:</strong>{" "}
        <Chip
          label={bank.allowPlayerOverdraft ? "Allowed" : "Prohibited"}
          size="small"
          color={bank.allowPlayerOverdraft ? "warning" : "success"}
          variant="outlined"
          sx={{ ml: 0.5 }}
        />
      </Typography>
    </Stack>
  );
}

function PlayerCountDisplay({
  playerCount,
}: {
  playerCount?: TemplatePreviewData["playerCount"];
}) {
  if (!playerCount) return null;

  return (
    <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
      <PeopleIcon fontSize="small" color="primary" />
      <Typography variant="body2">
        <strong>{playerCount.min}–{playerCount.max} players</strong>
      </Typography>
    </Stack>
  );
}

function DenominationsDisplay({
  denominations,
}: {
  denominations?: TemplatePreviewData["denominations"];
}) {
  if (!denominations || denominations.length === 0) return null;

  return (
    <Stack spacing={0.5}>
      {denominations.map((denom) => (
        <Chip
          key={denom.value}
          label={`${denom.label} (value: ${denom.value})`}
          size="small"
          variant="outlined"
        />
      ))}
    </Stack>
  );
}

function PlayerFieldsDisplay({
  fields,
}: {
  fields?: TemplatePreviewData["playerFields"];
}) {
  if (!fields || fields.length === 0) return null;

  return (
    <Stack spacing={1}>
      {fields.map((field) => (
        <Card key={field.id} variant="outlined" sx={{ p: 1.5 }}>
          <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
            <Box>
              <Typography variant="body2" sx={{ fontWeight: 500 }}>
                {field.label}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                ID: {field.id} · Type: {field.type}
              </Typography>
            </Box>
          </Stack>
        </Card>
      ))}
    </Stack>
  );
}

function ActionsDisplay({
  actions,
}: {
  actions?: TemplatePreviewData["actions"];
}) {
  if (!actions || actions.length === 0) return null;

  return (
    <Stack spacing={1}>
      {actions.map((action) => (
        <Card key={action.id} variant="outlined" sx={{ p: 1.5 }}>
          <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
            <ExtensionIcon fontSize="small" color="primary" />
            <Box>
              <Typography variant="body2" sx={{ fontWeight: 500 }}>
                {action.label}
              </Typography>
              <Typography variant="caption" color="text.secondary">
                ID: {action.id} · Type: {action.actionType}
              </Typography>
            </Box>
          </Stack>
        </Card>
      ))}
    </Stack>
  );
}

export function TemplatePreview({
  templateId,
  editionId,
  version,
}: TemplatePreviewProps) {
  const previewQuery = useTemplatePreviewQuery(templateId, editionId, version);

  if (previewQuery.isPending) {
    return (
      <Stack spacing={1.5} sx={{ py: 3, alignItems: "center" }}>
        <CircularProgress size={40} />
        <Typography color="text.secondary" variant="body2">
          Loading template preview…
        </Typography>
      </Stack>
    );
  }

  if (previewQuery.isError) {
    return (
      <Alert severity="error" sx={{ mb: 2 }}>
        Could not load template preview. Please verify the template exists and
        is valid.
      </Alert>
    );
  }

  if (!previewQuery.data) {
    return (
      <Alert severity="warning" sx={{ mb: 2 }}>
        No template data available.
      </Alert>
    );
  }

  const data = previewQuery.data;

  return (
    <Stack spacing={2} sx={{ width: "100%" }}>
      {/* Header */}
      <Card variant="outlined">
        <CardContent>
          <Stack spacing={1}>
            <Typography variant="h5" sx={{ fontWeight: 600 }}>
              {data.templateName}
            </Typography>
            <Typography variant="body2" color="text.secondary">
              {data.editionName} · v{data.templateVersion}
            </Typography>
            {data.description && (
              <Typography variant="body2" sx={{ mt: 1 }}>
                {data.description}
              </Typography>
            )}
          </Stack>
        </CardContent>
      </Card>

      {/* Game Info Grid */}
      <Grid container spacing={2}>
        {/* Currency */}
        {data.currency && (
          <Grid size={{ xs: 12, sm: 6 }}>
            <Card variant="outlined">
              <CardContent>
                <PreviewSection title="Currency">
                  <CurrencyDisplay currency={data.currency} />
                </PreviewSection>
              </CardContent>
            </Card>
          </Grid>
        )}

        {/* Player Count & Bank */}
        <Grid size={{ xs: 12, sm: 6 }}>
          <Card variant="outlined">
            <CardContent>
              {data.playerCount && (
                <PreviewSection title="Players">
                  <PlayerCountDisplay playerCount={data.playerCount} />
                </PreviewSection>
              )}
              <Divider sx={{ my: 2 }} />
              {data.bank && (
                <PreviewSection title="Bank Settings">
                  <BankDisplay bank={data.bank} />
                </PreviewSection>
              )}
            </CardContent>
          </Card>
        </Grid>
      </Grid>

      {/* Denominations */}
      {data.denominations && data.denominations.length > 0 && (
        <Card variant="outlined">
          <CardContent>
            <PreviewSection title="Denominations">
              <DenominationsDisplay denominations={data.denominations} />
            </PreviewSection>
          </CardContent>
        </Card>
      )}

      {/* Player Fields */}
      {data.playerFields && data.playerFields.length > 0 && (
        <Card variant="outlined">
          <CardContent>
            <PreviewSection title={`Custom Fields (${data.playerFields.length})`}>
              <PlayerFieldsDisplay fields={data.playerFields} />
            </PreviewSection>
          </CardContent>
        </Card>
      )}

      {/* Actions */}
      {data.actions && data.actions.length > 0 && (
        <Card variant="outlined">
          <CardContent>
            <PreviewSection
              title={`Actions & Payments (${data.actions.length})`}
            >
              <ActionsDisplay actions={data.actions} />
            </PreviewSection>
          </CardContent>
        </Card>
      )}

      {/* Metadata */}
      <Card variant="outlined" sx={{ bgcolor: "action.hover" }}>
        <CardContent>
          <Stack direction="row" spacing={3} sx={{ alignItems: "flex-start" }}>
            <Box>
              <Typography variant="caption" color="text.secondary">
                Template ID
              </Typography>
              <Typography variant="body2" sx={{ fontFamily: "monospace" }}>
                {data.templateId}
              </Typography>
            </Box>
            <Divider orientation="vertical" flexItem />
            <Box>
              <Typography variant="caption" color="text.secondary">
                Schema Version
              </Typography>
              <Typography variant="body2">{data.schemaVersion}</Typography>
            </Box>
          </Stack>
        </CardContent>
      </Card>
    </Stack>
  );
}
