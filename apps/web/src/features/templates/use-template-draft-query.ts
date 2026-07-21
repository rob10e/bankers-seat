import { useMutation, useQuery } from "@tanstack/react-query";

interface TemplateDraftResponse {
  draftId: string;
  userId: string;
  templateId: string;
  editionId: string;
  templateVersion: string;
  templateData: object;
  createdAtUtc: string;
  updatedAtUtc: string;
}

const API_BASE = "/api/v1";

export interface UseTemplateDraftParams {
  draftId?: string;
}

/**
 * React Query hook for fetching and managing template drafts
 */
export function useTemplateDraftQuery(
  params?: UseTemplateDraftParams,
) {
  const draftId = params?.draftId;

  // Fetch single draft
  const draftQuery = useQuery({
    queryKey: ["template-draft", draftId],
    queryFn: async (): Promise<TemplateDraftResponse | null> => {
      if (!draftId) return null;

      const response = await fetch(`${API_BASE}/templates/drafts/${draftId}`);
      if (!response.ok) {
        if (response.status === 404) return null;
        throw new Error(`Failed to fetch draft: ${response.statusText}`);
      }
      return response.json();
    },
    enabled: !!draftId,
    staleTime: 1000 * 60 * 5, // 5 minutes
  });

  // Create new draft from template
  const createDraftMutation = useMutation({
    mutationFn: async ({
      templateId,
      editionId,
      version,
    }: {
      templateId: string;
      editionId: string;
      version: string;
    }): Promise<TemplateDraftResponse> => {
      const response = await fetch(`${API_BASE}/templates/drafts`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          templateId,
          editionId,
          templateVersion: version,
        }),
      });
      if (!response.ok) throw new Error("Failed to create draft");
      const data = await response.json();
      return data as TemplateDraftResponse;
    },
  });

  // Update draft
  const updateDraftMutation = useMutation({
    mutationFn: async (data: { draftId: string; templateData: object }): Promise<TemplateDraftResponse> => {
      const response = await fetch(`${API_BASE}/templates/drafts/${data.draftId}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ templateData: data.templateData }),
      });
      if (!response.ok) throw new Error("Failed to update draft");
      const responseData = await response.json();
      return responseData as TemplateDraftResponse;
    },
    onSuccess: () => {
      // Refetch draft after update
      draftQuery.refetch();
    },
  });

  // Delete draft
  const deleteDraftMutation = useMutation({
    mutationFn: async (draftId: string) => {
      const response = await fetch(`${API_BASE}/templates/drafts/${draftId}`, {
        method: "DELETE",
      });
      if (!response.ok) throw new Error("Failed to delete draft");
    },
  });

  // Export draft as JSON
  const exportDraftMutation = useMutation({
    mutationFn: async (draftId: string) => {
      const response = await fetch(`${API_BASE}/templates/drafts/${draftId}/export`);
      if (!response.ok) throw new Error("Failed to export draft");

      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `draft-${draftId}.json`;
      a.click();
      URL.revokeObjectURL(url);
    },
  });

  return {
    draft: draftQuery.data,
    isLoadingDraft: draftQuery.isLoading,
    createDraft: (templateId: string, editionId: string, version: string) =>
      createDraftMutation.mutateAsync({ templateId, editionId, version }),
    updateDraft: updateDraftMutation.mutateAsync,
    deleteDraft: deleteDraftMutation.mutateAsync,
    exportDraft: exportDraftMutation.mutateAsync,
    isCreatingDraft: createDraftMutation.isPending,
    isUpdatingDraft: updateDraftMutation.isPending,
    isDeletingDraft: deleteDraftMutation.isPending,
    isExportingDraft: exportDraftMutation.isPending,
  };
}
