import {
  Dialog,
  DialogContent,
  DialogTitle,
  IconButton,
  Stack,
} from "@mui/material";
import CloseIcon from "@mui/icons-material/Close";
import { TemplatePreview } from "./template-preview.tsx";

export interface TemplatePreviewDialogProps {
  open: boolean;
  onClose: () => void;
  templateId: string;
  editionId: string;
  version: string;
}

export function TemplatePreviewDialog({
  open,
  onClose,
  templateId,
  editionId,
  version,
}: TemplatePreviewDialogProps) {
  return (
    <Dialog
      open={open}
      onClose={onClose}
      maxWidth="sm"
      fullWidth
      slotProps={{
        paper: {
          sx: {
            borderRadius: 2,
          },
        },
      }}
    >
      <DialogTitle
        sx={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          pb: 1,
        }}
      >
        Template Preview
        <IconButton
          onClick={onClose}
          size="small"
          sx={{
            color: "text.secondary",
          }}
        >
          <CloseIcon fontSize="small" />
        </IconButton>
      </DialogTitle>
      <DialogContent dividers>
        <Stack sx={{ pt: 1 }}>
          <TemplatePreview
            templateId={templateId}
            editionId={editionId}
            version={version}
          />
        </Stack>
      </DialogContent>
    </Dialog>
  );
}
