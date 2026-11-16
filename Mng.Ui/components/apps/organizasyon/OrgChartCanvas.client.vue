<script setup lang="ts">
import { computed, nextTick, onMounted, ref, watch } from 'vue';
import { MarkerType, useVueFlow, VueFlow, type Node, type Edge } from '@vue-flow/core';
import { Background } from '@vue-flow/background';
import { Controls } from '@vue-flow/controls';
import { MiniMap } from '@vue-flow/minimap';
import { fetchFromMngKeeper } from '@/services/apiService';
import {
  SEAT_W,
  STAFF_PREVIEW,
  descendantIds,
  layoutSeats,
  seatAtPoint,
  type SeatRow,
} from '@/utils/org/seatLayout';
import { keeperProfileBody, planOrgPublish } from '@/utils/org/publishSeats';
import MngDirectoryPickerField from '@/components/shared/directory/MngDirectoryPickerField.vue';
import OrgStaffList from '@/components/apps/organizasyon/OrgStaffList.vue';

type UserOption = { id: string; title: string };

const auth = useAuthStore();
const config = useRuntimeConfig();
const { fitView } = useVueFlow();

const seats = ref<SeatRow[]>([]);
const users = ref<UserOption[]>([]);
const selectedId = ref('');
const loading = ref(false);
const saving = ref(false);
const notice = ref('');
const noticeColor = ref<'error' | 'success'>('error');
const publishOpen = ref(false);
const editorOpen = ref(false);
const form = ref({ departman: '', sira: 0, oturan: '', calisanlar: [] as string[] });

const flowNodes = ref<Node[]>([]);
const flowEdges = ref<Edge[]>([]);

const selected = computed(() => seats.value.find((row) => row.id === selectedId.value) || null);
const childCount = computed(() => seats.value.filter((row) => row.ust === selectedId.value).length);
const hasRoot = computed(() => seats.value.some((row) => !row.ust));
const hasPeople = computed(() => seats.value.some((row) => row.oturan || row.calisanlar.length > 0));

const userTitle = computed(() => {
  const map = new Map(users.value.map((user) => [user.id, user.title]));
  return (id: string) => map.get(id) || '';
});

function gateway() {
  return String(config.public.gatewayUrl || '').replace(/\/$/, '');
}

function asRows(payload: unknown): Record<string, unknown>[] {
  if (Array.isArray(payload)) return payload as Record<string, unknown>[];
  if (payload && typeof payload === 'object') {
    const data = (payload as { data?: unknown }).data;
    if (Array.isArray(data)) return data as Record<string, unknown>[];
    if (data && typeof data === 'object') return [data as Record<string, unknown>];
  }
  return [];
}

function refId(value: unknown): string {
  if (!value) return '';
  if (typeof value === 'string') return value.trim();
  if (Array.isArray(value)) return refId(value[0]);
  if (typeof value === 'object') {
    const row = value as Record<string, unknown>;
    return String(row.__dataId || row.id || row.userId || row.dataId || '');
  }
  return '';
}

function refIds(value: unknown): string[] {
  if (!value) return [];
  const source = Array.isArray(value) ? value : [value];
  const ids: string[] = [];
  for (const item of source) {
    const id = refId(item);
    if (id && !ids.includes(id)) ids.push(id);
  }
  return ids;
}

function toSeat(row: Record<string, unknown>): SeatRow {
  return {
    id: String(row.__dataId || row.dataId || ''),
    ad: String(row.ad || ''),
    ust: refId(row.ust),
    departman: String(row.departman || ''),
    oturan: refId(row.oturan),
    calisanlar: refIds(row.calisanlar),
    sira: Number(row.sira || 0),
  };
}

function messageOf(error: unknown) {
  const data = (error as {
    data?: {
      error?: { message?: string; details?: { message?: string }[] };
      errorMessage?: string;
      ErrorMessage?: string;
    };
    message?: string;
  })?.data;
  const detail = data?.error?.details?.find((item) => item.message)?.message;
  return detail
    || data?.error?.message
    || data?.errorMessage
    || data?.ErrorMessage
    || (error instanceof Error ? error.message : '')
    || 'Kayıt yazılamadı.';
}

async function dg(path: string, method = 'GET', body?: object) {
  return $fetch(`${gateway()}${path}`, {
    method,
    body,
    headers: { Authorization: `Bearer ${auth.accessToken}` },
  });
}

function boxHeadline(seat: SeatRow) {
  return seat.departman.trim();
}

function personInitial(name: string) {
  const clean = name.trim();
  if (!clean || clean === 'Boş koltuk' || clean === 'Kişi') return '';
  return clean.charAt(0).toLocaleUpperCase('tr');
}

function paint() {
  const boxes = layoutSeats(seats.value);
  const byId = new Map(seats.value.map((row) => [row.id, row]));
  flowNodes.value = boxes.map((box) => {
    const seat = byId.get(box.id)!;
    const staffNames = seat.calisanlar.map((id) => userTitle.value(id) || 'Kişi');
    return {
      id: seat.id,
      type: 'seat',
      position: { x: box.x, y: box.y },
      data: {
        ad: boxHeadline(seat) || 'Departman',
        unnamed: !boxHeadline(seat),
        oturan: userTitle.value(seat.oturan) || (seat.oturan ? 'Kişi' : 'Boş koltuk'),
        staff: staffNames.slice(0, STAFF_PREVIEW),
        staffAll: staffNames,
        staffMore: Math.max(0, staffNames.length - STAFF_PREVIEW),
        h: box.h,
        empty: !seat.oturan,
      },
      draggable: true,
      connectable: false,
    };
  });
  flowEdges.value = seats.value
    .filter((row) => row.ust && byId.has(row.ust))
    .map((row) => ({
      id: `${row.ust}-${row.id}`,
      source: row.ust,
      target: row.id,
      type: 'smoothstep',
      selectable: false,
      updatable: false,
      markerEnd: MarkerType.ArrowClosed,
    }));
}

async function load() {
  loading.value = true;
  try {
    const payload = await dg('/data/api/v1/data/org_mevkiler?limit=500&expand=false');
    seats.value = asRows(payload).map(toSeat).filter((row) => row.id);
    if (selectedId.value && !seats.value.some((row) => row.id === selectedId.value)) selectedId.value = '';
    paint();
    await nextTick();
    if (seats.value.length) fitView({ padding: 0.2, duration: 180 });
  } catch (error: unknown) {
    show(messageOf(error));
  } finally {
    loading.value = false;
  }
}

async function loadUsers() {
  const response = await fetchFromMngKeeper('/user?page=1&pageSize=500&isActive=true&includeInApplication=true', 'GET');
  const list = (response.users || response.Users || []) as Record<string, unknown>[];
  users.value = list.map((user) => {
    const id = String(user.id || user.userId || user.Id || '');
    const name = [user.firstName || user.FirstName, user.lastName || user.LastName].filter(Boolean).join(' ');
    const username = String(user.username || user.Username || '');
    return { id, title: name || username || id };
  }).filter((user) => user.id);
}

function show(text: string, color: 'error' | 'success' = 'error') {
  notice.value = text;
  noticeColor.value = color;
}

function copyForm(row: SeatRow | null) {
  form.value = {
    departman: row?.departman || '',
    sira: row?.sira || 0,
    oturan: row?.oturan || '',
    calisanlar: [...(row?.calisanlar || [])],
  };
}

function takenElsewhere(personId: string) {
  return seats.value.some((row) =>
    row.id !== selectedId.value
    && (row.oturan === personId || row.calisanlar.includes(personId)),
  );
}

function acceptedStaff(ids: string[]) {
  const next: string[] = [];
  let sameSeat = false;
  let otherSeat = false;
  for (const id of ids) {
    if (!id || next.includes(id)) continue;
    if (id === form.value.oturan) {
      sameSeat = true;
      continue;
    }
    if (takenElsewhere(id)) {
      otherSeat = true;
      continue;
    }
    next.push(id);
  }
  const message = sameSeat
    ? 'Oturan kişi çalışanlar arasında olamaz.'
    : otherSeat
      ? 'Bu kullanıcı başka bir mevkide oturuyor.'
      : '';
  return { next, message };
}

watch(selected, (row) => {
  copyForm(row);
  if (!row) editorOpen.value = false;
}, { immediate: true });

function openEditor(id: string) {
  const row = seats.value.find((seat) => seat.id === id) || null;
  if (!row) return;
  selectedId.value = id;
  copyForm(row);
  editorOpen.value = true;
}

watch(
  () => form.value.oturan,
  (id, previous) => {
    if (!id) return;
    if (form.value.calisanlar.includes(id)) {
      form.value.oturan = previous && previous !== id ? previous : '';
      show('Oturan kişi çalışanlar arasında olamaz.');
      return;
    }
    if (!takenElsewhere(id)) return;
    form.value.oturan = previous && previous !== id ? previous : '';
    show('Bu kullanıcı başka bir mevkide oturuyor.');
  }
);

watch(
  () => form.value.calisanlar.join('\u0001'),
  () => {
    const { next, message } = acceptedStaff(form.value.calisanlar);
    const same = next.length === form.value.calisanlar.length
      && next.every((id, index) => id === form.value.calisanlar[index]);
    if (same) return;
    form.value.calisanlar = next;
    if (message) show(message);
  }
);

async function write(path: string, method: string, body?: object) {
  saving.value = true;
  try {
    await dg(path, method, body);
    await load();
    return true;
  } catch (error: unknown) {
    show(messageOf(error));
    paint();
    return false;
  } finally {
    saving.value = false;
  }
}

async function createRoot() {
  const ok = await write('/data/api/v1/data/org_mevkiler', 'POST', { ad: 'Kök mevki', sira: 1 });
  if (ok) show('Kök mevki açıldı.', 'success');
}

async function createChild() {
  if (!selected.value) return;
  const siblings = seats.value.filter((row) => row.ust === selected.value!.id);
  const sira = siblings.reduce((max, row) => Math.max(max, row.sira), 0) + 1;
  const ok = await write('/data/api/v1/data/org_mevkiler', 'POST', {
    ad: 'Yeni mevki',
    ust: selected.value.id,
    sira,
  });
  if (ok) show('Alt mevki açıldı.', 'success');
}

async function saveSeat() {
  if (!selected.value) return;
  const staff = acceptedStaff(form.value.calisanlar);
  if (staff.message) {
    form.value.calisanlar = staff.next;
    show(staff.message);
    return;
  }
  if (form.value.oturan && takenElsewhere(form.value.oturan)) {
    show('Bu kullanıcı başka bir mevkide oturuyor.');
    return;
  }
  const ok = await write(`/data/api/v1/data/org_mevkiler/${selected.value.id}`, 'PUT', {
    departman: form.value.departman.trim(),
    sira: Number(form.value.sira) || 0,
    oturan: form.value.oturan || '',
    calisanlar: staff.next,
  });
  if (!ok) return;
  editorOpen.value = false;
  show('Mevki kaydedildi.', 'success');
}

async function clearSeat() {
  if (!selected.value) return;
  form.value.oturan = '';
  await saveSeat();
}

async function removeSeat() {
  if (!selected.value || childCount.value > 0) return;
  const ok = await write(`/data/api/v1/data/org_mevkiler/${selected.value.id}`, 'DELETE');
  if (ok) {
    selectedId.value = '';
    show('Mevki silindi.', 'success');
  }
}

function personCards(payload: unknown) {
  return asRows(payload).map((row) => ({
    id: String(row.__dataId || row.dataId || ''),
    keeperId: refId(row.keeperKisiId),
  })).filter((row) => row.id);
}

async function keeper(path: string, method = 'GET', body?: object) {
  return $fetch(`/api/keeper${path}`, {
    method,
    body,
    headers: { Authorization: `Bearer ${auth.accessToken}` },
  });
}

function askPublish() {
  const plan = planOrgPublish(seats.value, []);
  if (plan.error) {
    show(plan.error);
    return;
  }
  publishOpen.value = true;
}

async function publish() {
  publishOpen.value = false;
  saving.value = true;
  try {
    const payload = await dg('/data/api/v1/data/izin_personel?limit=500&expand=false');
    const plan = planOrgPublish(seats.value, personCards(payload));
    if (plan.error) {
      show(plan.error);
      return;
    }
    for (const item of plan.users) {
      const response = await keeper(`/user/${item.userId}`) as { user?: Record<string, unknown>; User?: Record<string, unknown> };
      const card = response.user || response.User;
      if (!card) {
        show('Kişi kartı alınamadı.');
        return;
      }
      const currentTitle = card.title !== undefined ? card.title : card.Title;
      const title = item.title == null ? (currentTitle == null ? '' : String(currentTitle)) : item.title;
      await keeper(`/user/${item.userId}`, 'PUT', keeperProfileBody(card, title, item.department));
    }
    for (const item of plan.managers) {
      await dg(`/data/api/v1/data/izin_personel/${item.dataId}`, 'PUT', { yoneticiId: item.yoneticiId });
    }
    const cardNote = plan.withoutCard
      ? ` ${plan.withoutCard} kişinin izin kartı yok.`
      : '';
    show(`${plan.users.length} kişi yayınlandı.${cardNote}`, 'success');
  } catch (error: unknown) {
    show(messageOf(error));
  } finally {
    saving.value = false;
  }
}

async function onDragStop(event: { node: Node }) {
  const node = event.node;
  const boxes = layoutSeats(seats.value);
  const height = Number(node.data?.h) || 92;
  const centerX = node.position.x + SEAT_W / 2;
  const centerY = node.position.y + height / 2;
  const target = seatAtPoint(boxes, centerX, centerY, node.id);
  const current = seats.value.find((row) => row.id === node.id);
  if (!target || !current || target.id === current.ust) {
    paint();
    return;
  }
  if (descendantIds(seats.value, current.id).has(target.id)) {
    paint();
    show('Mevki kendi altının altına bağlanamaz.');
    return;
  }
  const siblings = seats.value.filter((row) => row.ust === target.id && row.id !== current.id);
  const sira = siblings.reduce((max, row) => Math.max(max, row.sira), 0) + 1;
  const ok = await write(`/data/api/v1/data/org_mevkiler/${current.id}`, 'PUT', { ust: target.id, sira });
  if (ok) show('Mevki taşındı.', 'success');
}

onMounted(async () => {
  await Promise.all([load(), loadUsers().catch(() => show('Kişi listesi alınamadı.'))]);
  paint();
});
</script>

<template>
  <div>
    <div class="d-flex flex-wrap align-center ga-2 mb-3">
      <v-btn v-if="!hasRoot" color="primary" :disabled="saving" @click="createRoot">Kök mevki</v-btn>
      <v-btn variant="tonal" :disabled="saving || !selected" @click="createChild">Alt mevki</v-btn>
      <v-btn variant="text" :disabled="saving || !selected || childCount > 0" @click="removeSeat">Sil</v-btn>
      <div class="org-legend">
        <span><i class="swatch occupant" /> Oturan</span>
        <span><i class="swatch staff" /> Çalışan</span>
      </div>
      <v-spacer />
      <v-btn variant="outlined" :disabled="saving || !hasPeople" @click="askPublish">Yayınla</v-btn>
    </div>
    <p class="text-body-2 text-medium-emphasis mb-3">
      Kutuyu başka kutunun üzerine bırakınca o mevkinin altına bağlanır.
    </p>
    <v-alert v-if="notice" :type="noticeColor" variant="tonal" class="mb-3" closable @click:close="notice = ''">
      {{ notice }}
    </v-alert>
    <v-dialog v-model="publishOpen" max-width="480">
      <v-card class="pa-4">
        <div class="text-h6 mb-2">Yayın</div>
        <p class="text-body-2 mb-4">
          Oturan kişinin unvanı ve departmanı, kutunun departmanından yazılır. Çalışanın departmanı yazılır, unvanı durur.
          İzin kartı varsa oturanın yöneticisi üst koltuktaki kişidir. Çalışanın yöneticisi oturandır.
          Oturan boşsa çalışanın yöneticisi bir üst koltuğun oturanı olur.
        </p>
        <div class="d-flex justify-end ga-2">
          <v-btn variant="text" @click="publishOpen = false">Vazgeç</v-btn>
          <v-btn color="primary" :loading="saving" @click="publish">Yayınla</v-btn>
        </div>
      </v-card>
    </v-dialog>
    <v-dialog v-model="editorOpen" max-width="560" :persistent="saving">
      <v-card v-if="selected" class="pa-4">
        <div class="text-h6 mb-3">{{ boxHeadline(selected) || 'Departman' }}</div>
        <v-text-field v-model="form.departman" label="Departman" density="comfortable" />
        <v-text-field v-model.number="form.sira" type="number" min="0" label="Sıra" density="comfortable" />
        <MngDirectoryPickerField
          v-model="form.oturan"
          entity="user"
          label="Oturan"
          density="comfortable"
          variant="outlined"
        />
        <MngDirectoryPickerField
          v-model="form.calisanlar"
          entity="user"
          multiple
          label="Çalışanlar"
          density="comfortable"
          variant="outlined"
        />
        <div class="d-flex flex-wrap ga-2">
          <v-btn color="primary" :loading="saving" @click="saveSeat">Kaydet</v-btn>
          <v-btn variant="text" :disabled="saving || !form.oturan" @click="clearSeat">Koltuğu boşalt</v-btn>
          <v-spacer />
          <v-btn variant="text" :disabled="saving" @click="editorOpen = false">Vazgeç</v-btn>
        </div>
      </v-card>
    </v-dialog>
    <v-card variant="outlined" class="rounded-lg org-canvas" :loading="loading">
          <VueFlow
            class="org-flow"
            :nodes="flowNodes"
            :edges="flowEdges"
            :nodes-connectable="false"
            :edges-updatable="false"
            fit-view-on-init
            @node-click="selectedId = $event.node.id"
            @pane-click="selectedId = ''"
            @node-drag-stop="onDragStop"
          >
            <Background pattern-color="rgba(120,130,150,.22)" :gap="20" />
            <Controls position="bottom-left" />
            <MiniMap position="bottom-right" pannable zoomable />
            <template #node-seat="{ data, id }">
              <div
                class="seat"
                role="group"
                :aria-label="data.ad"
                :class="{ selected: id === selectedId, empty: data.empty }"
                :style="{ minHeight: `${data.h}px`, width: `${SEAT_W}px` }"
              >
                <div class="seat-accent" />
                <div class="seat-body">
                  <button
                    type="button"
                    class="seat-title nodrag nopan"
                    :class="{ 'is-empty': data.unnamed }"
                    @click.stop="openEditor(id)"
                    @mousedown.stop
                    @pointerdown.stop
                  >
                    {{ data.ad }}
                  </button>
                  <div class="person occupant" :class="{ vacant: data.empty }">
                    <span v-if="data.empty" class="avatar occupant">
                      <v-icon size="16" icon="mdi-account-outline" />
                    </span>
                    <span v-else class="avatar occupant">{{ personInitial(data.oturan) }}</span>
                    <span class="person-copy">
                      <span class="role-label">Oturan</span>
                      <span class="role-name">{{ data.oturan }}</span>
                    </span>
                  </div>
                  <div v-if="data.staff.length" class="staff-block">
                    <div class="role-label staff-label">
                      <v-icon size="14" icon="mdi-account-multiple-outline" />
                      Çalışanlar
                    </div>
                    <ul class="seat-staff">
                      <li v-for="(name, index) in data.staff" :key="`${id}-${index}`">
                        <span class="avatar staff">{{ personInitial(name) }}</span>
                        <span>{{ name }}</span>
                      </li>
                    </ul>
                    <v-menu
                      v-if="data.staffMore"
                      location="end"
                      offset="8"
                      :close-on-content-click="false"
                    >
                      <template #activator="{ props: menuProps }">
                        <button
                          type="button"
                          class="seat-more nodrag nopan"
                          v-bind="menuProps"
                          @mousedown.stop
                          @pointerdown.stop
                        >
                          ve {{ data.staffMore }} kişi
                        </button>
                      </template>
                      <v-card elevation="8">
                        <OrgStaffList :names="data.staffAll" />
                      </v-card>
                    </v-menu>
                  </div>
                </div>
              </div>
            </template>
          </VueFlow>
        </v-card>
  </div>
</template>

<style scoped>
.org-canvas {
  height: calc(100vh - 268px);
  min-height: 480px;
}
.org-flow {
  width: 100%;
  height: 100%;
}
.org-flow :deep(.vue-flow__node-seat) {
  padding: 0;
  border: none;
  background: transparent;
  box-shadow: none;
}
.org-legend {
  display: flex;
  align-items: center;
  gap: 14px;
  color: rgba(var(--v-theme-on-surface), 0.7);
  font-size: 12px;
}
.org-legend span {
  display: inline-flex;
  align-items: center;
  gap: 6px;
}
.swatch {
  width: 10px;
  height: 10px;
  border-radius: 50%;
}
.swatch.occupant {
  background: rgb(var(--v-theme-primary));
}
.swatch.staff {
  background: rgb(var(--v-theme-secondary));
}
.seat {
  overflow: hidden;
  border: 1px solid rgba(var(--v-theme-on-surface), 0.12);
  border-radius: 14px;
  background: rgb(var(--v-theme-surface));
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.08);
}
.seat-accent {
  height: 4px;
  background: linear-gradient(90deg, rgb(var(--v-theme-primary)), rgb(var(--v-theme-secondary)));
}
.seat-body {
  padding: 12px 12px 12px;
}
.seat.selected {
  border-color: rgb(var(--v-theme-primary));
  box-shadow: 0 0 0 2px rgba(var(--v-theme-primary), 0.22), 0 10px 24px rgba(15, 23, 42, 0.08);
}
.seat-title {
  display: block;
  width: 100%;
  margin: 0;
  padding: 0;
  border: 0;
  background: transparent;
  color: inherit;
  font: inherit;
  font-size: 14px;
  font-weight: 700;
  line-height: 1.3;
  text-align: left;
  cursor: pointer;
}
.seat-title:hover {
  color: rgb(var(--v-theme-primary));
}
.seat-title.is-empty {
  color: rgba(var(--v-theme-on-surface), 0.45);
  font-weight: 600;
}
.seat-meta {
  margin-top: 2px;
  color: rgba(var(--v-theme-on-surface), 0.62);
  font-size: 11px;
  line-height: 14px;
}
.person {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-top: 10px;
}
.person-copy {
  display: flex;
  min-width: 0;
  flex-direction: column;
}
.role-label {
  color: rgb(var(--v-theme-primary));
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 0.04em;
  line-height: 12px;
  text-transform: uppercase;
}
.role-name,
.seat-staff li span:last-child {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.role-name {
  font-size: 13px;
  font-weight: 600;
  line-height: 18px;
}
.person.vacant .role-name {
  color: rgba(var(--v-theme-on-surface), 0.55);
  font-weight: 500;
}
.avatar {
  display: inline-flex;
  flex: 0 0 auto;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  font-size: 12px;
  font-weight: 700;
}
.avatar.occupant {
  width: 32px;
  height: 32px;
  background: rgb(var(--v-theme-primary));
  color: rgb(var(--v-theme-on-primary));
}
.person.vacant .avatar.occupant {
  background: transparent;
  color: rgba(var(--v-theme-on-surface), 0.45);
  border: 1px dashed rgba(var(--v-theme-on-surface), 0.28);
}
.staff-block {
  margin-top: 8px;
  padding-top: 8px;
  border-top: 1px solid rgba(var(--v-theme-on-surface), 0.08);
}
.staff-label {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  color: rgb(var(--v-theme-secondary));
}
.seat-staff {
  margin: 6px 0 0;
  padding: 0;
  list-style: none;
}
.seat-staff li {
  display: flex;
  align-items: center;
  gap: 8px;
  min-width: 0;
  margin-top: 4px;
  color: rgba(var(--v-theme-on-surface), 0.86);
  font-size: 12px;
  line-height: 18px;
}
.avatar.staff {
  width: 22px;
  height: 22px;
  background: rgba(var(--v-theme-secondary), 0.16);
  color: rgb(var(--v-theme-secondary));
  font-size: 11px;
}
.seat-more {
  display: inline-flex;
  margin-top: 4px;
  margin-left: 30px;
  padding: 0;
  border: 0;
  background: transparent;
  color: rgb(var(--v-theme-secondary));
  font-size: 11px;
  font-weight: 700;
  line-height: 16px;
  cursor: pointer;
  text-decoration: underline;
  text-underline-offset: 2px;
}
</style>

<style>
@import '@vue-flow/core/dist/style.css';
@import '@vue-flow/core/dist/theme-default.css';
@import '@vue-flow/controls/dist/style.css';
@import '@vue-flow/minimap/dist/style.css';
</style>
