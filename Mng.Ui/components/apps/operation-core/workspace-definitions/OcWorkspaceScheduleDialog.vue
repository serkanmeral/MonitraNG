<script setup lang="ts">
import { ref, computed, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { useKeeperUserPicker } from '@/composables/useKeeperUserPicker';
import MngDirectoryPickerField from '@/components/shared/directory/MngDirectoryPickerField.vue';
import OcScheduleTimingWizard from '@/components/apps/operation-core/workspace-definitions/OcScheduleTimingWizard.vue';
import {
  buildMultiWeeklyQuartzCron,
  formatScheduleHumanSummary,
  getQuartzCronValidationIssue,
  OC_SCHEDULE_WEEKDAY_KEYS,
  type OcScheduleWeekdayKey,
} from '@/utils/ocScheduleCron';

export type OcScheduleFormModel = {
  name: string;
  description: string;
  isActive: boolean;
  cronExpression: string;
  timezone: string;
  boardId: string;
  typeId: string;
  assignee: string | null;
  priorityId: string;
  title: string;
  templateDescription: string;
};

const props = defineProps<{
  modelValue: boolean;
  editId: string | null;
  workspaceId: string;
  boardItems: { value: string; title: string }[];
  typeItems: { value: string; title: string }[];
  priorityItems: { value: string; title: string }[];
  boardNameById: Map<string, string>;
  typeNameById: Map<string, string>;
  saving?: boolean;
}>();

const emit = defineEmits<{
  'update:modelValue': [boolean];
  save: [OcScheduleFormModel];
}>();

const { t } = useAppI18n();
const userPicker = useKeeperUserPicker();

const timingWizardRef = ref<InstanceType<typeof OcScheduleTimingWizard> | null>(null);
const validationAlertRef = ref<HTMLElement | null>(null);
const validationError = ref<string | null>(null);

const form = ref<OcScheduleFormModel>(emptyForm());

function emptyForm(): OcScheduleFormModel {
  return {
    name: '',
    description: '',
    isActive: true,
    cronExpression: buildMultiWeeklyQuartzCron(['mon'], 9, 0),
    timezone: 'Europe/Istanbul',
    boardId: '',
    typeId: '',
    assignee: null,
    priorityId: '',
    title: '',
    templateDescription: '',
  };
}

const weekdayLabels = computed(() =>
  Object.fromEntries(
    OC_SCHEDULE_WEEKDAY_KEYS.map((key) => [
      key,
      t(`operationCore.workspaceDefinitions.scheduled.weekday.${key}`),
    ])
  ) as Record<OcScheduleWeekdayKey, string>
);

const dialogOpen = computed({
  get: () => props.modelValue,
  set: (v: boolean) => emit('update:modelValue', v),
});

const whenSummary = computed(() =>
  formatScheduleHumanSummary(
    form.value.cronExpression,
    form.value.timezone,
    weekdayLabels.value,
    (key, params) => t(key, params ?? {})
  )
);

const assigneePreview = computed(() => {
  if (!form.value.assignee) return t('operationCore.workspaceDefinitions.scheduled.livePreviewAssigneeEmpty');
  return userPicker.labelFor(form.value.assignee) || form.value.assignee;
});

const livePreviewLines = computed(() => [
  {
    icon: 'mdi-calendar-clock',
    text: whenSummary.value,
  },
  {
    icon: 'mdi-view-dashboard-outline',
    text: t('operationCore.workspaceDefinitions.scheduled.livePreviewBoard', {
      board:
        props.boardNameById.get(form.value.boardId) ??
        t('operationCore.workspaceDefinitions.scheduled.livePreviewPlaceholder'),
    }),
  },
  {
    icon: 'mdi-shape-outline',
    text: t('operationCore.workspaceDefinitions.scheduled.livePreviewType', {
      type:
        props.typeNameById.get(form.value.typeId) ??
        t('operationCore.workspaceDefinitions.scheduled.livePreviewPlaceholder'),
    }),
  },
  {
    icon: 'mdi-file-document-outline',
    text: t('operationCore.workspaceDefinitions.scheduled.livePreviewTitle', {
      title:
        form.value.title.trim() ||
        t('operationCore.workspaceDefinitions.scheduled.livePreviewPlaceholder'),
    }),
  },
  {
    icon: 'mdi-account-outline',
    text: t('operationCore.workspaceDefinitions.scheduled.livePreviewAssignee', {
      assignee: assigneePreview.value,
    }),
  },
]);

watch(
  () => props.modelValue,
  (open) => {
    if (open && form.value.assignee) {
      void userPicker.ensureSelectedLabels([form.value.assignee]);
    }
  }
);

watch(
  () => form.value.assignee,
  (id) => {
    if (id) void userPicker.ensureSelectedLabels([id]);
  }
);

function setForm(next: OcScheduleFormModel) {
  form.value = { ...next };
  if (next.assignee) void userPicker.ensureSelectedLabels([next.assignee]);
}

function resetForm(defaults?: Partial<OcScheduleFormModel>) {
  form.value = { ...emptyForm(), ...defaults };
  validationError.value = null;
}

function validate(): string | null {
  if (!form.value.name.trim()) return t('operationCore.workspaceDefinitions.scheduled.validationName');
  if (!form.value.boardId) return t('operationCore.workspaceDefinitions.scheduled.validationBoard');
  if (!form.value.typeId) return t('operationCore.workspaceDefinitions.scheduled.validationType');
  if (!form.value.assignee) return t('operationCore.workspaceDefinitions.scheduled.validationAssignee');
  if (!form.value.title.trim()) return t('operationCore.workspaceDefinitions.scheduled.validationTitle');

  const timingErr = timingWizardRef.value?.validate() ?? null;
  if (timingErr) return timingErr;

  const cronIssue = getQuartzCronValidationIssue(form.value.cronExpression);
  if (cronIssue === 'empty') {
    return t('operationCore.workspaceDefinitions.scheduled.validationCronEmpty');
  }
  if (cronIssue === 'tooFew') {
    return t('operationCore.workspaceDefinitions.scheduled.validationCronTooFew');
  }
  if (cronIssue === 'tooMany') {
    return t('operationCore.workspaceDefinitions.scheduled.validationCronTooMany');
  }
  return null;
}

function scrollValidationIntoView() {
  requestAnimationFrame(() => {
    validationAlertRef.value?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
  });
}

function submit() {
  validationError.value = validate();
  if (validationError.value) {
    scrollValidationIntoView();
    return;
  }
  emit('save', { ...form.value });
}

defineExpose({ setForm, resetForm, validationError });
</script>

<template>
  <v-dialog v-model="dialogOpen" max-width="920" scrollable>
    <v-card rounded="lg" class="oc-schedule-dialog">
      <v-card-title class="d-flex align-start gap-3 pt-5 px-5 pb-2">
        <v-avatar color="primary" variant="tonal" size="44" rounded="lg">
          <v-icon icon="mdi-calendar-clock" size="24" />
        </v-avatar>
        <div class="flex-grow-1 min-width-0">
          <div class="text-h6 font-weight-bold">
            {{
              editId
                ? t('operationCore.workspaceDefinitions.scheduled.editSchedule')
                : t('operationCore.workspaceDefinitions.scheduled.addSchedule')
            }}
          </div>
          <p class="text-body-2 text-medium-emphasis mb-0 mt-1">
            {{ t('operationCore.workspaceDefinitions.scheduled.dialogIntro') }}
          </p>
        </div>
      </v-card-title>

      <v-divider />

      <v-card-text class="px-5 py-4">
        <div v-if="validationError" ref="validationAlertRef">
          <v-alert
            type="warning"
            variant="tonal"
            density="compact"
            class="mb-4 rounded-lg"
            closable
            @click:close="validationError = null"
          >
            {{ validationError }}
          </v-alert>
        </div>

        <v-row dense>
          <v-col cols="12" lg="7">
            <section class="oc-schedule-dialog__section mb-4">
              <div class="oc-schedule-dialog__section-head mb-3">
                <span class="oc-schedule-dialog__step">1</span>
                <div>
                  <div class="text-subtitle-1 font-weight-bold">
                    {{ t('operationCore.workspaceDefinitions.scheduled.sectionGeneral') }}
                  </div>
                  <p class="text-body-2 text-medium-emphasis mb-0">
                    {{ t('operationCore.workspaceDefinitions.scheduled.sectionGeneralHint') }}
                  </p>
                </div>
              </div>
              <v-text-field
                v-model="form.name"
                :label="t('operationCore.workspaceDefinitions.scheduled.fieldName')"
                :placeholder="t('operationCore.workspaceDefinitions.scheduled.fieldNamePlaceholder')"
                variant="outlined"
                density="comfortable"
                class="mb-3"
              />
              <v-textarea
                v-model="form.description"
                :label="t('operationCore.workspaceDefinitions.scheduled.fieldDescription')"
                :placeholder="t('operationCore.workspaceDefinitions.scheduled.fieldDescriptionPlaceholder')"
                variant="outlined"
                density="comfortable"
                rows="2"
                auto-grow
                class="mb-3"
              />
              <v-switch v-model="form.isActive" color="primary" hide-details class="mt-1">
                <template #label>
                  <div>
                    <div class="text-body-2 font-weight-medium">
                      {{ t('operationCore.workspaceDefinitions.scheduled.fieldActive') }}
                    </div>
                    <div class="text-caption text-medium-emphasis">
                      {{ t('operationCore.workspaceDefinitions.scheduled.fieldActiveHint') }}
                    </div>
                  </div>
                </template>
              </v-switch>
            </section>

            <section class="oc-schedule-dialog__section mb-4">
              <div class="oc-schedule-dialog__section-head mb-3">
                <span class="oc-schedule-dialog__step">2</span>
                <div>
                  <div class="text-subtitle-1 font-weight-bold">
                    {{ t('operationCore.workspaceDefinitions.scheduled.sectionTiming') }}
                  </div>
                  <p class="text-body-2 text-medium-emphasis mb-0">
                    {{ t('operationCore.workspaceDefinitions.scheduled.sectionTimingHint') }}
                  </p>
                </div>
              </div>

              <OcScheduleTimingWizard
                ref="timingWizardRef"
                v-model:cron-expression="form.cronExpression"
                v-model:timezone="form.timezone"
              />
            </section>

            <section class="oc-schedule-dialog__section mb-4">
              <div class="oc-schedule-dialog__section-head mb-3">
                <span class="oc-schedule-dialog__step">3</span>
                <div>
                  <div class="text-subtitle-1 font-weight-bold">
                    {{ t('operationCore.workspaceDefinitions.scheduled.sectionTarget') }}
                  </div>
                  <p class="text-body-2 text-medium-emphasis mb-0">
                    {{ t('operationCore.workspaceDefinitions.scheduled.sectionTargetHint') }}
                  </p>
                </div>
              </div>
              <v-select
                v-model="form.boardId"
                :items="boardItems"
                item-title="title"
                item-value="value"
                :label="t('operationCore.workspaceDefinitions.scheduled.fieldBoard')"
                :placeholder="t('operationCore.workspaceDefinitions.scheduled.fieldBoardPlaceholder')"
                variant="outlined"
                density="comfortable"
                class="mb-3"
              />
              <v-select
                v-model="form.typeId"
                :items="typeItems"
                item-title="title"
                item-value="value"
                :label="t('operationCore.workspaceDefinitions.scheduled.fieldType')"
                :placeholder="t('operationCore.workspaceDefinitions.scheduled.fieldTypePlaceholder')"
                variant="outlined"
                density="comfortable"
              />
            </section>

            <section class="oc-schedule-dialog__section">
              <div class="oc-schedule-dialog__section-head mb-3">
                <span class="oc-schedule-dialog__step">4</span>
                <div>
                  <div class="text-subtitle-1 font-weight-bold">
                    {{ t('operationCore.workspaceDefinitions.scheduled.sectionTemplate') }}
                  </div>
                  <p class="text-body-2 text-medium-emphasis mb-0">
                    {{ t('operationCore.workspaceDefinitions.scheduled.sectionTemplateHint') }}
                  </p>
                </div>
              </div>
              <v-text-field
                v-model="form.title"
                :label="t('operationCore.workspaceDefinitions.scheduled.fieldTitle')"
                :placeholder="t('operationCore.workspaceDefinitions.scheduled.fieldTitlePlaceholder')"
                variant="outlined"
                density="comfortable"
                class="mb-3"
              />
              <v-textarea
                v-model="form.templateDescription"
                :label="t('operationCore.workspaceDefinitions.scheduled.fieldWorkItemDescription')"
                :placeholder="t('operationCore.workspaceDefinitions.scheduled.fieldWorkItemDescriptionPlaceholder')"
                variant="outlined"
                density="comfortable"
                rows="2"
                auto-grow
                class="mb-3"
              />
              <MngDirectoryPickerField
                v-model="form.assignee"
                entity="user"
                :label="t('operationCore.workspaceDefinitions.scheduled.fieldAssignee')"
                show-required-mark
                class="mb-3"
              />
              <v-select
                v-model="form.priorityId"
                :items="priorityItems"
                item-title="title"
                item-value="value"
                :label="t('operationCore.workspaceDefinitions.scheduled.fieldPriority')"
                variant="outlined"
                density="comfortable"
              />
            </section>
          </v-col>

          <v-col cols="12" lg="5">
            <v-card variant="tonal" color="primary" rounded="lg" class="oc-schedule-dialog__preview sticky-preview">
              <v-card-title class="text-subtitle-1 font-weight-bold d-flex align-center gap-2 py-4">
                <v-icon icon="mdi-eye-outline" size="20" />
                {{ t('operationCore.workspaceDefinitions.scheduled.livePreviewCardTitle') }}
              </v-card-title>
              <v-divider />
              <v-card-text class="pt-4">
                <p class="text-body-2 mb-4">
                  {{ t('operationCore.workspaceDefinitions.scheduled.livePreviewCardIntro') }}
                </p>
                <div class="d-flex flex-column ga-3">
                  <div
                    v-for="(line, idx) in livePreviewLines"
                    :key="idx"
                    class="d-flex align-start gap-3 oc-schedule-dialog__preview-line"
                  >
                    <v-icon :icon="line.icon" size="20" class="mt-1 flex-shrink-0" />
                    <span class="text-body-2">{{ line.text }}</span>
                  </div>
                </div>
                <v-divider class="my-4" />
                <p class="text-caption text-medium-emphasis mb-0">
                  {{ t('operationCore.workspaceDefinitions.scheduled.livePreviewFootnote') }}
                </p>
              </v-card-text>
            </v-card>
          </v-col>
        </v-row>
      </v-card-text>

      <v-divider />

      <v-card-actions class="pa-4 px-5">
        <v-spacer />
        <v-btn variant="text" class="text-none" @click="dialogOpen = false">
          {{ t('operationCore.definitions.cancel') }}
        </v-btn>
        <v-btn color="primary" rounded="lg" class="text-none px-5" :loading="saving" @click="submit">
          {{ t('operationCore.workspaceDefinitions.scheduled.saveSchedule') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.oc-schedule-dialog__section {
  padding-bottom: 0.25rem;
}

.oc-schedule-dialog__section-head {
  display: flex;
  align-items: flex-start;
  gap: 12px;
}

.oc-schedule-dialog__step {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border-radius: 999px;
  font-size: 0.8125rem;
  font-weight: 700;
  flex-shrink: 0;
  background: rgba(var(--v-theme-primary), 0.12);
  color: rgb(var(--v-theme-primary));
}

.oc-schedule-dialog__preview-line {
  line-height: 1.45;
}

@media (min-width: 1280px) {
  .sticky-preview {
    position: sticky;
    top: 12px;
  }
}
</style>
