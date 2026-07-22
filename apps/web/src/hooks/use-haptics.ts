import { useCallback, useMemo } from "react";
import { type IHapticsService, hapticsService, type VibrationPattern } from "@bankers-seat/ui";

interface UseHapticsResult {
  readonly isAvailable: boolean;
  vibrate: (pattern: VibrationPattern) => Promise<void>;
}

export function useHaptics(
  service: IHapticsService = hapticsService,
): UseHapticsResult {
  const vibrate = useCallback(
    async (pattern: VibrationPattern) => {
      await service.vibrate(pattern);
    },
    [service],
  );

  return useMemo(
    () => ({
      isAvailable: service.isAvailable(),
      vibrate,
    }),
    [service, vibrate],
  );
}
