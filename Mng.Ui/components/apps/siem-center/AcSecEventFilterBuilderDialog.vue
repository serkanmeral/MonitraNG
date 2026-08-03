<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { SEC_EVENT_ACTION_OPTIONS, sourceTypeLabelKey } from '@/composables/useSecEventList';
import {
  SEC_EVENT_FILTER_INTENTS,
  buildSecEventFilterBuilderResult,
  emptyFieldValues,
  getSecEventFilterIntent,
  type SecEventFilterBuilderResult,
  type SecEventFilterIntent,
  type SecEventFilterIntentField,
} from '@/utils/secEventFilterIntents';

const props = defineProps<{
  modelValue: boolean;
  /** Prefill when reopening. */
  initialIntentId?: string | null;
  initialValues?: Partial<Record<string, string | null>>;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: boolean];
  apply: [result: SecEventFilterBuilderResult];
}>();

const { t } = useAppI18n();

const open = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

const selectedIntentId = ref<string>(props.initialIntentId || 'rdp');
const fieldValues = reactive<Record<string, string | null>>({});

const selectedIntent = computed(() => getSecEventFilterIntent(selectedIntentId.value));

function resetForIntent(intent: SecEventFilterIntent) {
  const next = emptyFieldValues(intent);
  // Only restore prior values when reopening the same applied intent.
  if (props.initialValues && props.initialIntentId && intent.id === props.initialIntentId) {
    for (const [k, v] of Object.entries(props.initialValues)) {
      if (k in next) next[k] = v == null ? '' : String(v);
    }
  }
  for (const key of Object.keys(fieldValues)) delete fieldValues[key];
  Object.assign(fieldValues, next);
}

watch(
  () => props.modelValue,
  (isOpen) => {
    if (!isOpen) return;
    selectedIntentId.value = props.initialIntentId || selectedIntentId.value || 'rdp';
    const intent = getSecEventFilterIntent(selectedIntentId.value) ?? SEC_EVENT_FILTER_INTENTS[0];
    selectedIntentId.value = intent.id;
    resetForIntent(intent);
  },
);

watch(selectedIntentId, (id) => {
  const intent = getSecEventFilterIntent(id);
  if (intent) resetForIntent(intent);
});

function actionLabel(action: string): string {
  const fromOptions = SEC_EVENT_ACTION_OPTIONS.find((o) => o.value === action);
  if (fromOptions) {
    const translated = t(fromOptions.labelKey);
    if (translated !== fromOptions.labelKey) return translated;
  }
  const key = `siemCenter.events.actions.${action.replace(/\./g, '_')}`;
  const translated = t(key);
  return translated !== key ? translated : action;
}

function outcomeLabel(outcome: string): string {
  const key = `siemCenter.events.filterBuilder.outcomes.${outcome}`;
  const translated = t(key);
  return translated !== key ? translated : outcome;
}

function sourceLabel(value: string): string {
  return t(sourceTypeLabelKey(value));
}

function selectItemsForField(intent: SecEventFilterIntent, field: SecEventFilterIntentField) {
  // Use '' (not null) for "Any" — Vuetify v-select handles null item-values poorly.
  const anyItem = { title: t('siemCenter.events.filterBuilder.any'), value: '' };

  if (field.actionRefine) {
    return [
      anyItem,
      ...intent.eventActions.map((a) => ({ title: actionLabel(a), value: a })),
    ];
  }

  if (field.mapTo === 'eventAction' && intent.id === 'custom') {
    return [
      anyItem,
      ...SEC_EVENT_ACTION_OPTIONS.map((o) => ({
        title: t(o.labelKey),
        value: o.value,
      })),
    ];
  }

  if (field.mapTo === 'eventOutcome') {
    return [
      anyItem,
      ...(field.options ?? []).map((o) => ({ title: outcomeLabel(o), value: o })),
    ];
  }

  if (field.mapTo === 'sourceType') {
    return [
      anyItem,
      ...(field.options ?? []).map((o) => ({ title: sourceLabel(o), value: o })),
    ];
  }

  return [
    anyItem,
    ...(field.options ?? []).map((o) => ({ title: o, value: o })),
  ];
}

function onApply() {
  const intent = selectedIntent.value;
  if (!intent) return;
  emit('apply', buildSecEventFilterBuilderResult(intent, fieldValues));
  open.value = false;
}

function applyIntentQuick(intent: SecEventFilterIntent) {
  selectedIntentId.value = intent.id;
  resetForIntent(intent);
  emit('apply', buildSecEventFilterBuilderResult(intent, emptyFieldValues(intent)));
  open.value = false;
}

function onCancel() {
  open.value = false;
}
</script>

<template>
  <v-dialog v-model="open" max-width="720" scrollable>
    <v-card>
      <v-card-title class="d-flex align-center ga-2">
        <v-icon icon="mdi-filter-plus" />
        {{ t('siemCenter.events.filterBuilder.title') }}
      </v-card-title>
      <v-card-subtitle>
        {{ t('siemCenter.events.filterBuilder.subtitle') }}
      </v-card-subtitle>

      <v-card-text>
        <div class="text-caption text-medium-emphasis mb-2">
          {{ t('siemCenter.events.filterBuilder.stepIntent') }}
        </div>
        <v-row dense class="mb-4">
          <v-col
            v-for="intent in SEC_EVENT_FILTER_INTENTS"
            :key="intent.id"
            cols="12"
            sm="6"
          >
            <v-card
              :variant="selectedIntentId === intent.id ? 'tonal' : 'outlined'"
              :color="selectedIntentId === intent.id ? (intent.color || 'primary') : undefined"
              class="pa-3 h-100"
            >
              <div class="d-flex align-start ga-2" style="cursor: pointer" @click="selectedIntentId = intent.id">
                <v-icon :icon="intent.icon" class="mt-1" />
                <div class="flex-grow-1">
                  <div class="text-body-2 font-weight-bold">{{ t(intent.titleKey) }}</div>
                  <div class="text-caption text-medium-emphasis">{{ t(intent.descKey) }}</div>
                </div>
              </div>
              <v-btn
                class="mt-2"
                size="small"
                color="primary"
                variant="tonal"
                block
                @click.stop="applyIntentQuick(intent)"
              >
                {{ t('siemCenter.events.filterBuilder.applyIntent') }}
              </v-btn>
            </v-card>
          </v-col>
        </v-row>

        <template v-if="selectedIntent">
          <div class="text-caption text-medium-emphasis mb-2">
            {{ t('siemCenter.events.filterBuilder.stepFields') }}
          </div>
          <v-alert type="info" variant="tonal" density="compact" class="mb-3">
            {{ t('siemCenter.events.filterBuilder.anyHint') }}
          </v-alert>
          <v-row dense>
            <v-col
              v-for="field in selectedIntent.fields"
              :key="field.mapTo"
              cols="12"
              sm="6"
            >
              <v-select
                v-if="field.input === 'select'"
                v-model="fieldValues[field.mapTo]"
                :items="selectItemsForField(selectedIntent, field)"
                item-title="title"
                item-value="value"
                :label="t(field.labelKey)"
                :hint="field.hintKey ? t(field.hintKey) : undefined"
                :persistent-hint="!!field.hintKey"
                variant="outlined"
                density="compact"
                clearable
              />
              <v-text-field
                v-else
                v-model="fieldValues[field.mapTo]"
                :label="t(field.labelKey)"
                :placeholder="field.placeholderKey ? t(field.placeholderKey) : undefined"
                :hint="field.hintKey ? t(field.hintKey) : (field.catalogField ? field.catalogField : undefined)"
                :persistent-hint="!!(field.hintKey || field.catalogField)"
                variant="outlined"
                density="compact"
                clearable
                hide-details="auto"
              />
            </v-col>
          </v-row>
        </template>
      </v-card-text>

      <v-card-actions>
        <v-spacer />
        <v-btn variant="text" @click="onCancel">{{ t('siemCenter.events.filterBuilder.cancel') }}</v-btn>
        <v-btn color="primary" prepend-icon="mdi-check" @click="onApply">
          {{ t('siemCenter.events.filterBuilder.apply') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.cursor-pointer {
  cursor: pointer;
}
</style>
