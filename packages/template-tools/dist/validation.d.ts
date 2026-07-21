import { type ValidateFunction } from "ajv";
export type TemplateValidationIssue = {
    code: string;
    path: string;
    message: string;
};
export type TemplateValidationResult = {
    valid: boolean;
    issues: TemplateValidationIssue[];
};
export declare const discoverTemplateFiles: (rootDir: string) => Promise<string[]>;
export declare const loadSchemaValidator: (schemaPath: string) => Promise<ValidateFunction<unknown>>;
export declare const validateTemplateObject: (template: unknown, schemaValidator: ValidateFunction<unknown>, templateDir: string) => TemplateValidationResult;
export declare const validateTemplateFile: (templatePath: string, schemaValidator: ValidateFunction<unknown>) => Promise<TemplateValidationResult>;
//# sourceMappingURL=validation.d.ts.map