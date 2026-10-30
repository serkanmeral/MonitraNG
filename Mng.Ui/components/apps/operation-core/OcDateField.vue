<script setup lang="ts">
import { computed, ref } from 'vue';

const model = defineModel<unknown>({ required: true });

const props = withDefaults(
  defineProps<{
    label?: string;
    required?: boolean;
    readonly?: boolean;
    error?: boolean;
    errorMessages?: string | readonly string[] | null;
    /** datetime alanları günü takvimden, saati ayrıca alır. */
    withTime?: boolean;
    density?: 'default' | 'comfortable' | 'compact';
    variant?: 'outlined' | 'plain' | 'underlined' | 'solo' | 'solo-inverted' | 'solo-filled';
    hideDetails?: boolean | 'auto';
  }>(),
  {
    label: '',
    required: false,
    readonly: false,
    error: false,
    errorMessages: null,
    withTime: false,
    density: 'comfortable',
    variant: 'outlined',
    hideDetails: 'auto',
  }
);

const open = ref(false);

function dayPart(value: unknown): string {
  const text = String(value ?? '').trim();
  const match = text.match(/^(\d{4}-\d{2}-\d{2})/);
  return match?.[1] ?? '';
}

function timePart(value: unknown): string {
  const text = String(value ?? '').trim();
  const match = text.match(/T(\d{2}:\d{2})/);
  return match?.[1] ?? '';
}

function formatDay(day: string): string {
  const [year, month, date] = day.split('-');
  if (!year || !month || !date) return '';
  return `${date}.${month}.${year}`;
}

const display = computed(() => {
  const day = dayPart(model.value);
  if (!day) return '';
  const dateText = formatDay(day);
  if (!props.withTime) return dateText;
  const time = timePart(model.value);
  return time ? `${dateText} ${time}` : dateText;
});

const pickerModel = computed(() => {
  const day = dayPart(model.value);
  return day ? new Date(`${day}T00:00:00`) : undefined;
});

function toDay(value: unknown): string {
  if (value instanceof Date && !Number.isNaN(value.getTime())) {
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const date = String(value.getDate()).padStart(2, '0');
    return `${value.getFullYear()}-${month}-${date}`;
  }
  return dayPart(value);
}

function write(day: string, time: string) {
  if (!day) {
    model.value = null;
    return;
  }
  model.value = props.withTime ? `${day}T${time || '00:00'}` : day;
}

function onPick(value: unknown) {
  write(toDay(value), timePart(model.value));
  if (!props.withTime) open.value = false;
}

function onTime(value: string | null) {
  const day = dayPart(model.value);
  if (!day) return;
  write(day, value ?? '');
}

function clear() {
  model.value = null;
  open.value = false;
}
</script>

<template>
  <div class="d-flex ga-2 align-start">
    <v-menu
      v-model="open"
      :close-on-content-click="false"
      :disabled="readonly"
      location="bottom start"
      :z-index="3200"
    >
      <template #activator="{ props: menuProps }">
        <v-text-field
          v-bind="readonly ? {} : menuProps"
          :model-value="display"
          readonly
          :clearable="!readonly && !!dayPart(model)"
          :error="error"
          :error-messages="errorMessages"
          :density="density"
          :variant="variant"
          :hide-details="hideDetails"
          class="flex-grow-1"
          prepend-inner-icon="mdi-calendar"
          @click:clear="clear"
        >
          <template v-if="label" #label>
            <span>{{ label }}</span>
            <span v-if="required" class="oc-field-required" aria-hidden="true"> *</span>
          </template>
        </v-text-field>
      </template>
      <v-date-picker
        :model-value="pickerModel"
        color="primary"
        show-adjacent-months
        hide-header
        @update:model-value="onPick"
      />
    </v-menu>
    <v-text-field
      v-if="withTime"
      :model-value="timePart(model)"
      type="time"
      :readonly="readonly"
      :disabled="!dayPart(model)"
      :density="density"
      :variant="variant"
      :hide-details="hideDetails"
      class="oc-date-field__time"
      @update:model-value="onTime"
    />
  </div>
</template>

<style scoped>
.oc-field-required {
  color: rgb(var(--v-theme-error));
  font-weight: 600;
}

.oc-date-field__time {
  max-width: 140px;
}
</style>
