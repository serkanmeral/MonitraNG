/**
 * One-off patch: Operation Core Vue dosyalarında ocExtractDgErrorMessage → usePanelErrorNotify.
 * Usage: node scripts/patch-oc-api-error-notify.mjs
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const root = path.join(path.dirname(fileURLToPath(import.meta.url)), '..');

const SKIP = new Set([
  path.join(root, 'Mng.Ui/components/apps/operation-core/OcWorkItemFormDialog.vue'),
]);

function walk(dir, out = []) {
  if (!fs.existsSync(dir)) return out;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) walk(full, out);
    else if (entry.name.endsWith('.vue')) out.push(full);
  }
  return out;
}

const files = [
  ...walk(path.join(root, 'Mng.Ui/components/apps/operation-core')),
  ...walk(path.join(root, 'Mng.Ui/pages/apps/operation-core')),
].filter((f) => !SKIP.has(f));

function replaceExtract(src) {
  let next = src.replace(
    /ocExtractDgErrorMessage\s*\(\s*(\w+)\s*,\s*\n\s*t\(\s*'([^']+)'\s*\)\s*\n\s*\)/g,
    "panelError($1, '$2')",
  );
  next = next.replace(
    /ocExtractDgErrorMessage\s*\(\s*(\w+)\s*,\s*t\(\s*'([^']+)'\s*\)\s*\)/g,
    "panelError($1, '$2')",
  );
  return next;
}

function ensureSetup(src) {
  if (!src.includes('panelError(')) return src;

  if (!src.includes('usePanelErrorNotify')) {
    if (src.includes("import { useAppI18n } from '@/composables/useAppI18n';")) {
      src = src.replace(
        "import { useAppI18n } from '@/composables/useAppI18n';",
        "import { useAppI18n } from '@/composables/useAppI18n';\nimport { usePanelErrorNotify } from '@/composables/useApiErrorNotify';",
      );
    } else {
      src = src.replace(
        '<script setup lang="ts">',
        "<script setup lang=\"ts\">\nimport { usePanelErrorNotify } from '@/composables/useApiErrorNotify';",
      );
    }
  }

  if (!src.includes('const panelError = usePanelErrorNotify')) {
    if (src.includes('const { t } = useAppI18n();')) {
      src = src.replace(
        'const { t } = useAppI18n();',
        "const { t } = useAppI18n();\nconst panelError = usePanelErrorNotify('errors.dg.generic');",
      );
    } else if (src.includes('const { t, locale } = useAppI18n();')) {
      src = src.replace(
        'const { t, locale } = useAppI18n();',
        "const { t, locale } = useAppI18n();\nconst panelError = usePanelErrorNotify('errors.dg.generic');",
      );
    }
  }

  return src;
}

function dropUnusedImport(src, symbol) {
  if (new RegExp(`\\b${symbol}\\s*\\(`).test(src)) return src;
  return src
    .replace(new RegExp(`\\n\\s*${symbol},`, 'g'), '\n')
    .replace(new RegExp(`,\\s*${symbol}(?=\\s*[,}])`, 'g'), '')
    .replace(new RegExp(`${symbol},\\s*`, 'g'), '');
}

for (const file of files) {
  const before = fs.readFileSync(file, 'utf8');
  if (!before.includes('ocExtractDgErrorMessage')) continue;

  let src = replaceExtract(before);
  src = ensureSetup(src);
  src = dropUnusedImport(src, 'ocExtractDgErrorMessage');

  if (src !== before) {
    fs.writeFileSync(file, src, 'utf8');
    console.log('patched', path.relative(root, file));
  }
}
