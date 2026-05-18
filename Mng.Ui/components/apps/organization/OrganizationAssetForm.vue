<script setup lang="ts">
import { ref, watch, computed } from 'vue';
import type { MonAsset, MonAssetType, CollectibleDefinition } from '@/types/apps/organization';
import { AlertTriangleIcon } from 'vue-tabler-icons';

const props = defineProps<{
  asset: MonAsset | Partial<MonAsset> | null;
  itemOptions: Array<{ title: string; value: string }>;
  typeOptions: Array<{ title: string; value: string }>;
  /** Tam type listesi (connection_info / collectible_config alanları için) */
  assetTypes?: MonAssetType[];
  /** HTTP auth config seçenekleri (Bearer token için) */
  httpAuthConfigOptions?: Array<{ title: string; value: string }>;
  loading?: boolean;
  /** Kaydet/Sil butonları sadece is_manager veya is_admin için gösterilir */
  canEdit?: boolean;
}>();

const emit = defineEmits<{
  save: [data: Partial<MonAsset>];
  delete: [dataId: string];
  cancel: [];
}>();

function normalizeTypeId(value: unknown): string {
  if (value == null) return '';
  if (typeof value === 'string') return value;
  if (typeof value === 'object' && value !== null && ('__dataId' in value || 'dataId' in value))
    return (value as any).__dataId ?? (value as any).dataId ?? '';
  return String(value);
}

function normalizeConnectionInfo(raw: Record<string, unknown> | null | undefined): Record<string, unknown> {
  const info = raw && typeof raw === 'object' ? { ...raw } : {};
  if (!info.endpoint || typeof info.endpoint !== 'object') info.endpoint = { host: '', port: null };
  const ep = info.endpoint as Record<string, unknown>;
  if (ep.host === undefined) ep.host = '';
  if (ep.port === undefined) ep.port = null;
  if (!info.auth || typeof info.auth !== 'object') info.auth = {};
  if (info.baseUrl === undefined) info.baseUrl = '';
  const auth = info.auth as Record<string, unknown>;
  if (auth.type === undefined) auth.type = 'none';
  if (auth.authConfigId === undefined) auth.authConfigId = '';
  return info;
}

function normalizeCollectibleConfig(
  raw: MonAsset['collectible_config'],
  collectibles: CollectibleDefinition[] | undefined
): Array<{ code: string; enabled: boolean; params?: Record<string, unknown> }> {
  const list = collectibles ?? [];
  const existing = Array.isArray(raw) ? raw : [];
  const byCode = new Map(existing.map((c) => [c.code, c]));
  return list.map((def) => {
    const cur = byCode.get(def.code);
    return {
      code: def.code,
      enabled: cur?.enabled ?? true,
      params: cur?.params ?? (def.overridable_params?.length ? {} : undefined),
    };
  });
}

const form = ref<Partial<MonAsset>>({
  name: '',
  type: '',
  itemId: '',
  description: null,
  status: 'active',
  connection_info: {},
  collectible_config: null,
});

const deleteDialogOpen = ref(false);

const selectedAssetType = computed(() =>
  (props.assetTypes ?? []).find((t) => t.__dataId === form.value.type)
);

const collectionMethod = computed(() => selectedAssetType.value?.collection_method ?? '');

const collectiblesFromType = computed(() => selectedAssetType.value?.collectibles ?? []);

watch(
  () => props.asset,
  (v) => {
    if (v) {
      const conn = normalizeConnectionInfo(v.connection_info as Record<string, unknown>);
      const typeDef = (props.assetTypes ?? []).find((t) => t.__dataId === normalizeTypeId(v.type));
      const coll = normalizeCollectibleConfig(v.collectible_config ?? null, typeDef?.collectibles);
      form.value = {
        name: v.name ?? '',
        type: normalizeTypeId(v.type),
        itemId: normalizeTypeId(v.itemId),
        description: v.description ?? null,
        status: v.status ?? 'active',
        connection_info: conn,
        collectible_config: coll.length ? coll : (v.collectible_config ?? null),
      };
      if ('__dataId' in v && v.__dataId) (form.value as any).__dataId = v.__dataId;
    } else {
      form.value = {
        name: '',
        type: '',
        itemId: '',
        description: null,
        status: 'active',
        connection_info: normalizeConnectionInfo({}),
        collectible_config: null,
      };
    }
  },
  { immediate: true }
);

watch(
  () => [form.value.type, props.assetTypes] as const,
  ([typeId, assetTypes]) => {
    const typeDef = (assetTypes ?? []).find((t) => t.__dataId === typeId);
    if (!typeDef?.collectibles?.length) return;
    const currentCodes = new Set((form.value.collectible_config ?? []).map((c: any) => c.code).filter(Boolean));
    const typeCodes = new Set(typeDef.collectibles.map((c) => c.code).filter(Boolean));
    const codesMatch = currentCodes.size === typeCodes.size && [...typeCodes].every((c) => currentCodes.has(c));
    if (!codesMatch) {
      form.value.collectible_config = normalizeCollectibleConfig(null, typeDef.collectibles);
    }
  },
  { immediate: true }
);

const isEdit = ref(false);
watch(() => props.asset, (v) => { isEdit.value = !!(v && '__dataId' in v && v.__dataId); }, { immediate: true });

const statusItems = [
  { title: 'Aktif', value: 'active' },
  { title: 'Bakımda', value: 'maintenance' },
  { title: 'Devre dışı', value: 'decommissioned' },
];

function conn(): Record<string, any> {
  const c = form.value.connection_info;
  if (!c || typeof c !== 'object') form.value.connection_info = normalizeConnectionInfo({}) as Record<string, unknown>;
  return (form.value.connection_info as Record<string, any>) ?? {};
}
function connEndpoint(): Record<string, any> {
  const c = conn();
  if (!c.endpoint || typeof c.endpoint !== 'object') c.endpoint = { host: '', port: null };
  return c.endpoint;
}
function connAuth(): Record<string, any> {
  const c = conn();
  if (!c.auth || typeof c.auth !== 'object') c.auth = {};
  return c.auth;
}
function getConnHost(): string {
  return connEndpoint().host ?? '';
}
function setConnHost(v: string): void {
  connEndpoint().host = v ?? '';
}
function getConnPort(): string | number {
  const p = connEndpoint().port;
  return p === null || p === undefined ? '' : p;
}
function setConnPort(v: string): void {
  const n = v !== '' && v != null ? Number(v) : null;
  connEndpoint().port = Number.isFinite(n) ? n : null;
}
function getAuthUsername(): string {
  return connAuth().username ?? '';
}
function setAuthUsername(v: string): void {
  connAuth().username = v ?? '';
}
function getAuthPassword(): string {
  return connAuth().password ?? '';
}
function setAuthPassword(v: string): void {
  connAuth().password = v ?? '';
}
function getAuthCommunity(): string {
  return connAuth().community ?? '';
}
function setAuthCommunity(v: string): void {
  connAuth().community = v ?? '';
}
function getConnBaseUrl(): string {
  return (conn().baseUrl ?? '') as string;
}
function setConnBaseUrl(v: string): void {
  conn().baseUrl = v ?? '';
}
function getAuthType(): string {
  return (connAuth().type ?? 'none') as string;
}
function setAuthType(v: string): void {
  connAuth().type = v ?? 'none';
}
function getAuthConfigId(): string {
  return (connAuth().authConfigId ?? '') as string;
}
function setAuthConfigId(v: string): void {
  connAuth().authConfigId = v ?? '';
}

function setCollectibleEnabled(code: string, enabled: boolean) {
  let list = form.value.collectible_config ?? [];
  const idx = list.findIndex((c) => c.code === code);
  if (idx >= 0) {
    list = [...list];
    list[idx] = { ...list[idx], enabled };
  } else {
    list = [...list, { code, enabled }];
  }
  form.value.collectible_config = list;
}

function setCollectibleParam(code: string, paramKey: string, value: string) {
  let list = form.value.collectible_config ?? [];
  const idx = list.findIndex((c) => c.code === code);
  const entry = idx >= 0 ? list[idx] : { code, enabled: true };
  const params = { ...(entry.params ?? {}), [paramKey]: value || undefined };
  if (idx >= 0) {
    list = [...list];
    list[idx] = { ...entry, params };
  } else {
    list = [...list, { ...entry, params }];
  }
  form.value.collectible_config = list;
}

function getCollectibleEntry(code: string) {
  return (form.value.collectible_config ?? []).find((c) => c.code === code);
}

function getCollectibleParam(code: string, paramKey: string): string {
  const entry = getCollectibleEntry(code);
  const params = entry?.params;
  if (!params || typeof params !== 'object') return '';
  const v = params[paramKey];
  return v != null ? String(v) : '';
}

function save() {
  const data = { ...form.value };
  if (!data.name?.trim() || !data.itemId || !data.type) return;
  if (typeof data.connection_info !== 'object') data.connection_info = {};
  const coll = data.collectible_config;
  if (Array.isArray(coll)) {
    data.collectible_config = coll.filter((c) => c.enabled || (c.params && Object.keys(c.params).length > 0));
    if (data.collectible_config.length === 0) data.collectible_config = null;
  }
  emit('save', data);
}

function openDeleteDialog() {
  if ((form.value as any).__dataId) deleteDialogOpen.value = true;
}

function confirmDelete() {
  const id = (form.value as any).__dataId;
  if (id) {
    emit('delete', id);
    deleteDialogOpen.value = false;
  }
}

function closeDeleteDialog() {
  deleteDialogOpen.value = false;
}
</script>

<template>
  <div class="org-asset-form">
    <v-form @submit.prevent="save">
      <v-text-field
        v-model="form.name"
        label="Ad *"
        variant="outlined"
        density="comfortable"
        class="mb-3"
        hide-details
      />
      <v-select
        v-model="form.itemId"
        :items="itemOptions"
        item-title="title"
        item-value="value"
        label="Item (içinde bulunduğu) *"
        variant="outlined"
        density="comfortable"
        class="mb-3"
        hide-details
      />
      <v-select
        v-model="form.type"
        :items="typeOptions"
        item-title="title"
        item-value="value"
        label="Asset tipi *"
        variant="outlined"
        density="comfortable"
        class="mb-3"
        hide-details
      />
      <v-textarea
        v-model="form.description"
        label="Açıklama"
        variant="outlined"
        density="comfortable"
        rows="2"
        class="mb-3"
        hide-details
      />
      <v-select
        v-model="form.status"
        :items="statusItems"
        item-title="title"
        item-value="value"
        label="Durum"
        variant="outlined"
        density="comfortable"
        class="mb-3"
        hide-details
      />

      <!-- Bağlantı bilgisi (connection_info) - tip seçildikten sonra collection_method'a göre -->
      <v-expand-transition>
        <div v-if="form.type" class="mb-4">
          <div class="text-subtitle-2 text-medium-emphasis mb-2">Bağlantı bilgisi</div>
          <v-row dense>
            <!-- SSH, WMI, SNMP: host + port -->
            <template v-if="collectionMethod !== 'HTTP' && collectionMethod !== 'REST'">
              <v-col cols="12" sm="6">
                <v-text-field
                  :model-value="getConnHost()"
                  @update:model-value="setConnHost"
                  label="Host / IP"
                  variant="outlined"
                  density="comfortable"
                  hide-details
                />
              </v-col>
              <v-col cols="12" sm="6">
                <v-text-field
                  :model-value="getConnPort()"
                  @update:model-value="setConnPort"
                  label="Port"
                  type="number"
                  variant="outlined"
                  density="comfortable"
                  hide-details
                />
              </v-col>
            </template>
            <!-- HTTP / REST: baseUrl -->
            <template v-if="collectionMethod === 'HTTP' || collectionMethod === 'REST'">
              <v-col cols="12">
                <v-text-field
                  :model-value="getConnBaseUrl()"
                  @update:model-value="setConnBaseUrl"
                  label="Base URL *"
                  variant="outlined"
                  density="comfortable"
                  hide-details
                  placeholder="https://api.example.com"
                />
              </v-col>
              <v-col cols="12" sm="6">
                <v-select
                  :model-value="getAuthType()"
                  @update:model-value="setAuthType"
                  :items="[
                    { title: 'Yok (Auth yok)', value: 'none' },
                    { title: 'Basic (kullanıcı/parola)', value: 'basic' },
                    { title: 'Bearer Token (HTTP Auth Config)', value: 'bearer_token' },
                  ]"
                  item-title="title"
                  item-value="value"
                  label="Auth tipi"
                  variant="outlined"
                  density="comfortable"
                  hide-details
                />
              </v-col>
              <template v-if="getAuthType() === 'basic'">
                <v-col cols="12" sm="6">
                  <v-text-field
                    :model-value="getAuthUsername()"
                    @update:model-value="setAuthUsername"
                    label="Kullanıcı adı"
                    variant="outlined"
                    density="comfortable"
                    hide-details
                  />
                </v-col>
                <v-col cols="12" sm="6">
                  <v-text-field
                    :model-value="getAuthPassword()"
                    @update:model-value="setAuthPassword"
                    label="Parola"
                    type="password"
                    variant="outlined"
                    density="comfortable"
                    hide-details
                    autocomplete="off"
                  />
                </v-col>
              </template>
              <v-col v-else-if="getAuthType() === 'bearer_token'" cols="12" sm="6">
                <v-select
                  :model-value="getAuthConfigId()"
                  @update:model-value="setAuthConfigId"
                  :items="httpAuthConfigOptions ?? []"
                  item-title="title"
                  item-value="value"
                  label="HTTP Auth Config"
                  variant="outlined"
                  density="comfortable"
                  hide-details
                  clearable
                  placeholder="Token endpoint tanımı seçin"
                />
              </v-col>
            </template>
            <template v-if="collectionMethod === 'SSH' || collectionMethod === 'WMI'">
              <v-col cols="12" sm="6">
                <v-text-field
                  :model-value="getAuthUsername()"
                  @update:model-value="setAuthUsername"
                  label="Kullanıcı adı"
                  variant="outlined"
                  density="comfortable"
                  hide-details
                />
              </v-col>
              <v-col cols="12" sm="6">
                <v-text-field
                  :model-value="getAuthPassword()"
                  @update:model-value="setAuthPassword"
                  label="Parola"
                  type="password"
                  variant="outlined"
                  density="comfortable"
                  hide-details
                  autocomplete="off"
                />
              </v-col>
            </template>
            <template v-else-if="collectionMethod === 'SNMP' || collectionMethod === 'SNMP_V3'">
              <v-col cols="12" sm="6">
                <v-text-field
                  :model-value="getAuthCommunity()"
                  @update:model-value="setAuthCommunity"
                  label="Community (SNMP v2c)"
                  variant="outlined"
                  density="comfortable"
                  hide-details
                />
              </v-col>
            </template>
          </v-row>
        </div>
      </v-expand-transition>

      <!-- Collectible ayarları (type'a göre toplanacak metrikler) -->
      <v-expand-transition>
        <div v-if="collectiblesFromType.length > 0" class="mb-4">
          <div class="text-subtitle-2 text-medium-emphasis mb-2">Toplanacak metrikler</div>
          <v-card variant="outlined" class="pa-3">
            <div
              v-for="col in collectiblesFromType"
              :key="col.code"
              class="d-flex align-center flex-wrap gap-2 mb-2"
            >
              <v-checkbox
                :model-value="getCollectibleEntry(col.code)?.enabled ?? true"
                @update:model-value="(v) => setCollectibleEnabled(col.code, !!v)"
                hide-details
                density="compact"
                :label="col.name || col.code"
              />
              <template v-if="col.overridable_params?.length && (getCollectibleEntry(col.code)?.enabled !== false)">
                <v-text-field
                  v-for="paramKey in col.overridable_params"
                  :key="paramKey"
                  :model-value="getCollectibleParam(col.code, paramKey)"
                  @update:model-value="(v) => setCollectibleParam(col.code, paramKey, v ?? '')"
                  :label="paramKey"
                  variant="outlined"
                  density="compact"
                  hide-details
                  class="flex-grow-1"
                  style="max-width: 200px;"
                />
              </template>
            </div>
          </v-card>
        </div>
      </v-expand-transition>

      <div class="d-flex gap-2 mt-4">
        <v-btn v-if="canEdit" color="primary" type="submit" :loading="loading">Kaydet</v-btn>
        <v-btn v-if="canEdit && isEdit" color="error" variant="outlined" @click="openDeleteDialog" :disabled="loading">Sil</v-btn>
        <v-btn variant="outlined" @click="emit('cancel')">İptal</v-btn>
      </div>
    </v-form>

    <v-dialog v-model="deleteDialogOpen" max-width="440" persistent>
      <v-card>
        <v-card-title class="d-flex align-center text-body-1">
          <AlertTriangleIcon size="24" class="mr-2 text-warning" />
          Asset silinsin mi?
        </v-card-title>
        <v-card-text>
          <span class="text-body-2">"<strong>{{ form.name }}</strong>" asset'ini silmek istediğinize emin misiniz? Bu işlem geri alınamaz.</span>
        </v-card-text>
        <v-card-actions class="pt-0">
          <v-spacer />
          <v-btn variant="text" @click="closeDeleteDialog">İptal</v-btn>
          <v-btn color="error" variant="flat" :loading="loading" @click="confirmDelete">Sil</v-btn>
        </v-card-actions>
      </v-card>
    </v-dialog>
  </div>
</template>
