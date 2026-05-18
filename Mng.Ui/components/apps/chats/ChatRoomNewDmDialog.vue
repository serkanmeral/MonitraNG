<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue';
import { useAppI18n } from '@/composables/useAppI18n';
import { fetchFromMngKeeper } from '@/services/apiService';
import { useAuthStore } from '@/stores/auth';
import { useChatRoomWorkspaceStore } from '@/stores/apps/chatRoomWorkspace';
import { useUserStore, type User } from '@/stores/apps/user';
import type { ChtDirectConversationVm } from '@/types/apps/chatRoom';
import {
  displayNameForStoredPersonId,
  keeperPrimaryChatPersonId,
  participantMatchesAnyAlias,
} from '@/utils/chatRoomPersonId';

const { t } = useAppI18n();
const store = useChatRoomWorkspaceStore();
const userStore = useUserStore();
const authStore = useAuthStore();

const props = defineProps<{ modelValue: boolean }>();
const emit = defineEmits<{
  (e: 'update:modelValue', v: boolean): void;
  (e: 'created', row: ChtDirectConversationVm): void;
}>();

const otherId = ref<string>('');
/** `fetchUsers` listeyi değiştirince seçilen satır `items`’dan düşer; combobox id gösterir. Seçim anındaki başlık burada tutulur. */
const peerTitleById = ref<Map<string, string>>(new Map());
const busy = ref(false);
const error = ref<string | null>(null);
const userSearchLoading = ref(false);

/**
 * Yeni DM araması `userStore.fetchUsers` kullanmaz — o, global `users` dizisini değiştirir ve
 * sol listedeki DM başlıkları (`displayNameForStoredPersonId`) anlık olarak id’ye düşer.
 */
const searchHitUsers = ref<User[]>([]);

let searchDebounce: ReturnType<typeof setTimeout> | null = null;

function mapKeeperListUser(user: Record<string, unknown>): User {
  const primaryId = String(user.id ?? user.Id ?? user.userId ?? user.UserId ?? '').trim();
  return {
    id: primaryId,
    userId: String(user.userId ?? user.UserId ?? user.id ?? user.Id ?? primaryId),
    domainId: String(user.domainId ?? user.DomainId ?? ''),
    keycloakUserId: (user.keycloakUserId ?? user.KeycloakUserId) as string | undefined,
    username: String(user.username ?? user.Username ?? ''),
    email: String(user.email ?? user.Email ?? ''),
    firstName: String(user.firstName ?? user.FirstName ?? ''),
    lastName: String(user.lastName ?? user.LastName ?? ''),
    title: (user.title ?? user.Title ?? null) as string | null,
    department: (user.department ?? user.Department ?? null) as string | null,
    gender: (user.gender ?? user.Gender ?? 'NotSpecified') as User['gender'],
    phoneNumber: (user.phoneNumber ?? user.PhoneNumber ?? null) as string | null,
    photoUrl: (user.photoUrl ?? user.PhotoUrl ?? null) as string | null,
    isActive:
      user.isActive !== undefined
        ? Boolean(user.isActive)
        : user.IsActive !== undefined
          ? Boolean(user.IsActive)
          : true,
    groups: (Array.isArray(user.groups) ? user.groups : Array.isArray(user.Groups) ? user.Groups : []) as string[],
    roles: (Array.isArray(user.roles) ? user.roles : Array.isArray(user.Roles) ? user.Roles : []) as string[],
    createdAt: (user.createdAt ?? user.CreatedAt ?? new Date()) as string | Date,
    lastLoginAt: (user.lastLoginAt ?? user.LastLoginAt ?? null) as string | Date | null,
    createdBy: user.createdBy as string | undefined,
    updatedAt: (user.updatedAt ?? user.UpdatedAt ?? null) as string | Date | null,
    updatedBy: (user.updatedBy ?? user.UpdatedBy ?? null) as string | null,
  };
}

function userMatchesMe(u: User): boolean {
  const sid = String(authStore.userInfo?.sub ?? '').trim();
  if (!sid) return false;
  const pid = keeperPrimaryChatPersonId(u);
  if (!pid) return false;
  return participantMatchesAnyAlias(pid, sid);
}

const userItems = computed(() => {
  const seen = new Set<string>();
  const out: { title: string; value: string }[] = [];
  for (const u of searchHitUsers.value) {
    if (!u.isActive) continue;
    if (userMatchesMe(u)) continue;
    const v = keeperPrimaryChatPersonId(u);
    if (!v || seen.has(v.toLowerCase())) continue;
    seen.add(v.toLowerCase());
    const name = `${u.firstName ?? ''} ${u.lastName ?? ''}`.trim();
    const base = (name || u.username || u.email || '').trim();
    const title = base || (v.length > 20 ? `${v.slice(0, 8)}…${v.slice(-4)}` : v);
    out.push({ title, value: v });
  }
  return out;
});

/** Seçili kişi arama sonucunda yoksa bile tek satır eklenir — alan başlığı id’ye dönüşmez. */
const dropdownItems = computed(() => {
  const base = userItems.value;
  const id = String(otherId.value ?? '').trim();
  if (!id) return base;
  const il = id.toLowerCase();
  if (base.some((it) => it.value.toLowerCase() === il)) return base;
  const cached = peerTitleById.value.get(il);
  if (cached) return [{ title: cached, value: id }, ...base];
  const fallback = displayNameForStoredPersonId(id);
  return [{ title: fallback, value: id }, ...base];
});

function rememberPeerTitle(id: string, title: string) {
  const k = id.trim().toLowerCase();
  const t = title.trim();
  if (!k || !t) return;
  const next = new Map(peerTitleById.value);
  next.set(k, t);
  peerTitleById.value = next;
}

function onPeerSelected(v: unknown) {
  if (v == null || v === '') {
    otherId.value = '';
    return;
  }
  const raw = v as string | { value?: string };
  const id =
    typeof raw === 'string'
      ? raw.trim()
      : String((raw as { value?: string }).value ?? '').trim();
  if (!id) {
    otherId.value = '';
    return;
  }
  otherId.value = id;
  const fromList = userItems.value.find((it) => it.value.toLowerCase() === id.toLowerCase());
  if (fromList?.title) rememberPeerTitle(id, fromList.title);
  else {
    const u = userStore.getUserById(id);
    if (u) {
      const name = `${u.firstName ?? ''} ${u.lastName ?? ''}`.trim();
      const label = (name || u.username || u.email || '').trim();
      if (label) rememberPeerTitle(id, label);
    }
  }
}

async function runUserSearch(term: string) {
  userSearchLoading.value = true;
  try {
    const queryParams = new URLSearchParams();
    queryParams.append('page', '1');
    queryParams.append('pageSize', '40');
    queryParams.append('isActive', 'true');
    queryParams.append('sortBy', 'username');
    queryParams.append('sortOrder', 'asc');
    const q = term.trim();
    if (q) queryParams.append('searchTerm', q);
    const response = await fetchFromMngKeeper(`/user?${queryParams.toString()}`, 'GET');
    if (response.IsSuccess === false) {
      throw new Error(response.ErrorMessage || 'Kullanıcılar yüklenemedi');
    }
    const usersArray = response.users || response.Users;
    if (usersArray && Array.isArray(usersArray)) {
      searchHitUsers.value = usersArray.map((u: Record<string, unknown>) => mapKeeperListUser(u));
    } else {
      searchHitUsers.value = [];
    }
  } catch (e) {
    console.error('[ChatRoomNewDmDialog] runUserSearch', e);
    searchHitUsers.value = [];
  } finally {
    userSearchLoading.value = false;
  }
}

function onUpdateSearch(q: string | null) {
  if (searchDebounce) clearTimeout(searchDebounce);
  const raw = (q ?? '').trim();
  searchDebounce = setTimeout(() => {
    searchDebounce = null;
    void runUserSearch(raw);
  }, 350);
}

watch(
  () => props.modelValue,
  (v) => {
    if (v) {
      otherId.value = '';
      peerTitleById.value = new Map();
      searchHitUsers.value = [];
      error.value = null;
      const sub = String(authStore.userInfo?.sub ?? '').trim();
      if (sub && !userStore.getUserById(sub)) {
        void userStore.fetchUserById(sub).catch(() => {});
      }
      void runUserSearch('');
    }
  }
);

onUnmounted(() => {
  if (searchDebounce) clearTimeout(searchDebounce);
});

function close() {
  emit('update:modelValue', false);
}

function mapStoreError(e: unknown): string {
  const code = typeof e === 'object' && e !== null && 'code' in e ? String((e as { code: string }).code) : '';
  if (code === 'NO_SESSION') return t('chatRoom.waNewDmErrNoSession');
  if (code === 'EMPTY_PEER') return t('chatRoom.waNewDmErrEmpty');
  if (code === 'NO_MY_MNG_PERSON_ID') return t('chatRoom.waNewDmErrNoMyPersonId');
  if (code === 'NO_PEER_MNG_PERSON_ID') return t('chatRoom.waNewDmErrNoPeerPersonId');
  if (code === 'SELF_DM') return t('chatRoom.waNewDmErrSelf');
  if (e instanceof Error) return e.message;
  return String(e);
}

function normalizedOtherId(): string {
  const v = otherId.value;
  if (typeof v === 'string') return v.trim();
  if (v != null && typeof v === 'object' && 'value' in (v as object))
    return String((v as { value: string }).value ?? '').trim();
  return String(v ?? '').trim();
}

async function submit() {
  error.value = null;
  busy.value = true;
  try {
    const row = await store.ensureDirectConversation(normalizedOtherId());
    emit('created', row);
    close();
  } catch (e: unknown) {
    error.value = mapStoreError(e);
  } finally {
    busy.value = false;
  }
}
</script>

<template>
  <v-dialog :model-value="modelValue" max-width="480" @update:model-value="emit('update:modelValue', $event)">
    <v-card rounded="lg">
      <v-card-title class="text-h6">{{ t('chatRoom.waNewDmTitle') }}</v-card-title>
      <v-card-text>
        <p class="text-body-2 text-medium-emphasis mb-4">{{ t('chatRoom.waNewDmHint') }}</p>
        <v-autocomplete
          v-model="otherId"
          :items="dropdownItems"
          item-title="title"
          item-value="value"
          :return-object="false"
          :custom-filter="() => true"
          variant="outlined"
          density="comfortable"
          :label="t('chatRoom.waNewDmLabel')"
          :hint="t('chatRoom.waNewDmComboboxHint')"
          persistent-hint
          autocomplete="off"
          clearable
          hide-details="auto"
          :loading="userSearchLoading"
          :menu-props="{ maxHeight: 320 }"
          :no-data-text="t('chatRoom.waNewDmNoUsers')"
          @update:model-value="onPeerSelected"
          @update:search="onUpdateSearch"
          @keyup.enter="submit"
        />
        <v-alert v-if="error" type="error" variant="tonal" density="compact" class="mt-3">
          {{ error }}
        </v-alert>
      </v-card-text>
      <v-card-actions class="px-4 pb-4">
        <v-spacer />
        <v-btn variant="text" :disabled="busy" @click="close">{{ t('chatRoom.waNewDmCancel') }}</v-btn>
        <v-btn color="primary" :loading="busy" :disabled="!normalizedOtherId()" @click="submit">
          {{ t('chatRoom.waNewDmStart') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>
