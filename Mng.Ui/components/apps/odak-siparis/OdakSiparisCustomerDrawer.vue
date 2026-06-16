<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OdakCustomerRow } from '@/utils/odakSiparisConfig';
import {
  customerSektorLabel,
  fetchOdakCustomerById,
  packagesByCustomerRoute,
} from '@/utils/odakSiparisCustomerService';
import { customerHubRoute } from '@/utils/odakSiparisService';

const props = defineProps<{
  modelValue: boolean;
  customerId?: string;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  edit: [customerId: string];
}>();

const { t } = useAppI18n();
const router = useRouter();

const loading = ref(false);
const errorMessage = ref('');
const customer = ref<OdakCustomerRow | null>(null);

const customerTitle = computed(() => customer.value?.unvan ?? customer.value?.kod ?? '—');
const customerKod = computed(() => customer.value?.kod ?? '—');

async function loadCustomer() {
  const id = props.customerId?.trim();
  if (!id) {
    customer.value = null;
    return;
  }
  loading.value = true;
  errorMessage.value = '';
  try {
    customer.value = await fetchOdakCustomerById(id);
    if (!customer.value) errorMessage.value = t('odakSiparis.customers.drawer.notFound');
  } catch (e: unknown) {
    errorMessage.value = e instanceof Error ? e.message : String(e);
    customer.value = null;
  } finally {
    loading.value = false;
  }
}

function closeDrawer() {
  emit('update:modelValue', false);
}

function openEdit() {
  const id = props.customerId?.trim();
  if (!id) return;
  emit('edit', id);
}

function openHub() {
  const id = props.customerId;
  if (!id) return;
  closeDrawer();
  void router.push(customerHubRoute(id));
}

function openPackages() {
  const id = props.customerId;
  if (!id) return;
  closeDrawer();
  void router.push(packagesByCustomerRoute(id));
}

watch(
  () => [props.modelValue, props.customerId] as const,
  () => {
    if (props.modelValue) void loadCustomer();
  }
);
</script>

<template>
  <v-navigation-drawer
    :model-value="modelValue"
    location="right"
    temporary
    width="380"
    @update:model-value="emit('update:modelValue', $event)"
  >
    <div class="d-flex flex-column h-100">
      <div class="d-flex align-center px-4 py-3 border-b">
        <div class="flex-grow-1 min-w-0">
          <div class="text-overline text-medium-emphasis">{{ t('odakSiparis.customers.drawer.title') }}</div>
          <div class="text-h6 text-truncate">{{ customerTitle }}</div>
          <div class="text-caption text-medium-emphasis">{{ customerKod }}</div>
        </div>
        <v-btn icon variant="text" size="small" @click="closeDrawer">
          <v-icon>mdi-close</v-icon>
        </v-btn>
      </div>

      <div class="flex-grow-1 overflow-y-auto pa-4">
        <v-alert v-if="errorMessage" type="warning" variant="tonal" density="compact" class="mb-3">
          {{ errorMessage }}
        </v-alert>
        <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-3" />

        <v-table v-else-if="customer" density="compact" class="border rounded-md">
          <tbody>
            <tr>
              <td class="font-weight-medium text-medium-emphasis" width="120">{{ t('odakSiparis.customers.fields.kod') }}</td>
              <td>{{ customer.kod ?? '—' }}</td>
            </tr>
            <tr>
              <td class="font-weight-medium text-medium-emphasis">{{ t('odakSiparis.customers.fields.unvan') }}</td>
              <td>{{ customer.unvan ?? '—' }}</td>
            </tr>
            <tr>
              <td class="font-weight-medium text-medium-emphasis">{{ t('odakSiparis.customers.fields.sektor') }}</td>
              <td>{{ customerSektorLabel(customer.sektor) }}</td>
            </tr>
            <tr>
              <td class="font-weight-medium text-medium-emphasis">{{ t('odakSiparis.customers.fields.ulke') }}</td>
              <td>{{ customer.ulke ?? '—' }}</td>
            </tr>
            <tr>
              <td class="font-weight-medium text-medium-emphasis">{{ t('odakSiparis.customers.fields.aktif') }}</td>
              <td>
                <v-chip
                  size="x-small"
                  :color="customer.aktif !== false ? 'success' : 'error'"
                  variant="tonal"
                >
                  {{ customer.aktif !== false ? t('odakSiparis.customers.activeYes') : t('odakSiparis.customers.activeNo') }}
                </v-chip>
              </td>
            </tr>
            <tr v-if="customer.notlar">
              <td class="font-weight-medium text-medium-emphasis align-top">{{ t('odakSiparis.customers.fields.notlar') }}</td>
              <td class="text-body-2">{{ customer.notlar }}</td>
            </tr>
          </tbody>
        </v-table>
      </div>

      <div class="pa-4 border-t d-flex flex-column ga-2">
        <v-btn color="primary" variant="flat" block :disabled="!customerId || loading" @click="openEdit">
          {{ t('odakSiparis.customers.drawer.edit') }}
        </v-btn>
        <v-btn variant="tonal" block :disabled="!customerId" @click="openHub">
          {{ t('odakSiparis.customers.drawer.openHub') }}
        </v-btn>
        <v-btn variant="text" block :disabled="!customerId" @click="openPackages">
          {{ t('odakSiparis.customers.drawer.openPackages') }}
        </v-btn>
      </div>
    </div>
  </v-navigation-drawer>
</template>

<style scoped>
.border-b {
  border-bottom: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
.border-t {
  border-top: 1px solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
