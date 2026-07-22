import { useCallback, useMemo, useState } from "react";
import { type IShareService, shareService } from "@bankers-seat/ui";

interface UseShareResult {
  readonly canShare: boolean;
  readonly isSharing: boolean;
  readonly error: Error | null;
  share: (
    title?: string,
    text?: string,
    url?: string,
    files?: readonly string[],
  ) => Promise<void>;
  copy: (text: string) => Promise<void>;
}

export function useShare(service: IShareService = shareService): UseShareResult {
  const [isSharing, setIsSharing] = useState(false);
  const [error, setError] = useState<Error | null>(null);

  const share = useCallback(
    async (
      title?: string,
      text?: string,
      url?: string,
      files?: readonly string[],
    ) => {
      setIsSharing(true);
      try {
        await service.share(title, text, url, files);
        setError(null);
      } catch (unknownError) {
        const nextError =
          unknownError instanceof Error
            ? unknownError
            : new Error("Failed to share content.");
        setError(nextError);
        throw nextError;
      } finally {
        setIsSharing(false);
      }
    },
    [service],
  );

  const copy = useCallback(
    async (text: string) => {
      try {
        await service.copy(text);
        setError(null);
      } catch (unknownError) {
        const nextError =
          unknownError instanceof Error
            ? unknownError
            : new Error("Failed to copy content.");
        setError(nextError);
        throw nextError;
      }
    },
    [service],
  );

  return useMemo(
    () => ({
      canShare: service.canShare(),
      isSharing,
      error,
      share,
      copy,
    }),
    [copy, error, isSharing, service, share],
  );
}
