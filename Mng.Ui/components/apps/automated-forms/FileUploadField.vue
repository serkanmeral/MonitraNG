<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { UploadIcon, XIcon, DownloadIcon, FileIcon, ImageIcon, EyeIcon } from 'vue-tabler-icons';
import { fetchFromDataGateway, getDataGatewayProxyUrl, fetchBlobFromDataGateway } from '@/services/apiService';

const props = defineProps<{
  field: any;
  modelValue: any; // Legacy: string (path). New: object { path, file_name, file_ext, file_size (KB), upload_time, upload_person }. Or upload payload { content, ... }. Or array of these.
  readonly?: boolean;
  disabled?: boolean;
  errorMessages?: string[];
  datasetName?: string;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: any];
}>();

// Get i18n instance
const nuxtApp = useNuxtApp();
const i18n = nuxtApp.vueApp.config.globalProperties.$i18n;
const t = (key: string, params?: any) => {
  if (i18n && i18n.t) {
    return i18n.t(key, params);
  }
  if (i18n?.global?.t) {
    return i18n.global.t(key, params);
  }
  return key;
};

const fileInput = ref<HTMLInputElement | null>(null);
const isUploading = ref(false);
const error = ref<string | null>(null);
const previewUrls = ref<Array<{ url: string; fileName: string; mimeType: string; filePath?: string; isNew?: boolean; fileSize?: number }>>([]);
const fileMetadata = ref<Array<any>>([]);
const showPreviewDialog = ref(false);
const previewDialogImage = ref<string | null>(null);
const previewDialogObjectUrl = ref<string | null>(null); // blob için; kapatırken revoke
const previewDialogLoading = ref(false);
const isDragging = ref(false);
const newFiles = ref<Array<{ file: File; preview: string; fileName: string; mimeType: string; fileSize: number }>>([]);

// Check if field is array
const isArray = computed(() => props.field?.isArray || false);

// Normalize backend value: support legacy string path and new object { path, file_name, ... }
const getPathFromFileValue = (v: any): string | null => {
  if (v == null) return null;
  if (typeof v === 'string') return v.trim() || null;
  if (typeof v === 'object' && v && typeof v.path === 'string') return v.path.trim() || null;
  return null;
};

const getFileNameFromFileValue = (v: any): string => {
  if (v && typeof v === 'object' && v.file_name) return String(v.file_name);
  const p = getPathFromFileValue(v);
  return p ? p.split('/').pop() || 'file' : 'file';
};

// Local value for two-way binding
const localValue = computed({
  get: () => {
    return props.modelValue;
  },
  set: (val) => {
    emit('update:modelValue', val);
  },
});

// Update previews with new files
const updatePreviewsWithNewFiles = () => {
  // Get existing previews (both existing files and already-added new files)
  const existingPreviews = previewUrls.value.filter(p => !p.isNew);
  const existingNewPreviews = previewUrls.value.filter(p => p.isNew);
  
  // Create previews for all new files
  const allNewPreviews = newFiles.value.map(nf => ({
    url: nf.preview,
    fileName: nf.fileName,
    mimeType: nf.mimeType,
    isNew: true,
    fileSize: nf.fileSize
  }));
  
  // For array field, merge existing previews with all new previews
  // For single field, replace with new previews if any, otherwise keep existing
  if (isArray.value) {
    previewUrls.value = [...existingPreviews, ...allNewPreviews];
  } else {
    previewUrls.value = allNewPreviews.length > 0 ? allNewPreviews : existingPreviews;
  }
};

// Uzantıyı nokta ile ekler; zaten varsa eklemez
const withExtension = (baseName: string, fileExt?: string | null): string => {
  if (!fileExt || typeof fileExt !== 'string') return baseName;
  const ext = fileExt.startsWith('.') ? fileExt : `.${fileExt}`;
  return baseName.toLowerCase().endsWith(ext.toLowerCase()) ? baseName : baseName + ext;
};

// Load preview for single file. stored: optional new backend format { file_name, file_ext, file_size (KB), ... }
const loadPreview = async (filePath: string, stored?: { file_name?: string; file_ext?: string; file_size?: number }) => {
  if (!filePath || typeof filePath !== 'string') {
    previewUrls.value = [];
    return;
  }
  const defaultFileName = stored?.file_name || filePath.split('/').pop() || 'file';
  const fileSizeBytes = stored && typeof stored.file_size === 'number' ? stored.file_size * 1024 : undefined;

  try {
    const metadataResponse = await fetchFromDataGateway(
      `/api/v1/files/metadata?filePath=${encodeURIComponent(filePath)}`,
      'GET'
    );

    if (metadataResponse && metadataResponse.data) {
      const metadata = metadataResponse.data;
      const mimeType = metadata.mimeType || 'application/octet-stream';
      const isImage = mimeType.startsWith('image/');
      const baseName = stored?.file_name || metadata.originalFileName || defaultFileName;
      const fileName = withExtension(baseName, stored?.file_ext);
      const size = fileSizeBytes ?? metadata.fileSize;
      if (isImage) {
        const downloadUrl = getDataGatewayProxyUrl(`/api/v1/files/download?filePath=${encodeURIComponent(filePath)}`);
        previewUrls.value = [{
          url: downloadUrl,
          fileName,
          mimeType,
          filePath,
          isNew: false,
          fileSize: size
        }];
      } else {
        previewUrls.value = [{
          url: '',
          fileName,
          mimeType,
          filePath,
          isNew: false,
          fileSize: size
        }];
      }
      fileMetadata.value = [metadata];
    }
  } catch (err) {
    console.error('Error loading file preview:', err);
    previewUrls.value = [{
      url: '',
      fileName: defaultFileName,
      mimeType: 'application/octet-stream',
      filePath,
      isNew: false,
      fileSize: fileSizeBytes
    }];
  }
};

// Load previews for array of files (items: legacy path string, or new { path, file_name, file_size, ... }, or upload { content, ... })
const loadPreviewsForArray = async (items: (string | any)[]) => {
  const existingPreviews: typeof previewUrls.value = [];
  fileMetadata.value = [];

  const existingItems = items.filter((item: any) => getPathFromFileValue(item) != null && !(typeof item === 'object' && item && item.content));

  for (const item of existingItems) {
    const filePath = getPathFromFileValue(item)!;
    const stored = typeof item === 'object' && item ? item : null;
    const defaultFileName = getFileNameFromFileValue(item);
    const fileSizeBytes = stored && typeof stored.file_size === 'number' ? stored.file_size * 1024 : undefined;
    try {
      const metadataResponse = await fetchFromDataGateway(
        `/api/v1/files/metadata?filePath=${encodeURIComponent(filePath)}`,
        'GET'
      );

      if (metadataResponse && metadataResponse.data) {
        const metadata = metadataResponse.data;
        const mimeType = metadata.mimeType || 'application/octet-stream';
        const isImage = mimeType.startsWith('image/');
        const baseName = (stored && stored.file_name) || metadata.originalFileName || defaultFileName;
        const fileName = withExtension(baseName, stored?.file_ext);
        const size = fileSizeBytes ?? metadata.fileSize;
        if (isImage) {
          const downloadUrl = getDataGatewayProxyUrl(`/api/v1/files/download?filePath=${encodeURIComponent(filePath)}`);
          existingPreviews.push({
            url: downloadUrl,
            fileName,
            mimeType,
            filePath,
            isNew: false,
            fileSize: size
          });
        } else {
          existingPreviews.push({
            url: '',
            fileName,
            mimeType,
            filePath,
            isNew: false,
            fileSize: size
          });
        }
        fileMetadata.value.push(metadata);
      }
    } catch (err) {
      console.error('Error loading file preview:', err);
      existingPreviews.push({
        url: '',
        fileName: defaultFileName,
        mimeType: 'application/octet-stream',
        filePath,
        isNew: false,
        fileSize: fileSizeBytes
      });
    }
  }

  previewUrls.value = existingPreviews;
  updatePreviewsWithNewFiles();
};

// Handle file select
const handleFileSelect = (event: Event) => {
  const target = event.target as HTMLInputElement;
  if (target.files && target.files.length > 0) {
    const files = Array.from(target.files);
    if (isArray.value) {
      handleMultipleFiles(files);
    } else {
      // For single file field, only take the first file even if multiple are selected
      handleSingleFile(files[0]);
    }
  }
  
  // Reset input
  if (fileInput.value) {
    fileInput.value.value = '';
  }
};

// Handle drag and drop
const handleDragOver = (event: DragEvent) => {
  event.preventDefault();
  event.stopPropagation();
  if (!readonly && !disabled) {
    isDragging.value = true;
  }
};

const handleDragLeave = (event: DragEvent) => {
  event.preventDefault();
  event.stopPropagation();
  isDragging.value = false;
};

const handleDrop = (event: DragEvent) => {
  event.preventDefault();
  event.stopPropagation();
  isDragging.value = false;
  
  if (readonly || disabled) return;
  
  if (event.dataTransfer?.files && event.dataTransfer.files.length > 0) {
    const files = Array.from(event.dataTransfer.files);
    if (isArray.value) {
      handleMultipleFiles(files);
    } else {
      handleSingleFile(files[0]);
    }
  }
};

// Handle single file
const handleSingleFile = async (file: File) => {
  error.value = null;
  isUploading.value = true;

  try {
    // Validate file size (5MB default, can be configured from field.fileOptions)
    const maxSize = props.field?.fileOptions?.maxSize || 5 * 1024 * 1024; // 5MB default
    if (file.size > maxSize) {
      error.value = t('automated-forms.fileUpload.errors.fileTooBig', { max: (maxSize / 1024 / 1024).toFixed(0) });
      isUploading.value = false;
      return;
    }

    // Validate file extension if configured
    if (props.field?.fileOptions?.allowedExtensions && props.field.fileOptions.allowedExtensions.length > 0) {
      const fileExtension = '.' + file.name.split('.').pop()?.toLowerCase();
      if (!props.field.fileOptions.allowedExtensions.includes(fileExtension)) {
        error.value = t('automated-forms.fileUpload.errors.fileTypeNotAllowed', { types: props.field.fileOptions.allowedExtensions.join(', ') });
        isUploading.value = false;
        return;
      }
    }

    // Create preview for new file
    const preview = await createFilePreview(file);
    newFiles.value = [{
      file: file,
      preview: preview,
      fileName: file.name,
      mimeType: file.type || 'application/octet-stream',
      fileSize: file.size
    }];

    // Convert to base64
    const base64 = await fileToBase64(file);

    // Create file object for backend (originalFileName = gerçek dosya adı; yoksa backend file_YYYYMMDD_HHmmss üretir)
    const fileObject = {
      content: base64,
      folder: undefined, // Can be configured later
      useCompression: props.field?.fileOptions?.defaultCompression !== undefined 
        ? props.field.fileOptions.defaultCompression 
        : true,
      useEncryption: props.field?.fileOptions?.defaultEncryption !== undefined 
        ? props.field.fileOptions.defaultEncryption 
        : true,
      originalFileName: file.name,
    };

    // Emit the file object (backend will handle upload)
    localValue.value = fileObject;
  } catch (err: any) {
    console.error('Error processing file:', err);
    error.value = err.message || t('automated-forms.fileUpload.errors.processingFile');
  } finally {
    isUploading.value = false;
  }
};

// Handle multiple files
const handleMultipleFiles = async (files: File[]) => {
  error.value = null;
  isUploading.value = true;

  try {
    const maxSize = props.field?.fileOptions?.maxSize || 5 * 1024 * 1024;
    const maxFiles = props.field?.fileOptions?.maxFiles || 10;

    // Check total file count (existing + new)
    const existingFileCount = Array.isArray(localValue.value) ? localValue.value.length : 0;
    const totalFileCount = existingFileCount + files.length;
    
    if (totalFileCount > maxFiles) {
      error.value = t('automated-forms.fileUpload.errors.maxFilesExceeded', { max: maxFiles, current: existingFileCount, extra: files.length });
      isUploading.value = false;
      return;
    }

    if (files.length > maxFiles) {
      error.value = t('automated-forms.fileUpload.errors.maxFiles', { max: maxFiles });
      isUploading.value = false;
      return;
    }

    const fileObjects: any[] = [];
    const newFilePreviews: typeof newFiles.value = [];

    for (const file of files) {
      // Validate size
      if (file.size > maxSize) {
        error.value = t('automated-forms.fileUpload.errors.fileTooBigNamed', { name: file.name, max: (maxSize / 1024 / 1024).toFixed(0) });
        isUploading.value = false;
        return;
      }

      // Validate extension
      if (props.field?.fileOptions?.allowedExtensions && props.field.fileOptions.allowedExtensions.length > 0) {
        const fileExtension = '.' + file.name.split('.').pop()?.toLowerCase();
        if (!props.field.fileOptions.allowedExtensions.includes(fileExtension)) {
          error.value = t('automated-forms.fileUpload.errors.fileTypeNotAllowedNamed', { name: file.name, types: props.field.fileOptions.allowedExtensions.join(', ') });
          isUploading.value = false;
          return;
        }
      }

      // Create preview
      const preview = await createFilePreview(file);
      newFilePreviews.push({
        file: file,
        preview: preview,
        fileName: file.name,
        mimeType: file.type || 'application/octet-stream',
        fileSize: file.size
      });

      // Convert to base64
      const base64 = await fileToBase64(file);

      fileObjects.push({
        content: base64,
        folder: undefined,
        useCompression: props.field?.fileOptions?.defaultCompression !== undefined 
          ? props.field.fileOptions.defaultCompression 
          : true,
        useEncryption: props.field?.fileOptions?.defaultEncryption !== undefined 
          ? props.field.fileOptions.defaultEncryption 
          : true,
        originalFileName: file.name,
      });
    }

    // Add new files to existing newFiles (preserve existing new files)
    newFiles.value = [...newFiles.value, ...newFilePreviews];

    // Emit array of file objects - merge with existing files if any
    const existingFiles = Array.isArray(localValue.value) ? localValue.value : [];
    localValue.value = [...existingFiles, ...fileObjects];
  } catch (err: any) {
    console.error('Error processing files:', err);
    error.value = err.message || t('automated-forms.fileUpload.errors.processingFiles');
  } finally {
    isUploading.value = false;
  }
};

// Create file preview (for new files)
const createFilePreview = (file: File): Promise<string> => {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      resolve(reader.result as string);
    };
    reader.onerror = reject;
    reader.readAsDataURL(file);
  });
};

// Convert file to base64
const fileToBase64 = (file: File): Promise<string> => {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const result = reader.result as string;
      // Remove data URL prefix (data:image/png;base64,)
      const base64 = result.split(',')[1];
      resolve(base64);
    };
    reader.onerror = reject;
    reader.readAsDataURL(file);
  });
};

// Trigger file input
const triggerFileInput = () => {
  fileInput.value?.click();
};

// Remove file
const removeFile = (index: number) => {
  if (readonly || disabled) return;
  
  const preview = previewUrls.value[index];
  
  // Remove from previews first
  previewUrls.value.splice(index, 1);
  
  // If it's a new file, remove from newFiles
  if (preview.isNew) {
    // Find and remove from newFiles by matching fileName
    const newFileIndex = newFiles.value.findIndex(nf => nf.fileName === preview.fileName);
    if (newFileIndex >= 0) {
      newFiles.value.splice(newFileIndex, 1);
    }
    
    // Update localValue to remove the file object
    if (isArray.value && Array.isArray(localValue.value)) {
      // Count how many new files are before this index
      let newFileCount = 0;
      for (let i = 0; i < index; i++) {
        if (previewUrls.value[i]?.isNew) {
          newFileCount++;
        }
      }
      // Remove the file object at the calculated index
      const newValue = [...localValue.value];
      // Find the index in localValue (new files are at the end)
      const fileObjectIndex = localValue.value.length - newFiles.value.length + newFileCount;
      if (fileObjectIndex >= 0 && fileObjectIndex < newValue.length) {
        newValue.splice(fileObjectIndex, 1);
        localValue.value = newValue;
      }
    } else {
      localValue.value = null;
      newFiles.value = [];
    }
  } else {
    // If it's an existing file, remove from localValue
    if (isArray.value && Array.isArray(localValue.value)) {
      const newValue = [...localValue.value];
      // Find the file path index (count only non-new files before this index)
      let filePathIndex = 0;
      for (let i = 0; i < index; i++) {
        if (!previewUrls.value[i]?.isNew) {
          filePathIndex++;
        }
      }
      // Find the existing-item index (legacy path string or new object with path)
      let actualIndex = -1;
      let existingCount = 0;
      for (let i = 0; i < newValue.length; i++) {
        if (getPathFromFileValue(newValue[i]) != null && !(typeof newValue[i] === 'object' && newValue[i] && newValue[i].content)) {
          if (existingCount === filePathIndex) {
            actualIndex = i;
            break;
          }
          existingCount++;
        }
      }
      if (actualIndex >= 0) {
        newValue.splice(actualIndex, 1);
        localValue.value = newValue;
      }
    } else {
      localValue.value = null;
    }
    
    // Remove from metadata
    if (fileMetadata.value.length > index) {
      fileMetadata.value.splice(index, 1);
    }
  }
};

// İndir: fileName verilirse blob ile veritabanındaki kayıtlı adla indirir; yoksa eski davranış (yeni sekme).
const downloadFile = async (filePath: string, fileName?: string) => {
  if (fileName) {
    try {
      const apiUrl = `/api/v1/files/download?filePath=${encodeURIComponent(filePath)}`;
      const blob = await fetchBlobFromDataGateway(apiUrl);
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = fileName;
      a.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      console.error('İndirme hatası:', err);
    }
    return;
  }
  const downloadUrl = getDataGatewayProxyUrl(`/api/v1/files/download?filePath=${encodeURIComponent(filePath)}`);
  window.open(downloadUrl, '_blank');
};

// Önceki object URL varsa serbest bırak
const revokePreviewObjectUrl = () => {
  if (previewDialogObjectUrl.value) {
    URL.revokeObjectURL(previewDialogObjectUrl.value);
    previewDialogObjectUrl.value = null;
  }
};

// Show preview dialog — sunucudaki dosyalar için auth’lı blob, yeni dosyalar için data URL
const showPreview = async (preview: typeof previewUrls.value[0]) => {
  if (!preview.mimeType.startsWith('image/') || !preview.url) return;
  revokePreviewObjectUrl();
  if (preview.isNew) {
    previewDialogImage.value = preview.url;
    showPreviewDialog.value = true;
    return;
  }
  previewDialogImage.value = null;
  previewDialogLoading.value = true;
  showPreviewDialog.value = true;
  try {
    const apiUrl = `/api/v1/files/download?filePath=${encodeURIComponent(preview.filePath!)}`;
    const blob = await fetchBlobFromDataGateway(apiUrl);
    revokePreviewObjectUrl();
    const url = URL.createObjectURL(blob);
    previewDialogObjectUrl.value = url;
    previewDialogImage.value = url;
  } catch (err) {
    console.error('Önizleme yüklenemedi:', err);
    previewDialogImage.value = null;
  } finally {
    previewDialogLoading.value = false;
  }
};

// Get file icon based on mime type
const getFileIcon = (mimeType: string) => {
  if (mimeType.startsWith('image/')) {
    return 'mdi-image';
  } else if (mimeType.includes('pdf')) {
    return 'mdi-file-pdf-box';
  } else if (mimeType.includes('word') || mimeType.includes('document')) {
    return 'mdi-file-word-box';
  } else if (mimeType.includes('excel') || mimeType.includes('spreadsheet')) {
    return 'mdi-file-excel-box';
  } else if (mimeType.includes('zip') || mimeType.includes('archive')) {
    return 'mdi-folder-zip';
  }
  return 'mdi-file';
};

// Format file size
const formatFileSize = (bytes: number): string => {
  if (bytes === 0) return '0 ' + t('automated-forms.fileUpload.units.bytes');
  const k = 1024;
  const sizes = [
    t('automated-forms.fileUpload.units.bytes'),
    t('automated-forms.fileUpload.units.kb'),
    t('automated-forms.fileUpload.units.mb'),
    t('automated-forms.fileUpload.units.gb'),
  ];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i];
};

// Check if file can be previewed
const canPreview = (mimeType: string): boolean => {
  return mimeType.startsWith('image/');
};

// Format date for display
const formatDate = (dateString: string | Date): string => {
  if (!dateString) return '';
  try {
    const date = typeof dateString === 'string' ? new Date(dateString) : dateString;
    const locale = 'tr-TR';
    return date.toLocaleDateString(locale, {
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    });
  } catch {
    return String(dateString);
  }
};

// Get metadata for a preview item
const getMetadataForPreview = (preview: typeof previewUrls.value[0]): any => {
  if (preview.isNew) return null; // New files don't have metadata yet
  
  const index = previewUrls.value.findIndex(p => 
    p.fileName === preview.fileName && 
    p.filePath === preview.filePath &&
    p.isNew === false
  );
  
  // For existing files, metadata index should match preview index (since we load them in order)
  if (index >= 0 && index < fileMetadata.value.length) {
    return fileMetadata.value[index];
  }
  
  return null;
};

// Get preview index in previewUrls array
const getPreviewIndex = (preview: typeof previewUrls.value[0]): number => {
  return previewUrls.value.findIndex(p => 
    p.fileName === preview.fileName && 
    p.filePath === preview.filePath &&
    p.isNew === preview.isNew
  );
};

// Watch for changes in modelValue to load previews
// IMPORTANT: Must be defined after all helper functions (loadPreview, loadPreviewsForArray, etc.)
watch(() => props.modelValue, async (newValue) => {
  if (!newValue) {
    previewUrls.value = [];
    fileMetadata.value = [];
    newFiles.value = [];
    return;
  }

  // Handle array field
  if (isArray.value && Array.isArray(newValue)) {
    // Load previews for all items (both existing file paths and new file objects)
    // Note: newFiles is managed by handleMultipleFiles, not by watch
    // Watch only loads previews for existing file paths (strings)
    await loadPreviewsForArray(newValue);
  } 
  // Handle single field
  else if (!isArray.value) {
    const path = getPathFromFileValue(newValue);
    if (path && !(typeof newValue === 'object' && newValue && newValue.content)) {
      const stored = typeof newValue === 'object' && newValue ? { file_name: newValue.file_name, file_ext: newValue.file_ext, file_size: newValue.file_size } : undefined;
      await loadPreview(path, stored);
      newFiles.value = [];
    } else if (typeof newValue === 'object' && newValue && newValue.content) {
      updatePreviewsWithNewFiles();
    } else {
      newFiles.value = [];
    }
  }
}, { immediate: true });

// Dialog kapanırken blob object URL'ini serbest bırak
watch(showPreviewDialog, (open) => {
  if (!open) revokePreviewObjectUrl();
});

// Watch newFiles to update previews
watch(() => newFiles.value, () => {
  updatePreviewsWithNewFiles();
}, { deep: true });
</script>

<template>
  <div>
    <input
      ref="fileInput"
      type="file"
      :multiple="isArray"
      class="d-none"
      @change="handleFileSelect"
    />

    <!-- Drag & Drop Zone -->
    <div
      v-if="!readonly && !disabled"
      @dragover="handleDragOver"
      @dragleave="handleDragLeave"
      @drop="handleDrop"
      :class="[
        'border-dashed border-2 rounded-lg pa-4 mb-3 text-center transition-all',
        isDragging ? 'border-primary bg-primary-lighten-5' : 'border-grey-lighten-2'
      ]"
      style="cursor: pointer;"
      @click="triggerFileInput"
    >
      <div class="d-flex flex-column align-center ga-2">
        <v-icon :icon="isDragging ? 'mdi-cloud-upload' : 'mdi-cloud-upload-outline'" size="48" :color="isDragging ? 'primary' : 'grey'"></v-icon>
        <div class="text-body-1 font-weight-medium">
          {{ isDragging ? t('automated-forms.fileUpload.dropZoneDropHere') : (isArray ? t('automated-forms.fileUpload.dropZoneDragFiles') : t('automated-forms.fileUpload.dropZoneDragFile')) }}
        </div>
        <div class="text-caption text-medium-emphasis">
          {{ isArray ? t('automated-forms.fileUpload.dropZoneMultipleHint') : t('automated-forms.fileUpload.dropZoneSingleHint') }}
        </div>
      </div>
    </div>

    <!-- Upload Button (Alternative) -->
    <div v-if="!readonly && !disabled" class="mb-3">
      <v-btn
        variant="outlined"
        color="primary"
        size="small"
        @click="triggerFileInput"
        :loading="isUploading"
        :disabled="isUploading || disabled"
      >
        <UploadIcon size="18" class="mr-2" />
        {{ isArray ? t('automated-forms.fileUpload.uploadFiles') : t('automated-forms.fileUpload.uploadFile') }}
      </v-btn>
    </div>

    <!-- Error Message -->
    <v-alert
      v-if="error"
      type="error"
      variant="tonal"
      density="compact"
      class="mb-3"
      closable
      @click:close="error = null"
    >
      {{ error }}
    </v-alert>

    <!-- Field Error Messages -->
    <v-alert
      v-if="errorMessages && errorMessages.length > 0"
      type="error"
      variant="tonal"
      density="compact"
      class="mb-3"
    >
      <div v-for="(msg, idx) in errorMessages" :key="idx">{{ msg }}</div>
    </v-alert>

    <!-- File Previews -->
    <div v-if="previewUrls.length > 0" class="d-flex flex-column ga-4">
      <!-- Existing Files Section -->
      <div v-if="previewUrls.filter(p => !p.isNew).length > 0" class="d-flex flex-column ga-2">
        <div class="d-flex align-center ga-2 mb-2">
          <v-icon icon="mdi-file-check" size="20" color="primary"></v-icon>
          <span class="text-subtitle-2 font-weight-medium text-primary">{{ t('automated-forms.fileUpload.existingFiles') }}</span>
          <v-chip size="x-small" color="primary" variant="tonal">
            {{ previewUrls.filter(p => !p.isNew).length }}
          </v-chip>
        </div>
        
        <v-row dense>
          <v-col
            v-for="(preview, localIndex) in previewUrls.filter(p => !p.isNew)"
            :key="`existing-${localIndex}`"
            cols="12"
            :sm="6"
            :md="4"
            :lg="3"
          >
            <v-card
              variant="outlined"
              class="file-preview-card h-100"
              style="transition: all 0.2s; cursor: pointer;"
              @mouseenter="(e) => { (e.currentTarget as HTMLElement).style.borderColor = 'var(--v-primary-base)'; (e.currentTarget as HTMLElement).style.transform = 'translateY(-2px)'; }"
              @mouseleave="(e) => { (e.currentTarget as HTMLElement).style.borderColor = 'rgba(var(--v-border-color), 0.12)'; (e.currentTarget as HTMLElement).style.transform = 'translateY(0)'; }"
            >
              <v-card-text class="pa-3">
                <!-- Image Preview -->
                <div v-if="preview.mimeType.startsWith('image/') && preview.url" class="text-center mb-2">
                  <div class="position-relative d-inline-block" style="width: 100%; max-width: 150px;">
                    <img
                      :src="preview.url"
                      :alt="preview.fileName"
                      style="width: 100%; height: 120px; object-fit: cover; border-radius: 8px; cursor: pointer; border: 2px solid rgba(var(--v-border-color), 0.12);"
                      @click="showPreview(preview)"
                      @error="(e) => { (e.target as HTMLImageElement).style.display = 'none' }"
                    />
                    <v-btn
                      icon
                      size="x-small"
                      variant="flat"
                      color="primary"
                      class="position-absolute"
                      style="top: 4px; right: 4px;"
                      @click.stop="showPreview(preview)"
                      :title="t('automated-forms.fileUpload.preview')"
                    >
                      <EyeIcon size="14" />
                    </v-btn>
                  </div>
                </div>
                
                <!-- File Icon (for non-images) -->
                <div v-else class="text-center mb-2">
                  <div class="position-relative d-inline-block">
                    <v-icon :icon="getFileIcon(preview.mimeType)" size="64" color="primary"></v-icon>
                    <v-btn
                      v-if="preview.filePath"
                      icon
                      size="x-small"
                      variant="flat"
                      color="primary"
                      class="position-absolute"
                      style="top: -4px; right: -4px;"
                      @click.stop="downloadFile(preview.filePath!, preview.fileName)"
                      :title="t('automated-forms.fileUpload.download')"
                    >
                      <DownloadIcon size="14" />
                    </v-btn>
                  </div>
                </div>

                <!-- File Info -->
                <div class="text-center">
                  <div class="text-body-2 font-weight-medium text-truncate mb-1" :title="preview.fileName" style="line-height: 1.2;">
                    {{ preview.fileName }}
                  </div>
                  <div class="text-caption text-medium-emphasis mb-2">
                    <div v-if="preview.fileSize">{{ formatFileSize(preview.fileSize) }}</div>
                    <template v-if="getMetadataForPreview(preview)">
                      <div v-if="getMetadataForPreview(preview)?.uploadedAt" class="mt-1">
                        {{ formatDate(getMetadataForPreview(preview)!.uploadedAt) }}
                      </div>
                      <div v-if="getMetadataForPreview(preview)?.uploadedBy" class="text-caption text-medium-emphasis">
                        {{ getMetadataForPreview(preview)!.uploadedBy }}
                      </div>
                    </template>
                  </div>
                </div>

                <!-- Actions -->
                <div class="d-flex justify-center ga-1 mt-2">
                  <!-- Preview Button (for images) -->
                  <v-btn
                    v-if="canPreview(preview.mimeType) && preview.url"
                    icon
                    size="small"
                    variant="text"
                    @click="showPreview(preview)"
                    :title="t('automated-forms.fileUpload.preview')"
                  >
                    <EyeIcon size="18" />
                  </v-btn>

                  <!-- Download Button -->
                  <v-btn
                    v-if="preview.filePath"
                    icon
                    size="small"
                    variant="text"
                    @click="downloadFile(preview.filePath!, preview.fileName)"
                    :title="t('automated-forms.fileUpload.download')"
                  >
                    <DownloadIcon size="18" />
                  </v-btn>

                  <!-- Remove Button -->
                  <v-btn
                    v-if="!readonly && !disabled"
                    icon
                    size="small"
                    variant="text"
                    color="error"
                    @click="removeFile(getPreviewIndex(preview))"
                    :title="t('automated-forms.fileUpload.remove')"
                  >
                    <XIcon size="18" />
                  </v-btn>
                </div>
              </v-card-text>
            </v-card>
          </v-col>
        </v-row>
      </div>

      <!-- New Files Section -->
      <div v-if="previewUrls.filter(p => p.isNew).length > 0" class="d-flex flex-column ga-2">
        <div class="d-flex align-center ga-2 mb-2">
          <v-icon icon="mdi-file-plus" size="20" color="success"></v-icon>
          <span class="text-subtitle-2 font-weight-medium text-success">{{ t('automated-forms.fileUpload.newFiles') }}</span>
          <v-chip size="x-small" color="success" variant="tonal">
            {{ previewUrls.filter(p => p.isNew).length }}
          </v-chip>
        </div>
        
        <v-row dense>
          <v-col
            v-for="(preview, localIndex) in previewUrls.filter(p => p.isNew)"
            :key="`new-${localIndex}`"
            cols="12"
            :sm="6"
            :md="4"
            :lg="3"
          >
            <v-card
              variant="outlined"
              class="file-preview-card h-100 border-success"
              style="transition: all 0.2s; cursor: pointer; border-color: var(--v-success-base) !important;"
              @mouseenter="(e) => { (e.currentTarget as HTMLElement).style.borderColor = 'var(--v-success-base)'; (e.currentTarget as HTMLElement).style.transform = 'translateY(-2px)'; }"
              @mouseleave="(e) => { (e.currentTarget as HTMLElement).style.borderColor = 'var(--v-success-base)'; (e.currentTarget as HTMLElement).style.transform = 'translateY(0)'; }"
            >
              <v-card-text class="pa-3">
                <!-- Image Preview -->
                <div v-if="preview.mimeType.startsWith('image/') && preview.url" class="text-center mb-2">
                  <div class="position-relative d-inline-block" style="width: 100%; max-width: 150px;">
                    <img
                      :src="preview.url"
                      :alt="preview.fileName"
                      style="width: 100%; height: 120px; object-fit: cover; border-radius: 8px; cursor: pointer; border: 2px solid var(--v-success-base);"
                      @click="showPreview(preview)"
                      @error="(e) => { (e.target as HTMLImageElement).style.display = 'none' }"
                    />
                    <v-chip
                      size="x-small"
                      color="success"
                      class="position-absolute"
                      style="top: 4px; right: 4px;"
                    >
                      {{ t('automated-forms.fileUpload.newFileChip') }}
                    </v-chip>
                    <v-btn
                      icon
                      size="x-small"
                      variant="flat"
                      color="success"
                      class="position-absolute"
                      style="top: 4px; left: 4px;"
                      @click.stop="showPreview(preview)"
                      :title="t('automated-forms.fileUpload.preview')"
                    >
                      <EyeIcon size="14" />
                    </v-btn>
                  </div>
                </div>
                
                <!-- File Icon (for non-images) -->
                <div v-else class="text-center mb-2">
                  <div class="position-relative d-inline-block">
                    <v-icon :icon="getFileIcon(preview.mimeType)" size="64" color="success"></v-icon>
                    <v-chip
                      size="x-small"
                      color="success"
                      class="position-absolute"
                      style="top: -4px; right: -4px;"
                    >
                      {{ t('automated-forms.fileUpload.newFileChip') }}
                    </v-chip>
                  </div>
                </div>

                <!-- File Info -->
                <div class="text-center">
                  <div class="text-body-2 font-weight-medium text-truncate mb-1" :title="preview.fileName" style="line-height: 1.2;">
                    {{ preview.fileName }}
                  </div>
                  <div class="text-caption text-medium-emphasis mb-2">
                    <div v-if="preview.fileSize">{{ formatFileSize(preview.fileSize) }}</div>
                    <div class="text-success mt-1 font-weight-medium">
                      {{ t('automated-forms.fileUpload.willUploadOnSave') }}
                    </div>
                  </div>
                </div>

                <!-- Actions -->
                <div class="d-flex justify-center ga-1 mt-2">
                  <!-- Preview Button (for images) -->
                  <v-btn
                    v-if="canPreview(preview.mimeType) && preview.url"
                    icon
                    size="small"
                    variant="text"
                    @click="showPreview(preview)"
                    :title="t('automated-forms.fileUpload.preview')"
                  >
                    <EyeIcon size="18" />
                  </v-btn>

                  <!-- Remove Button -->
                  <v-btn
                    v-if="!readonly && !disabled"
                    icon
                    size="small"
                    variant="text"
                    color="error"
                    @click="removeFile(getPreviewIndex(preview))"
                    :title="t('automated-forms.fileUpload.remove')"
                  >
                    <XIcon size="18" />
                  </v-btn>
                </div>
              </v-card-text>
            </v-card>
          </v-col>
        </v-row>
      </div>
    </div>

    <!-- Empty State -->
    <div v-if="previewUrls.length === 0 && !isUploading" class="text-center pa-4 text-medium-emphasis">
      <v-icon icon="mdi-file-outline" size="48" color="grey-lighten-1" class="mb-2"></v-icon>
      <div class="text-body-2">{{ t('automated-forms.fileUpload.noFilesYet') }}</div>
    </div>

    <!-- Preview Dialog -->
    <v-dialog v-model="showPreviewDialog" max-width="90vw" max-height="90vh" @click:outside="showPreviewDialog = false">
      <v-card>
        <v-card-title class="d-flex justify-space-between align-center">
          <span>{{ t('automated-forms.fileUpload.previewDialogTitle') }}</span>
          <v-btn icon variant="text" @click="showPreviewDialog = false">
            <XIcon size="18" />
          </v-btn>
        </v-card-title>
        <v-card-text class="text-center pa-4" style="max-height: 80vh; overflow: auto;">
          <v-progress-circular v-if="previewDialogLoading" indeterminate color="primary" size="48" class="my-8" />
          <img
            v-else-if="previewDialogImage"
            :src="previewDialogImage"
            :alt="t('automated-forms.fileUpload.preview')"
            style="max-width: 100%; max-height: 80vh; object-fit: contain;"
          />
          <div v-else-if="!previewDialogLoading" class="text-medium-emphasis py-8">
            {{ t('automated-forms.fileUpload.previewLoadFailed') }}
          </div>
        </v-card-text>
      </v-card>
    </v-dialog>
  </div>
</template>

<style scoped>
.file-preview-card {
  transition: all 0.2s ease;
}

.file-preview-card:hover {
  box-shadow: 0 2px 8px rgba(0, 0, 0, 0.1);
}
</style>
