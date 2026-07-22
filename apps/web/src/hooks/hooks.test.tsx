import type { ReactElement } from "react";
import { act, renderHook, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import {
  type IDeepLinkService,
  type INetworkStatusService,
  type ISecureStorageService,
  type IShareService,
} from "@bankers-seat/ui";
import {
  useDeepLink,
  useNetworkStatus,
  useQrScanner,
  useSecureStorage,
  useShare,
} from "./index.ts";

describe("mobile hooks", () => {
  it("loads and updates secure storage values", async () => {
    const service: ISecureStorageService = {
      getCredential: vi.fn().mockResolvedValue("initial"),
      setCredential: vi.fn().mockResolvedValue(undefined),
      clearCredential: vi.fn().mockResolvedValue(undefined),
      clearAllCredentials: vi.fn().mockResolvedValue(undefined),
    };

    const { result } = renderHook(() => useSecureStorage("reconnect", service));

    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.value).toBe("initial");

    await act(async () => {
      await result.current.setValue("updated");
    });

    expect(result.current.value).toBe("updated");
    expect(service.setCredential).toHaveBeenCalledWith("reconnect", "updated");
  });

  it("delegates share actions through the share hook", async () => {
    const service: IShareService = {
      canShare: () => true,
      share: vi.fn().mockResolvedValue(undefined),
      copy: vi.fn().mockResolvedValue(undefined),
    };

    const { result } = renderHook(() => useShare(service));

    await act(async () => {
      await result.current.share("Invite", "Join", "https://example.com/join/ABCD12");
    });

    expect(result.current.canShare).toBe(true);
    expect(service.share).toHaveBeenCalledWith(
      "Invite",
      "Join",
      "https://example.com/join/ABCD12",
      undefined,
    );
  });

  it("renders a web QR scanner overlay and resolves scans", async () => {
    const { result } = renderHook(() =>
      useQrScanner({
        onScanned: vi.fn(),
      }),
    );

    let scanPromise: Promise<string> | undefined;
    act(() => {
      scanPromise = result.current.startScanning();
    });
    await waitFor(() => expect(result.current.isScanning).toBe(true));
    expect(result.current.overlay).not.toBeNull();

    await act(async () => {
      const overlayProps = result.current.overlay as ReactElement<{
        onScanned: (value: string) => void;
      }>;
      overlayProps.props.onScanned("ROOM42");
    });

    await expect(scanPromise).resolves.toBe("ROOM42");
    expect(result.current.lastScannedValue).toBe("ROOM42");
  });

  it("captures deep links and forwards room codes", async () => {
    let callback: ((link: { roomCode: string; url: string }) => void) | undefined;
    const service: IDeepLinkService = {
      parseJoinLink: vi.fn().mockReturnValue({
        roomCode: "ABCD12",
        url: "https://example.com/join/ABCD12",
      }),
      handleDeepLink: vi.fn().mockResolvedValue(undefined),
      onDeepLinkReceived: vi.fn().mockImplementation((nextCallback) => {
        callback = nextCallback;
      }),
    };
    const onJoinLink = vi.fn();
    const { result } = renderHook(() => useDeepLink({ service, onJoinLink }));

    await act(async () => {
      callback?.({ roomCode: "ROOM42", url: "https://example.com/join/ROOM42" });
      await result.current.handleDeepLink("https://example.com/join/ABCD12");
    });

    expect(result.current.lastJoinLink).toEqual({
      roomCode: "ABCD12",
      url: "https://example.com/join/ABCD12",
    });
    expect(onJoinLink).toHaveBeenCalledWith("ROOM42");
    expect(onJoinLink).toHaveBeenCalledWith("ABCD12");
  });

  it("subscribes to network status changes", async () => {
    let callback: ((isOnline: boolean) => void) | undefined;
    const service: INetworkStatusService = {
      isOnline: () => false,
      onStatusChange: vi.fn().mockImplementation((nextCallback) => {
        callback = nextCallback;
        return () => {
          callback = undefined;
        };
      }),
    };

    const { result } = renderHook(() => useNetworkStatus(service));

    expect(result.current.isOnline).toBe(false);

    await act(async () => {
      callback?.(true);
    });

    expect(result.current.isOnline).toBe(true);
  });
});
