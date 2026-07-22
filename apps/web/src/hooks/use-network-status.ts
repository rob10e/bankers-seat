import { useEffect, useMemo, useState } from "react";
import {
  type INetworkStatusService,
  networkStatusService,
} from "@bankers-seat/ui";

interface UseNetworkStatusResult {
  readonly isOnline: boolean;
}

export function useNetworkStatus(
  service: INetworkStatusService = networkStatusService,
): UseNetworkStatusResult {
  const [isOnline, setIsOnline] = useState(service.isOnline());

  useEffect(() => {
    return service.onStatusChange((nextIsOnline) => {
      setIsOnline(nextIsOnline);
    });
  }, [service]);

  return useMemo(
    () => ({
      isOnline,
    }),
    [isOnline],
  );
}
