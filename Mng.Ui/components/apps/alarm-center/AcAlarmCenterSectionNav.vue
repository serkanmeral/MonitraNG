<script setup lang="ts">
import { computed } from 'vue';
import { useRoute } from 'vue-router';
import { useAppI18n } from '@/composables/useAppI18n';

const { t } = useAppI18n();
const route = useRoute();

const activeTab = computed(() =>
  route.path.startsWith('/apps/alarm-center/rules') ? 'rules' : 'alarms',
);
</script>

<template>
  <v-btn-toggle
    :model-value="activeTab"
    mandatory
    density="compact"
    variant="outlined"
    divided
    class="ac-alarm-center-nav mb-4"
  >
    <v-btn value="alarms" prepend-icon="mdi-inbox-outline" to="/apps/alarm-center/alarms">
      {{ t('alarmCenter.navAlarms') }}
    </v-btn>
    <v-btn value="rules" prepend-icon="mdi-shield-crown-outline" to="/apps/alarm-center/rules">
      {{ t('alarmCenter.navRules') }}
    </v-btn>
  </v-btn-toggle>
</template>
