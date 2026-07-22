import { useEffect, useMemo } from "react";
import {
  crashReportingService,
  type CrashMessageLevel,
  type ICrashReportingService,
} from "@bankers-seat/ui";

interface UseCrashReportingOptions {
  readonly service?: ICrashReportingService;
  readonly apiKey?: string;
}

interface UseCrashReportingResult {
  captureException: (error: unknown) => void;
  captureMessage: (message: string, level: CrashMessageLevel) => void;
  setBreadcrumb: (action: string, data: Record<string, unknown>) => void;
}

export function useCrashReporting(
  options: UseCrashReportingOptions = {},
): UseCrashReportingResult {
  const service = options.service ?? crashReportingService;

  useEffect(() => {
    service.initialize(options.apiKey ?? "console");

    const handleError = (event: ErrorEvent) => {
      service.captureException(event.error ?? new Error(event.message));
    };
    const handleRejection = (event: PromiseRejectionEvent) => {
      service.captureException(event.reason);
    };

    window.addEventListener("error", handleError);
    window.addEventListener("unhandledrejection", handleRejection);

    return () => {
      window.removeEventListener("error", handleError);
      window.removeEventListener("unhandledrejection", handleRejection);
    };
  }, [options.apiKey, service]);

  return useMemo(
    () => ({
      captureException: (error: unknown) => {
        service.captureException(error);
      },
      captureMessage: (message: string, level: CrashMessageLevel) => {
        service.captureMessage(message, level);
      },
      setBreadcrumb: (action: string, data: Record<string, unknown>) => {
        service.setBreadcrumb(action, data);
      },
    }),
    [service],
  );
}
