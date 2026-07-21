import {
  Alert,
  Box,
  Card,
  CardContent,
  CardHeader,
  Chip,
  CircularProgress,
  List,
  ListItem,
  ListItemIcon,
  Stack,
  Tab,
  Tabs,
  Typography,
} from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import WarningIcon from "@mui/icons-material/Warning";
import RemoveCircleIcon from "@mui/icons-material/RemoveCircle";
import AddCircleIcon from "@mui/icons-material/AddCircle";
import EditIcon from "@mui/icons-material/Edit";
import { useState } from "react";
import { useTemplateDiffQuery } from "./use-template-diff-query";

export interface TemplateDiffComparisonProps {
  templateId: string;
  fromEditionId: string;
  fromVersion: string;
  toEditionId: string;
  toVersion: string;
}

function TabPanel(props: {
  children?: React.ReactNode;
  index: number;
  value: number;
}) {
  const { children, value, index } = props;

  return (
    <div
      role="tabpanel"
      hidden={value !== index}
      id={`tabpanel-${index}`}
      aria-labelledby={`tab-${index}`}
    >
      {value === index && <Box sx={{ p: 2 }}>{children}</Box>}
    </div>
  );
}

function ChangesList({
  items,
  icon: Icon,
  title,
}: {
  items?: string[];
  icon: React.ComponentType<any>;
  title: string;
}) {
  if (!items || items.length === 0) {
    return null;
  }

  return (
    <Box sx={{ mb: 2 }}>
      <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1 }}>
        {title} ({items.length})
      </Typography>
      <List sx={{ bgcolor: "background.paper" }}>
        {items.map((item, idx) => (
          <ListItem key={idx} sx={{ py: 0.5, px: 0 }}>
            <ListItemIcon sx={{ minWidth: 32 }}>
              <Icon fontSize="small" />
            </ListItemIcon>
            <Box>
              <Typography variant="body2">{item}</Typography>
            </Box>
          </ListItem>
        ))}
      </List>
    </Box>
  );
}

export function TemplateDiffComparison({
  templateId,
  fromEditionId,
  fromVersion,
  toEditionId,
  toVersion,
}: TemplateDiffComparisonProps) {
  const [tabIndex, setTabIndex] = useState(0);
  const { data, isLoading, error } = useTemplateDiffQuery(
    templateId,
    fromEditionId,
    fromVersion,
    toEditionId,
    toVersion
  );

  if (isLoading) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", p: 4 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (error) {
    return (
      <Alert severity="error" sx={{ m: 2 }}>
        Failed to load diff: {(error as Error).message}
      </Alert>
    );
  }

  if (!data) {
    return null;
  }

  const compatibilityColor = data.compatibleUpgrade
    ? "success"
    : ("error" as const);
  const compatibilityIcon = data.compatibleUpgrade
    ? CheckCircleIcon
    : WarningIcon;
  const compatibilityText = data.compatibleUpgrade
    ? "Compatible Upgrade"
    : "Breaking Changes Detected";

  return (
    <Stack spacing={2}>
      <Card>
        <CardHeader
          title={`${fromVersion} → ${toVersion}`}
          subheader={`${fromEditionId} edition`}
        />
        <CardContent>
          <Stack
            direction="row"
            spacing={1}
            sx={{ alignItems: "center", mb: 2 }}
          >
            <Chip
              label={compatibilityText}
              color={compatibilityColor}
              variant="filled"
              icon={compatibilityIcon as any}
            />
          </Stack>

          <Tabs
            value={tabIndex}
            onChange={(_, value) => setTabIndex(value)}
            sx={{ borderBottom: 1, borderColor: "divider" }}
          >
            <Tab label="Summary" id="tab-0" aria-controls="tabpanel-0" />
            <Tab label="Changes" id="tab-1" aria-controls="tabpanel-1" />
            <Tab label="Changelog" id="tab-2" aria-controls="tabpanel-2" />
          </Tabs>

          <TabPanel value={tabIndex} index={0}>
            <Stack spacing={2}>
              {data.migrationAdvice && (
                <Alert
                  severity={data.compatibleUpgrade ? "info" : "warning"}
                  sx={{ whiteSpace: "pre-wrap" }}
                >
                  {data.migrationAdvice}
                </Alert>
              )}

              <Box>
                <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 1 }}>
                  Summary
                </Typography>
                <Stack direction="row" spacing={2}>
                  <Typography variant="body2">
                    <strong>Breaking Changes:</strong>{" "}
                    {data.breakingChanges?.length || 0}
                  </Typography>
                  <Typography variant="body2">
                    <strong>New Features:</strong>{" "}
                    {data.newFeatures?.length || 0}
                  </Typography>
                  <Typography variant="body2">
                    <strong>Removed:</strong>{" "}
                    {data.removedFeatures?.length || 0}
                  </Typography>
                </Stack>
              </Box>
            </Stack>
          </TabPanel>

          <TabPanel value={tabIndex} index={1}>
            <Stack spacing={2}>
              <ChangesList
                items={data.breakingChanges}
                icon={RemoveCircleIcon}
                title="Breaking Changes"
              />
              <ChangesList
                items={data.newFeatures}
                icon={AddCircleIcon}
                title="New Features"
              />
              <ChangesList
                items={data.removedFeatures}
                icon={RemoveCircleIcon}
                title="Removed"
              />
              <ChangesList
                items={data.changedFields}
                icon={EditIcon}
                title="Modified"
              />

              {!data.breakingChanges &&
                !data.newFeatures &&
                !data.removedFeatures &&
                !data.changedFields && (
                  <Typography variant="body2" color="text.secondary">
                    No changes detected
                  </Typography>
                )}
            </Stack>
          </TabPanel>

          <TabPanel value={tabIndex} index={2}>
            {data.changelog ? (
              <Typography
                variant="body2"
                component="pre"
                sx={{
                  whiteSpace: "pre-wrap",
                  wordWrap: "break-word",
                  fontFamily: "monospace",
                  bgcolor: "background.default",
                  p: 2,
                  borderRadius: 1,
                  overflow: "auto",
                }}
              >
                {data.changelog}
              </Typography>
            ) : (
              <Typography variant="body2" color="text.secondary">
                No changelog available
              </Typography>
            )}
          </TabPanel>
        </CardContent>
      </Card>
    </Stack>
  );
}
