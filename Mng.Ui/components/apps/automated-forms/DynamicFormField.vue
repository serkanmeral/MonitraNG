<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useDatasetStore } from '@/stores/apps/dataset';
import { useUserStore } from '@/stores/apps/user';
import { useGroupStore } from '@/stores/apps/group';
import { fetchFromDataGateway } from '@/services/apiService';
import { useFieldLabel } from '@/composables/useFieldLabel';
import FileUploadField from './FileUploadField.vue';

const props = defineProps<{
  field: any;
  modelValue: any;
  readonly?: boolean;
  disabled?: boolean;
  errorMessages?: string[];
  relationConfig?: {
    idField?: string; // Which field to use as value (default: '__dataId')
    displayField?: string; // Which field to display in dropdown
  };
  form?: any; // Form object (for field label translation)
  datasetName?: string; // Dataset name (for field label translation)
}>();

const emit = defineEmits<{
  'update:modelValue': [value: any];
}>();

const datasetStore = useDatasetStore();
const userStore = useUserStore();
const groupStore = useGroupStore();
const { getFieldLabel } = useFieldLabel();

// Get field label with translation support
const fieldLabel = computed(() => {
  const label = getFieldLabel(props.field.name, props.field, props.form, props.datasetName);
  // Add asterisk for required fields
  return props.field.mandatory ? `${label} *` : label;
});

// Get field type icon
const fieldTypeIcon = computed(() => {
  const type = props.field.fieldType;
  const iconMap: Record<string, string> = {
    text: 'mdi-text',
    number: 'mdi-numeric',
    bool: 'mdi-toggle-switch',
    datetime: 'mdi-calendar-clock',
    object: 'mdi-code-json',
    relation: 'mdi-link-variant',
    persons: 'mdi-account-multiple',
    personGroups: 'mdi-account-group',
    incremental: 'mdi-auto-fix',
    file: 'mdi-file-upload',
  };
  return iconMap[type] || 'mdi-form-select';
});

// Helper function to extract ID from expanded relation/person/personGroup object
const extractIdFromObject = (item: any, fieldType: string): any => {
  if (typeof item !== 'object' || item === null) {
    return item;
  }
  
  // For person fields, prioritize id (which matches usersOptions value)
  if (fieldType === 'persons') {
    // Try id first (matches userStore.users[].id)
    if (item.id) return item.id;
    // Then try userId (alternative field name)
    if (item.userId) return item.userId;
    // Then try common ID fields
    if (item.__dataId) return item.__dataId;
    if (item._id) return item._id;
  }
  
  // For personGroup fields, prioritize id (which matches groupsOptions value)
  if (fieldType === 'personGroups') {
    // Try id first (matches groupStore.groups[].id)
    if (item.id) return item.id;
    // Then try groupId (alternative field name)
    if (item.groupId) return item.groupId;
    // Then try common ID fields
    if (item.__dataId) return item.__dataId;
    if (item._id) return item._id;
  }
  
  // For relation fields, try common ID fields
  if (fieldType === 'relation') {
    if (item.__dataId) return item.__dataId;
    if (item.id) return item.id;
    if (item._id) return item._id;
  }
  
  // If no ID found, return the item as is (might be a string ID already)
  return item;
};

// Local value for two-way binding
// For array fields, ensure value is always an array (or empty array if null/undefined)
// For relation/person/personGroup fields, convert expanded objects to IDs
const localValue = computed({
  get: () => {
    const value = props.modelValue;
    
    // Handle relation/person/personGroup fields: convert expanded objects to IDs
    if (props.field.fieldType === 'relation' || props.field.fieldType === 'persons' || props.field.fieldType === 'personGroups') {
      if (props.field.isArray) {
        if (value === null || value === undefined || value === '') {
          return [];
        }
        if (Array.isArray(value)) {
          // Convert each object to ID
          return value.map(item => extractIdFromObject(item, props.field.fieldType));
        }
        // Single value but field is array - convert to array and extract ID
        return [extractIdFromObject(value, props.field.fieldType)];
      } else {
        // Single value - extract ID from object if needed
        return extractIdFromObject(value, props.field.fieldType);
      }
    }
    
    // For other array fields, ensure we always return an array
    if (props.field.isArray) {
      if (value === null || value === undefined || value === '') {
        return [];
      }
      if (Array.isArray(value)) {
        return value;
      }
      // Single value but field is array - convert to array
      return [value];
    }
    
    return value;
  },
  set: (val) => {
    // For array fields, ensure we always store as array
    if (props.field.isArray) {
      if (val === null || val === undefined || val === '') {
        emit('update:modelValue', []);
      } else if (Array.isArray(val)) {
        emit('update:modelValue', val);
      } else {
        // Single value selected but field is array - convert to array
        emit('update:modelValue', [val]);
      }
    } else {
      emit('update:modelValue', val);
    }
  },
});

// Relation dataset options
const relationOptions = ref<any[]>([]);
const relationLoading = ref(false);

// Users options
const usersOptions = computed(() => {
  return userStore.users.map(user => ({
    title: `${user.firstName} ${user.lastName} (${user.username})`,
    value: user.id,
    subtitle: user.email,
  }));
});

// Groups options
const groupsOptions = computed(() => {
  return groupStore.groups.map(group => ({
    title: group.name,
    value: group.id,
    subtitle: group.description || '',
  }));
});

// Load relation options if field type is relation
const loadRelationOptions = async () => {
  if (props.field.fieldType !== 'relation' || !props.field.relationDataset) return;
  
  relationLoading.value = true;
  try {
    const url = `/api/v1/data/${encodeURIComponent(props.field.relationDataset)}?skip=0&limit=1000`;
    const response = await fetchFromDataGateway(url, 'GET');
    
    // Get display field from relationConfig or fallback to relationField or '__dataId'
    const displayField = props.relationConfig?.displayField 
      || props.field.relationField 
      || '__dataId';
    
    // Get ID field from relationConfig or fallback to '__dataId'
    const idField = props.relationConfig?.idField || '__dataId';
    
    if (Array.isArray(response)) {
      relationOptions.value = response.map((item: any) => ({
        title: item[displayField] || item.__dataId || '',
        value: item[idField] || item.__dataId || '',
        subtitle: displayField !== idField ? `${idField}: ${item[idField] || item.__dataId || ''}` : undefined,
      }));
    } else {
      const items = response.items || response.Items || [];
      relationOptions.value = items.map((item: any) => ({
        title: item[displayField] || item.__dataId || '',
        value: item[idField] || item.__dataId || '',
        subtitle: displayField !== idField ? `${idField}: ${item[idField] || item.__dataId || ''}` : undefined,
      }));
    }
  } catch (error) {
    console.error('Error loading relation options:', error);
    relationOptions.value = [];
  } finally {
    relationLoading.value = false;
  }
};

// Load users if field type is persons
const loadUsers = async () => {
  if (props.field.fieldType !== 'persons') return;
  // Always load users to ensure we have the latest data
  // The userStore might have cached users, but we want fresh data for the form
  try {
    await userStore.fetchUsers({ page: 1, pageSize: 1000 });
  } catch (error) {
    console.error('Error loading users:', error);
  }
};

// Load groups if field type is personGroups
const loadGroups = async () => {
  if (props.field.fieldType !== 'personGroups' || groupStore.groups.length > 0) return;
  try {
    await groupStore.fetchGroups({ page: 1, pageSize: 1000 });
  } catch (error) {
    console.error('Error loading groups:', error);
  }
};

// Watch field type and load options
watch(() => props.field.fieldType, () => {
  if (props.field.fieldType === 'relation') {
    loadRelationOptions();
  } else if (props.field.fieldType === 'persons') {
    loadUsers();
  } else if (props.field.fieldType === 'personGroups') {
    loadGroups();
  }
}, { immediate: true });

// Object JSON value (for object type)
const objectJsonValue = computed({
  get: () => {
    if (props.field.fieldType === 'object' && localValue.value) {
      try {
        return JSON.stringify(localValue.value, null, 2);
      } catch {
        return '';
      }
    }
    return '';
  },
  set: (val: string) => {
    if (props.field.fieldType === 'object') {
      try {
        localValue.value = val ? JSON.parse(val) : null;
      } catch (error) {
        // Invalid JSON, keep as is
      }
    }
  },
});

// DateTime local value (for datetime type)
// Check if datetime field should show time
const showTime = computed(() => {
  if (props.field.fieldType === 'datetime' && props.field.datetimeOptions) {
    return props.field.datetimeOptions.showTime !== undefined ? props.field.datetimeOptions.showTime : true;
  }
  return true; // Default: show time
});

const datetimeLocalValue = computed({
  get: () => {
    if (props.field.fieldType === 'datetime' && localValue.value) {
      try {
        const date = new Date(localValue.value);
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        
        if (showTime.value) {
          // Convert to local datetime-local format (YYYY-MM-DDTHH:mm)
          const hours = String(date.getHours()).padStart(2, '0');
          const minutes = String(date.getMinutes()).padStart(2, '0');
          return `${year}-${month}-${day}T${hours}:${minutes}`;
        } else {
          // Convert to date format (YYYY-MM-DD)
          return `${year}-${month}-${day}`;
        }
      } catch {
        return '';
      }
    }
    return '';
  },
  set: (val: string) => {
    if (props.field.fieldType === 'datetime') {
      if (val) {
        const date = new Date(val);
        localValue.value = date.toISOString();
      } else {
        localValue.value = null;
      }
    }
  },
});

// Rules based on field validation
const rules = computed(() => {
  const rulesArray: any[] = [];
  
  if (props.field.mandatory) {
    if (props.field.fieldType === 'bool') {
      // Boolean fields are always valid (they have a default value)
    } else if (props.field.isArray) {
      rulesArray.push((v: any) => {
        if (!v || (Array.isArray(v) && v.length === 0)) {
          return `${props.field.title || props.field.name} zorunludur`;
        }
        return true;
      });
    } else {
      rulesArray.push((v: any) => {
        if (v === null || v === undefined || v === '') {
          return `${props.field.title || props.field.name} zorunludur`;
        }
        return true;
      });
    }
  }
  
  if (props.field.validation) {
    const validation = props.field.validation;
    
    if (props.field.fieldType === 'text' && validation.minLength !== undefined) {
      rulesArray.push((v: string) => {
        if (!v) return true; // Mandatory check is separate
        if (v.length < validation.minLength!) {
          return `En az ${validation.minLength} karakter olmalıdır`;
        }
        return true;
      });
    }
    
    if (props.field.fieldType === 'text' && validation.maxLength !== undefined) {
      rulesArray.push((v: string) => {
        if (!v) return true;
        if (v.length > validation.maxLength!) {
          return `En fazla ${validation.maxLength} karakter olabilir`;
        }
        return true;
      });
    }
    
    if (props.field.fieldType === 'number' && validation.min !== undefined) {
      rulesArray.push((v: number) => {
        if (v === null || v === undefined || v === '') return true;
        if (v < validation.min!) {
          return `Minimum değer ${validation.min} olmalıdır`;
        }
        return true;
      });
    }
    
    if (props.field.fieldType === 'number' && validation.max !== undefined) {
      rulesArray.push((v: number) => {
        if (v === null || v === undefined || v === '') return true;
        if (v > validation.max!) {
          return `Maksimum değer ${validation.max} olmalıdır`;
        }
        return true;
      });
    }
    
    if (validation.pattern) {
      rulesArray.push((v: string) => {
        if (!v) return true;
        const regex = new RegExp(validation.pattern!);
        if (!regex.test(v)) {
          return `Geçerli bir format giriniz`;
        }
        return true;
      });
    }
  }
  
  return rulesArray;
});
</script>

<template>
  <!-- Text Field -->
  <v-text-field
    v-if="field.fieldType === 'text'"
    v-model="localValue"
    :label="fieldLabel"
    :hint="field.description"
    :persistent-hint="!!field.description"
    :required="field.mandatory"
    :readonly="readonly || field.fieldType === 'incremental'"
    :disabled="disabled || field.fieldType === 'incremental'"
    :rules="rules"
    :error-messages="errorMessages"
    variant="outlined"
    clearable
    density="comfortable"
  >
    <template v-slot:prepend-inner>
      <v-icon :icon="fieldTypeIcon" size="20" class="text-medium-emphasis mr-2"></v-icon>
    </template>
  </v-text-field>
  
  <!-- Number Field -->
  <v-text-field
    v-else-if="field.fieldType === 'number'"
    v-model.number="localValue"
    type="number"
    :label="fieldLabel"
    :hint="field.description"
    :persistent-hint="!!field.description"
    :required="field.mandatory"
    :readonly="readonly"
    :disabled="disabled"
    :rules="rules"
    :error-messages="errorMessages"
    variant="outlined"
    clearable
    density="comfortable"
  >
    <template v-slot:prepend-inner>
      <v-icon :icon="fieldTypeIcon" size="20" class="text-medium-emphasis mr-2"></v-icon>
    </template>
  </v-text-field>
  
  <!-- Boolean Field -->
  <div v-else-if="field.fieldType === 'bool'" class="mb-2">
    <div class="d-flex align-center ga-2 mb-1">
      <v-icon :icon="fieldTypeIcon" size="20" class="text-medium-emphasis"></v-icon>
      <v-label class="text-body-1 font-weight-medium">{{ fieldLabel }}</v-label>
    </div>
    <v-switch
      v-model="localValue"
      :hint="field.description"
      :persistent-hint="!!field.description"
      :readonly="readonly"
      :disabled="disabled"
      color="primary"
      :error-messages="errorMessages"
      class="ml-2"
    ></v-switch>
  </div>
  
  <!-- DateTime Field -->
  <v-text-field
    v-else-if="field.fieldType === 'datetime'"
    v-model="datetimeLocalValue"
    :type="showTime ? 'datetime-local' : 'date'"
    :label="fieldLabel"
    :hint="field.description"
    :persistent-hint="!!field.description"
    :required="field.mandatory"
    :readonly="readonly"
    :disabled="disabled"
    :error-messages="errorMessages"
    variant="outlined"
    clearable
    density="comfortable"
  >
    <template v-slot:prepend-inner>
      <v-icon :icon="fieldTypeIcon" size="20" class="text-medium-emphasis mr-2"></v-icon>
    </template>
  </v-text-field>
  
  <!-- Object Field (JSON Textarea) -->
  <div v-else-if="field.fieldType === 'object'">
    <div class="d-flex align-center ga-2 mb-2">
      <v-icon :icon="fieldTypeIcon" size="20" class="text-medium-emphasis"></v-icon>
      <v-label class="text-body-1 font-weight-medium">{{ fieldLabel }}</v-label>
    </div>
    <v-textarea
      v-model="objectJsonValue"
      :hint="field.description || 'JSON formatında object giriniz'"
      persistent-hint
      :required="field.mandatory"
      :readonly="readonly"
      :disabled="disabled"
      :error-messages="errorMessages"
      variant="outlined"
      rows="4"
      clearable
      density="comfortable"
    ></v-textarea>
  </div>
  
  <!-- Relation Field (Autocomplete) -->
  <v-autocomplete
    v-else-if="field.fieldType === 'relation'"
    v-model="localValue"
    :items="relationOptions"
    item-title="title"
    item-value="value"
    :label="fieldLabel"
    :hint="field.description"
    :persistent-hint="!!field.description"
    :required="field.mandatory"
    :readonly="readonly"
    :disabled="disabled || relationLoading"
    :loading="relationLoading"
    :multiple="field.isArray"
    :rules="rules"
    :error-messages="errorMessages"
    variant="outlined"
    clearable
    chips
    :closable-chips="field.isArray"
    density="comfortable"
    @update:search="loadRelationOptions"
  >
    <template v-slot:prepend-inner>
      <v-icon :icon="fieldTypeIcon" size="20" class="text-medium-emphasis mr-2"></v-icon>
    </template>
    <template #item="{ props: itemProps, item: itemData }">
      <v-list-item v-bind="itemProps" :subtitle="itemData.raw.subtitle"></v-list-item>
    </template>
  </v-autocomplete>
  
  <!-- Persons Field (Autocomplete) -->
  <v-autocomplete
    v-else-if="field.fieldType === 'persons'"
    v-model="localValue"
    :items="usersOptions"
    item-title="title"
    item-value="value"
    :label="fieldLabel"
    :hint="field.description"
    :persistent-hint="!!field.description"
    :required="field.mandatory"
    :readonly="readonly"
    :disabled="disabled"
    :multiple="field.isArray"
    :rules="rules"
    :error-messages="errorMessages"
    variant="outlined"
    clearable
    chips
    closable-chips
    density="comfortable"
  >
    <template v-slot:prepend-inner>
      <v-icon :icon="fieldTypeIcon" size="20" class="text-medium-emphasis mr-2"></v-icon>
    </template>
    <template #item="{ props, item }">
      <v-list-item v-bind="props" :subtitle="item.raw.subtitle"></v-list-item>
    </template>
  </v-autocomplete>
  
  <!-- Person Groups Field (Autocomplete) -->
  <v-autocomplete
    v-else-if="field.fieldType === 'personGroups'"
    v-model="localValue"
    :items="groupsOptions"
    item-title="title"
    item-value="value"
    :label="fieldLabel"
    :hint="field.description"
    :persistent-hint="!!field.description"
    :required="field.mandatory"
    :readonly="readonly"
    :disabled="disabled"
    :multiple="field.isArray"
    :rules="rules"
    :error-messages="errorMessages"
    variant="outlined"
    clearable
    chips
    closable-chips
    density="comfortable"
  >
    <template v-slot:prepend-inner>
      <v-icon :icon="fieldTypeIcon" size="20" class="text-medium-emphasis mr-2"></v-icon>
    </template>
    <template #item="{ props, item }">
      <v-list-item v-bind="props" :subtitle="item.raw.subtitle"></v-list-item>
    </template>
  </v-autocomplete>
  
  <!-- Incremental Field (Read-only) -->
  <v-text-field
    v-else-if="field.fieldType === 'incremental'"
    :model-value="localValue || '(Otomatik)'"
    :label="fieldLabel"
    :hint="field.description || 'Bu alan otomatik olarak oluşturulacaktır'"
    persistent-hint
    readonly
    disabled
    variant="outlined"
    density="comfortable"
  >
    <template v-slot:prepend-inner>
      <v-icon :icon="fieldTypeIcon" size="20" class="text-medium-emphasis mr-2"></v-icon>
    </template>
  </v-text-field>
  
  <!-- File Field -->
  <div v-else-if="field.fieldType === 'file'">
    <div class="d-flex align-center ga-2 mb-2">
      <v-icon :icon="fieldTypeIcon" size="20" class="text-medium-emphasis"></v-icon>
      <v-label class="text-body-1 font-weight-medium">{{ fieldLabel }}</v-label>
    </div>
    <FileUploadField
      :field="field"
      v-model="localValue"
      :readonly="readonly"
      :disabled="disabled"
      :error-messages="errorMessages"
      :dataset-name="datasetName"
    />
  </div>
  
  <!-- Unknown Field Type -->
  <v-alert
    v-else
    type="warning"
    variant="tonal"
    density="compact"
  >
    Bilinmeyen field type: {{ field.fieldType }}
  </v-alert>
</template>
