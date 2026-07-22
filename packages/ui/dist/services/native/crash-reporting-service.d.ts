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
export declare class CrashReportingService implements ICrashReportingService {
    private readonly dependencies;
    private apiKey;
    private readonly breadcrumbs;
    constructor(dependencies?: Partial<CrashReportingServiceDependencies>);
    initialize(apiKey: string): void;
    captureException(error: unknown): void;
    captureMessage(message: string, level: CrashMessageLevel): void;
    setBreadcrumb(action: string, data: Record<string, unknown>): void;
}
export declare const crashReportingService: CrashReportingService;
export {};
//# sourceMappingURL=crash-reporting-service.d.ts.map