import { describe, expect, it, vi } from "vitest";
import { CrashReportingService } from "./crash-reporting-service.ts";

describe("CrashReportingService", () => {
  it("initializes the reporter with an API key", () => {
    const logger = { info: vi.fn() };
    const service = new CrashReportingService({ logger });

    service.initialize("test-key");

    expect(logger.info).toHaveBeenCalledWith("[CrashReporting] initialized", {
      apiKey: "test-key",
    });
  });

  it("captures exceptions to the logger", () => {
    const logger = { error: vi.fn() };
    const service = new CrashReportingService({ logger });

    service.captureException(new Error("boom"));

    expect(logger.error).toHaveBeenCalledWith(
      "[CrashReporting] exception",
      expect.objectContaining({
        error: expect.any(Error),
      }),
    );
  });

  it("captures messages at the correct log level", () => {
    const logger = { info: vi.fn(), warn: vi.fn(), error: vi.fn() };
    const service = new CrashReportingService({ logger });

    service.captureMessage("heads up", "warning");
    service.captureMessage("broken", "error");

    expect(logger.warn).toHaveBeenCalledWith(
      "[CrashReporting] message",
      expect.objectContaining({ message: "heads up", level: "warning" }),
    );
    expect(logger.error).toHaveBeenCalledWith(
      "[CrashReporting] message",
      expect.objectContaining({ message: "broken", level: "error" }),
    );
  });

  it("records breadcrumbs for later diagnostics", () => {
    const logger = { info: vi.fn() };
    const service = new CrashReportingService({ logger });

    service.setBreadcrumb("join-room", { roomCode: "ABCD12" });

    expect(logger.info).toHaveBeenCalledWith("[CrashReporting] breadcrumb", {
      action: "join-room",
      data: { roomCode: "ABCD12" },
    });
  });
});
