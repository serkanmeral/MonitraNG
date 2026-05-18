<script setup lang="ts">
import { ref, watch } from 'vue';
import type { MonItem } from '@/types/apps/organization';
import { AlertTriangleIcon } from 'vue-tabler-icons';
import LocationPickerModal from './LocationPickerModal.vue';

const props = defineProps<{
  item: MonItem | Partial<MonItem> | null;
  parentOptions: Array<{ title: string; value: string | null }>;
  loading?: boolean;
  /** Kaydet/Sil butonları sadece is_manager veya is_admin için gösterilir */
  canEdit?: boolean;
}>();

const emit = defineEmits<{
  save: [data: Partial<MonItem>];
  delete: [dataId: string];
  cancel: [];
}>();

const form = ref<Partial<MonItem>>({
  name: '',
  parentId: null,
  description: null,
  location: null,
  kind: null,
  tags: null,
});

const deleteDialogOpen = ref(false);
const locationPickerOpen = ref(false);

watch(
  () => props.item,
  (v) => {
    if (v) {
      form.value = {
        name: v.name ?? '',
        parentId: v.parentId ?? null,
        description: v.description ?? null,
        location: v.location ?? null,
        kind: v.kind ?? null,
        tags: v.tags ?? null,
      };
      if ('__dataId' in v && v.__dataId) (form.value as any).__dataId = v.__dataId;
    } else {
      form.value = { name: '', parentId: null, description: null, location: null, kind: null, tags: null };
    }
  },
  { immediate: true }
);

const isEdit = ref(false);
watch(() => props.item, (v) => { isEdit.value = !!(v && '__dataId' in v && v.__dataId); }, { immediate: true });

function save() {
  const data = { ...form.value };
  if (!data.name?.trim()) return;
  emit('save', data);
}

function openDeleteDialog() {
  if ((form.value as any).__dataId) deleteDialogOpen.value = true;
}

function confirmDelete() {
  const id = (form.value as any).__dataId;
  if (id) {
    emit('delete', id);
    deleteDialogOpen.value = false;
  }
}

function closeDeleteDialog() {
  deleteDialogOpen.value = false;
}
</script>

<template>
  <div class="org-item-form">
    <v-form @submit.prevent="save">
      <v-text-field
        v-model="form.name"
        label="Ad *"
        variant="outlined"
        density="comfortable"
        class="mb-3"
        hide-details
      />
      <v-select
        v-model="form.parentId"
        :items="parentOptions"
        item-title="title"
        item-value="value"
        label="Üst Item"
        variant="outlined"
        density="comfortable"
        clearable
        class="mb-3"
        hide-details
      />
      <v-textarea
        v-model="form.description"
        label="Açıklama"
        variant="outlined"
        density="comfortable"
        rows="2"
        class="mb-3"
        hide-details
      />
      <v-text-field
        v-model="form.kind"
        label="Tür (city, region, room, cabinet, server, pdu...)"
        variant="outlined"
        density="comfortable"
        class="mb-3"
        hide-details
      />
      <div class="mb-3">
        <div class="text-caption text-medium-emphasis mb-1">{{ $t('organization.locationPicker.label', 'Konum') }}</div>
        <div class="d-flex align-center gap-2">
          <v-btn
            variant="outlined"
            size="small"
            prepend-icon="mdi-map-marker"
            @click="locationPickerOpen = true"
          >
            {{ form.location?.lat != null ? $t('organization.locationPicker.change', 'Konum değiştir') : $t('organization.locationPicker.select', 'Konum seç') }}
          </v-btn>
          <span v-if="form.location?.lat != null" class="text-caption text-medium-emphasis">
            {{ form.location.lat?.toFixed(4) }}, {{ form.location.lon?.toFixed(4) }}
          </span>
        </div>
      </div>
      <LocationPickerModal
        v-model="form.location"
        :open="locationPickerOpen"
        @update:open="locationPickerOpen = $event"
      />
      <div class="d-flex gap-2 mt-4">
        <v-btn v-if="canEdit" color="primary" type="submit" :loading="loading">Kaydet</v-btn>
        <v-btn v-if="canEdit && isEdit" color="error" variant="outlined" @click="openDeleteDialog" :disabled="loading">Sil</v-btn>
        <v-btn variant="outlined" @click="emit('cancel')">İptal</v-btn>
      </div>
    </v-form>

    <v-dialog v-model="deleteDialogOpen" max-width="440" persistent>
      <v-card>
        <v-card-title class="d-flex align-center text-body-1">
          <AlertTriangleIcon size="24" class="mr-2 text-warning" />
          Item silinsin mi?
        </v-card-title>
        <v-card-text>
          <span class="text-body-2">"<strong>{{ form.name }}</strong>" öğesini silmek istediğinize emin misiniz? Alt öğe veya asset'ler varsa silme işlemi başarısız olabilir.</span>
        </v-card-text>
        <v-card-actions class="pt-0">
          <v-spacer />
          <v-btn variant="text" @click="closeDeleteDialog">İptal</v-btn>
          <v-btn color="error" variant="flat" :loading="loading" @click="confirmDelete">Sil</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
