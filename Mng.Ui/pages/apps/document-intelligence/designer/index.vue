<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import BaseBreadcrumb from '@/components/shared/BaseBreadcrumb.vue';
import DiResourceTree from '@/components/apps/document-intelligence/DiResourceTree.vue';
import DiTemplatePageStructureForm from '@/components/apps/document-intelligence/DiTemplatePageStructureForm.vue';
import { useResizableTreePanel } from '@/composables/useResizableTreePanel';
import { useAppI18n } from '@/composables/useAppI18n';
import { usePanelErrorNotify } from '@/composables/useApiErrorNotify';
import {
  diCreateBlankTemplate,
  diCreateTemplateCategory,
  diCreateTemplateFromReference,
  diDeleteTemplate,
  diDeleteTemplateCategory,
  diDuplicateTemplate,
  diErrorCode,

  diGetTemplate,
  diGetTemplateCategoryTree,
  diListLetterheads,
  diListCoverPages,
  diListTemplates,
  diPublishTemplate,
  diUnpublishTemplate,
  diRenameTemplateCategory,
  diUpdateTemplateMetadata,
  diUpdateTemplatePageStructure,
} from '@/services/documentIntelligenceService';
import type {
  DiTemplateParameter,
  DiTemplateSummary,
  DiTreeNode,
  DiLetterhead,
  DiCoverPage,
} from '@/types/apps/documentIntelligence';
import {
  LayoutSidebarLeftCollapseIcon,
  LayoutSidebarLeftExpandIcon,
} from 'vue-tabler-icons';

definePageMeta({ layout: 'default' });

const { t } = useAppI18n();
const panelError = usePanelErrorNotify('errors.dg.generic');
const route = useRoute();
const router = useRouter();

const {
  treeWidth,
  treeCollapsed,
  resizeActive,
  startResize,
  toggleTreeCollapse,
} = useResizableTreePanel('document-intelligence-designer-tree', {
  minWidth: 220,
  maxWidth: 420,
  defaultWidth: 280,
});

const breadcrumbs = computed(() => [
  { title: t('documentIntelligence.designer.title'), disabled: true },
]);

const tableHeaders = computed(() => [
  { title: t('documentIntelligence.designer.columnName'), key: 'name', sortable: true },
  { title: t('documentIntelligence.designer.columnCode'), key: 'code', sortable: true },
  { title: t('documentIntelligence.designer.columnStatus'), key: 'status', sortable: true, width: '120px' },
  { title: t('documentIntelligence.designer.columnActions'), key: 'actions', sortable: false, align: 'end' as const, width: '260px' },
]);

function resolveCatalogDefaultLetterheadId(items: DiLetterhead[]): string | null {
  const catalogDefault = items.find((item) => item.isDefault && item.isActive);
  if (catalogDefault) return catalogDefault.id;
  const firstActive = items.find((item) => item.isActive);
  return firstActive?.id ?? null;
}

function resolveCatalogDefaultCoverPageId(items: DiCoverPage[]): string | null {
  const catalogDefault = items.find((item) => item.isDefault && item.isActive);
  if (catalogDefault) return catalogDefault.id;
  const firstActive = items.find((item) => item.isActive);
  return firstActive?.id ?? null;
}

function isDraftTemplate(tpl: DiTemplateSummary): boolean {
  return (tpl.status ?? '').toLowerCase() === 'draft';
}

function statusLabel(status: string): string {
  const normalized = status.toLowerCase();
  if (normalized === 'draft') return t('documentIntelligence.designer.statusDraft');
  if (normalized === 'published') return t('documentIntelligence.designer.statusActive');
  return status;
}

const categoryTree = ref<DiTreeNode[]>([]);
const categoryTreeLoading = ref(false);
const selectedCategoryId = ref<string | null>(null);

const templatesLoading = ref(false);
const templates = ref<DiTemplateSummary[]>([]);

const error = ref<string | null>(null);
const notify = ref<string | null>(null);

const newCategoryDialog = ref(false);
const newCategoryName = ref('');
const creatingCategory = ref(false);

const renameCategoryDialog = ref(false);
const renameCategoryName = ref('');
const renamingCategory = ref(false);

const deleteCategoryDialog = ref(false);
const deletingCategory = ref(false);

const newTemplateDialog = ref(false);
const newTemplateName = ref('');
const newTemplateCode = ref('');
const newTemplateDefaultLetterheadId = ref<string | null>(null);
const newTemplateDefaultCoverPageId = ref<string | null>(null);
const creatingTemplate = ref(false);

const deleteTemplateDialog = ref(false);
const templateToDelete = ref<DiTemplateSummary | null>(null);
const deletingTemplate = ref(false);

const editTemplateDialog = ref(false);
const templateToEdit = ref<DiTemplateSummary | null>(null);
const editTemplateName = ref('');
const editTemplateCode = ref('');
const editDefaultLetterheadId = ref<string | null>(null);
const editDefaultCoverPageId = ref<string | null>(null);
const editLetterheadLoading = ref(false);
const savingTemplateMetadata = ref(false);

const publishTemplateDialog = ref(false);
const templateToPublish = ref<DiTemplateSummary | null>(null);
const publishingTemplate = ref(false);

const unpublishTemplateDialog = ref(false);
const templateToUnpublish = ref<DiTemplateSummary | null>(null);
const unpublishingTemplate = ref(false);
const unpublishThenEdit = ref(false);

const copyTemplateDialog = ref(false);
const templateToCopy = ref<DiTemplateSummary | null>(null);
const copyTargetCategoryId = ref<string | null>(null);
const copyTemplateName = ref('');
const copyTemplateCode = ref('');
const copyTemplateDescription = ref('');
const copyDefaultLetterheadId = ref<string | null>(null);
const copyDefaultCoverPageId = ref<string | null>(null);
const copySourceParameters = ref<DiTemplateParameter[]>([]);
const copyBrandingLoading = ref(false);
const copyingTemplate = ref(false);

const uploadDialog = ref(false);
const uploadFile = ref<File | null>(null);
const uploadName = ref('');
const uploading = ref(false);

const letterheads = ref<DiLetterhead[]>([]);
const letterheadsLoading = ref(false);
const coverPages = ref<DiCoverPage[]>([]);
const coverPagesLoading = ref(false);

const letterheadSelectOptions = computed(() =>
  letterheads.value
    .filter((item) => item.isActive)
    .map((item) => ({
      value: item.id,
      title: item.name,
      subtitle: item.code,
    }))
);

const coverPageSelectOptions = computed(() =>
  coverPages.value
    .filter((item) => item.isActive)
    .map((item) => ({
      value: item.id,
      title: item.name,
      subtitle: item.code,
    }))
);

const selectedCategoryName = computed(() => {
  if (!selectedCategoryId.value) return '';
  return findNodeName(categoryTree.value, selectedCategoryId.value) ?? '';
});

const categorySelectItems = computed(() => flattenCategoryOptions(categoryTree.value));

function flattenCategoryOptions(
  nodes: DiTreeNode[],
  prefix = ''
): { value: string; title: string }[] {
  const items: { value: string; title: string }[] = [];
  for (const node of nodes) {
    const title = prefix ? `${prefix} / ${node.name}` : node.name;
    items.push({ value: node.id, title });
    items.push(...flattenCategoryOptions(node.children, title));
  }
  return items;
}

function suggestCopyName(sourceName: string): string {
  return `${sourceName.trim()} (Kopya)`;
}

function suggestCopyCode(sourceCode: string | null | undefined): string {
  const base = (sourceCode ?? 'BELGE').trim() || 'BELGE';
  return `${base}-KOPYA`;
}

function resolveUniqueTemplateCode(preferred: string, existingCodes: string[]): string {
  const taken = new Set(
    existingCodes.map((code) => (code ?? '').trim().toLowerCase()).filter(Boolean)
  );
  const base = preferred.trim() || 'BELGE-KOPYA';
  if (!taken.has(base.toLowerCase())) return base;
  let suffix = 2;
  while (taken.has(`${base}-${suffix}`.toLowerCase())) suffix += 1;
  return `${base}-${suffix}`;
}

async function refreshCopySuggestedCode() {
  const categoryId = copyTargetCategoryId.value;
  const source = templateToCopy.value;
  if (!categoryId || !source) return;
  try {
    const res = await diListTemplates(categoryId);
    const codes = res.items.map((item) => item.code ?? '');
    copyTemplateCode.value = resolveUniqueTemplateCode(suggestCopyCode(source.code), codes);
  } catch {
    // Keep current code if category lookup fails.
  }
}

watch(copyTargetCategoryId, (categoryId) => {
  if (!copyTemplateDialog.value || !categoryId) return;
  void refreshCopySuggestedCode();
});

function findNodeName(nodes: DiTreeNode[], id: string): string | null {
  for (const node of nodes) {
    if (node.id === id) return node.name;
    const nested = findNodeName(node.children, id);
    if (nested) return nested;
  }
  return null;
}

async function loadLetterheads() {
  letterheadsLoading.value = true;
  try {
    const res = await diListLetterheads(true);
    letterheads.value = res.items;
  } catch {
    letterheads.value = [];
  } finally {
    letterheadsLoading.value = false;
  }
}

async function loadCoverPages() {
  coverPagesLoading.value = true;
  try {
    const res = await diListCoverPages(true);
    coverPages.value = res.items;
  } catch {
    coverPages.value = [];
  } finally {
    coverPagesLoading.value = false;
  }
}

async function loadCategoryTree() {
  categoryTreeLoading.value = true;
  error.value = null;
  try {
    categoryTree.value = await diGetTemplateCategoryTree();
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.designer.errors.categoryTree');
  } finally {
    categoryTreeLoading.value = false;
  }
}

async function refreshTemplates() {
  const categoryId = selectedCategoryId.value;
  if (!categoryId) {
    templates.value = [];
    return;
  }

  templatesLoading.value = true;
  error.value = null;
  try {
    const res = await diListTemplates(categoryId);
    templates.value = res.items;
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.designer.errors.list');
    templates.value = [];
  } finally {
    templatesLoading.value = false;
  }
}

async function selectCategory(categoryId: string | null) {
  selectedCategoryId.value = categoryId;
  await refreshTemplates();
}

function openNewTemplateDialog() {
  newTemplateName.value = '';
  newTemplateCode.value = '';
  newTemplateDefaultLetterheadId.value = resolveCatalogDefaultLetterheadId(letterheads.value);
  newTemplateDefaultCoverPageId.value = resolveCatalogDefaultCoverPageId(coverPages.value);
  newTemplateDialog.value = true;
}

async function submitNewTemplate() {
  const categoryId = selectedCategoryId.value;
  const name = newTemplateName.value.trim();
  const code = newTemplateCode.value.trim();
  if (!categoryId || !name || !code) {
    if (!code) error.value = t('documentIntelligence.designer.errors.codeRequired');
    return;
  }

  creatingTemplate.value = true;
  error.value = null;
  notify.value = null;
  try {
    const created = await diCreateBlankTemplate({ categoryId, name, code });
    await diUpdateTemplatePageStructure(created.id, {
      defaultLetterheadId: newTemplateDefaultLetterheadId.value,
      defaultCoverPageId: newTemplateDefaultCoverPageId.value,
    });
    newTemplateDialog.value = false;
    await refreshTemplates();
    notify.value = t('documentIntelligence.designer.blankCreated');
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.designer.errors.createBlank');
  } finally {
    creatingTemplate.value = false;
  }
}

function openEditor(tpl: DiTemplateSummary) {
  if (!isDraftTemplate(tpl)) return;
  navigateToTemplateEditor(tpl.id);
}

function openViewTemplate(tpl: DiTemplateSummary) {
  navigateToTemplateEditor(tpl.id);
}

function navigateToTemplateEditor(templateId: string) {
  const query = selectedCategoryId.value ? { categoryId: selectedCategoryId.value } : undefined;
  router.push({
    path: `/apps/document-intelligence/designer/${templateId}/edit`,
    query,
  });
}

function openUnpublishTemplateDialog(tpl: DiTemplateSummary, thenEdit = false) {
  templateToUnpublish.value = tpl;
  unpublishThenEdit.value = thenEdit;
  unpublishTemplateDialog.value = true;
}

async function submitUnpublishTemplate() {
  const tpl = templateToUnpublish.value;
  if (!tpl) return;

  unpublishingTemplate.value = true;
  error.value = null;
  notify.value = null;
  try {
    await diUnpublishTemplate(tpl.id);
    unpublishTemplateDialog.value = false;
    const editAfter = unpublishThenEdit.value;
    const templateId = tpl.id;
    templateToUnpublish.value = null;
    unpublishThenEdit.value = false;
    await refreshTemplates();
    notify.value = t('documentIntelligence.designer.unpublished');
    if (editAfter) {
      navigateToTemplateEditor(templateId);
    }
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.designer.errors.unpublish');
  } finally {
    unpublishingTemplate.value = false;
  }
}

function openParametersDialog(tpl: DiTemplateSummary) {
  const query = selectedCategoryId.value ? { categoryId: selectedCategoryId.value } : undefined;
  router.push({
    path: `/apps/document-intelligence/designer/${tpl.id}/parameters`,
    query,
  });
}

function openDeleteTemplateDialog(tpl: DiTemplateSummary) {
  templateToDelete.value = tpl;
  deleteTemplateDialog.value = true;
}

async function submitDeleteTemplate() {
  const tpl = templateToDelete.value;
  if (!tpl) return;

  deletingTemplate.value = true;
  error.value = null;
  try {
    await diDeleteTemplate(tpl.id);
    deleteTemplateDialog.value = false;
    templateToDelete.value = null;
    await refreshTemplates();
    notify.value = t('documentIntelligence.designer.templateDeleted');
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.designer.errors.deleteTemplate');
  } finally {
    deletingTemplate.value = false;
  }
}

function openEditTemplateDialog(tpl: DiTemplateSummary) {
  templateToEdit.value = tpl;
  editTemplateName.value = tpl.name;
  editTemplateCode.value = tpl.code ?? '';
  editDefaultLetterheadId.value = null;
  editDefaultCoverPageId.value = null;
  editLetterheadLoading.value = isDraftTemplate(tpl);
  editTemplateDialog.value = true;

  if (isDraftTemplate(tpl)) {
    void loadEditTemplateBranding(tpl.id);
  }
}

async function loadEditTemplateBranding(templateId: string) {
  editLetterheadLoading.value = true;
  try {
    const detail = await diGetTemplate(templateId);
    editDefaultLetterheadId.value = detail.defaultLetterheadId ?? resolveCatalogDefaultLetterheadId(letterheads.value);
    editDefaultCoverPageId.value = detail.defaultCoverPageId ?? resolveCatalogDefaultCoverPageId(coverPages.value);
  } catch {
    // Branding is optional; metadata dialog still works without it.
  } finally {
    editLetterheadLoading.value = false;
  }
}

async function submitEditTemplateMetadata() {
  const tpl = templateToEdit.value;
  const name = editTemplateName.value.trim();
  const code = editTemplateCode.value.trim();
  if (!tpl || !name || !code) {
    if (!code) error.value = t('documentIntelligence.designer.errors.codeRequired');
    return;
  }

  savingTemplateMetadata.value = true;
  error.value = null;
  notify.value = null;
  try {
    await diUpdateTemplateMetadata(tpl.id, { name, code });
    if (isDraftTemplate(tpl)) {
      await diUpdateTemplatePageStructure(tpl.id, {
        defaultLetterheadId: editDefaultLetterheadId.value,
        defaultCoverPageId: editDefaultCoverPageId.value,
      });
    }
    editTemplateDialog.value = false;
    templateToEdit.value = null;
    await refreshTemplates();
    notify.value = t('documentIntelligence.designer.metadataUpdated');
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.designer.errors.updateMetadata');
  } finally {
    savingTemplateMetadata.value = false;
  }
}

function openPublishTemplateDialog(tpl: DiTemplateSummary) {
  templateToPublish.value = tpl;
  publishTemplateDialog.value = true;
}

async function submitPublishTemplate() {
  const tpl = templateToPublish.value;
  if (!tpl) return;

  publishingTemplate.value = true;
  error.value = null;
  notify.value = null;
  try {
    await diPublishTemplate(tpl.id);
    publishTemplateDialog.value = false;
    templateToPublish.value = null;
    await refreshTemplates();
    notify.value = t('documentIntelligence.published');
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.designer.errors.publish');
  } finally {
    publishingTemplate.value = false;
  }
}

function openCopyTemplateDialog(tpl: DiTemplateSummary) {
  templateToCopy.value = tpl;
  copyTargetCategoryId.value = selectedCategoryId.value;
  copyTemplateName.value = suggestCopyName(tpl.name);
  copyTemplateCode.value = suggestCopyCode(tpl.code);
  copyTemplateDescription.value = '';
  copyDefaultLetterheadId.value = null;
  copyDefaultCoverPageId.value = null;
  copySourceParameters.value = [];
  copyBrandingLoading.value = true;
  copyTemplateDialog.value = true;
  void loadCopyTemplateDefaults(tpl.id);
  void refreshCopySuggestedCode();
}

async function loadCopyTemplateDefaults(templateId: string) {
  copyBrandingLoading.value = true;
  try {
    const detail = await diGetTemplate(templateId);
    copyTemplateDescription.value = detail.description ?? '';
    copyDefaultLetterheadId.value =
      detail.defaultLetterheadId ?? resolveCatalogDefaultLetterheadId(letterheads.value);
    copyDefaultCoverPageId.value =
      detail.defaultCoverPageId ?? resolveCatalogDefaultCoverPageId(coverPages.value);
    copySourceParameters.value = detail.parameters ?? [];
  } catch {
    // Defaults are enough to proceed.
  } finally {
    copyBrandingLoading.value = false;
  }
}

async function submitCopyTemplate() {
  const source = templateToCopy.value;
  const categoryId = copyTargetCategoryId.value;
  const name = copyTemplateName.value.trim();
  const code = copyTemplateCode.value.trim();
  if (!source || !categoryId || !name || !code) {
    if (!code) error.value = t('documentIntelligence.designer.errors.codeRequired');
    return;
  }

  copyingTemplate.value = true;
  error.value = null;
  notify.value = null;
  try {
    const created = await diDuplicateTemplate(source.id, {
      categoryId,
      name,
      code,
      description: copyTemplateDescription.value.trim() || null,
    });
    await diUpdateTemplatePageStructure(created.id, {
      defaultLetterheadId: copyDefaultLetterheadId.value,
      defaultCoverPageId: copyDefaultCoverPageId.value,
    });
    copyTemplateDialog.value = false;
    templateToCopy.value = null;
    await router.push({
      path: `/apps/document-intelligence/designer/${created.id}/edit`,
      query: { categoryId },
    });
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.designer.errors.duplicate');
  } finally {
    copyingTemplate.value = false;
  }
}

function openNewCategoryDialog() {
  newCategoryName.value = '';
  newCategoryDialog.value = true;
}

async function submitNewCategory() {
  const name = newCategoryName.value.trim();
  if (!name) return;
  creatingCategory.value = true;
  error.value = null;
  try {
    const created = await diCreateTemplateCategory({
      name,
      parentId: selectedCategoryId.value,
    });
    await loadCategoryTree();
    newCategoryDialog.value = false;
    await selectCategory(created.id);
    notify.value = t('documentIntelligence.designer.categoryCreated');
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.designer.errors.categoryCreate');
  } finally {
    creatingCategory.value = false;
  }
}

function openRenameCategoryDialog() {
  if (!selectedCategoryId.value) return;
  renameCategoryName.value = selectedCategoryName.value;
  renameCategoryDialog.value = true;
}

async function submitRenameCategory() {
  const id = selectedCategoryId.value;
  const name = renameCategoryName.value.trim();
  if (!id || !name) return;
  renamingCategory.value = true;
  error.value = null;
  try {
    await diRenameTemplateCategory(id, { name });
    await loadCategoryTree();
    renameCategoryDialog.value = false;
    notify.value = t('documentIntelligence.designer.categoryRenamed');
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.designer.errors.categoryRename');
  } finally {
    renamingCategory.value = false;
  }
}

function openDeleteCategoryDialog() {
  if (!selectedCategoryId.value) return;
  deleteCategoryDialog.value = true;
}

async function submitDeleteCategory() {
  const id = selectedCategoryId.value;
  if (!id) return;
  deletingCategory.value = true;
  error.value = null;
  try {
    await diDeleteTemplateCategory(id);
    deleteCategoryDialog.value = false;
    await loadCategoryTree();
    await selectCategory(null);
    notify.value = t('documentIntelligence.designer.categoryDeleted');
  } catch (e: unknown) {
    const code = diErrorCode(e);
    if (code === 'CATEGORY_HAS_TEMPLATES') {
      error.value = t('documentIntelligence.designer.errors.categoryHasTemplates');
    } else if (code === 'CATEGORY_NOT_EMPTY') {
      error.value = t('documentIntelligence.designer.errors.categoryNotEmpty');
    } else {
      error.value = panelError(e, 'documentIntelligence.designer.errors.categoryDelete');
    }
  } finally {
    deletingCategory.value = false;
  }
}

function openUploadDialog() {
  uploadFile.value = null;
  uploadName.value = '';
  uploadDialog.value = true;
}

function onUploadFileChange(files: File | File[] | null) {
  if (Array.isArray(files)) {
    uploadFile.value = files[0] ?? null;
  } else {
    uploadFile.value = files;
  }
  if (uploadFile.value && !uploadName.value.trim()) {
    const base = uploadFile.value.name.replace(/\.docx$/i, '');
    uploadName.value = base;
  }
}

function fileToBase64(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const result = reader.result;
      if (typeof result !== 'string') {
        reject(new Error('Invalid file read result'));
        return;
      }
      const base64 = result.includes(',') ? result.split(',')[1]! : result;
      resolve(base64);
    };
    reader.onerror = () => reject(reader.error ?? new Error('File read failed'));
    reader.readAsDataURL(file);
  });
}

async function submitUpload() {
  const categoryId = selectedCategoryId.value;
  const file = uploadFile.value;
  if (!categoryId || !file) return;

  uploading.value = true;
  error.value = null;
  try {
    const content = await fileToBase64(file);
    await diCreateTemplateFromReference({
      categoryId,
      name: uploadName.value.trim() || undefined,
      content,
      fileName: file.name,
      size: file.size,
    });
    uploadDialog.value = false;
    uploadFile.value = null;
    uploadName.value = '';
    await refreshTemplates();
    notify.value = t('documentIntelligence.designer.templateCreatedPlaceholderHint');
  } catch (e: unknown) {
    error.value = panelError(e, 'documentIntelligence.designer.errors.create');
  } finally {
    uploading.value = false;
  }
}

onMounted(async () => {
  await Promise.all([loadCategoryTree(), loadLetterheads(), loadCoverPages()]);
  const categoryId = typeof route.query.categoryId === 'string' ? route.query.categoryId : '';
  if (categoryId) {
    await selectCategory(categoryId);
  }
});
</script>

<template>
  <div>
    <BaseBreadcrumb :title="t('documentIntelligence.designer.title')" :breadcrumbs="breadcrumbs" />

    <div class="d-flex align-center justify-space-between mb-4 ga-2 flex-wrap">
      <p class="text-body-2 text-medium-emphasis mb-0">
        {{ t('documentIntelligence.designer.subtitle') }}
      </p>
      <v-btn variant="text" class="text-none" to="/apps/document-intelligence/designer/letterheads">
        {{ t('documentIntelligence.letterheads.title') }}
      </v-btn>
      <v-btn variant="text" class="text-none" to="/apps/document-intelligence/designer/cover-pages">
        {{ t('documentIntelligence.coverPages.title') }}
      </v-btn>
      <v-btn variant="text" class="text-none" to="/apps/document-intelligence/designer/tags">
        {{ t('documentIntelligence.tagsCatalog.title') }}
      </v-btn>
      <v-btn variant="tonal" class="text-none" to="/apps/document-intelligence" prepend-icon="mdi-folder-outline">
        {{ t('documentIntelligence.designer.openDocuments') }}
      </v-btn>
    </div>

    <v-alert v-if="error" type="error" variant="tonal" closable class="mb-3 rounded-lg" @click:close="error = null">
      {{ error }}
    </v-alert>
    <v-alert v-if="notify" type="success" variant="tonal" closable class="mb-3 rounded-lg" @click:close="notify = null">
      {{ notify }}
    </v-alert>

    <v-card elevation="10" rounded="lg" class="overflow-hidden">
      <div class="d-flex di-layout">
        <div
          v-if="!treeCollapsed"
          class="di-tree-panel flex-shrink-0"
          :style="{ width: treeWidth + 'px' }"
        >
          <div class="d-flex align-center justify-space-between px-3 py-2 border-b">
            <span class="text-subtitle-2 font-weight-bold">{{ t('documentIntelligence.designer.categoryTree') }}</span>
            <v-btn icon size="x-small" variant="text" :title="t('documentIntelligence.collapse')" @click="toggleTreeCollapse">
              <LayoutSidebarLeftCollapseIcon size="18" />
            </v-btn>
          </div>
          <div class="px-2 py-2 border-b">
            <v-btn
              block
              size="small"
              variant="tonal"
              color="primary"
              class="text-none"
              prepend-icon="mdi-folder-plus-outline"
              @click="openNewCategoryDialog"
            >
              {{ t('documentIntelligence.designer.newCategory') }}
            </v-btn>
          </div>
          <div class="pa-2 di-tree-scroll">
            <v-progress-linear v-if="categoryTreeLoading" indeterminate color="primary" class="mb-2" />
            <DiResourceTree
              :nodes="categoryTree"
              :selected-id="selectedCategoryId"
              :root-label="t('documentIntelligence.designer.allCategories')"
              :empty-label="t('documentIntelligence.designer.noCategories')"
              @select="selectCategory"
            />
          </div>
        </div>

        <div
          v-if="!treeCollapsed"
          :class="['di-resize-handle', { 'di-resize-handle--active': resizeActive }]"
          @mousedown.prevent="startResize"
        />

        <div class="di-content-panel flex-grow-1">
          <div class="d-flex align-center ga-2 px-4 py-2 border-b flex-wrap">
            <v-btn
              v-if="treeCollapsed"
              icon
              size="small"
              variant="text"
              :title="t('documentIntelligence.expand')"
              @click="toggleTreeCollapse"
            >
              <LayoutSidebarLeftExpandIcon size="18" />
            </v-btn>

            <template v-if="selectedCategoryId">
              <span class="text-subtitle-2 font-weight-bold">{{ selectedCategoryName }}</span>
              <v-spacer />
              <v-btn
                size="small"
                variant="text"
                class="text-none"
                prepend-icon="mdi-pencil-outline"
                @click="openRenameCategoryDialog"
              >
                {{ t('documentIntelligence.rename') }}
              </v-btn>
              <v-btn
                size="small"
                variant="text"
                color="error"
                class="text-none"
                prepend-icon="mdi-delete-outline"
                @click="openDeleteCategoryDialog"
              >
                {{ t('documentIntelligence.delete') }}
              </v-btn>
              <v-btn
                color="primary"
                variant="flat"
                size="small"
                class="text-none"
                prepend-icon="mdi-file-plus-outline"
                @click="openNewTemplateDialog"
              >
                {{ t('documentIntelligence.designer.newDocument') }}
              </v-btn>
              <v-btn
                size="small"
                variant="text"
                class="text-none"
                prepend-icon="mdi-file-upload-outline"
                @click="openUploadDialog"
              >
                {{ t('documentIntelligence.designer.uploadReference') }}
              </v-btn>
            </template>
          </div>

          <div v-if="!selectedCategoryId" class="pa-8 text-center text-body-2 text-medium-emphasis">
            {{ t('documentIntelligence.designer.pickCategory') }}
          </div>

          <div v-else class="pa-4">
            <div class="text-subtitle-2 font-weight-bold mb-3">
              {{ t('documentIntelligence.designer.templateList') }}
            </div>

            <v-data-table
              :headers="tableHeaders"
              :items="templates"
              :loading="templatesLoading"
              item-value="id"
              density="comfortable"
              class="rounded-lg border"
              :no-data-text="t('documentIntelligence.designer.noTemplates')"
            >
              <template #item.name="{ item }">
                <span class="font-weight-medium">{{ item.name }}</span>
              </template>

              <template #item.code="{ item }">
                <code v-if="item.code">{{ item.code }}</code>
                <span v-else class="text-medium-emphasis">—</span>
              </template>

              <template #item.status="{ item }">
                <v-chip
                  size="small"
                  variant="tonal"
                  label
                  :color="isDraftTemplate(item) ? 'warning' : 'success'"
                >
                  {{ statusLabel(item.status) }}
                </v-chip>
              </template>

              <template #item.actions="{ item }">
                <v-btn
                  icon
                  size="small"
                  variant="text"
                  :title="t('documentIntelligence.designer.copyTemplate')"
                  @click="openCopyTemplateDialog(item)"
                >
                  <v-icon icon="mdi-content-copy" />
                </v-btn>
                <v-btn
                  icon
                  size="small"
                  variant="text"
                  :title="t('documentIntelligence.designer.advancedParameters')"
                  @click="openParametersDialog(item)"
                >
                  <v-icon icon="mdi-form-select" />
                </v-btn>
                <v-btn
                  icon
                  size="small"
                  variant="text"
                  :title="t('documentIntelligence.designer.editMetadata')"
                  @click="openEditTemplateDialog(item)"
                >
                  <v-icon icon="mdi-pencil-outline" />
                </v-btn>
                <v-btn
                  v-if="isDraftTemplate(item)"
                  icon
                  size="small"
                  variant="text"
                  color="primary"
                  :title="t('documentIntelligence.designer.openEditor')"
                  @click="openEditor(item)"
                >
                  <v-icon icon="mdi-file-document-edit-outline" />
                </v-btn>
                <v-btn
                  v-else
                  icon
                  size="small"
                  variant="text"
                  color="primary"
                  :title="t('documentIntelligence.designer.viewTemplate')"
                  @click="openViewTemplate(item)"
                >
                  <v-icon icon="mdi-eye-outline" />
                </v-btn>
                <v-btn
                  v-if="!isDraftTemplate(item)"
                  icon
                  size="small"
                  variant="text"
                  color="warning"
                  :title="t('documentIntelligence.designer.unpublishToEdit')"
                  @click="openUnpublishTemplateDialog(item, true)"
                >
                  <v-icon icon="mdi-lock-open-variant-outline" />
                </v-btn>
                <v-btn
                  v-if="isDraftTemplate(item)"
                  icon
                  size="small"
                  variant="text"
                  color="success"
                  :title="t('documentIntelligence.designer.activateForGeneration')"
                  @click="openPublishTemplateDialog(item)"
                >
                  <v-icon icon="mdi-publish" />
                </v-btn>
                <v-btn
                  icon
                  size="small"
                  variant="text"
                  color="error"
                  :title="t('documentIntelligence.delete')"
                  @click="openDeleteTemplateDialog(item)"
                >
                  <v-icon icon="mdi-delete-outline" />
                </v-btn>
              </template>
            </v-data-table>
          </div>
        </div>
      </div>
    </v-card>

    <v-dialog v-model="newTemplateDialog" max-width="520">
      <v-card rounded="lg">
        <v-card-title class="text-h6 font-weight-bold">
          {{ t('documentIntelligence.designer.newDocument') }}
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <p class="text-body-2 text-medium-emphasis mb-4">
            {{ t('documentIntelligence.designer.newDocumentHint') }}
          </p>
          <v-text-field
            v-model="newTemplateName"
            :label="t('documentIntelligence.designer.templateName')"
            density="comfortable"
            variant="outlined"
            hide-details
            autofocus
            class="mb-3"
          />
          <v-text-field
            v-model="newTemplateCode"
            :label="t('documentIntelligence.designer.templateCode')"
            density="comfortable"
            variant="outlined"
            hide-details
            class="mb-2"
            @keydown.enter="submitNewTemplate"
          />

          <v-divider class="my-4" />
          <DiTemplatePageStructureForm
            v-model:default-letterhead-id="newTemplateDefaultLetterheadId"
            v-model:default-cover-page-id="newTemplateDefaultCoverPageId"
            :letterhead-options="letterheadSelectOptions"
            :cover-page-options="coverPageSelectOptions"
            :loading="letterheadsLoading || coverPagesLoading"
          />
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="newTemplateDialog = false">
            {{ t('documentIntelligence.cancel') }}
          </v-btn>
          <v-btn
            color="primary"
            variant="flat"
            class="text-none"
            :loading="creatingTemplate"
            :disabled="!newTemplateName.trim() || !newTemplateCode.trim()"
            @click="submitNewTemplate"
          >
            {{ t('documentIntelligence.create') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="editTemplateDialog" max-width="640">
      <v-card rounded="lg">
        <v-card-title class="text-h6 font-weight-bold">
          {{ t('documentIntelligence.designer.editMetadata') }}
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <p class="text-body-2 text-medium-emphasis mb-4">
            {{ t('documentIntelligence.designer.editMetadataHint') }}
          </p>
          <v-text-field
            v-model="editTemplateName"
            :label="t('documentIntelligence.designer.templateName')"
            density="comfortable"
            variant="outlined"
            hide-details
            autofocus
            class="mb-3"
          />
          <v-text-field
            v-model="editTemplateCode"
            :label="t('documentIntelligence.designer.templateCode')"
            density="comfortable"
            variant="outlined"
            hide-details
            class="mb-2"
            @keydown.enter="submitEditTemplateMetadata"
          />

          <template v-if="templateToEdit && isDraftTemplate(templateToEdit)">
            <v-divider class="my-4" />
            <DiTemplatePageStructureForm
              v-model:default-letterhead-id="editDefaultLetterheadId"
              v-model:default-cover-page-id="editDefaultCoverPageId"
              :letterhead-options="letterheadSelectOptions"
              :cover-page-options="coverPageSelectOptions"
              draft-hint
              :loading="editLetterheadLoading || letterheadsLoading || coverPagesLoading"
            />
          </template>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="editTemplateDialog = false">
            {{ t('documentIntelligence.cancel') }}
          </v-btn>
          <v-btn
            color="primary"
            variant="flat"
            class="text-none"
            :loading="savingTemplateMetadata"
            :disabled="!editTemplateName.trim() || !editTemplateCode.trim()"
            @click="submitEditTemplateMetadata"
          >
            {{ t('documentIntelligence.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="copyTemplateDialog" max-width="560">
      <v-card rounded="lg">
        <v-card-title class="text-h6 font-weight-bold">
          {{ t('documentIntelligence.designer.copyTemplateTitle') }}
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <p class="text-body-2 text-medium-emphasis mb-4">
            {{ t('documentIntelligence.designer.copyTemplateHint') }}
          </p>
          <p v-if="templateToCopy" class="text-body-2 mb-4">
            {{ t('documentIntelligence.designer.columnName') }}:
            <strong>{{ templateToCopy.name }}</strong>
          </p>
          <v-select
            v-model="copyTargetCategoryId"
            :items="categorySelectItems"
            item-title="title"
            item-value="value"
            :label="t('documentIntelligence.designer.copyTargetCategory')"
            density="comfortable"
            variant="outlined"
            hide-details
            class="mb-3"
          />
          <v-text-field
            v-model="copyTemplateName"
            :label="t('documentIntelligence.designer.templateName')"
            density="comfortable"
            variant="outlined"
            hide-details
            autofocus
            class="mb-3"
          />
          <v-text-field
            v-model="copyTemplateCode"
            :label="t('documentIntelligence.designer.templateCode')"
            density="comfortable"
            variant="outlined"
            hide-details
            class="mb-3"
          />
          <v-text-field
            v-model="copyTemplateDescription"
            :label="t('documentIntelligence.designer.templateDescription')"
            density="comfortable"
            variant="outlined"
            hide-details
            class="mb-2"
          />

          <div v-if="copySourceParameters.length" class="mb-4">
            <div class="text-subtitle-2 font-weight-bold mb-1">
              {{ t('documentIntelligence.designer.copyParametersTitle', { count: copySourceParameters.length }) }}
            </div>
            <p class="text-caption text-medium-emphasis mb-2">
              {{ t('documentIntelligence.designer.copyParametersHint') }}
            </p>
            <div class="d-flex flex-wrap ga-2">
              <v-chip
                v-for="param in copySourceParameters"
                :key="param.key"
                size="small"
                variant="tonal"
                label
              >
                {{ param.label || param.key }}
                <span
                  v-if="param.label && param.key !== param.label"
                  class="text-medium-emphasis ms-1"
                >
                  ({{ param.key }})
                </span>
              </v-chip>
            </div>
          </div>

          <v-progress-linear v-if="copyBrandingLoading" indeterminate color="primary" class="my-4" />
          <template v-else>
            <v-divider class="my-4" />
            <DiTemplatePageStructureForm
              v-model:default-letterhead-id="copyDefaultLetterheadId"
              v-model:default-cover-page-id="copyDefaultCoverPageId"
              :letterhead-options="letterheadSelectOptions"
              :cover-page-options="coverPageSelectOptions"
              :loading="copyBrandingLoading || letterheadsLoading || coverPagesLoading"
            />
          </template>
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="copyTemplateDialog = false">
            {{ t('documentIntelligence.cancel') }}
          </v-btn>
          <v-btn
            color="primary"
            variant="flat"
            class="text-none"
            :loading="copyingTemplate"
            :disabled="!copyTemplateName.trim() || !copyTemplateCode.trim() || !copyTargetCategoryId"
            @click="submitCopyTemplate"
          >
            {{ t('documentIntelligence.designer.copyTemplate') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="publishTemplateDialog" max-width="480">
      <v-card rounded="lg">
        <v-card-title class="text-h6 font-weight-bold">
          {{ t('documentIntelligence.designer.activateForGeneration') }}
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4 text-body-2">
          {{
            t('documentIntelligence.designer.publishConfirm', {
              name: templateToPublish?.name ?? '',
            })
          }}
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="publishTemplateDialog = false">
            {{ t('documentIntelligence.cancel') }}
          </v-btn>
          <v-btn
            color="success"
            variant="flat"
            class="text-none"
            :loading="publishingTemplate"
            @click="submitPublishTemplate"
          >
            {{ t('documentIntelligence.designer.activateForGeneration') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="unpublishTemplateDialog" max-width="520">
      <v-card rounded="lg">
        <v-card-title class="text-h6 font-weight-bold">
          {{ t('documentIntelligence.designer.unpublishToEdit') }}
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4 text-body-2">
          {{
            t('documentIntelligence.designer.unpublishConfirm', {
              name: templateToUnpublish?.name ?? '',
            })
          }}
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="unpublishTemplateDialog = false">
            {{ t('documentIntelligence.cancel') }}
          </v-btn>
          <v-btn
            color="warning"
            variant="flat"
            class="text-none"
            :loading="unpublishingTemplate"
            @click="submitUnpublishTemplate"
          >
            {{ t('documentIntelligence.designer.unpublishToEdit') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="deleteTemplateDialog" max-width="480">
      <v-card rounded="lg">
        <v-card-title class="text-h6 font-weight-bold">
          {{ t('documentIntelligence.delete') }}
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4 text-body-2">
          {{
            t('documentIntelligence.designer.deleteTemplateConfirm', {
              name: templateToDelete?.name ?? '',
            })
          }}
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="deleteTemplateDialog = false">
            {{ t('documentIntelligence.cancel') }}
          </v-btn>
          <v-btn
            color="error"
            variant="flat"
            class="text-none"
            :loading="deletingTemplate"
            @click="submitDeleteTemplate"
          >
            {{ t('documentIntelligence.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="newCategoryDialog" max-width="440">
      <v-card rounded="lg">
        <v-card-title class="text-h6 font-weight-bold">
          {{ t('documentIntelligence.designer.newCategory') }}
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <p v-if="selectedCategoryId" class="text-body-2 text-medium-emphasis mb-3">
            {{ t('documentIntelligence.designer.newCategoryUnder', { name: selectedCategoryName }) }}
          </p>
          <v-text-field
            v-model="newCategoryName"
            :label="t('documentIntelligence.designer.categoryName')"
            density="comfortable"
            variant="outlined"
            hide-details
            autofocus
            @keydown.enter="submitNewCategory"
          />
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="newCategoryDialog = false">
            {{ t('documentIntelligence.cancel') }}
          </v-btn>
          <v-btn
            color="primary"
            variant="flat"
            class="text-none"
            :loading="creatingCategory"
            :disabled="!newCategoryName.trim()"
            @click="submitNewCategory"
          >
            {{ t('documentIntelligence.create') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="renameCategoryDialog" max-width="440">
      <v-card rounded="lg">
        <v-card-title class="text-h6 font-weight-bold">
          {{ t('documentIntelligence.rename') }}
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <v-text-field
            v-model="renameCategoryName"
            :label="t('documentIntelligence.designer.categoryName')"
            density="comfortable"
            variant="outlined"
            hide-details
            autofocus
            @keydown.enter="submitRenameCategory"
          />
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="renameCategoryDialog = false">
            {{ t('documentIntelligence.cancel') }}
          </v-btn>
          <v-btn
            color="primary"
            variant="flat"
            class="text-none"
            :loading="renamingCategory"
            :disabled="!renameCategoryName.trim()"
            @click="submitRenameCategory"
          >
            {{ t('documentIntelligence.save') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="deleteCategoryDialog" max-width="480">
      <v-card rounded="lg">
        <v-card-title class="text-h6 font-weight-bold">
          {{ t('documentIntelligence.delete') }}
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4 text-body-2">
          {{ t('documentIntelligence.designer.deleteCategoryConfirm', { name: selectedCategoryName }) }}
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="deleteCategoryDialog = false">
            {{ t('documentIntelligence.cancel') }}
          </v-btn>
          <v-btn
            color="error"
            variant="flat"
            class="text-none"
            :loading="deletingCategory"
            @click="submitDeleteCategory"
          >
            {{ t('documentIntelligence.delete') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

    <v-dialog v-model="uploadDialog" max-width="520">
      <v-card rounded="lg">
        <v-card-title class="text-h6 font-weight-bold">
          {{ t('documentIntelligence.designer.uploadReference') }}
        </v-card-title>
        <v-divider />
        <v-card-text class="pa-4">
          <p class="text-body-2 text-medium-emphasis mb-4">
            {{ t('documentIntelligence.designer.uploadHint') }}
          </p>
          <v-file-input
            accept=".docx,application/vnd.openxmlformats-officedocument.wordprocessingml.document"
            :label="t('documentIntelligence.designer.referenceFile')"
            density="comfortable"
            variant="outlined"
            prepend-icon="mdi-file-word-outline"
            show-size
            class="mb-3"
            @update:model-value="onUploadFileChange"
          />
          <v-text-field
            v-model="uploadName"
            :label="t('documentIntelligence.designer.uploadTemplateName')"
            density="comfortable"
            variant="outlined"
            hide-details
          />
        </v-card-text>
        <v-divider />
        <v-card-actions class="pa-4">
          <v-spacer />
          <v-btn variant="text" class="text-none" @click="uploadDialog = false">
            {{ t('documentIntelligence.cancel') }}
          </v-btn>
          <v-btn
            color="primary"
            variant="flat"
            class="text-none"
            :loading="uploading"
            :disabled="!uploadFile"
            @click="submitUpload"
          >
            {{ t('documentIntelligence.create') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>

  </div>
</template>

<style scoped>
.di-layout {
  min-height: 480px;
}

.di-tree-panel {
  border-right: 1px solid rgba(var(--v-theme-on-surface), 0.08);
  display: flex;
  flex-direction: column;
  min-height: 480px;
}

.di-tree-scroll {
  flex: 1;
  overflow: auto;
  max-height: calc(100vh - 280px);
}

.di-content-panel {
  min-width: 0;
  min-height: 480px;
}

.di-resize-handle {
  width: 6px;
  cursor: col-resize;
  flex-shrink: 0;
  background: transparent;
  transition: background 0.15s ease;
}

.di-resize-handle:hover,
.di-resize-handle--active {
  background: rgba(var(--v-theme-primary), 0.25);
}

.border-b {
  border-bottom: 1px solid rgba(var(--v-theme-on-surface), 0.08);
}

.border {
  border: 1px solid rgba(var(--v-theme-on-surface), 0.08);
}
</style>
