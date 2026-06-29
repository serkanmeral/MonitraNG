/**
 * One-off patch: Odak Sipariş Vue dosyalarında API catch bloklarına usePanelErrorNotify ekler.
 * Usage: node scripts/patch-odak-api-error-notify.mjs
 */
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const root = path.join(path.dirname(fileURLToPath(import.meta.url)), '..');
const dir = path.join(root, 'Mng.Ui/components/apps/odak-siparis');

const SKIP = new Set([
  'OdakSiparisCustomerDialog.vue', // already integrated
]);

const CATCH_OLD = /errorMessage\.value = e instanceof Error \? e\.message : String\(e\);/g;
const CATCH_NEW = "errorMessage.value = panelError(e, 'errors.dg.generic');";

for (const name of fs.readdirSync(dir)) {
  if (!name.endsWith('.vue') || SKIP.has(name)) continue;
  const file = path.join(dir, name);
  let src = fs.readFileSync(file, 'utf8');
  if (!CATCH_OLD.test(src)) continue;
  CATCH_OLD.lastIndex = 0;

  if (!src.includes('usePanelErrorNotify')) {
    if (src.includes("from '@/composables/useAppI18n'")) {
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
    const insertAfter = src.includes('const { t } = useAppI18n();')
      ? 'const { t } = useAppI18n();'
      : "const { t } = useAppI18n();";
    if (src.includes('const { t } = useAppI18n();') && !src.includes('const panelError = usePanelErrorNotify')) {
      src = src.replace(
        'const { t } = useAppI18n();',
        "const { t } = useAppI18n();\nconst panelError = usePanelErrorNotify('errors.dg.generic');",
      );
    }
  }

  src = src.replace(CATCH_OLD, CATCH_NEW);
  fs.writeFileSync(file, src, 'utf8');
  console.log('patched', name);
}
