import { defineStore } from 'pinia';
import type { OdakHubListConfig } from '@/utils/odakSiparisHubListConfig';
import {
  defaultKeeperGroupListConfig,
  mergeKeeperGroupListConfig,
} from '@/utils/keeperGroupListSettings';
import {
  defaultKeeperUserListConfig,
  mergeKeeperUserListConfig,
} from '@/utils/keeperUserListSettings';
import {
  clearKeeperListConfigFromStorage,
  loadKeeperListConfigFromStorage,
  saveKeeperListConfigToStorage,
  type KeeperListSettingsScope,
} from '@/utils/keeperListSettingsStorage';
import { useAuthStore } from '@/stores/auth';

export const KEEPER_LIST_SETTINGS_SCOPES: KeeperListSettingsScope[] = ['users_list', 'groups_list'];

function cloneJson<T>(value: T): T {
  return JSON.parse(JSON.stringify(value)) as T;
}

function baselineOf(value: unknown): string {
  return JSON.stringify(value);
}

type ScopeSlice = {
  config: OdakHubListConfig;
  baselineJson: string;
  status: 'idle' | 'ready' | 'saving';
};

function defaultConfigForScope(scope: KeeperListSettingsScope): OdakHubListConfig {
  return scope === 'users_list' ? defaultKeeperUserListConfig() : defaultKeeperGroupListConfig();
}

function mergeConfigForScope(scope: KeeperListSettingsScope, saved: unknown): OdakHubListConfig {
  return scope === 'users_list' ? mergeKeeperUserListConfig(saved) : mergeKeeperGroupListConfig(saved);
}

function createSlice(config: OdakHubListConfig): ScopeSlice {
  return {
    config: cloneJson(config),
    baselineJson: baselineOf(config),
    status: 'idle',
  };
}

function resolveDomainId(): string {
  const auth = useAuthStore();
  return String(auth.userInfo?.domain_id ?? auth.domainInfo?.id ?? 'default').trim() || 'default';
}

export const useKeeperListSettingsStore = defineStore('keeperListSettings', {
  state: () => ({
    domainId: '',
    scopes: {
      users_list: createSlice(defaultKeeperUserListConfig()),
      groups_list: createSlice(defaultKeeperGroupListConfig()),
    } as Record<KeeperListSettingsScope, ScopeSlice>,
    bootstrapped: false,
  }),

  getters: {
    userListConfig(state): OdakHubListConfig {
      return state.scopes.users_list.config;
    },
    groupListConfig(state): OdakHubListConfig {
      return state.scopes.groups_list.config;
    },
    canSaveScope:
      (state) =>
      (scope: KeeperListSettingsScope): boolean =>
        baselineOf(state.scopes[scope].config) !== state.scopes[scope].baselineJson,
    isScopeDirty:
      (state) =>
      (scope: KeeperListSettingsScope): boolean =>
        baselineOf(state.scopes[scope].config) !== state.scopes[scope].baselineJson,
    scopeSaving:
      (state) =>
      (scope: KeeperListSettingsScope): boolean =>
        state.scopes[scope].status === 'saving',
  },

  actions: {
    ensureReady(force = false) {
      if (this.bootstrapped && !force) return;
      this.domainId = resolveDomainId();
      for (const scope of KEEPER_LIST_SETTINGS_SCOPES) {
        const saved = loadKeeperListConfigFromStorage(this.domainId, scope);
        const merged = mergeConfigForScope(scope, saved);
        this.scopes[scope] = createSlice(merged);
        this.scopes[scope].status = 'ready';
      }
      this.bootstrapped = true;
    },

    resetScopeToDefaults(scope: KeeperListSettingsScope) {
      this.scopes[scope].config = cloneJson(defaultConfigForScope(scope));
    },

    async saveScope(scope: KeeperListSettingsScope) {
      if (!this.domainId) this.domainId = resolveDomainId();
      this.scopes[scope].status = 'saving';
      try {
        saveKeeperListConfigToStorage(this.domainId, scope, cloneJson(this.scopes[scope].config));
        this.scopes[scope].baselineJson = baselineOf(this.scopes[scope].config);
      } finally {
        this.scopes[scope].status = 'ready';
      }
    },

    clearScope(scope: KeeperListSettingsScope) {
      if (!this.domainId) this.domainId = resolveDomainId();
      clearKeeperListConfigFromStorage(this.domainId, scope);
      this.scopes[scope] = createSlice(defaultConfigForScope(scope));
      this.scopes[scope].status = 'ready';
    },
  },
});
