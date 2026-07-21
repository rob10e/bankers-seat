import { useQuery } from "@tanstack/react-query";

export interface TemplateDiffResponse {
  compatibleUpgrade: boolean;
  breakingChanges?: string[];
  newFeatures?: string[];
  removedFeatures?: string[];
  changedFields?: string[];
  migrationAdvice?: string;
  changelog?: string;
}

async function fetchTemplateDiff(
  templateId: string,
  fromEditionId: string,
  fromVersion: string,
  toEditionId: string,
  toVersion: string
): Promise<TemplateDiffResponse> {
  const params = new URLSearchParams({
    fromEditionId,
    fromVersion,
    toEditionId,
    toVersion,
  });

  const response = await fetch(`/api/v1/templates/${templateId}/diff?${params}`);

  if (!response.ok) {
    throw new Error(`Failed to fetch diff: ${response.statusText}`);
  }

  return response.json();
}

export function useTemplateDiffQuery(
  templateId: string,
  fromEditionId: string,
  fromVersion: string,
  toEditionId: string,
  toVersion: string
) {
  return useQuery({
    queryKey: [
      "template-diff",
      templateId,
      fromEditionId,
      fromVersion,
      toEditionId,
      toVersion,
    ],
    queryFn: () =>
      fetchTemplateDiff(
        templateId,
        fromEditionId,
        fromVersion,
        toEditionId,
        toVersion
      ),
    enabled: !!(
      templateId &&
      fromEditionId &&
      fromVersion &&
      toEditionId &&
      toVersion
    ),
  });
}
