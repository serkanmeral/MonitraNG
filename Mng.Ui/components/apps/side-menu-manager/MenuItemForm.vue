<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue';
import type { SideMenuItem } from '@/stores/apps/sideMenu';
import { TrashIcon, XIcon, LanguageIcon } from 'vue-tabler-icons';
import IconPicker from './IconPicker.vue';
import PermissionEditor from './PermissionEditor.vue';
import { fetchFromMngKeeper, fetchFromMngLLM } from '@/services/apiService';
import { useAutomatedFormsStore } from '@/stores/apps/automatedForms';
import { useAuthStore } from '@/stores/auth';

// Note: In Event Messages, $t() is only used in template, not in script setup
// For script setup, we'll use hardcoded text for alerts/confirms
// Template uses $t() directly

const props = defineProps<{
  item: SideMenuItem | null;
  allItems: SideMenuItem[];
  loading?: boolean;
}>();

const emit = defineEmits<{
  'save': [itemData: Partial<SideMenuItem>];
  'delete': [itemId: string];
  'cancel': [];
}>();

// Automated Forms Store
const automatedFormsStore = useAutomatedFormsStore();

// Auth Store
const authStore = useAuthStore();

// Selected form code (for dropdown)
const selectedFormCode = ref<string | null>(null);

// Default page type - Her zaman "user" olarak başlar
const getDefaultPageType = (): 'admin' | 'manager' | 'user' => {
  return 'user';
};

// Form data - Her zaman pageType: 'user' olarak başlar
const formData = ref<Partial<SideMenuItem>>({
  itemType: 'item',
  pageType: 'user', // Her zaman 'user' olarak başlar
  level: 0,
  parentId: null,
  order: 0,
  disabled: false,
  type: 'internal',
  iconType: 'tabler',
});

// Watch item changes
watch(
  () => props.item,
  (newItem) => {
    if (newItem) {
      formData.value = {
        ...newItem,
      };
      // Check if route path matches a form route
      if (newItem.to && newItem.to.startsWith('/apps/automated-forms/view/')) {
        const formCode = newItem.to.replace('/apps/automated-forms/view/', '');
        selectedFormCode.value = formCode;
      } else {
        selectedFormCode.value = null;
      }
    } else {
      // Reset form for new item - Her zaman pageType: 'user' olarak başlar
      formData.value = {
        itemType: 'item',
        pageType: 'user', // Her zaman 'user' olarak başlar
        level: 0,
        parentId: null,
        order: props.allItems.length,
        disabled: false,
        type: 'internal',
        iconType: 'tabler',
      };
      selectedFormCode.value = null;
    }
  },
  { immediate: true }
);

// Watch form selection to update route path
watch(
  () => selectedFormCode.value,
  (newFormCode) => {
    if (newFormCode) {
      // Set route path to form view route
      formData.value.to = `/apps/automated-forms/view/${newFormCode}`;
    }
  }
);

// Watch route path changes to clear form selection if path doesn't match form route
watch(
  () => formData.value.to,
  (newRoutePath) => {
    if (!newRoutePath || !newRoutePath.startsWith('/apps/automated-forms/view/')) {
      // If route path doesn't match form route pattern, clear form selection
      if (selectedFormCode.value) {
        selectedFormCode.value = null;
      }
    } else if (newRoutePath.startsWith('/apps/automated-forms/view/')) {
      // If route path matches form route pattern, update form selection
      const formCode = newRoutePath.replace('/apps/automated-forms/view/', '');
      if (selectedFormCode.value !== formCode) {
        selectedFormCode.value = formCode;
      }
    }
  }
);

// Computed properties
const isEditMode = computed(() => {
  return props.item?.__dataId !== undefined;
});

const parentOptions = computed(() => {
  // Filter out self and descendants for circular reference prevention
  const excludeIds = new Set<string>();
  if (props.item?.__dataId) {
    excludeIds.add(props.item.__dataId);
    // Add all descendants (find recursively in flat array)
    const addDescendants = (parentId: string) => {
      props.allItems.forEach((item) => {
        if (item.parentId === parentId && item.__dataId) {
          excludeIds.add(item.__dataId);
          addDescendants(item.__dataId);
        }
      });
    };
    addDescendants(props.item.__dataId);
  }

  // Filter: Only show headers as parent options
  // Build tree structure from flat array (only headers)
  const headerItems = props.allItems
    .filter(item => item.itemType === 'header' && item.__dataId && !excludeIds.has(item.__dataId))
    .sort((a, b) => (a.order || 0) - (b.order || 0)); // Sort headers by order

  // Build flat list for dropdown (only headers)
  // Note: For i18n support in dropdown, we use hardcoded text for now
  // Template uses $t() for labels, but dropdown items use hardcoded text
  const flatItems: Array<{ value: string | null; title: string; level: number }> = [
    { value: null, title: '(Yok - Root)', level: 0 },
  ];

  // Add headers to dropdown
  headerItems.forEach((item) => {
    const label = item.header || item.title || 'Untitled';
    flatItems.push({
      value: item.__dataId!,
      title: label,
      level: item.level || 0,
    });
  });

  return flatItems;
});

// Available forms for dropdown (only active forms)
const formOptions = computed(() => {
  const activeForms = automatedFormsStore.activeForms || [];
  return activeForms.map(form => ({
    title: form.formName || form.formCode,
    value: form.formCode,
    subtitle: form.datasetName,
  }));
});

// Calculate level based on selected parent
watch(
  () => formData.value.parentId,
  (parentId) => {
    if (parentId) {
      const parent = findItemById(props.allItems, parentId);
      if (parent) {
        formData.value.level = (parent.level || 0) + 1;
      }
    } else {
      formData.value.level = 0;
    }
  }
);

const findItemById = (items: SideMenuItem[], id: string): SideMenuItem | null => {
  for (const item of items) {
    if (item.__dataId === id) {
      return item;
    }
    if (item.children) {
      const found = findItemById(item.children, id);
      if (found) return found;
    }
  }
  return null;
};

// Handle save
const handleSave = () => {
  // Validate required fields
  // Note: Using hardcoded text for alerts (Event Messages approach - $t() only in template)
  if (formData.value.itemType === 'header' && !formData.value.header) {
    alert('Header metni gereklidir');
    return;
  }
  if (formData.value.itemType === 'item' && !formData.value.title) {
    alert('Menu başlığı gereklidir');
    return;
  }

  // Prepare data for save
  const itemData: Partial<SideMenuItem> = {
    ...formData.value,
  };

  // Remove undefined fields
  Object.keys(itemData).forEach((key) => {
    if (itemData[key as keyof SideMenuItem] === undefined) {
      delete itemData[key as keyof SideMenuItem];
    }
  });

  emit('save', itemData);
};

// Handle delete
const handleDelete = () => {
  if (props.item?.__dataId) {
    emit('delete', props.item.__dataId);
  }
};

// Handle icon selection
const handleIconSelect = (iconName: string, iconType: 'mdi' | 'tabler') => {
  formData.value.icon = iconName;
  formData.value.iconType = iconType;
};

// Handle permissions change
const handlePermissionsChange = (permissions: SideMenuItem['permissions']) => {
  formData.value.permissions = permissions;
};

// Page Type Options - Manager kullanıcılar "Administration" seçeneğini göremez
const pageTypeOptions = computed(() => {
  const options = [
    { title: 'User', value: 'user' },
    { title: 'Manager', value: 'manager' },
  ];
  
  // Sadece admin kullanıcılar "Administration" seçeneğini görebilir
  if (authStore.isAdmin) {
    options.push({ title: 'Administration', value: 'admin' });
  }
  
  return options;
});

// Navigate to route
const navigateToRoute = async (route: string) => {
  if (!route || !route.trim()) {
    return;
  }
  
  if (!process.client) {
    return;
  }
  
  try {
    // Clean route
    const cleanRoute = route.trim();
    
    // Check if external link
    if (formData.value.type === 'external' || cleanRoute.startsWith('http://') || cleanRoute.startsWith('https://')) {
      window.open(cleanRoute, '_blank');
      return;
    }
    
    // Internal route - navigate using Nuxt router
    // navigateTo is auto-imported in Nuxt 3
    await navigateTo(cleanRoute);
  } catch (error) {
    // Fallback: try window.location
    window.location.href = route;
  }
};

// Generate pageCode automatically
const generatePageCode = () => {
  if (formData.value.to && formData.value.to.startsWith('/')) {
    // Generate from route: /dashboards/analytical -> dashboards-analytical
    formData.value.pageCode = formData.value.to.substring(1).replace(/\//g, '-').replace(/[^a-zA-Z0-9-_]/g, '').toLowerCase();
  } else if (formData.value.title) {
    // Generate from title: "Analytical Dashboard" -> analytical-dashboard
    formData.value.pageCode = formData.value.title.toLowerCase()
      .replace(/[^a-zA-Z0-9\s-]/g, '')
      .replace(/\s+/g, '-')
      .replace(/-+/g, '-')
      .replace(/^-|-$/g, '');
  } else if (formData.value.header) {
    // Generate from header
    formData.value.pageCode = formData.value.header.toLowerCase()
      .replace(/[^a-zA-Z0-9\s-]/g, '')
      .replace(/\s+/g, '-')
      .replace(/-+/g, '-')
      .replace(/^-|-$/g, '');
  }
  
  // If still no pageCode, use item type + order
  if (!formData.value.pageCode) {
    formData.value.pageCode = `${formData.value.itemType}-${formData.value.order || 0}`;
  }
};

// Update locale files
const updatingLocales = ref(false);
const localeUpdateMessage = ref('');
const localeUpdateError = ref('');

const updateLocaleFiles = async () => {
  // Validate pageCode
  // Note: Error messages will be shown in template using $t(), here we use hardcoded text
  if (!formData.value.pageCode) {
    localeUpdateError.value = 'Sayfa kodu (pageCode) gereklidir. Lütfen önce pageCode oluşturun.';
    setTimeout(() => {
      localeUpdateError.value = '';
    }, 5000);
    return;
  }

  // Check if this is a header item
  const isHeader = formData.value.itemType === 'header';

  // Get source text (title for item, header for header)
  const sourceText = isHeader
    ? formData.value.header 
    : formData.value.title;
  
  if (!sourceText) {
    localeUpdateError.value = isHeader
      ? 'Header metni gereklidir.'
      : 'Menu başlığı gereklidir.';
    setTimeout(() => {
      localeUpdateError.value = '';
    }, 5000);
    return;
  }

  // Get subCaption source text (if exists and not header)
  const sourceSubCaption = !isHeader && formData.value.subCaption 
    ? formData.value.subCaption 
    : null;

  updatingLocales.value = true;
  localeUpdateMessage.value = '';
  localeUpdateError.value = '';

  try {
    // Available locales
    const locales = ['tr', 'en', 'fr', 'ar', 'zh'];
    
    // Get translations from MngLLM API (skip Turkish - it's the source)
    const targetLocales = locales.filter(locale => locale !== 'tr');
    let translations: Record<string, string> = {};
    let subCaptionTranslations: Record<string, string> = {};
    
    try {
      // Call MngLLM translation API for title/header
      const translationResponse = await fetchFromMngLLM('/api/v1/llm/translate', 'POST', {
        text: sourceText,
        sourceLanguage: 'tr',
        targetLanguages: targetLocales,
      });
      
      if (translationResponse?.translations) {
        translations = translationResponse.translations;
        console.log('[MenuItemForm] Title translations received:', translations);
      }
    } catch (translationError: any) {
      console.warn('[MenuItemForm] Translation API failed for title, using source text as fallback:', translationError);
      // If translation fails, use source text as fallback for all languages
      targetLocales.forEach(locale => {
        translations[locale] = sourceText;
      });
    }

    // Translate subCaption if exists
    if (sourceSubCaption) {
      try {
        const subCaptionTranslationResponse = await fetchFromMngLLM('/api/v1/llm/translate', 'POST', {
          text: sourceSubCaption,
          sourceLanguage: 'tr',
          targetLanguages: targetLocales,
        });
        
        if (subCaptionTranslationResponse?.translations) {
          subCaptionTranslations = subCaptionTranslationResponse.translations;
          console.log('[MenuItemForm] SubCaption translations received:', subCaptionTranslations);
        }
      } catch (subCaptionTranslationError: any) {
        console.warn('[MenuItemForm] Translation API failed for subCaption, using source text as fallback:', subCaptionTranslationError);
        // If translation fails, use source text as fallback for all languages
        targetLocales.forEach(locale => {
          subCaptionTranslations[locale] = sourceSubCaption;
        });
      }
    }
    
    // Update each locale file
    for (const locale of locales) {
      try {
        // Load existing locale file
        let localeData: any = {};
        try {
          localeData = await fetchFromMngKeeper(`/system/locales/${locale}`, 'GET');
        } catch (error: any) {
          // 404 means file doesn't exist, which is OK - we'll create it
          if (error.message?.includes('404') || error.statusCode === 404) {
            localeData = {};
          } else {
            throw error;
          }
        }

        // Ensure menu object exists
        if (!localeData.menu) {
          localeData.menu = {};
        }

        // Get translation text for this locale
        let translationText = sourceText; // Default to source text
        if (locale === 'tr') {
          // Turkish is the source language
          translationText = sourceText;
        } else if (translations[locale]) {
          // Use translated text
          translationText = translations[locale];
        } else {
          // Fallback to source text if translation not available
          translationText = sourceText;
        }

        // Get subCaption translation text for this locale (if exists)
        let subCaptionTranslationText: string | null = null;
        if (sourceSubCaption) {
          if (locale === 'tr') {
            // Turkish is the source language
            subCaptionTranslationText = sourceSubCaption;
          } else if (subCaptionTranslations[locale]) {
            // Use translated text
            subCaptionTranslationText = subCaptionTranslations[locale];
          } else {
            // Fallback to source text if translation not available
            subCaptionTranslationText = sourceSubCaption;
          }
        }

        if (isHeader) {
          // For headers: menu.headers.{pageCode}
          if (!localeData.menu.headers) {
            localeData.menu.headers = {};
          }
          localeData.menu.headers[formData.value.pageCode] = translationText;
        } else {
          // For items: menu.{pageCode} (title)
          localeData.menu[formData.value.pageCode] = translationText;
          
          // For items: menu.{pageCode}.subCaption (subCaption, if exists)
          if (subCaptionTranslationText) {
            // Store as nested object: menu.{pageCode}.subCaption
            if (typeof localeData.menu[formData.value.pageCode] === 'string') {
              // Convert string to object if it's currently a string
              const existingTitle = localeData.menu[formData.value.pageCode];
              localeData.menu[formData.value.pageCode] = {
                title: existingTitle,
                subCaption: subCaptionTranslationText
              };
            } else if (typeof localeData.menu[formData.value.pageCode] === 'object') {
              // Already an object, just add/update subCaption
              localeData.menu[formData.value.pageCode].subCaption = subCaptionTranslationText;
            }
          }
        }

        // Save locale file
        await fetchFromMngKeeper(`/system/locales/${locale}`, 'PUT', localeData);
      } catch (error: any) {
        console.error(`Failed to update locale ${locale}:`, error);
        // Note: Error message shown in template using $t()
        localeUpdateError.value = `Dil dosyası güncellenirken hata oluştu: ${error.message || error}`;
        updatingLocales.value = false;
        setTimeout(() => {
          localeUpdateError.value = '';
        }, 10000);
        return;
      }
    }

    // Invalidate cache and reload locales from MinIO
    if (process.client) {
      try {
        const nuxtApp = useNuxtApp();
        const reloadLocales = (nuxtApp as any).$reloadLocales;
        if (reloadLocales) {
          await reloadLocales(); // Invalidate cache and reload from MinIO
          console.log('[MenuItemForm] Locale cache invalidated and locales reloaded from MinIO');
        } else {
          // Fallback: just invalidate cache if reloadLocales is not available
          const invalidateCache = (nuxtApp as any).$invalidateLocaleCache;
          if (invalidateCache) {
            invalidateCache();
            console.log('[MenuItemForm] Locale cache invalidated (reload not available)');
          }
        }
      } catch (cacheError) {
        console.warn('Failed to reload locales:', cacheError);
      }
    }

    // Note: Success message shown in template using $t()
    localeUpdateMessage.value = `Dil dosyaları başarıyla güncellendi! (${formData.value.pageCode})`;
    setTimeout(() => {
      localeUpdateMessage.value = '';
    }, 5000);
  } catch (error: any) {
    console.error('Failed to update locale files:', error);
    // Note: Error message shown in template using $t()
    localeUpdateError.value = `Dil dosyaları güncellenirken hata oluştu: ${error.message || error}`;
    setTimeout(() => {
      localeUpdateError.value = '';
    }, 10000);
  } finally {
    updatingLocales.value = false;
  }
};

// Load forms on mount
onMounted(async () => {
  try {
    if (automatedFormsStore.forms.length === 0) {
      await automatedFormsStore.fetchForms({ pageNumber: 1, pageSize: 1000, isActive: true });
    }
  } catch (error) {
    console.error('Failed to load forms:', error);
  }
});
</script>

<template>
  <div class="menu-item-form">
    <div v-if="!item" class="text-center pa-8 text-medium-emphasis">
      <p>{{ $t('side-menu-manager.form.empty') }}</p>
    </div>

    <v-form v-else @submit.prevent="handleSave">
      <v-row>
        <!-- Item Type -->
        <v-col cols="12" md="6">
          <v-select
            v-model="formData.itemType"
            :items="[
              { title: 'Header', value: 'header' },
              { title: 'Menu Item', value: 'item' },
            ]"
            :label="$t('side-menu-manager.form.fields.itemType')"
            variant="outlined"
            required
          ></v-select>
        </v-col>

        <!-- Page Type -->
        <v-col cols="12" md="6">
          <v-select
            v-model="formData.pageType"
            :items="pageTypeOptions"
            :label="$t('side-menu-manager.form.fields.pageType')"
            variant="outlined"
          ></v-select>
        </v-col>

        <!-- Header (for header type) -->
        <v-col v-if="formData.itemType === 'header'" cols="12">
          <v-text-field
            v-model="formData.header"
            :label="$t('side-menu-manager.form.fields.header')"
            variant="outlined"
            required
          ></v-text-field>
        </v-col>

        <!-- Title (for item type) -->
        <v-col v-if="formData.itemType === 'item'" cols="12">
          <v-text-field
            v-model="formData.title"
            :label="$t('side-menu-manager.form.fields.title')"
            variant="outlined"
            required
          ></v-text-field>
        </v-col>

        <!-- Page Code -->
        <v-col cols="12" md="6">
          <v-text-field
            v-model="formData.pageCode"
            :label="$t('side-menu-manager.form.fields.pageCode')"
            variant="outlined"
            :hint="$t('side-menu-manager.form.fields.pageCodeHint')"
            persistent-hint
          >
            <template #append-inner>
              <v-btn
                icon
                size="small"
                variant="text"
                @click="generatePageCode"
                :title="$t('side-menu-manager.form.buttons.generatePageCode')"
              >
                <v-icon size="18">mdi-auto-fix</v-icon>
              </v-btn>
            </template>
          </v-text-field>
        </v-col>

        <!-- Order -->
        <v-col cols="12" md="6">
          <v-text-field
            v-model.number="formData.order"
            :label="$t('side-menu-manager.form.fields.order')"
            type="number"
            variant="outlined"
            min="0"
          ></v-text-field>
        </v-col>

        <!-- Parent Selector -->
        <v-col cols="12">
          <v-select
            v-model="formData.parentId"
            :items="parentOptions"
            item-title="title"
            item-value="value"
            :label="$t('side-menu-manager.form.fields.parent')"
            variant="outlined"
            clearable
          ></v-select>
        </v-col>

        <!-- Level (auto-calculated, readonly) -->
        <v-col cols="12" md="6">
          <v-text-field
            v-model.number="formData.level"
            :label="$t('side-menu-manager.form.fields.level')"
            type="number"
            variant="outlined"
            readonly
            :hint="$t('side-menu-manager.form.fields.levelHint')"
            persistent-hint
          ></v-text-field>
        </v-col>

        <!-- Disabled -->
        <v-col cols="12" md="6">
          <v-switch
            v-model="formData.disabled"
            :label="$t('side-menu-manager.form.fields.disabled')"
            color="error"
          ></v-switch>
        </v-col>

        <!-- Automated Form Selection (for item type) -->
        <v-col v-if="formData.itemType === 'item'" cols="12" md="6">
          <v-select
            v-model="selectedFormCode"
            :items="formOptions"
            :label="$t('side-menu-manager.form.fields.forms')"
            :placeholder="$t('side-menu-manager.form.fields.formsPlaceholder')"
            variant="outlined"
            clearable
            :hint="$t('side-menu-manager.form.fields.formsHint')"
            persistent-hint
            prepend-inner-icon="mdi-form-select"
          >
            <template #item="{ props: itemProps, item }">
              <v-list-item v-bind="itemProps">
                <template #prepend>
                  <v-icon>mdi-form-select</v-icon>
                </template>
                <v-list-item-title>{{ item.raw.title }}</v-list-item-title>
                <v-list-item-subtitle v-if="item.raw.subtitle">{{ item.raw.subtitle }}</v-list-item-subtitle>
              </v-list-item>
            </template>
            <template #selection="{ item }">
              <span>{{ item.raw.title }}</span>
            </template>
          </v-select>
        </v-col>

        <!-- Route Path (for item type) -->
        <v-col v-if="formData.itemType === 'item'" cols="12" md="6">
          <v-text-field
            v-model="formData.to"
            :label="$t('side-menu-manager.form.fields.route')"
            variant="outlined"
            :hint="$t('side-menu-manager.form.fields.routeHint')"
            persistent-hint
          >
            <template #append-inner v-if="formData.to && formData.to.trim()">
              <v-btn
                icon
                size="small"
                variant="text"
                @click="navigateToRoute(formData.to!)"
                :title="$t('side-menu-manager.form.buttons.navigate')"
              >
                <v-icon size="18">mdi-open-in-new</v-icon>
              </v-btn>
            </template>
          </v-text-field>
        </v-col>

        <!-- Link Type (for item type) -->
        <v-col v-if="formData.itemType === 'item'" cols="12" md="6">
          <v-select
            v-model="formData.type"
            :items="[
              { title: 'Internal', value: 'internal' },
              { title: 'External', value: 'external' },
            ]"
            :label="$t('side-menu-manager.form.fields.linkType')"
            variant="outlined"
          ></v-select>
        </v-col>

        <!-- Icon Type & Icon (for item type) -->
        <v-col v-if="formData.itemType === 'item'" cols="12">
          <IconPicker
            :icon-type="formData.iconType || 'tabler'"
            :icon-name="formData.icon || ''"
            @icon-select="handleIconSelect"
          />
        </v-col>

        <!-- Sub Caption -->
        <v-col cols="12">
          <v-text-field
            v-model="formData.subCaption"
            :label="$t('side-menu-manager.form.fields.subCaption')"
            variant="outlined"
          ></v-text-field>
        </v-col>

        <!-- Chip Section -->
        <v-col cols="12">
          <v-divider class="mb-4"></v-divider>
          <h3 class="text-h6 mb-4">{{ $t('side-menu-manager.form.sections.chip') }}</h3>
        </v-col>

        <v-col cols="12" md="6">
          <v-text-field
            v-model="formData.chip"
            :label="$t('side-menu-manager.form.fields.chip')"
            variant="outlined"
          ></v-text-field>
        </v-col>

        <v-col cols="12" md="6">
          <v-text-field
            v-model="formData.chipVariant"
            :label="$t('side-menu-manager.form.fields.chipVariant')"
            variant="outlined"
          ></v-text-field>
        </v-col>

        <v-col cols="12" md="6">
          <v-text-field
            v-model="formData.chipColor"
            :label="$t('side-menu-manager.form.fields.chipColor')"
            variant="outlined"
          ></v-text-field>
        </v-col>

        <v-col cols="12" md="6">
          <v-text-field
            v-model="formData.chipBgColor"
            :label="$t('side-menu-manager.form.fields.chipBgColor')"
            variant="outlined"
          ></v-text-field>
        </v-col>

        <v-col cols="12">
          <v-text-field
            v-model="formData.chipIcon"
            :label="$t('side-menu-manager.form.fields.chipIcon')"
            variant="outlined"
          ></v-text-field>
        </v-col>

        <!-- Permissions Section -->
        <v-col cols="12">
          <v-divider class="mb-4"></v-divider>
          <h3 class="text-h6 mb-4">{{ $t('side-menu-manager.form.sections.permissions') }}</h3>
          <PermissionEditor
            :permissions="formData.permissions"
            @change="handlePermissionsChange"
          />
        </v-col>

        <!-- Locale Update Section -->
        <v-col cols="12" v-if="isEditMode && formData.pageCode">
          <v-divider class="mb-4"></v-divider>
          <h3 class="text-h6 mb-4">{{ $t('side-menu-manager.form.sections.locale') }}</h3>
          
          <!-- Success Message -->
          <v-alert
            v-if="localeUpdateMessage"
            type="success"
            variant="tonal"
            density="compact"
            class="mb-4"
            closable
            @click:close="localeUpdateMessage = ''"
          >
            {{ localeUpdateMessage }}
          </v-alert>
          
          <!-- Error Message -->
          <v-alert
            v-if="localeUpdateError"
            type="error"
            variant="tonal"
            density="compact"
            class="mb-4"
            closable
            @click:close="localeUpdateError = ''"
          >
            {{ localeUpdateError }}
          </v-alert>
          
          <v-btn
            color="info"
            variant="outlined"
            :loading="updatingLocales"
            :disabled="!formData.pageCode || updatingLocales || loading"
            @click="updateLocaleFiles"
          >
            <template #prepend>
              <LanguageIcon size="18" />
            </template>
            {{ $t('side-menu-manager.form.buttons.updateLocales') }}
          </v-btn>
          <p class="text-caption text-disabled mt-2">
            {{ $t('side-menu-manager.form.locale.description', { pageCode: formData.pageCode }) }}
          </p>
        </v-col>

        <!-- Action Buttons -->
        <v-col cols="12">
          <v-divider class="mb-4"></v-divider>
          <div class="d-flex gap-2">
            <v-btn
              type="submit"
              color="primary"
              variant="flat"
              :loading="loading"
            >
              <template #prepend>
                <v-icon>mdi-content-save</v-icon>
              </template>
              {{ isEditMode ? $t('side-menu-manager.form.buttons.update') : $t('side-menu-manager.form.buttons.save') }}
            </v-btn>

            <v-btn
              v-if="isEditMode"
              color="error"
              variant="flat"
              :loading="loading"
              prepend-icon="TrashIcon"
              @click="handleDelete"
            >
              <template #prepend>
                <TrashIcon size="20" />
              </template>
              {{ $t('side-menu-manager.form.buttons.delete') }}
            </v-btn>

            <v-spacer></v-spacer>

            <v-btn
              color="default"
              variant="outlined"
              prepend-icon="XIcon"
              @click="emit('cancel')"
            >
              <template #prepend>
                <XIcon size="20" />
              </template>
              {{ $t('side-menu-manager.form.buttons.cancel') }}
            </v-btn>
          </div>
        </v-col>
      </v-row>
    </v-form>
  </div>
</template>

<style scoped>
.menu-item-form {
  padding: 16px 0;
}
</style>
