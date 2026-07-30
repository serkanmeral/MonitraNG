<script setup lang="ts">
import { useAppI18n } from '@/composables/useAppI18n';
import {
  SIEM_SCENARIO_CATALOG,
  scenarioEventsLink,
  type SiemScenarioDef,
} from '@/composables/useSiemScenarioCatalog';

const { t } = useAppI18n();

function scenarioTitle(def: SiemScenarioDef): string {
  const key = `siemCenter.scenarios.${def.id}.title`;
  const translated = t(key);
  return translated !== key ? translated : def.id;
}

function scenarioDesc(def: SiemScenarioDef): string {
  const key = `siemCenter.scenarios.${def.id}.desc`;
  const translated = t(key);
  return translated !== key ? translated : def.matchKey;
}
</script>

<template>
  <div class="siem-settings-scenarios">
    <p class="text-body-2 text-medium-emphasis mb-4">
      {{ t('siemCenter.settings.scenarios.intro') }}
    </p>

    <v-alert type="info" variant="tonal" density="comfortable" class="mb-4">
      {{ t('siemCenter.settings.scenarios.rulesHint') }}
      <div class="mt-2">
        <v-btn
          size="small"
          variant="tonal"
          color="primary"
          to="/apps/alarm-center/rules"
          prepend-icon="mdi-bell-cog"
        >
          {{ t('siemCenter.reference.openAlarmRules') }}
        </v-btn>
      </div>
    </v-alert>

    <v-table density="comfortable">
      <thead>
        <tr>
          <th>{{ t('siemCenter.settings.scenarios.colId') }}</th>
          <th>{{ t('siemCenter.settings.scenarios.colTitle') }}</th>
          <th>{{ t('siemCenter.settings.scenarios.colAction') }}</th>
          <th>{{ t('siemCenter.settings.scenarios.colLinks') }}</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="def in SIEM_SCENARIO_CATALOG" :key="def.id">
          <td>
            <v-chip size="small" color="primary" variant="tonal">{{ def.id }}</v-chip>
          </td>
          <td>
            <div class="text-body-2 font-weight-medium">{{ scenarioTitle(def) }}</div>
            <div class="text-caption text-medium-emphasis">{{ scenarioDesc(def) }}</div>
          </td>
          <td class="text-body-2">
            <code v-if="def.eventAction">{{ def.eventAction }}</code>
            <span v-else class="text-medium-emphasis">{{ def.matchKey }}</span>
          </td>
          <td>
            <v-btn
              size="small"
              variant="text"
              color="primary"
              :to="scenarioEventsLink(def)"
            >
              {{ t('siemCenter.settings.scenarios.openEvents') }}
            </v-btn>
          </td>
        </tr>
      </tbody>
    </v-table>
  </div>
</template>
