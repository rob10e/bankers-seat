import { useQuery } from "@tanstack/react-query";

export interface TemplatePreviewData {
  templateId: string;
  templateName: string;
  editionId: string;
  editionName: string;
  templateVersion: string;
  description: string;
  schemaVersion: number;
  currency?: {
    code: string;
    symbol: string;
    name: string;
  };
  bank?: {
    startingPlayerBalance: number;
    bankMode: string;
    allowPlayerOverdraft: boolean;
  };
  playerCount?: {
    min: number;
    max: number;
  };
  denominations?: Array<{
    value: number;
    label: string;
    asset?: string;
  }>;
  playerFields?: Array<{
    id: string;
    label: string;
    type: string;
  }>;
  actions?: Array<{
    id: string;
    label: string;
    actionType: string;
  }>;
  assets?: {
    logo?: string;
    thumbnail?: string;
    background?: string;
  };
}

const fetchTemplatePreview = async (
  templateId: string,
  editionId: string,
  version: string
): Promise<TemplatePreviewData> => {
  const url = new URL(
    `/api/v1/templates/${templateId}/preview`,
    window.location.origin
  );
  url.searchParams.set("editionId", editionId);
  url.searchParams.set("version", version);

  const response = await fetch(url.toString());
  if (!response.ok) {
    throw new Error(
      `Failed to fetch template preview: ${response.statusText}`
    );
  }

  return response.json() as Promise<TemplatePreviewData>;
};

export const useTemplatePreviewQuery = (
  templateId: string,
  editionId: string,
  version: string,
  enabled = true
) => {
  return useQuery({
    queryKey: ["template-preview", templateId, editionId, version],
    queryFn: () => fetchTemplatePreview(templateId, editionId, version),
    staleTime: 5 * 60 * 1000, // 5 minutes
    enabled: enabled && Boolean(templateId && editionId && version),
  });
};
