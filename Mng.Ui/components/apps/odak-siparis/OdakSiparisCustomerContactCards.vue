<script setup lang="ts">
import { onMounted, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { OdakCustomerContactRow } from '@/utils/odakSiparisConfig';
import { listContactsForCustomer } from '@/utils/odakSiparisCustomerContactService';

const props = defineProps<{
  customerId?: string;
  compact?: boolean;
}>();

const { t } = useAppI18n();

const loading = ref(false);
const contacts = ref<OdakCustomerContactRow[]>([]);

async function loadContacts() {
  const id = props.customerId?.trim();
  if (!id) {
    contacts.value = [];
    return;
  }
  loading.value = true;
  try {
    contacts.value = await listContactsForCustomer(id);
  } catch {
    contacts.value = [];
  } finally {
    loading.value = false;
  }
}

watch(
  () => props.customerId,
  () => {
    void loadContacts();
  }
);

onMounted(() => {
  void loadContacts();
});
</script>

<template>
  <div class="odak-customer-contact-cards">
    <div class="text-subtitle-2 font-weight-medium mb-2">
      {{ t('odakSiparis.customers.drawer.contactsSection') }}
    </div>

    <v-progress-linear v-if="loading" indeterminate color="primary" class="mb-2" />

    <div v-else-if="!contacts.length" class="text-body-2 text-medium-emphasis py-2">
      {{ t('odakSiparis.customers.drawer.noContacts') }}
    </div>

    <div v-else class="d-flex flex-column ga-2">
      <v-card
        v-for="contact in contacts"
        :key="contact.__dataId ?? contact.dataId ?? contact.ad"
        variant="outlined"
        :density="compact ? 'compact' : 'default'"
      >
        <v-card-text class="pa-3">
          <div class="d-flex align-start ga-2">
            <div class="flex-grow-1 min-w-0">
              <div class="d-flex align-center flex-wrap ga-1 mb-1">
                <span class="text-body-2 font-weight-medium">{{ contact.ad ?? '—' }}</span>
                <v-chip v-if="contact.birincilKisi" size="x-small" color="primary" variant="tonal">
                  {{ t('odakSiparis.customers.contacts.primaryYes') }}
                </v-chip>
                <v-chip
                  v-if="contact.aktif === false"
                  size="x-small"
                  color="error"
                  variant="tonal"
                >
                  {{ t('odakSiparis.customers.activeNo') }}
                </v-chip>
              </div>
              <div v-if="contact.gorevUnvani" class="text-caption text-medium-emphasis mb-1">
                {{ contact.gorevUnvani }}
              </div>
              <div v-if="contact.email" class="text-caption">
                <v-icon size="14" class="mr-1">mdi-email-outline</v-icon>
                <a :href="`mailto:${contact.email}`" class="text-primary">{{ contact.email }}</a>
              </div>
              <div v-if="contact.telefon" class="text-caption mt-1">
                <v-icon size="14" class="mr-1">mdi-phone-outline</v-icon>
                {{ contact.telefon }}
              </div>
            </div>
          </div>
        </v-card-text>
      </v-card>
    </div>
  </div>
</template>
