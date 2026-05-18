<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useHubStore } from '@/stores/hub';

const { t } = useAppI18n();
const hubStore = useHubStore();

const isHubConnected = computed(() => hubStore.connected);
const isHubConnecting = computed(() => hubStore.connecting);
const hubError = computed(() => hubStore.error);

const props = defineProps<{
  connectError: string | null;
}>();

const hubStatusTooltip = computed(() => {
  const err = ((props.connectError ?? '') || (hubError.value ?? '')).trim();
  return err || '';
});
</script>

<template>
  <div class="chat-room-sidebar-header chat-room-sidebar-header--with-status">
    <span class="header-title">{{ t('chatRoom.sidebarTitle') }}</span>
    <v-tooltip :disabled="!hubStatusTooltip" :text="hubStatusTooltip" location="bottom">
      <template #activator="{ props: activatorProps }">
        <span v-bind="activatorProps" class="d-inline-flex">
          <v-chip v-if="isHubConnecting" size="x-small" color="warning" variant="flat" class="flex-shrink-0">
            {{ t('chatRoom.liveFeedConnecting') }}
          </v-chip>
          <v-chip v-else-if="isHubConnected" size="x-small" color="success" variant="flat" class="flex-shrink-0">
            {{ t('chatRoom.liveFeedConnected') }}
          </v-chip>
          <v-chip v-else size="x-small" color="error" variant="flat" class="flex-shrink-0">
            {{ t('chatRoom.liveFeedDisconnected') }}
          </v-chip>
        </span>
      </template>
    </v-tooltip>
    <v-spacer />
    <v-btn icon variant="text" density="comfortable" color="white" class="opacity-90">
      <v-icon size="22">mdi-dots-vertical</v-icon>
    </v-btn>
  </div>
</template>
