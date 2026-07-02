import { defineStore } from 'pinia';
import type { OdakFieldPoliciesBlob } from '@/utils/odakSiparisFieldPolicies';
import { emptyOdakFieldPoliciesBlob } from '@/utils/odakSiparisFieldPolicies';
import type { OdakHubListConfig } from '@/utils/odakSiparisHubListConfig';
import {
  defaultOdakHubListConfig,
  loadOdakHubFieldPoliciesBlob,
  loadOdakHubListConfig,
  loadOdakPackagePersonnelConfig,
  loadOdakPackagePoDocumentAccessConfig,
  saveOdakHubFieldPoliciesBlob,
  saveOdakHubListConfig,
  saveOdakPackagePersonnelConfig,
  saveOdakPackagePoDocumentAccessConfig,
  type OdakHubFieldPoliciesScope,
  type OdakHubListSettingsScope,
  type OdakHubSettingsScope,
} from '@/utils/odakSiparisHubSettingsService';
import {
  defaultOdakPackagePersonnelConfig,
  type OdakPackagePersonnelConfig,
} from '@/utils/odakSiparisPackagePersonnel';
import {
  defaultOdakPackagePoDocumentAccessConfig,
  type OdakPackagePoDocumentAccessConfig,
} from '@/utils/odakSiparisPoDocumentAccess';
import type { OdakPackageListConfig } from '@/utils/odakSiparisPackageListSettings';

export type OdakHubScopeStatus = 'idle' | 'loading' | 'ready' | 'saving' | 'error';

export interface OdakHubScopeSlice<T> {
  rowId: string | null;
  config: T;
  baselineJson: string;
  status: OdakHubScopeStatus;
  error: string | null;
}

export type OdakHubScopeConfigMap = {
  packages_list: OdakHubListConfig;
  lines_list: OdakHubListConfig;
  shipments_list: OdakHubListConfig;
  field_policies: OdakFieldPoliciesBlob;
  lines_field_policies: OdakFieldPoliciesBlob;
  shipments_field_policies: OdakFieldPoliciesBlob;
  package_po_document_access: OdakPackagePoDocumentAccessConfig;
  package_odak_personnel: OdakPackagePersonnelConfig;
};

export const ODAK_HUB_SETTINGS_SCOPES: OdakHubSettingsScope[] = [
  'packages_list',
  'lines_list',
  'shipments_list',
  'field_policies',
  'lines_field_policies',
  'shipments_field_policies',
  'package_po_document_access',
  'package_odak_personnel',
];

const LIST_SCOPES = new Set<OdakHubSettingsScope>(['packages_list', 'lines_list', 'shipments_list']);
const FIELD_POLICY_SCOPES = new Set<OdakHubSettingsScope>([
  'field_policies',
  'lines_field_policies',
  'shipments_field_policies',
]);

function cloneJson<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

function baselineOf<T>(value: T): string {
  return JSON.stringify(value);
}

function defaultConfigForScope(scope: OdakHubSettingsScope): OdakHubScopeConfigMap[OdakHubSettingsScope] {
  if (LIST_SCOPES.has(scope)) {
    return defaultOdakHubListConfig(scope as OdakHubListSettingsScope);
  }
  if (FIELD_POLICY_SCOPES.has(scope)) {
    return emptyOdakFieldPoliciesBlob();
  }
  if (scope === 'package_po_document_access') {
    return defaultOdakPackagePoDocumentAccessConfig();
  }
  return defaultOdakPackagePersonnelConfig();
}

function createScopeSlice<T>(config: T): OdakHubScopeSlice<T> {
  return {
    rowId: null,
    config: cloneJson(config),
    baselineJson: baselineOf(config),
    status: 'idle',
    error: null,
  };
}

function initialScopesState(): Record<OdakHubSettingsScope, OdakHubScopeSlice<unknown>> {
  return Object.fromEntries(
    ODAK_HUB_SETTINGS_SCOPES.map((scope) => [scope, createScopeSlice(defaultConfigForScope(scope))])
  ) as Record<OdakHubSettingsScope, OdakHubScopeSlice<unknown>>;
}

let bootstrapInflight: Promise<void> | null = null;

async function fetchScopeFromServer(scope: OdakHubSettingsScope): Promise<{
  config: OdakHubScopeConfigMap[OdakHubSettingsScope];
  rowId: string | null;
}> {
  if (LIST_SCOPES.has(scope)) {
    const resp = await loadOdakHubListConfig(scope as OdakHubListSettingsScope);
    return { config: resp.config, rowId: resp.rowId };
  }
  if (FIELD_POLICY_SCOPES.has(scope)) {
    const resp = await loadOdakHubFieldPoliciesBlob(scope as OdakHubFieldPoliciesScope);
    return { config: resp.blob, rowId: resp.rowId };
  }
  if (scope === 'package_po_document_access') {
    const resp = await loadOdakPackagePoDocumentAccessConfig();
    return { config: resp.config, rowId: resp.rowId };
  }
  const resp = await loadOdakPackagePersonnelConfig();
  return { config: resp.config, rowId: resp.rowId };
}

async function persistScopeToServer(
  scope: OdakHubSettingsScope,
  config: OdakHubScopeConfigMap[OdakHubSettingsScope],
  rowId: string | null
): Promise<string> {
  if (LIST_SCOPES.has(scope)) {
    return saveOdakHubListConfig(scope as OdakHubListSettingsScope, config as OdakHubListConfig, rowId);
  }
  if (FIELD_POLICY_SCOPES.has(scope)) {
    return saveOdakHubFieldPoliciesBlob(
      scope as OdakHubFieldPoliciesScope,
      config as OdakFieldPoliciesBlob,
      rowId
    );
  }
  if (scope === 'package_po_document_access') {
    return saveOdakPackagePoDocumentAccessConfig(
      config as OdakPackagePoDocumentAccessConfig,
      rowId
    );
  }
  return saveOdakPackagePersonnelConfig(config as OdakPackagePersonnelConfig, rowId);
}

export const useOdakSiparisHubSettingsStore = defineStore('odakSiparisHubSettings', {
  state: () => ({
    bootstrapStatus: 'idle' as 'idle' | 'loading' | 'ready' | 'error',
    bootstrapError: null as string | null,
    loadedAt: null as number | null,
    scopes: initialScopesState(),
  }),

  getters: {
    isBootstrapping: (state) => state.bootstrapStatus === 'loading',
    isReady: (state) => state.bootstrapStatus === 'ready',

    packageListConfig: (state): OdakPackageListConfig =>
      state.scopes.packages_list.config as OdakPackageListConfig,

    packageFieldPolicies: (state): OdakFieldPoliciesBlob =>
      state.scopes.field_policies.config as OdakFieldPoliciesBlob,

    scopeReady:
      (state) =>
      (scope: OdakHubSettingsScope): boolean =>
        state.bootstrapStatus === 'ready' && state.scopes[scope].status === 'ready',

    scopeSaving:
      (state) =>
      (scope: OdakHubSettingsScope): boolean =>
        state.scopes[scope].status === 'saving',

    canSaveScope:
      (state) =>
      (scope: OdakHubSettingsScope): boolean =>
        state.bootstrapStatus === 'ready' &&
        state.scopes[scope].status === 'ready',

    canEditScope:
      (state) =>
      (scope: OdakHubSettingsScope): boolean =>
        state.bootstrapStatus === 'ready' &&
        (state.scopes[scope].status === 'ready' || state.scopes[scope].status === 'saving'),

    isScopeDirty:
      (state) =>
      (scope: OdakHubSettingsScope): boolean =>
        baselineOf(state.scopes[scope].config) !== state.scopes[scope].baselineJson,

    listConfig:
      (state) =>
      (scope: OdakHubListSettingsScope): OdakHubListConfig =>
        state.scopes[scope].config as OdakHubListConfig,

    fieldPoliciesBlob:
      (state) =>
      (scope: OdakHubFieldPoliciesScope): OdakFieldPoliciesBlob =>
        state.scopes[scope].config as OdakFieldPoliciesBlob,

    personnelConfig: (state): OdakPackagePersonnelConfig =>
      state.scopes.package_odak_personnel.config as OdakPackagePersonnelConfig,

    poDocumentAccessConfig: (state): OdakPackagePoDocumentAccessConfig =>
      state.scopes.package_po_document_access.config as OdakPackagePoDocumentAccessConfig,
  },

  actions: {
    scopeSlice<S extends OdakHubSettingsScope>(scope: S): OdakHubScopeSlice<OdakHubScopeConfigMap[S]> {
      return this.scopes[scope] as OdakHubScopeSlice<OdakHubScopeConfigMap[S]>;
    },

    applyScopeLoaded(scope: OdakHubSettingsScope, config: unknown, rowId: string | null) {
      const slice = this.scopes[scope];
      const cloned = cloneJson(config);
      slice.config = cloned;
      slice.baselineJson = baselineOf(cloned);
      slice.rowId = rowId;
      slice.status = 'ready';
      slice.error = null;
    },

    async bootstrap(force = false) {
      if (!force && this.bootstrapStatus === 'ready' && this.loadedAt != null) {
        return;
      }

      if (bootstrapInflight) {
        await bootstrapInflight;
        if (!force && this.bootstrapStatus === 'ready') {
          return;
        }
      }

      bootstrapInflight = this.runBootstrap();
      try {
        await bootstrapInflight;
      } finally {
        bootstrapInflight = null;
      }
    },

    async runBootstrap() {
      this.bootstrapStatus = 'loading';
      this.bootstrapError = null;
      for (const scope of ODAK_HUB_SETTINGS_SCOPES) {
        this.scopes[scope].status = 'loading';
        this.scopes[scope].error = null;
      }

      try {
        const results = await Promise.all(
          ODAK_HUB_SETTINGS_SCOPES.map(async (scope) => {
            try {
              return { scope, ...(await fetchScopeFromServer(scope)) };
            } catch (e: unknown) {
              return { scope, config: defaultConfigForScope(scope), rowId: null, loadError: e };
            }
          })
        );

        for (const result of results) {
          this.applyScopeLoaded(result.scope, result.config, result.rowId);
          if ('loadError' in result && result.loadError) {
            this.scopes[result.scope].error =
              result.loadError instanceof Error ? result.loadError.message : String(result.loadError);
          }
        }

        this.bootstrapStatus = 'ready';
        this.loadedAt = Date.now();
      } catch (e: unknown) {
        this.bootstrapStatus = 'error';
        this.bootstrapError = e instanceof Error ? e.message : String(e);
        for (const scope of ODAK_HUB_SETTINGS_SCOPES) {
          if (this.scopes[scope].status === 'loading') {
            this.scopes[scope].status = 'error';
          }
        }
        throw e;
      }
    },

    async ensureReady(force = false) {
      await this.bootstrap(force);
    },

    async reloadScope(scope: OdakHubSettingsScope) {
      const previousStatus = this.scopes[scope].status;
      this.scopes[scope].status = 'loading';
      this.scopes[scope].error = null;
      try {
        const loaded = await fetchScopeFromServer(scope);
        this.applyScopeLoaded(scope, loaded.config, loaded.rowId);
        this.loadedAt = Date.now();
      } catch (e: unknown) {
        this.scopes[scope].status = previousStatus === 'ready' ? 'ready' : 'error';
        this.scopes[scope].error = e instanceof Error ? e.message : String(e);
        throw e;
      }
    },

    async saveScope(scope: OdakHubSettingsScope) {
      if (!this.canSaveScope(scope)) {
        throw new Error(`Scope not ready for save: ${scope}`);
      }

      const slice = this.scopes[scope];
      slice.status = 'saving';
      slice.error = null;

      try {
        slice.rowId = await persistScopeToServer(
          scope,
          cloneJson(slice.config) as OdakHubScopeConfigMap[OdakHubSettingsScope],
          slice.rowId
        );
        await this.reloadScope(scope);
        this.loadedAt = Date.now();
      } catch (e: unknown) {
        slice.status = 'ready';
        slice.error = e instanceof Error ? e.message : String(e);
        throw e;
      }
    },

    resetListScopeToDefaults(scope: OdakHubListSettingsScope) {
      if (!this.canEditScope(scope)) return;
      this.scopes[scope].config = cloneJson(defaultOdakHubListConfig(scope));
    },

    resetScopeToBaseline(scope: OdakHubSettingsScope) {
      if (!this.canEditScope(scope)) return;
      const slice = this.scopes[scope];
      slice.config = cloneJson(JSON.parse(slice.baselineJson));
    },

    setFieldPoliciesBlob(scope: OdakHubFieldPoliciesScope, blob: OdakFieldPoliciesBlob) {
      if (!this.canEditScope(scope)) return;
      this.scopes[scope].config = cloneJson(blob);
    },

    setDesignPersonnelIds(ids: string[]) {
      if (!this.canEditScope('package_odak_personnel')) return;
      (this.scopes.package_odak_personnel.config as OdakPackagePersonnelConfig).designPersonnelIds = [...ids];
    },

    setManufacturePersonnelIds(ids: string[]) {
      if (!this.canEditScope('package_odak_personnel')) return;
      (this.scopes.package_odak_personnel.config as OdakPackagePersonnelConfig).manufacturePersonnelIds = [...ids];
    },

    setPoDocumentRestrictedGroups(groups: string[]) {
      if (!this.canEditScope('package_po_document_access')) return;
      (this.scopes.package_po_document_access.config as OdakPackagePoDocumentAccessConfig).restrictedViewerGroups = [
        ...groups,
      ];
    },

    invalidate() {
      this.bootstrapStatus = 'idle';
      this.loadedAt = null;
      for (const scope of ODAK_HUB_SETTINGS_SCOPES) {
        const slice = this.scopes[scope];
        slice.status = 'idle';
        slice.error = null;
      }
    },
  },
});
