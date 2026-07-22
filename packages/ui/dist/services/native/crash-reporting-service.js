export class CrashReportingService {
    dependencies;
    apiKey = null;
    breadcrumbs = [];
    constructor(dependencies) {
        this.dependencies = {
            logger: dependencies?.logger ?? console,
        };
    }
    initialize(apiKey) {
        this.apiKey = apiKey;
        this.dependencies.logger.info?.("[CrashReporting] initialized", { apiKey });
    }
    captureException(error) {
        this.dependencies.logger.error?.("[CrashReporting] exception", {
            apiKey: this.apiKey,
            breadcrumbs: this.breadcrumbs,
            error,
        });
    }
    captureMessage(message, level) {
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
    setBreadcrumb(action, data) {
        this.breadcrumbs.push({ action, data });
        this.dependencies.logger.info?.("[CrashReporting] breadcrumb", { action, data });
    }
}
export const crashReportingService = new CrashReportingService();
