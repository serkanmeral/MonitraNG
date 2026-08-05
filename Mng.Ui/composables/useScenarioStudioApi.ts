import { readonly, ref } from 'vue';
import { useAuthStore } from '@/stores/auth';
import type {
  CreateScenarioDraftRequest,
  ScenarioAuditEntry,
  ScenarioCatalogItem,
  ScenarioPreviewRequest,
  ScenarioPreviewResponse,
  ScenarioValidationSnapshot,
  ScenarioVersion,
  UpdateScenarioDraftRequest,
} from '@/types/apps/scenario';

const baseUrl = '/api/alarm/v1/scenarios';

export function useScenarioStudioApi() {
  const auth = useAuthStore();
  const pending = ref(false);
  const error = ref<string | null>(null);

  function headers(): Record<string, string> {
    return auth.domainName ? { 'X-Domain-Name': auth.domainName } : {};
  }

  async function execute<T>(request: () => Promise<T>): Promise<T> {
    pending.value = true;
    error.value = null;
    try {
      return await request();
    } catch (cause: any) {
      error.value =
        cause?.data?.message
        || cause?.data?.statusMessage
        || cause?.statusMessage
        || cause?.message
        || 'Scenario API request failed.';
      throw cause;
    } finally {
      pending.value = false;
    }
  }

  const scenarioPath = (scenarioId: string) =>
    `${baseUrl}/${encodeURIComponent(scenarioId)}`;
  const versionPath = (scenarioId: string, version: number) =>
    `${scenarioPath(scenarioId)}/versions/${version}`;

  return {
    pending: readonly(pending),
    error: readonly(error),
    clearError: () => { error.value = null; },

    listScenarios: (includeDrafts = true) =>
      execute(() => $fetch<ScenarioCatalogItem[]>(baseUrl, {
        method: 'GET',
        headers: headers(),
        query: { includeDrafts },
      })),

    createDraft: (body: CreateScenarioDraftRequest) =>
      execute(() => $fetch<ScenarioVersion>(`${baseUrl}/drafts`, {
        method: 'POST',
        headers: headers(),
        body,
      })),

    createNextDraft: (scenarioId: string, body?: CreateScenarioDraftRequest) =>
      execute(() => $fetch<ScenarioVersion>(`${scenarioPath(scenarioId)}/drafts`, {
        method: 'POST',
        headers: headers(),
        body,
      })),

    cloneTemplateToDraft: (scenarioId: string, version: number) =>
      execute(() => $fetch<ScenarioVersion>(
        `${versionPath(scenarioId, version)}/clone-to-draft`,
        { method: 'POST', headers: headers() },
      )),

    getScenario: (scenarioId: string, version?: number) =>
      execute(() => $fetch<ScenarioVersion>(scenarioPath(scenarioId), {
        method: 'GET',
        headers: headers(),
        query: version == null ? undefined : { version },
      })),

    updateDraft: (scenarioId: string, version: number, body: UpdateScenarioDraftRequest) =>
      execute(() => $fetch<ScenarioVersion>(`${versionPath(scenarioId, version)}/draft`, {
        method: 'PUT',
        headers: headers(),
        body,
      })),

    validate: (scenarioId: string, version: number) =>
      execute(() => $fetch<ScenarioValidationSnapshot>(
        `${versionPath(scenarioId, version)}/validate`,
        { method: 'POST', headers: headers() },
      )),

    publish: (scenarioId: string, version: number) =>
      execute(() => $fetch<ScenarioVersion>(
        `${versionPath(scenarioId, version)}/publish`,
        { method: 'POST', headers: headers() },
      )),

    rollback: (scenarioId: string, version: number) =>
      execute(() => $fetch<ScenarioVersion>(
        `${versionPath(scenarioId, version)}/rollback`,
        { method: 'POST', headers: headers() },
      )),

    archive: (scenarioId: string, version: number) =>
      execute(() => $fetch<ScenarioVersion>(
        `${versionPath(scenarioId, version)}/archive`,
        { method: 'POST', headers: headers() },
      )),

    audit: (scenarioId: string) =>
      execute(() => $fetch<ScenarioAuditEntry[]>(`${scenarioPath(scenarioId)}/audit`, {
        method: 'GET',
        headers: headers(),
      })),

    compile: (body: ScenarioPreviewRequest) =>
      execute(() => $fetch<ScenarioPreviewResponse>(`${baseUrl}/compile`, {
        method: 'POST',
        headers: headers(),
        body,
      })),

    preview: (body: ScenarioPreviewRequest) =>
      execute(() => $fetch<ScenarioPreviewResponse>(`${baseUrl}/preview`, {
        method: 'POST',
        headers: headers(),
        body,
      })),

    simulate: (body: ScenarioPreviewRequest, scenarioId?: string, version?: number) => {
      const url = scenarioId && version != null
        ? `${versionPath(scenarioId, version)}/simulate`
        : `${baseUrl}/simulate`;
      return execute(() => $fetch<ScenarioPreviewResponse>(url, {
        method: 'POST',
        headers: headers(),
        body,
      }));
    },
  };
}
