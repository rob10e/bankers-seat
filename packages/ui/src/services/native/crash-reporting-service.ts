import type { LoggerLike } from "./native-platform.js";

export type CrashMessageLevel = "info" | "warning" | "error";

export interface ICrashReportingService {
  initialize(apiKey: string): void;
  captureException(error: unknown): void;
  captureMessage(message: string, level: CrashMessageLevel): void;
  setBreadcrumb(action: string, data: Record<string, unknown>): void;
}

interface CrashReportingServiceDependencies {
  readonly logger: LoggerLike;
}

export class CrashReportingService implements ICrashReportingService {
  private readonly dependencies: CrashReportingServiceDependencies;
  private apiKey: string | null = null;
  private readonly breadcrumbs: Array<{
    readonly action: string;
    readonly data: Record<string, unknown>;
  }> = [];

  public constructor(dependencies?: Partial<CrashReportingServiceDependencies>) {
    this.dependencies = {
      logger: dependencies?.logger ?? console,
    };
  }

  public initialize(apiKey: string): void {
    this.apiKey = apiKey;
    this.dependencies.logger.info?.("[CrashReporting] initialized", { apiKey });
  }

  public captureException(error: unknown): void {
    this.dependencies.logger.error?.("[CrashReporting] exception", {
      apiKey: this.apiKey,
      breadcrumbs: this.breadcrumbs,
      error,
    });
  }

  public captureMessage(message: string, level: CrashMessageLevel): void {
    const payload = {
      apiKey: this.apiKey,
      breadcrumbs: this.breadcrumbs,
      message,
      level,
    };

    if (level === "error") {
      this.dependencies.logger.error?.("[CrashReporting] message", payload);
      return;
    }

    if (level === "warning") {
      this.dependencies.logger.warn?.("[CrashReporting] message", payload);
      return;
    }

    this.dependencies.logger.info?.("[CrashReporting] message", payload);
  }

  public setBreadcrumb(action: string, data: Record<string, unknown>): void {
    this.breadcrumbs.push({ action, data });
    this.dependencies.logger.info?.("[CrashReporting] breadcrumb", { action, data });
  }
}

export const crashReportingService = new CrashReportingService();
