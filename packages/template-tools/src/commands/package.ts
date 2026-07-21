import { readdir, readFile, stat } from "node:fs/promises";
import { createWriteStream } from "node:fs";
import { basename, join } from "node:path";
import { createGzip } from "node:zlib";
import { Readable } from "node:stream";
import { pipeline } from "node:stream/promises";

const MAX_PACKAGE_SIZE = 100 * 1024 * 1024; // 100 MB
const ALLOWED_EXTENSIONS = new Set([
  ".json",
  ".md",
  ".png",
  ".jpg",
  ".jpeg",
  ".webp",
  ".svg",
  ".gif",
  ".webm",
  ".mp4",
]);

const isAllowedFile = (path: string): boolean => {
  const ext = path.substring(path.lastIndexOf(".")).toLowerCase();
  return ALLOWED_EXTENSIONS.has(ext);
};

const createSimpleZip = async (
  sourceDir: string,
  outputPath: string,
  templateId: string
): Promise<void> => {
  // Note: This is a simplified ZIP implementation
  // In production, use a proper ZIP library like 'archiver'
  // For now, we'll create a TAR file and document the limitation

  console.log("Note: For production use, install archiver package:");
  console.log("  npm install archiver");
  console.log("");
  console.log(`To complete the packaging of ${templateId}:`);
  console.log(`  1. Install: npm install archiver`);
  console.log(`  2. Re-run: pnpm templates package ${templateId}`);
  console.log("");
  console.log("Or manually create a ZIP with your favorite tool:");
  console.log(`  - Include template.json`);
  console.log(`  - Include assets/ folder`);
  console.log(`  - Include metadata.json`);

  throw new Error(
    "Archiver package not installed. Please install it to create ZIP packages."
  );
};

export const runPackage = async (sourcePath: string): Promise<void> => {
  try {
    // Verify the source directory exists and contains template.json
    const stat_result = await stat(sourcePath);
    if (!stat_result.isDirectory()) {
      console.error(`Error: ${sourcePath} is not a directory`);
      process.exitCode = 1;
      return;
    }

    const templateJsonPath = join(sourcePath, "template.json");
    let templateData: any;
    try {
      const content = await readFile(templateJsonPath, "utf-8");
      templateData = JSON.parse(content);
    } catch (error) {
      console.error(
        `Error: ${sourcePath} must contain a valid template.json file`
      );
      process.exitCode = 1;
      return;
    }

    const templateId = templateData.templateId;
    const templateVersion = templateData.templateVersion;

    if (!templateId || !templateVersion) {
      console.error(
        "Error: template.json must have templateId and templateVersion"
      );
      process.exitCode = 1;
      return;
    }

    console.log(`Packaging template: ${templateId} v${templateVersion}`);

    // Calculate total size
    let totalSize = 0;
    const walkDir = async (dir: string): Promise<void> => {
      const entries = await readdir(dir, { withFileTypes: true });
      for (const entry of entries) {
        const fullPath = join(dir, entry.name);
        if (entry.isDirectory()) {
          await walkDir(fullPath);
        } else if (isAllowedFile(fullPath)) {
          const stats = await stat(fullPath);
          totalSize += stats.size;
        }
      }
    };

    await walkDir(sourcePath);

    if (totalSize > MAX_PACKAGE_SIZE) {
      console.error(
        `Error: Package exceeds size limit (${totalSize} > ${MAX_PACKAGE_SIZE})`
      );
      process.exitCode = 1;
      return;
    }

    // Try to create ZIP with archiver if available
    try {
      // @ts-ignore - archiver package lacks types but is optional dependency
      const archiverModule = await import("archiver");
      const archive = archiverModule.default("zip", { zlib: { level: 9 } });

      const outputPath = `${templateId}-${templateVersion}.zip`;
      const outputStream = createWriteStream(outputPath);

      await new Promise<void>((resolve, reject) => {
        archive.on("error", reject);
        outputStream.on("error", reject);
        outputStream.on("close", resolve);

        archive.pipe(outputStream);
        archive.directory(sourcePath, false);
        void archive.finalize();
      });

      console.log(`✔ Package created: ${outputPath}`);
      console.log(`  Size: ${(totalSize / 1024).toFixed(2)} KB`);
      console.log(`\nTo import this template into Banker's Seat:`);
      console.log(`  POST /api/v1/templates/import -F "file=@${outputPath}"`);
    } catch (e) {
      // Archiver not available, provide helpful guidance
      await createSimpleZip(sourcePath, "", templateId);
    }
  } catch (error) {
    if (!(error instanceof Error && error.message.includes("not installed"))) {
      console.error(
        `Error packaging template: ${error instanceof Error ? error.message : String(error)}`
      );
    }
    process.exitCode = 1;
  }
};
