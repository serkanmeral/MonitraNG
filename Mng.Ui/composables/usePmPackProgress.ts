import { nextTick, ref } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import type { PmPackProgressStep } from '@/components/apps/project-management/PmPackApplyProgressDialog.vue';
import type { PmJobPack } from '@/types/apps/projectManagement';
import {
  packDiagramOf,
  packFolderNames,
  type PmPackDocProgress,
} from '@/utils/pmJobPack';

export type PmPackProgressMode = 'apply' | 'detach';

export function usePmPackProgress() {
  const { t } = useAppI18n();
  const open = ref(false);
  const running = ref(false);
  const failed = ref(false);
  const packName = ref('');
  const mode = ref<PmPackProgressMode>('apply');
  const steps = ref<PmPackProgressStep[]>([]);

  function setStep(id: string, status: PmPackProgressStep['status'], detail?: string) {
    steps.value = steps.value.map((step) =>
      step.id === id ? { ...step, status, detail: detail ?? step.detail } : step,
    );
  }

  function markPending(status: PmPackProgressStep['status']) {
    steps.value = steps.value.map((step) => (step.status === 'pending' ? { ...step, status } : step));
  }

  async function beginApply(pack: PmJobPack) {
    const diagram = packDiagramOf(pack);
    mode.value = 'apply';
    packName.value = pack.name;
    failed.value = false;
    running.value = true;
    steps.value = [
      { id: 'runtime', label: t('projectManagement.packCatalog.progressRuntime'), status: 'pending' },
      { id: 'hub', label: t('projectManagement.packCatalog.progressHub'), status: 'pending' },
      ...packFolderNames(pack).map((name) => ({
        id: `folder:${name}`,
        label: t('projectManagement.packCatalog.progressFolder', { name }),
        status: 'pending' as const,
      })),
      ...(pack.starters || []).map((starter) => ({
        id: `starter:${starter.title}`,
        label: t('projectManagement.packCatalog.progressStarter', { name: starter.title }),
        status: 'pending' as const,
      })),
      ...(diagram
        ? [
            {
              id: `diagram:${diagram.title}`,
              label: t('projectManagement.packCatalog.progressDiagram', { name: diagram.title }),
              status: 'pending' as const,
            },
          ]
        : []),
      { id: 'finish', label: t('projectManagement.packCatalog.progressFinish'), status: 'pending' },
    ];
    open.value = true;
    await nextTick();
  }

  async function beginDetach(pack: PmJobPack, folderNames: string[]) {
    mode.value = 'detach';
    packName.value = pack.name;
    failed.value = false;
    running.value = true;
    steps.value = [
      { id: 'runtime', label: t('projectManagement.packCatalog.progressRuntimeDetach'), status: 'pending' },
      ...folderNames.map((name) => ({
        id: `folder:${name}`,
        label: t('projectManagement.packCatalog.progressFolder', { name }),
        status: 'pending' as const,
      })),
    ];
    open.value = true;
    await nextTick();
  }

  function onDocProgress(event: PmPackDocProgress) {
    const named = event.phase === 'folder' || event.phase === 'starter' || event.phase === 'diagram';
    const id = named ? `${event.phase}:${event.name || ''}` : event.phase;
    if (event.status === 'start') setStep(id, 'running');
    else if (event.status === 'skip') setStep(id, 'skip', t('projectManagement.packCatalog.progressSkipped'));
    else setStep(id, 'done');
  }

  function succeed() {
    running.value = false;
  }

  function fail(stepId?: string) {
    failed.value = true;
    running.value = false;
    if (stepId) setStep(stepId, 'error');
    else {
      const runningStep = steps.value.find((step) => step.status === 'running');
      if (runningStep) setStep(runningStep.id, 'error');
    }
    markPending('skip');
  }

  function onToggle(nextOpen: boolean) {
    if (!nextOpen && !running.value) open.value = false;
  }

  return {
    open,
    running,
    failed,
    packName,
    mode,
    steps,
    setStep,
    markPending,
    beginApply,
    beginDetach,
    onDocProgress,
    succeed,
    fail,
    onToggle,
  };
}
