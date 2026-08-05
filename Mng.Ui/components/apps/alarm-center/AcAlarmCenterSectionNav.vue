<script setup lang="ts">
import { computed } from 'vue';
import { useRoute } from 'vue-router';
import { useAppI18n } from '@/composables/useAppI18n';

const { t } = useAppI18n();
const route = useRoute();

const activeTab = computed(() => {
  if (route.path.startsWith('/apps/alarm-center/notification-policies')) return 'policies';
  if (route.path.startsWith('/apps/alarm-center/scenario-studio')) return 'studio';
  if (route.path.startsWith('/apps/alarm-center/rules')) return 'rules';
  return 'alarms';
});
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
    <v-btn value="studio" prepend-icon="mdi-vector-polyline-edit" to="/apps/alarm-center/scenario-studio">
      {{ t('alarmCenter.navScenarioStudio') }}
    </v-btn>
    <v-btn
      value="policies"
      prepend-icon="mdi-bell-ring-outline"
      to="/apps/alarm-center/notification-policies"
    >
      {{ t('alarmCenter.navNotificationPolicies') }}
    </v-btn>
  </v-btn-toggle>
</template>
