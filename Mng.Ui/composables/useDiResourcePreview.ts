import { ref, type InjectionKey } from 'vue';
import { diGetById } from '@/services/documentIntelligenceService';
import { DI_RESOURCE_TYPE, type DiResource } from '@/types/apps/documentIntelligence';
import { isDiOfficeEditable, isDiPreviewable } from '@/utils/diFilePreview';
import { buildDiFolderUrl, buildDiResourceUrl } from '@/utils/diResourceLink';

export type DiResourcePreviewContext = {
  openResourceById: (resourceId: string, event?: MouseEvent) => Promise<void>;
};

export const DI_RESOURCE_PREVIEW_KEY: InjectionKey<DiResourcePreviewContext> =
  Symbol('diResourcePreview');

/** DI iç link tıklamasında modal önizleme (markdown / dosya / editör). */
export function useDiResourcePreview(options?: { onDownload?: (resource: DiResource) => void }) {
  const markdownPreviewOpen = ref(false);
  const markdownPreviewResource = ref<DiResource | null>(null);
  const filePreviewOpen = ref(false);
  const filePreviewResource = ref<DiResource | null>(null);
  const editorOpen = ref(false);
  const editorResource = ref<DiResource | null>(null);
  const opening = ref(false);

  async function openResourceById(resourceId: string, event?: MouseEvent) {
    const id = resourceId.trim();
    if (!id) return;

    if (event && (event.metaKey || event.ctrlKey || event.shiftKey)) {
      window.open(buildDiResourceUrl(id), '_blank', 'noopener,noreferrer');
      return;
    }
    event?.preventDefault();

    opening.value = true;
    try {
      const resource = await diGetById(id);
      await openResource(resource);
    } finally {
      opening.value = false;
    }
  }

  async function openResource(resource: DiResource) {
    if (resource.type === DI_RESOURCE_TYPE.folder) {
      await navigateTo(buildDiFolderUrl(resource.id));
      return;
    }

    if (resource.type === DI_RESOURCE_TYPE.markdown) {
      markdownPreviewResource.value = resource;
      markdownPreviewOpen.value = true;
      return;
    }

    if (resource.type === DI_RESOURCE_TYPE.file) {
      if (isDiOfficeEditable(resource)) {
        editorResource.value = resource;
        editorOpen.value = true;
        return;
      }
      if (isDiPreviewable(resource)) {
        filePreviewResource.value = resource;
        filePreviewOpen.value = true;
        return;
      }
      options?.onDownload?.(resource);
    }
  }

  const previewContext: DiResourcePreviewContext = {
    openResourceById,
  };

  return {
    markdownPreviewOpen,
    markdownPreviewResource,
    filePreviewOpen,
    filePreviewResource,
    editorOpen,
    editorResource,
    opening,
    openResourceById,
    openResource,
    previewContext,
  };
}
