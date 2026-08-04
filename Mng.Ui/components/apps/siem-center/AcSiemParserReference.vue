<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import {
  SIEM_ALARM_PACKAGE_ID,
  SIEM_ALARM_PACKAGE_VERSION,
  SIEM_COLLECTION_METHODS,
  SIEM_OUT_OF_SCOPE,
  SIEM_PARSERS,
  SIEM_REFERENCE_VERSION,
  SIEM_SCENARIO_REFERENCES,
  parserStatusColor,
  parserStatusLabelKey,
} from '@/composables/useSiemParserCatalog';
import { SIEM_SCENARIO_CATALOG, scenarioEventsLink } from '@/composables/useSiemScenarioCatalog';

const { t } = useAppI18n();

const scenarioById = computed(() => {
  const map = new Map<string, (typeof SIEM_SCENARIO_CATALOG)[number]>();
  for (const s of SIEM_SCENARIO_CATALOG) {
    map.set(s.id, s);
  }
  return map;
});

function collectionMethodTitle(id: string): string {
  const row = SIEM_COLLECTION_METHODS.find((m) => m.id === id);
  return row ? t(row.titleKey) : id;
}

function eventsLinkForScenario(scenarioId: string): string | undefined {
  const def = scenarioById.value.get(scenarioId);
  return def ? scenarioEventsLink(def) : undefined;
}

function parserPanelTitle(parser: (typeof SIEM_PARSERS)[number]): string {
  return t(parser.titleKey);
}
</script>

<template>
  <div class="siem-parser-reference">
    <p class="text-body-1 mb-6">
      {{ t('siemCenter.reference.intro') }}
    </p>

    <v-alert type="warning" variant="tonal" density="comfortable" class="mb-4">
      <div class="text-subtitle-2 font-weight-bold mb-2">
        {{ t('siemCenter.reference.readOnlyTitle') }}
      </div>
      <p class="text-body-2 mb-3">
        {{ t('siemCenter.reference.readOnlyLead') }}
      </p>
      <ul class="text-body-2 ps-4 mb-3">
        <li>{{ t('siemCenter.reference.readOnlyReason1') }}</li>
        <li>{{ t('siemCenter.reference.readOnlyReason2') }}</li>
        <li>{{ t('siemCenter.reference.readOnlyReason3') }}</li>
      </ul>
      <p class="text-body-2 mb-0">
        {{ t('siemCenter.reference.readOnlyContact') }}
      </p>
    </v-alert>

    <v-row class="mb-8">
      <v-col cols="12" md="6">
        <v-card variant="outlined" class="rounded-lg pa-4 h-100">
          <div class="text-subtitle-2 font-weight-bold mb-2 text-success">
            {{ t('siemCenter.reference.youCanEditTitle') }}
          </div>
          <v-list density="compact" class="py-0">
            <v-list-item prepend-icon="mdi-tune" :title="t('siemCenter.reference.youCanEditAlarms')">
              <template #append>
                <v-btn
                  size="small"
                  variant="text"
                  color="primary"
                  to="/apps/alarm-center/rules"
                >
                  {{ t('siemCenter.reference.openAlarmRules') }}
                </v-btn>
              </template>
            </v-list-item>
            <v-list-item
              prepend-icon="mdi-send"
              :title="t('siemCenter.reference.youCanEditCollectors')"
              :subtitle="t('siemCenter.reference.youCanEditCollectorsHint')"
            />
            <v-list-item
              prepend-icon="mdi-magnify"
              :title="t('siemCenter.reference.youCanEditSearch')"
              :subtitle="t('siemCenter.reference.youCanEditSearchHint')"
              to="/apps/siem-center/events"
            />
          </v-list>
        </v-card>
      </v-col>
      <v-col cols="12" md="6">
        <v-card variant="outlined" class="rounded-lg pa-4 h-100">
          <div class="text-subtitle-2 font-weight-bold mb-2 text-medium-emphasis">
            {{ t('siemCenter.reference.weProvideTitle') }}
          </div>
          <p class="text-body-2 text-medium-emphasis mb-0">
            {{ t('siemCenter.reference.weProvideBody') }}
          </p>
        </v-card>
      </v-col>
    </v-row>

    <h2 class="text-h6 font-weight-bold mb-2">
      {{ t('siemCenter.reference.collectionSectionTitle') }}
    </h2>
    <p class="text-body-2 text-medium-emphasis mb-4">
      {{ t('siemCenter.reference.collectionSectionHint') }}
    </p>
    <v-row class="mb-8">
      <v-col
        v-for="method in SIEM_COLLECTION_METHODS"
        :key="method.id"
        cols="12"
        md="4"
      >
        <v-card variant="outlined" class="rounded-lg pa-4 h-100">
          <div class="d-flex align-center mb-2">
            <v-icon :icon="method.icon" class="me-2" color="primary" />
            <span class="text-subtitle-1 font-weight-bold">{{ t(method.titleKey) }}</span>
          </div>
          <p class="text-body-2 text-medium-emphasis mb-2">
            {{ t(method.descriptionKey) }}
          </p>
          <p class="text-caption mb-0">
            <span class="font-weight-medium">{{ t('siemCenter.reference.exampleTargets') }}:</span>
            {{ t(method.exampleTargetsKey) }}
          </p>
        </v-card>
      </v-col>
    </v-row>

    <h2 class="text-h6 font-weight-bold mb-2">
      {{ t('siemCenter.reference.parsersSectionTitle') }}
    </h2>
    <p class="text-body-2 text-medium-emphasis mb-4">
      {{ t('siemCenter.reference.parsersSectionHint') }}
    </p>

    <v-expansion-panels variant="accordion" class="mb-8">
      <v-expansion-panel v-for="parser in SIEM_PARSERS" :key="parser.id">
        <v-expansion-panel-title>
          <div class="d-flex flex-wrap align-center gap-2 py-1">
            <span class="text-subtitle-2 font-weight-bold">{{ parserPanelTitle(parser) }}</span>
            <v-chip size="x-small" :color="parserStatusColor(parser.status)" variant="flat">
              {{ t(parserStatusLabelKey(parser.status)) }}
            </v-chip>
            <v-chip
              v-if="parser.builtInLocked"
              size="x-small"
              color="secondary"
              variant="tonal"
              prepend-icon="mdi-lock-outline"
            >
              {{ t('siemCenter.reference.builtInLockedChip') }}
            </v-chip>
          </div>
        </v-expansion-panel-title>
        <v-expansion-panel-text>
          <p class="text-caption text-medium-emphasis mb-2">
            {{ t('siemCenter.reference.parserTechnicalId', { id: parser.id }) }}
          </p>
          <p class="text-body-2 mb-3">
            {{ t(parser.descriptionKey) }}
          </p>
          <v-alert
            v-if="parser.builtInLocked"
            type="info"
            variant="tonal"
            density="compact"
            class="mb-3"
          >
            {{ t('siemCenter.reference.builtInLockedHint') }}
          </v-alert>
          <p v-if="parser.explainKey" class="text-body-2 text-medium-emphasis mb-3">
            {{ t(parser.explainKey) }}
          </p>
          <p class="text-body-2 text-medium-emphasis mb-3">
            {{ t('siemCenter.reference.collectionMethod') }}:
            <strong>{{ collectionMethodTitle(parser.collectionMethodId) }}</strong>
          </p>
          <v-table density="compact" class="parser-mapping-table">
            <thead>
              <tr>
                <th>{{ t('siemCenter.reference.colLogSignal') }}</th>
                <th>{{ t('siemCenter.reference.colMeaning') }}</th>
                <th>{{ t('siemCenter.reference.colScenarios') }}</th>
                <th>{{ t('siemCenter.reference.colDefaultAlarm') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(row, idx) in parser.mappings" :key="`${parser.id}-${idx}`">
                <td><code>{{ row.input }}</code></td>
                <td>
                  <v-chip
                    v-if="row.eventAction !== 'unknown'"
                    size="x-small"
                    variant="tonal"
                    :to="row.eventAction.includes('|')
                      ? undefined
                      : `/apps/siem-center/events?eventAction=${encodeURIComponent(row.eventAction.split('|')[0].trim())}`"
                    :link="!row.eventAction.includes('|')"
                  >
                    {{ row.eventAction }}
                  </v-chip>
                  <span v-else>{{ t('siemCenter.reference.meaningUnknown') }}</span>
                </td>
                <td>
                  <template v-if="row.scenarioIds.length">
                    <v-chip
                      v-for="sid in row.scenarioIds"
                      :key="sid"
                      size="x-small"
                      class="me-1 mb-1"
                      variant="outlined"
                    >
                      {{ sid }}
                    </v-chip>
                  </template>
                  <span v-else class="text-medium-emphasis">—</span>
                </td>
                <td>
                  <v-icon
                    v-if="row.inAlarmPack"
                    icon="mdi-check-circle"
                    color="success"
                    size="small"
                    :title="t('siemCenter.reference.defaultAlarmYes')"
                  />
                  <span v-else class="text-medium-emphasis">—</span>
                </td>
              </tr>
            </tbody>
          </v-table>

          <template v-if="parser.fieldMappings?.length">
            <h3 class="text-subtitle-2 font-weight-bold mt-5 mb-2">
              {{ t('siemCenter.reference.fieldMapTitle') }}
            </h3>
            <p class="text-caption text-medium-emphasis mb-2">
              {{ t('siemCenter.reference.fieldMapHint') }}
            </p>
            <v-table density="compact" class="parser-field-map-table">
              <thead>
                <tr>
                  <th>{{ t('siemCenter.reference.colRawField') }}</th>
                  <th>{{ t('siemCenter.reference.colTargetField') }}</th>
                  <th>{{ t('siemCenter.reference.colFieldNote') }}</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="(fm, fidx) in parser.fieldMappings"
                  :key="`${parser.id}-fm-${fidx}`"
                >
                  <td><code>{{ fm.raw }}</code></td>
                  <td><code>{{ fm.target }}</code></td>
                  <td class="text-body-2 text-medium-emphasis">
                    {{ fm.noteKey ? t(fm.noteKey) : '—' }}
                  </td>
                </tr>
              </tbody>
            </v-table>
          </template>
        </v-expansion-panel-text>
      </v-expansion-panel>
    </v-expansion-panels>

    <h2 class="text-h6 font-weight-bold mb-2">
      {{ t('siemCenter.reference.scenariosSectionTitle') }}
    </h2>
    <p class="text-body-2 text-medium-emphasis mb-4">
      {{ t('siemCenter.reference.scenariosSectionHint') }}
    </p>

    <v-table density="comfortable" class="mb-4 scenario-table">
      <thead>
        <tr>
          <th>{{ t('siemCenter.reference.colScenario') }}</th>
          <th>{{ t('siemCenter.reference.colDescription') }}</th>
          <th>{{ t('siemCenter.reference.colDefaultRule') }}</th>
          <th>{{ t('siemCenter.reference.colPackStatus') }}</th>
          <th>{{ t('siemCenter.reference.colEventsLink') }}</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="scenario in SIEM_SCENARIO_REFERENCES" :key="scenario.id">
          <td>
            <v-chip size="small" color="primary" variant="tonal">{{ scenario.id }}</v-chip>
          </td>
          <td class="text-body-2">{{ t(scenario.descriptionKey) }}</td>
          <td class="text-body-2">{{ t(scenario.defaultRuleKey) }}</td>
          <td>
            <v-chip
              v-if="scenario.inAlarmPack"
              size="x-small"
              color="success"
              variant="tonal"
            >
              {{ t('siemCenter.reference.inPackYes') }}
            </v-chip>
            <span v-else class="text-body-2 text-medium-emphasis">
              {{ t('siemCenter.reference.inPackNo') }}
            </span>
          </td>
          <td>
            <v-btn
              v-if="eventsLinkForScenario(scenario.id)"
              size="small"
              variant="text"
              :to="eventsLinkForScenario(scenario.id)"
            >
              {{ t('siemCenter.reference.viewEvents') }}
            </v-btn>
          </td>
        </tr>
      </tbody>
    </v-table>
    <p class="text-caption text-medium-emphasis mb-8">
      {{ t('siemCenter.reference.scenariosFootnote', {
        id: SIEM_ALARM_PACKAGE_ID,
        version: SIEM_ALARM_PACKAGE_VERSION,
      }) }}
    </p>

    <h2 class="text-h6 font-weight-bold mb-2">
      {{ t('siemCenter.reference.outOfScopeSectionTitle') }}
    </h2>
    <p class="text-body-2 text-medium-emphasis mb-3">
      {{ t('siemCenter.reference.outOfScopeHint') }}
    </p>
    <v-list lines="two" class="mb-6">
      <v-list-item
        v-for="(item, idx) in SIEM_OUT_OF_SCOPE"
        :key="idx"
        prepend-icon="mdi-minus-circle-outline"
      >
        <v-list-item-title class="font-weight-medium">{{ t(item.titleKey) }}</v-list-item-title>
        <v-list-item-subtitle>{{ t(item.descriptionKey) }}</v-list-item-subtitle>
      </v-list-item>
    </v-list>

    <p class="text-caption text-medium-emphasis mb-0">
      {{ t('siemCenter.reference.technicalFootnote', { version: SIEM_REFERENCE_VERSION }) }}
    </p>
  </div>
</template>

<style scoped>
.parser-mapping-table :deep(th),
.scenario-table :deep(th) {
  font-weight: 600;
  white-space: nowrap;
}

.parser-mapping-table :deep(td),
.scenario-table :deep(td) {
  vertical-align: top;
}
</style>
