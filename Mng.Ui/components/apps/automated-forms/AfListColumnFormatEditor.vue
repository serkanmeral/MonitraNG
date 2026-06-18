<script setup lang="ts">
import { computed } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { AfListColumnFormat, AfListColumnFormatType } from '@/utils/afListColumnFormat';

const props = defineProps<{
  modelValue: AfListColumnFormat;
  conditionFields: { value: string; title: string }[];
  /** Varsayılan koşul alanı (genelde sütunun fieldName). */
  defaultConditionField?: string;
  /** Sütun alan adı — uygun format türlerini filtreler. */
  fieldName?: string;
  /** i18n kök anahtarı (varsayılan: automated-forms format editörü). */
  i18nPrefix?: string;
}>();

const emit = defineEmits<{
  'update:modelValue': [value: AfListColumnFormat];
}>();

const { t } = useAppI18n();

const i18nRoot = computed(
  () => props.i18nPrefix ?? 'automated-forms.form.listConfig.modal.formatting'
);

function ft(key: string): string {
  return t(`${i18nRoot.value}.${key}`);
}

const format = computed({
  get: () => props.modelValue,
  set: (value: AfListColumnFormat) => emit('update:modelValue', value),
});

const allFormatTypeOptions = computed(() => [
  { title: ft('types.none'), value: 'none' as const },
  { title: ft('types.regex'), value: 'regex' as const },
  { title: ft('types.number'), value: 'number' as const },
  { title: ft('types.date'), value: 'date' as const },
  { title: ft('types.currency'), value: 'currency' as const },
  {
    title: ft('types.textTransform'),
    value: 'text-transform' as const,
  },
  { title: ft('types.color'), value: 'color' as const },
  {
    title: ft('types.conditionalColor'),
    value: 'conditional-color' as const,
  },
]);

const formatTypeOptions = computed(() => {
  const field = props.fieldName ?? '';
  const numberFields = new Set(['partCount', 'stockCount', 'lineCount', 'shippedCount']);
  const dateFields = new Set(['beginDate', 'deliveryDate', 'closedAt']);
  let allowed: AfListColumnFormatType[] | null = null;
  if (dateFields.has(field)) {
    allowed = ['none', 'date', 'color', 'conditional-color'];
  } else if (numberFields.has(field)) {
    allowed = ['none', 'number', 'currency', 'color', 'conditional-color'];
  }
  if (!allowed) return allFormatTypeOptions.value;
  const set = new Set(allowed);
  return allFormatTypeOptions.value.filter((o) => set.has(o.value));
});

const colorOptions = computed(() => [
  { title: ft('colors.primary'), value: 'primary' },
  { title: ft('colors.secondary'), value: 'secondary' },
  { title: ft('colors.success'), value: 'success' },
  { title: ft('colors.error'), value: 'error' },
  { title: ft('colors.warning'), value: 'warning' },
  { title: ft('colors.info'), value: 'info' },
  { title: ft('colors.custom'), value: 'custom' },
]);

const operatorOptions = computed(() => [
  { title: ft('operators.eq'), value: 'eq' },
  { title: ft('operators.ne'), value: 'ne' },
  { title: ft('operators.gt'), value: 'gt' },
  { title: ft('operators.gte'), value: 'gte' },
  { title: ft('operators.lt'), value: 'lt' },
  { title: ft('operators.lte'), value: 'lte' },
  { title: ft('operators.contains'), value: 'contains' },
  { title: ft('operators.startsWith'), value: 'startsWith' },
  { title: ft('operators.endsWith'), value: 'endsWith' },
  { title: ft('operators.in'), value: 'in' },
  { title: ft('operators.notIn'), value: 'notIn' },
]);

const dateFormatOptions = [
  { title: 'DD/MM/YYYY', value: 'DD/MM/YYYY' },
  { title: 'MM/DD/YYYY', value: 'MM/DD/YYYY' },
  { title: 'YYYY-MM-DD', value: 'YYYY-MM-DD' },
  { title: 'DD.MM.YYYY', value: 'DD.MM.YYYY' },
  { title: 'DD-MM-YYYY', value: 'DD-MM-YYYY' },
  { title: 'DD MMM YYYY', value: 'DD MMM YYYY' },
  { title: 'DD MMMM YYYY', value: 'DD MMMM YYYY' },
];

const timeFormatOptions = computed(() => [
  { title: ft('timeFormats.hhmm'), value: 'HH:mm' },
  { title: ft('timeFormats.hhmmss'), value: 'HH:mm:ss' },
]);

const textTransformOptions = computed(() => [
  {
    title: ft('textTransformTypes.uppercase'),
    value: 'uppercase',
  },
  {
    title: ft('textTransformTypes.lowercase'),
    value: 'lowercase',
  },
  {
    title: ft('textTransformTypes.capitalize'),
    value: 'capitalize',
  },
]);

function ensureFormatType() {
  if (!format.value.type) format.value = { ...format.value, type: 'none' };
}

function addCondition() {
  ensureFormatType();
  if (!format.value.conditions) format.value.conditions = [];
  format.value.conditions.push({
    field: props.defaultConditionField ?? props.conditionFields[0]?.value ?? '',
    operator: 'eq',
    value: '',
    textColor: 'primary',
  });
}

function removeCondition(index: number) {
  format.value.conditions?.splice(index, 1);
}
</script>

<template>
  <div>
    <v-select
      v-model="format.type"
      :items="formatTypeOptions"
      item-title="title"
      item-value="value"
      :label="ft('type')"
      variant="outlined"
      prepend-inner-icon="mdi-format-text"
      hide-details
      class="mb-3"
    />

    <v-row v-if="format.type === 'regex'">
      <v-col cols="12">
        <v-text-field
          v-model="format.pattern"
          :label="ft('regexPattern')"
          :placeholder="ft('regexPatternPlaceholder')"
          variant="outlined"
          prepend-inner-icon="mdi-regex"
          hide-details
          class="mb-2"
        />
        <v-text-field
          v-model="format.replacement"
          :label="ft('regexReplacement')"
          :placeholder="ft('regexReplacementPlaceholder')"
          variant="outlined"
          prepend-inner-icon="mdi-arrow-right"
          hide-details
        />
      </v-col>
    </v-row>

    <v-row v-if="format.type === 'number'">
      <v-col cols="6">
        <v-text-field
          v-model.number="format.decimalPlaces"
          :label="ft('decimalPlaces')"
          type="number"
          min="0"
          max="10"
          variant="outlined"
          hide-details
        />
      </v-col>
      <v-col cols="6">
        <v-switch
          v-model="format.thousandSeparator"
          :label="ft('thousandSeparator')"
          color="primary"
          hide-details
        />
      </v-col>
    </v-row>

    <v-row v-if="format.type === 'currency'">
      <v-col cols="6">
        <v-text-field
          v-model="format.currencySymbol"
          :label="ft('currencySymbol')"
          :placeholder="ft('currencySymbolPlaceholder')"
          variant="outlined"
          prepend-inner-icon="mdi-currency-usd"
          hide-details
          class="mb-2"
        />
      </v-col>
      <v-col cols="6">
        <v-text-field
          v-model.number="format.decimalPlaces"
          :label="ft('decimalPlaces')"
          type="number"
          min="0"
          max="10"
          variant="outlined"
          hide-details
          class="mb-2"
        />
      </v-col>
      <v-col cols="12">
        <v-switch
          v-model="format.thousandSeparator"
          :label="ft('thousandSeparator')"
          color="primary"
          hide-details
        />
      </v-col>
    </v-row>

    <v-row v-if="format.type === 'date'">
      <v-col cols="12">
        <v-select
          v-model="format.dateFormat"
          :items="dateFormatOptions"
          item-title="title"
          item-value="value"
          :label="ft('dateFormat')"
          variant="outlined"
          prepend-inner-icon="mdi-calendar"
          hide-details
          class="mb-3"
        />
        <v-switch
          v-model="format.showTime"
          :label="ft('showTime')"
          color="primary"
          hide-details
          class="mb-3"
        />
        <v-select
          v-if="format.showTime"
          v-model="format.timeFormat"
          :items="timeFormatOptions"
          item-title="title"
          item-value="value"
          :label="ft('timeFormat')"
          variant="outlined"
          prepend-inner-icon="mdi-clock-outline"
          hide-details
        />
      </v-col>
    </v-row>

    <v-row v-if="format.type === 'text-transform'">
      <v-col cols="12">
        <v-select
          v-model="format.textTransform"
          :items="textTransformOptions"
          item-title="title"
          item-value="value"
          :label="ft('textTransform')"
          variant="outlined"
          prepend-inner-icon="mdi-format-letter-case"
          hide-details
        />
      </v-col>
    </v-row>

    <v-row v-if="format.type === 'color'">
      <v-col cols="6">
        <v-select
          v-model="format.textColor"
          :items="colorOptions"
          item-title="title"
          item-value="value"
          :label="ft('textColor')"
          variant="outlined"
          hide-details
          class="mb-3"
        />
        <v-text-field
          v-if="format.textColor === 'custom'"
          v-model="format.customTextColor"
          :label="ft('customTextColor')"
          variant="outlined"
          hide-details
        >
          <template #append>
            <input v-model="format.customTextColor" type="color" class="af-color-input" />
          </template>
        </v-text-field>
      </v-col>
      <v-col cols="6">
        <v-select
          v-model="format.backgroundColor"
          :items="colorOptions"
          item-title="title"
          item-value="value"
          :label="ft('backgroundColor')"
          variant="outlined"
          hide-details
          class="mb-3"
        />
        <v-text-field
          v-if="format.backgroundColor === 'custom'"
          v-model="format.customBackgroundColor"
          :label="ft('customBackgroundColor')"
          variant="outlined"
          hide-details
        >
          <template #append>
            <input v-model="format.customBackgroundColor" type="color" class="af-color-input" />
          </template>
        </v-text-field>
      </v-col>
    </v-row>

    <div v-if="format.type === 'conditional-color'">
      <div class="text-caption mb-2">
        {{ ft('conditions.title') }}
      </div>

      <div
        v-for="(condition, index) in format.conditions || []"
        :key="index"
        class="mb-3 pa-3 border rounded"
      >
        <v-row>
          <v-col cols="12" md="4">
            <v-select
              v-model="condition.field"
              :items="conditionFields"
              item-title="title"
              item-value="value"
              :label="ft('conditions.field')"
              variant="outlined"
              density="compact"
              hide-details
            />
          </v-col>
          <v-col cols="12" md="3">
            <v-select
              v-model="condition.operator"
              :items="operatorOptions"
              item-title="title"
              item-value="value"
              :label="ft('conditions.operator')"
              variant="outlined"
              density="compact"
              hide-details
            />
          </v-col>
          <v-col cols="12" md="3">
            <v-text-field
              v-model="condition.value"
              :label="ft('conditions.value')"
              variant="outlined"
              density="compact"
              hide-details
            />
          </v-col>
          <v-col cols="12" md="2" class="d-flex align-center">
            <v-btn icon="mdi-delete" variant="text" size="small" color="error" @click="removeCondition(index)" />
          </v-col>
          <v-col cols="12" md="6">
            <v-select
              v-model="condition.textColor"
              :items="colorOptions"
              item-title="title"
              item-value="value"
              :label="ft('textColor')"
              variant="outlined"
              density="compact"
              hide-details
              class="mb-2"
            />
            <v-text-field
              v-if="condition.textColor === 'custom'"
              v-model="condition.customTextColor"
              :label="ft('customTextColor')"
              variant="outlined"
              density="compact"
              hide-details
            >
              <template #append>
                <input v-model="condition.customTextColor" type="color" class="af-color-input af-color-input--sm" />
              </template>
            </v-text-field>
          </v-col>
          <v-col cols="12" md="6">
            <v-select
              v-model="condition.backgroundColor"
              :items="colorOptions"
              item-title="title"
              item-value="value"
              :label="ft('backgroundColor')"
              variant="outlined"
              density="compact"
              hide-details
              class="mb-2"
            />
            <v-text-field
              v-if="condition.backgroundColor === 'custom'"
              v-model="condition.customBackgroundColor"
              :label="ft('customBackgroundColor')"
              variant="outlined"
              density="compact"
              hide-details
            >
              <template #append>
                <input
                  v-model="condition.customBackgroundColor"
                  type="color"
                  class="af-color-input af-color-input--sm"
                />
              </template>
            </v-text-field>
          </v-col>
        </v-row>
      </div>

      <v-btn variant="outlined" size="small" color="primary" class="mb-3" @click="addCondition">
        <v-icon class="mr-2" size="small">mdi-plus</v-icon>
        {{ ft('conditions.add') }}
      </v-btn>

      <v-divider class="my-3" />
      <div class="text-caption mb-2">
        {{ ft('conditions.default') }}
      </div>
      <v-row>
        <v-col cols="6">
          <v-select
            v-model="format.defaultTextColor"
            :items="colorOptions"
            item-title="title"
            item-value="value"
            :label="ft('defaultTextColor')"
            variant="outlined"
            hide-details
            class="mb-2"
          />
          <v-text-field
            v-if="format.defaultTextColor === 'custom'"
            v-model="format.customDefaultTextColor"
            :label="ft('customTextColor')"
            variant="outlined"
            hide-details
          >
            <template #append>
              <input v-model="format.customDefaultTextColor" type="color" class="af-color-input af-color-input--sm" />
            </template>
          </v-text-field>
        </v-col>
        <v-col cols="6">
          <v-select
            v-model="format.defaultBackgroundColor"
            :items="colorOptions"
            item-title="title"
            item-value="value"
            :label="ft('defaultBackgroundColor')"
            variant="outlined"
            hide-details
            class="mb-2"
          />
          <v-text-field
            v-if="format.defaultBackgroundColor === 'custom'"
            v-model="format.customDefaultBackgroundColor"
            :label="ft('customBackgroundColor')"
            variant="outlined"
            hide-details
          >
            <template #append>
              <input
                v-model="format.customDefaultBackgroundColor"
                type="color"
                class="af-color-input af-color-input--sm"
              />
            </template>
          </v-text-field>
        </v-col>
      </v-row>
    </div>
  </div>
</template>

<style scoped>
.af-color-input {
  width: 40px;
  height: 40px;
  border: none;
  cursor: pointer;
  background: transparent;
}

.af-color-input--sm {
  width: 30px;
  height: 30px;
}
</style>
