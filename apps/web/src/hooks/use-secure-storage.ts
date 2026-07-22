import { useCallback, useEffect, useMemo, useState } from "react";
import {
  type ISecureStorageService,
  secureStorageService,
} from "@bankers-seat/ui";

interface UseSecureStorageResult {
  readonly value: string | null;
  readonly loading: boolean;
  readonly error: Error | null;
  setValue: (nextValue: string) => Promise<void>;
  clearValue: () => Promise<void>;
  refresh: () => Promise<void>;
}

export function useSecureStorage(
  key: string,
  service: ISecureStorageService = secureStorageService,
): UseSecureStorageResult {
  const [value, setValueState] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);

  const refresh = useCallback(async () => {
    setLoading(true);
    try {
      const nextValue = await service.getCredential(key);
      setValueState(nextValue);
      setError(null);
    } catch (unknownError) {
      setError(
        unknownError instanceof Error
          ? unknownError
          : new Error("Failed to read secure storage."),
      );
    } finally {
      setLoading(false);
    }
  }, [key, service]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  const setValue = useCallback(
    async (nextValue: string) => {
      setLoading(true);
      try {
        await service.setCredential(key, nextValue);
        setValueState(nextValue);
        setError(null);
      } catch (unknownError) {
        setError(
          unknownError instanceof Error
            ? unknownError
            : new Error("Failed to write secure storage."),
        );
        throw unknownError;
      } finally {
        setLoading(false);
      }
    },
    [key, service],
  );

  const clearValue = useCallback(async () => {
    setLoading(true);
    try {
      await service.clearCredential(key);
      setValueState(null);
      setError(null);
    } catch (unknownError) {
      setError(
        unknownError instanceof Error
          ? unknownError
          : new Error("Failed to clear secure storage."),
      );
      throw unknownError;
    } finally {
      setLoading(false);
    }
  }, [key, service]);

  return useMemo(
    () => ({
      value,
      loading,
      error,
      setValue,
      clearValue,
      refresh,
    }),
    [clearValue, error, loading, refresh, setValue, value],
  );
}
