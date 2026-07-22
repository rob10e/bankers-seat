import { useCallback, useEffect, useMemo, useState } from "react";
import {
  type IDeepLinkService,
  type JoinLinkMatch,
  deepLinkService,
} from "@bankers-seat/ui";

interface UseDeepLinkOptions {
  readonly service?: IDeepLinkService;
  readonly onJoinLink?: (roomCode: string) => void;
}

interface UseDeepLinkResult {
  readonly lastJoinLink: JoinLinkMatch | null;
  handleDeepLink: (url: string) => Promise<void>;
}

export function useDeepLink(options: UseDeepLinkOptions = {}): UseDeepLinkResult {
  const service = options.service ?? deepLinkService;
  const [lastJoinLink, setLastJoinLink] = useState<JoinLinkMatch | null>(null);

  const handleDeepLink = useCallback(
    async (url: string) => {
      const parsedLink = service.parseJoinLink(url);
      if (parsedLink) {
        setLastJoinLink(parsedLink);
        options.onJoinLink?.(parsedLink.roomCode);
      }

      await service.handleDeepLink(url);
    },
    [options, service],
  );

  useEffect(() => {
    service.onDeepLinkReceived((link) => {
      setLastJoinLink(link);
      options.onJoinLink?.(link.roomCode);
    });
  }, [options, service]);

  return useMemo(
    () => ({
      lastJoinLink,
      handleDeepLink,
    }),
    [handleDeepLink, lastJoinLink],
  );
}
