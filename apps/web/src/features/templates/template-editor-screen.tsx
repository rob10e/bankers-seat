import React, { useEffect, useRef, useState } from "react";
import {
  Box,
  Button,
  Card,
  CardContent,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  Tab,
  Tabs,
  TextField,
  Typography,
} from "@mui/material";
import { Download as DownloadIcon, Save as SaveIcon } from "@mui/icons-material";
import { useTemplateDraftQuery } from "./use-template-draft-query";
import { TemplatePreview } from "./template-preview";

interface TabPanelProps {
  children?: React.ReactNode;
  index: number;
  value: number;
}

function TabPanel(props: TabPanelProps) {
  const { children, value, index, ...other } = props;
  return (
    <div
      role="tabpanel"
      hidden={value !== index}
      id={`editor-tabpanel-${index}`}
      aria-labelledby={`editor-tab-${index}`}
      {...other}
    >
      {value === index && <Box sx={{ p: 2 }}>{children}</Box>}
    </div>
  );
}

interface TemplateEditorScreenProps {
  /** Template ID to edit (or create from) */
  templateId?: string;
  editionId?: string;
  templateVersion?: string;
}

/**
 * Visual template editor for non-technical authors.
 * Provides form-based editing with live preview.
 */
export function TemplateEditorScreen({
  templateId,
  editionId,
  templateVersion,
}: TemplateEditorScreenProps) {
  const [tabValue, setTabValue] = useState(0);
  const [isDirty, setIsDirty] = useState(false);
  const [jsonInput, setJsonInput] = useState("");
  const [jsonError, setJsonError] = useState<string | null>(null);
  const draftInitializedRef = useRef(false);

  const {
    draft,
    isLoadingDraft,
    isUpdatingDraft,
    createDraft,
    updateDraft,
    exportDraft,
  } = useTemplateDraftQuery();

  // Initialize draft on mount - only once per template
  useEffect(() => {
    if (templateId && editionId && templateVersion && !draftInitializedRef.current) {
      draftInitializedRef.current = true;
      createDraft(templateId, editionId, templateVersion).catch(console.error);
    }
  }, [templateId, editionId, templateVersion, createDraft]);

  // Update JSON editor when draft changes
  useEffect(() => {
    if (draft?.templateData) {
      setJsonInput(JSON.stringify(draft.templateData, null, 2));
      setIsDirty(false);
    }
  }, [draft?.draftId]);

  const handleTabChange = (_: React.SyntheticEvent, newValue: number) => {
    setTabValue(newValue);
  };

  const handleJsonChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    const value = e.target.value;
    setJsonInput(value);
    setIsDirty(true);
    setJsonError(null);

    // Try to parse JSON for live validation
    try {
      JSON.parse(value);
    } catch (err) {
      setJsonError(err instanceof Error ? err.message : "Invalid JSON");
    }
  };

  const handleSaveJson = async () => {
    if (!draft) return;

    try {
      const templateData = JSON.parse(jsonInput);
      await updateDraft({
        draftId: draft.draftId.toString(),
        templateData,
      });
      setIsDirty(false);
    } catch (err) {
      setJsonError(err instanceof Error ? err.message : "Failed to parse JSON");
    }
  };

  const handleExport = async () => {
    if (!draft) return;
    await exportDraft(draft.draftId.toString());
  };

  if (isLoadingDraft || !draft) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", p: 4 }}>
        <CircularProgress />
      </Box>
    );
  }

  return (
    <Box sx={{ display: "flex", flexDirection: "column", height: "100%", gap: 2, p: 2 }}>
      {/* Header */}
      <Card>
        <CardContent>
          <Stack direction="row" sx={{ justifyContent: "space-between", alignItems: "center" }}>
            <Box>
              <Typography variant="h5">{draft.templateId}</Typography>
              <Typography variant="caption" color="text.secondary">
                Edition {draft.editionId} • Version {draft.templateVersion}
              </Typography>
            </Box>
            <Stack direction="row" spacing={1}>
              <Button
                variant="outlined"
                startIcon={<DownloadIcon />}
                onClick={handleExport}
                disabled={isUpdatingDraft}
              >
                Export
              </Button>
              <Button
                variant="contained"
                startIcon={<SaveIcon />}
                onClick={handleSaveJson}
                disabled={!isDirty || !!jsonError || isUpdatingDraft}
              >
                {isUpdatingDraft ? "Saving..." : "Save"}
              </Button>
            </Stack>
          </Stack>
        </CardContent>
      </Card>

      {/* Tabs */}
      <Box sx={{ borderBottom: 1, borderColor: "divider" }}>
        <Tabs
          value={tabValue}
          onChange={handleTabChange}
          aria-label="template editor tabs"
        >
          <Tab label="JSON Editor" id="editor-tab-0" aria-controls="editor-tabpanel-0" />
          <Tab label="Live Preview" id="editor-tab-1" aria-controls="editor-tabpanel-1" />
        </Tabs>
      </Box>

      {/* Tab Panels */}
      <Box sx={{ flex: 1, overflow: "auto" }}>
        <TabPanel value={tabValue} index={0}>
          {/* JSON Editor */}
          <Stack spacing={2}>
            <TextField
              multiline
              fullWidth
              minRows={15}
              maxRows={30}
              value={jsonInput}
              onChange={handleJsonChange}
              placeholder="Enter template JSON..."
              error={!!jsonError}
              helperText={jsonError}
              sx={{
                fontFamily: "monospace",
                fontSize: "0.85rem",
                "& .MuiInputBase-input": {
                  fontFamily: "monospace",
                  fontSize: "0.85rem",
                },
              }}
            />
            {isDirty && (
              <Typography variant="caption" color="warning.main">
                ⚠️ Unsaved changes — click Save to persist
              </Typography>
            )}
          </Stack>
        </TabPanel>

        <TabPanel value={tabValue} index={1}>
          {/* Live Preview */}
          <Box sx={{ maxWidth: "100%" }}>
            {draft.templateData && (
              <TemplatePreview
                templateId={draft.templateId}
                schemaVersion={2}
                {...(draft.templateData as any)}
              />
            )}
          </Box>
        </TabPanel>
      </Box>
    </Box>
  );
}

/**
 * Dialog wrapper for template editor.
 * Useful for embedded editing within a modal.
 */
interface TemplateEditorDialogProps extends TemplateEditorScreenProps {
  open: boolean;
  onOpenChange?: (open: boolean) => void;
}

export function TemplateEditorDialog({
  open,
  onOpenChange,
  ...screenProps
}: TemplateEditorDialogProps) {
  return (
    <Dialog
      open={open}
      onClose={() => onOpenChange?.(false)}
      maxWidth="lg"
      fullWidth
      slotProps={{
        paper: {
          sx: { maxHeight: "90vh" },
        },
      }}
    >
      <DialogTitle>Template Editor</DialogTitle>
      <DialogContent dividers sx={{ p: 0 }}>
        <TemplateEditorScreen {...screenProps} />
      </DialogContent>
      <DialogActions>
        <Button onClick={() => onOpenChange?.(false)}>Close</Button>
      </DialogActions>
    </Dialog>
  );
}
