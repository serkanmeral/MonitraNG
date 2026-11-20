<script setup lang="ts">
import { loginToMngKeeper } from '@/services/apiService';
import { resolveThemedDomainLogoSrc } from '@/composables/useDomain';
import { currentLoginHostDomain, fetchDomainBranding, fetchSoleDomainBranding, type DomainBranding } from '@/utils/loginHostDomain';

definePageMeta({ layout: 'blank' });

type DoorWelcome = {
  displayName?: string;
  fullName?: string;
  greeting?: string;
  direction?: string;
};

type CodePayload = {
  nonce: string;
  url: string;
  expiresAt: string;
  doorTitle: string;
  welcome?: DoorWelcome | null;
  boardStamp?: number;
};

type BoardCard = { key: string; label: string; value: number };
type DayRow = { name: string; entry: string; exit: string };
type BreakRow = { name: string; started: string; startedAt: string };

type BoardPayload = {
  title?: string | null;
  cards?: BoardCard[] | null;
  day?: DayRow[] | null;
  breaks?: BreakRow[] | null;
  refreshSeconds?: number | null;
};

type Place = { key: string; title: string };

type ScreenBinding = {
  doorKey: string;
  kioskToken: string;
  doorTitle?: string;
  locationTitle?: string;
};

const STORAGE_KEY = 'pdks.screen';
const config = useRuntimeConfig();

const binding = ref<ScreenBinding | null>(null);
const status = ref<'unbound' | 'loading' | 'ready' | 'offline' | 'unknown'>('loading');
const message = ref('');
const doorTitle = ref('');
const locationTitle = ref('');
const qrUrl = ref('');
const welcome = ref<DoorWelcome | null>(null);
const boardTitle = ref('');
const boardCards = ref<BoardCard[]>([]);
const dayRows = ref<DayRow[]>([]);
const breakRows = ref<BreakRow[]>([]);
const seenStamp = ref<number | null>(null);

const panelOpen = ref(false);
const step = ref<'login' | 'place'>('login');
const username = ref('');
const password = ref('');
const domain = ref('');
const domainLocked = ref(false);
const branding = ref<DomainBranding | null>(null);
const panelError = ref('');
const panelBusy = ref(false);
const managerToken = ref('');
const locations = ref<Place[]>([]);
const doors = ref<Place[]>([]);
const locationKey = ref('');
const doorKeyPick = ref('');

let timer: ReturnType<typeof setInterval> | null = null;
let boardTimer: ReturnType<typeof setTimeout> | null = null;

const brandTitle = computed(() => branding.value?.displayName || branding.value?.name || '');
const logoSrc = computed(() => resolveThemedDomainLogoSrc(branding.value, 'DARK_BLUE'));
const now = ref(new Date());
let clockTimer: ReturnType<typeof setInterval> | null = null;
const clockText = computed(() =>
  new Intl.DateTimeFormat('tr-TR', {
    timeZone: 'Europe/Istanbul',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  }).format(now.value),
);
const dateText = computed(() =>
  new Intl.DateTimeFormat('tr-TR', {
    timeZone: 'Europe/Istanbul',
    weekday: 'long',
    day: 'numeric',
    month: 'long',
  }).format(now.value),
);

function welcomeKind(direction?: string | null) {
  if (direction === 'out') return 'Çıkış';
  if (direction === 'break') return 'Mola';
  if (direction === 'return') return 'Dönüş';
  return 'Giriş';
}

function breakLength(value?: string) {
  const start = Date.parse(value || '');
  if (!Number.isFinite(start)) return '';
  const minutes = Math.max(0, Math.floor((now.value.getTime() - start) / 60000));
  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  return hours > 0 ? `${hours} sa ${rest} dk` : `${rest} dk`;
}

function extensionsBase() {
  const gateway = String(config.public.gatewayUrl || '').replace(/\/$/, '');
  return import.meta.dev ? 'http://127.0.0.1:5092' : `${gateway}/extensions`;
}

function readBinding(): ScreenBinding | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    const parsed = JSON.parse(raw) as ScreenBinding;
    if (!parsed.doorKey || !parsed.kioskToken) return null;
    return parsed;
  } catch {
    return null;
  }
}

function writeBinding(value: ScreenBinding) {
  localStorage.setItem(STORAGE_KEY, JSON.stringify(value));
  binding.value = value;
  doorTitle.value = value.doorTitle || '';
  locationTitle.value = value.locationTitle || '';
}

function clearBinding() {
  localStorage.removeItem(STORAGE_KEY);
  binding.value = null;
  qrUrl.value = '';
  welcome.value = null;
  boardTitle.value = '';
  boardCards.value = [];
  dayRows.value = [];
  breakRows.value = [];
}

async function drawQr(url: string) {
  if (!url) {
    qrUrl.value = '';
    return;
  }
  const QRCode = (await import('qrcode')).default;
  qrUrl.value = await QRCode.toDataURL(url, { margin: 1, width: 280, errorCorrectionLevel: 'M' });
}

async function refresh() {
  const current = binding.value;
  if (!current) {
    status.value = 'unbound';
    message.value = 'Bu ekran bağlanmamış.';
    qrUrl.value = '';
    return;
  }

  const url = `${extensionsBase()}/api/v1/pdks/doors/${encodeURIComponent(current.doorKey)}/code?k=${encodeURIComponent(current.kioskToken)}`;
  try {
    const payload = await $fetch<CodePayload>(url);
    doorTitle.value = payload.doorTitle || current.doorTitle || current.doorKey;
    welcome.value = payload.welcome?.greeting || payload.welcome?.fullName || payload.welcome?.displayName
      ? payload.welcome
      : null;
    await drawQr(payload.url);
    status.value = 'ready';
    message.value = '';
    const stamp = Number(payload.boardStamp ?? 0);
    if (seenStamp.value !== stamp) {
      seenStamp.value = stamp;
      void refreshBoard();
    }
  } catch (error: unknown) {
    const statusCode = (error as { statusCode?: number; status?: number })?.statusCode
      ?? (error as { status?: number })?.status;
    qrUrl.value = '';
    if (statusCode === 404) {
      clearBinding();
      status.value = 'unbound';
      message.value = 'Bağ geçersiz. Ekranı yeniden bağlayın.';
      return;
    }
    if (statusCode === 409) {
      status.value = 'unknown';
      message.value = 'Kapı pasif.';
      return;
    }
    status.value = 'offline';
    message.value = 'Bağlantı yok.';
  }
}

function boardDelay(seconds?: number | null) {
  const value = Number(seconds);
  if (!Number.isFinite(value) || value <= 0) return 60000;
  return Math.min(600, Math.max(15, Math.round(value))) * 1000;
}

function queueBoard(seconds?: number | null) {
  if (boardTimer) clearTimeout(boardTimer);
  boardTimer = setTimeout(() => {
    refreshBoard();
  }, boardDelay(seconds));
}

async function refreshBoard() {
  const current = binding.value;
  if (!current) {
    boardTitle.value = '';
    boardCards.value = [];
    dayRows.value = [];
    breakRows.value = [];
    return;
  }

  let seconds = 60;
  const url = `${extensionsBase()}/api/v1/pdks/doors/${encodeURIComponent(current.doorKey)}/board?k=${encodeURIComponent(current.kioskToken)}`;
  try {
    const payload = await $fetch<BoardPayload>(url);
    boardTitle.value = payload.title || '';
    boardCards.value = Array.isArray(payload.cards) ? payload.cards : [];
    dayRows.value = Array.isArray(payload.day) ? payload.day : [];
    breakRows.value = Array.isArray(payload.breaks) ? payload.breaks : [];
    if (payload.refreshSeconds) seconds = payload.refreshSeconds;
  } catch {
    boardTitle.value = '';
    boardCards.value = [];
    dayRows.value = [];
    breakRows.value = [];
  }
  queueBoard(seconds);
}

function startPolling() {
  if (timer) clearInterval(timer);
  if (boardTimer) clearTimeout(boardTimer);
  refresh();
  refreshBoard();
  timer = setInterval(refresh, 3000);
}

function openPanel() {
  panelOpen.value = true;
  step.value = 'login';
  panelError.value = '';
  password.value = '';
  managerToken.value = '';
  locations.value = [];
  doors.value = [];
  locationKey.value = '';
  doorKeyPick.value = '';
}

function closePanel() {
  panelOpen.value = false;
  managerToken.value = '';
  password.value = '';
  panelBusy.value = false;
}

function splitAccount() {
  const typed = username.value.trim();
  let selectedDomain = domain.value.trim();
  let selectedUsername = typed;
  if (typed.includes('@')) {
    const parts = typed.split('@');
    if (parts.length === 2 && parts[0] && parts[1]) {
      selectedDomain = parts[0];
      selectedUsername = parts[1];
    }
  }
  return { selectedDomain, selectedUsername };
}

async function signIn() {
  panelError.value = '';
  panelBusy.value = true;
  try {
    const account = splitAccount();
    const session = await loginToMngKeeper(account.selectedUsername, password.value, account.selectedDomain || undefined);
    password.value = '';
    managerToken.value = session.accessToken;
    locations.value = await $fetch<Place[]>(`${extensionsBase()}/api/v1/pdks/locations`, {
      headers: { Authorization: `Bearer ${managerToken.value}` },
    });
    step.value = 'place';
  } catch (error: unknown) {
    managerToken.value = '';
    const data = (error as { data?: { message?: string } })?.data;
    panelError.value = data?.message || (error instanceof Error ? error.message : 'Giriş başarısız.');
  } finally {
    panelBusy.value = false;
  }
}

async function loadDoors() {
  doorKeyPick.value = '';
  doors.value = [];
  if (!locationKey.value || !managerToken.value) return;
  panelBusy.value = true;
  panelError.value = '';
  try {
    doors.value = await $fetch<Place[]>(
      `${extensionsBase()}/api/v1/pdks/locations/${encodeURIComponent(locationKey.value)}/doors`,
      { headers: { Authorization: `Bearer ${managerToken.value}` } },
    );
  } catch (error: unknown) {
    const data = (error as { data?: { message?: string } })?.data;
    panelError.value = data?.message || 'Kapılar alınamadı.';
  } finally {
    panelBusy.value = false;
  }
}

async function bindScreen() {
  if (!doorKeyPick.value || !managerToken.value) return;
  panelBusy.value = true;
  panelError.value = '';
  try {
    const bound = await $fetch<ScreenBinding & { locationKey?: string }>(
      `${extensionsBase()}/api/v1/pdks/doors/${encodeURIComponent(doorKeyPick.value)}/bind`,
      { method: 'POST', headers: { Authorization: `Bearer ${managerToken.value}` } },
    );
    writeBinding({
      doorKey: bound.doorKey,
      kioskToken: bound.kioskToken,
      doorTitle: bound.doorTitle,
      locationTitle: bound.locationTitle,
    });
    managerToken.value = '';
    closePanel();
    opened = false;
    lastSeen = '';
    startPolling();
  } catch (error: unknown) {
    const data = (error as { data?: { message?: string } })?.data;
    panelError.value = data?.message || 'Ekran bağlanamadı.';
  } finally {
    panelBusy.value = false;
  }
}

async function loadBrand() {
  const host = currentLoginHostDomain();
  const sole = await fetchSoleDomainBranding();
  if (host) {
    domain.value = host;
    domainLocked.value = true;
    branding.value = (await fetchDomainBranding(host)) || (sole?.name === host ? sole : null);
    return;
  }
  if (sole?.name) {
    domain.value = sole.name;
    domainLocked.value = true;
    branding.value = sole;
    return;
  }
  domain.value = '';
  domainLocked.value = false;
}

onMounted(() => {
  binding.value = readBinding();
  locationTitle.value = binding.value?.locationTitle || '';
  doorTitle.value = binding.value?.doorTitle || '';
  clockTimer = setInterval(() => {
    now.value = new Date();
  }, 1000);
  loadBrand();
  startPolling();
});

onUnmounted(() => {
  if (timer) clearInterval(timer);
  if (boardTimer) clearTimeout(boardTimer);
  if (clockTimer) clearInterval(clockTimer);
  managerToken.value = '';
});
</script>

<template>
  <div class="kapi">
    <header class="kapi-brand">
      <div class="kapi-brand-name">
        <img v-if="logoSrc" :src="logoSrc" alt="" />
        <span v-if="brandTitle">{{ brandTitle }}</span>
      </div>
      <div class="kapi-clock">
        <p class="kapi-clock-time">{{ clockText }}</p>
        <p class="kapi-clock-date">{{ dateText }}</p>
      </div>
    </header>
    <section class="kapi-top">
      <div class="kapi-code">
        <img v-if="status === 'ready' && qrUrl" :src="qrUrl" alt="Geçiş kodu" />
        <p v-else class="kapi-message">{{ message || 'Kod hazırlanıyor' }}</p>
      </div>
      <div class="kapi-strip">
        <article v-if="welcome" class="kapi-hello" :class="welcome.direction === 'out' ? 'is-out' : 'is-in'">
          <div class="kapi-hello-mark" aria-hidden="true">
            <svg v-if="welcome.direction === 'out'" viewBox="0 0 64 64">
              <circle cx="32" cy="32" r="30" />
              <path d="M18 32h22M32 22l10 10-10 10" />
            </svg>
            <svg v-else viewBox="0 0 64 64">
              <circle cx="32" cy="32" r="30" />
              <path d="M40 32H18M26 22L16 32l10 10" />
            </svg>
          </div>
          <p class="kapi-hello-kind">{{ welcomeKind(welcome.direction) }}</p>
          <p class="kapi-hello-name">{{ welcome.fullName || welcome.displayName }}</p>
          <p class="kapi-hello-text">{{ welcome.greeting }}</p>
        </article>
        <template v-else>
          <p v-if="locationTitle" class="kapi-quiet">{{ locationTitle }}</p>
          <h1>{{ doorTitle || 'Kapı' }}</h1>
          <p v-if="status === 'unbound'" class="kapi-quiet">Bağlanınca kod burada görünür.</p>
          <p v-else class="kapi-quiet">Kodu okutun.</p>
        </template>
      </div>
    </section>
    <section class="kapi-board">
      <div v-if="boardCards.length" class="kapi-vitrin">
        <p class="kapi-board-title">{{ boardTitle || 'Devam' }}</p>
        <div class="kapi-metric-row">
          <article v-for="card in boardCards" :key="card.key" class="kapi-metric" :class="'is-' + card.key">
            <div class="kapi-metric-mark" aria-hidden="true">
              <svg v-if="card.key === 'inside'" viewBox="0 0 64 64">
                <circle cx="24" cy="22" r="8" />
                <circle cx="42" cy="24" r="6" />
                <path d="M8 50c2-10 8-14 16-14s14 4 16 14M38 50c1-7 5-10 10-10 6 0 9 3 10 10" />
              </svg>
              <svg v-else-if="card.key === 'break'" viewBox="0 0 64 64">
                <circle cx="32" cy="32" r="22" />
                <path d="M26 20v24M38 20v24" />
              </svg>
              <svg v-else-if="card.key === 'left'" viewBox="0 0 64 64">
                <circle cx="32" cy="32" r="22" />
                <path d="M22 33l7 7 14-16" />
              </svg>
              <svg v-else viewBox="0 0 64 64">
                <circle cx="32" cy="32" r="22" />
                <path d="M40 32H18M26 22L16 32l10 10" />
              </svg>
            </div>
            <p class="kapi-metric-value">{{ card.value }}</p>
            <p class="kapi-metric-label">{{ card.label }}</p>
          </article>
        </div>
        <div class="kapi-lists">
          <article class="kapi-panel">
            <h2>Gün</h2>
            <table v-if="dayRows.length" class="kapi-table">
              <thead>
                <tr>
                  <th>Ad</th>
                  <th>Giriş</th>
                  <th>Çıkış</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="row in dayRows" :key="row.name + row.entry">
                  <td>{{ row.name }}</td>
                  <td>{{ row.entry }}</td>
                  <td>{{ row.exit || '—' }}</td>
                </tr>
              </tbody>
            </table>
            <p v-else class="kapi-empty">Bugün giriş yok</p>
          </article>
          <article class="kapi-panel">
            <h2>Molada</h2>
            <table v-if="breakRows.length" class="kapi-table">
              <thead>
                <tr>
                  <th>Ad</th>
                  <th>Başlangıç</th>
                  <th>Süre</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="row in breakRows" :key="row.name + row.startedAt">
                  <td>{{ row.name }}</td>
                  <td>{{ row.started }}</td>
                  <td>{{ breakLength(row.startedAt) }}</td>
                </tr>
              </tbody>
            </table>
            <p v-else class="kapi-empty">Molada kimse yok</p>
          </article>
        </div>
      </div>
      <p v-else>Pano, lokasyona bağlandığında burada durur.</p>
    </section>
    <button type="button" class="kapi-bind" @click="openPanel">Bu ekranı bağla</button>

    <div v-if="panelOpen" class="kapi-modal" @click.self="closePanel">
      <form class="kapi-dialog" @submit.prevent="step === 'login' ? signIn() : bindScreen()">
        <h2>Bu ekranı bağla</h2>
        <template v-if="step === 'login'">
          <label>Kullanıcı<input v-model="username" autocomplete="username" /></label>
          <label>Şifre<input v-model="password" type="password" autocomplete="current-password" /></label>
          <label v-if="!domainLocked">Alan<input v-model="domain" autocomplete="organization" /></label>
        </template>
        <template v-else>
          <label>
            Lokasyon
            <select v-model="locationKey" @change="loadDoors">
              <option value="">Seçin</option>
              <option v-for="item in locations" :key="item.key" :value="item.key">{{ item.title }}</option>
            </select>
          </label>
          <label>
            Kapı
            <select v-model="doorKeyPick">
              <option value="">Seçin</option>
              <option v-for="item in doors" :key="item.key" :value="item.key">{{ item.title }}</option>
            </select>
          </label>
        </template>
        <p v-if="panelError" class="kapi-error">{{ panelError }}</p>
        <div class="kapi-actions">
          <button type="button" @click="closePanel">Vazgeç</button>
          <button type="submit" :disabled="panelBusy || (step === 'place' && !doorKeyPick)">
            {{ step === 'login' ? 'Giriş' : 'Bağla' }}
          </button>
        </div>
      </form>
    </div>
  </div>
</template>

<style scoped>
.kapi {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
  background: #0f172a;
  color: #f8fafc;
}
.kapi-brand {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 16px 24px 0;
}
.kapi-brand-name {
  display: flex;
  align-items: center;
  gap: 12px;
  font-size: 1.05rem;
  font-weight: 600;
}
.kapi-clock { text-align: right; }
.kapi-clock-time {
  margin: 0;
  font-size: 2rem;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
  letter-spacing: 0.04em;
  line-height: 1;
}
.kapi-clock-date {
  margin: 6px 0 0;
  color: #cbd5e1;
  font-size: 0.95rem;
}
.kapi-brand img {
  height: 36px;
  width: auto;
  max-width: 160px;
  object-fit: contain;
}
.kapi-top {
  display: grid;
  grid-template-columns: 320px 1fr;
  gap: 24px;
  padding: 24px;
  align-items: center;
}
.kapi-code {
  width: 280px;
  height: 280px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: #fff;
  color: #0f172a;
  border-radius: 12px;
}
.kapi-code img { width: 280px; height: 280px; }
.kapi-message { margin: 0; padding: 16px; text-align: center; font-size: 1.25rem; }
.kapi-strip h1 { margin: 0 0 12px; font-size: 1.75rem; font-weight: 600; }
.kapi-hello {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 8px;
  min-height: 240px;
  padding: 28px 24px;
  text-align: center;
  border-radius: 16px;
  color: #0f172a;
  color-scheme: light;
}
.kapi-hello.is-in { background: #dcfce7; }
.kapi-hello.is-out { background: #ffedd5; }
.kapi-hello-mark {
  width: 72px;
  height: 72px;
}
.kapi-hello-mark svg { width: 72px; height: 72px; }
.kapi-hello-mark circle { fill: none; stroke-width: 3; }
.kapi-hello-mark path { fill: none; stroke-width: 4; stroke-linecap: round; stroke-linejoin: round; }
.kapi-hello.is-in .kapi-hello-mark circle,
.kapi-hello.is-in .kapi-hello-mark path { stroke: #166534; }
.kapi-hello.is-out .kapi-hello-mark circle,
.kapi-hello.is-out .kapi-hello-mark path { stroke: #c2410c; }
.kapi-hello-kind { margin: 0; font-size: 1rem; font-weight: 700; letter-spacing: 0.04em; text-transform: uppercase; }
.kapi-hello.is-in .kapi-hello-kind { color: #166534; }
.kapi-hello.is-out .kapi-hello-kind { color: #c2410c; }
.kapi-hello-name { margin: 0; font-size: 2.1rem; font-weight: 700; line-height: 1.15; }
.kapi-hello-text { margin: 0; font-size: 1.35rem; }
.kapi-quiet, .kapi-board > p { color: #cbd5e1; }
.kapi-board {
  flex: 1;
  margin: 0 24px 24px;
  border-radius: 12px;
  background: rgba(255, 255, 255, 0.04);
  display: flex;
  align-items: stretch;
  justify-content: center;
  padding: 16px 18px 18px;
  min-height: 0;
  overflow: auto;
}
.kapi-vitrin { width: 100%; }
.kapi-board-title {
  margin: 0 0 16px;
  color: #e2e8f0;
  font-size: 1.15rem;
  font-weight: 600;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}
.kapi-metric-row {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 12px;
}
.kapi-metric {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 4px;
  min-height: 132px;
  padding: 16px 18px;
  border-radius: 16px;
  color: #0f172a;
  color-scheme: light;
}
.kapi-metric.is-arrived { background: #dbeafe; }
.kapi-metric.is-inside { background: #dcfce7; }
.kapi-metric.is-break { background: #ffedd5; }
.kapi-metric.is-left { background: #ede9fe; }
.kapi-metric-mark svg { width: 42px; height: 42px; fill: none; stroke-width: 3; stroke-linecap: round; stroke-linejoin: round; }
.kapi-metric.is-arrived .kapi-metric-mark svg { stroke: #1d4ed8; }
.kapi-metric.is-inside .kapi-metric-mark svg { stroke: #166534; }
.kapi-metric.is-break .kapi-metric-mark svg { stroke: #c2410c; }
.kapi-metric.is-left .kapi-metric-mark svg { stroke: #6d28d9; }
.kapi-metric-value { margin: 4px 0 0; font-size: 2.4rem; font-weight: 700; line-height: 1; }
.kapi-metric-label { margin: 0; font-size: 1rem; }
.kapi-lists {
  display: grid;
  grid-template-columns: minmax(0, 1.4fr) minmax(0, 1fr);
  gap: 12px;
  margin-top: 12px;
}
.kapi-panel {
  margin-top: 12px;
  padding: 14px 16px 8px;
  border-radius: 16px;
  background: rgba(15, 23, 42, 0.45);
}
.kapi-lists .kapi-panel { margin-top: 0; }
.kapi-panel h2 {
  margin: 0 0 4px;
  color: #e2e8f0;
  font-size: 0.95rem;
  font-weight: 600;
}
.kapi-empty { margin: 28px 0; text-align: center; color: #94a3b8; }
.kapi-table { width: 100%; border-collapse: collapse; margin: 8px 0 10px; }
.kapi-table th, .kapi-table td {
  padding: 8px 6px;
  border-bottom: 1px solid rgba(248, 250, 252, 0.1);
  text-align: left;
  font-size: 1rem;
}
.kapi-table th:nth-child(n + 2), .kapi-table td:nth-child(n + 2) { text-align: right; width: 22%; }
.kapi-table th { color: #94a3b8; font-weight: 600; font-size: 0.85rem; }
.kapi-bind {
  position: fixed;
  right: 16px;
  bottom: 16px;
  border: 0;
  border-radius: 999px;
  padding: 10px 14px;
  background: rgba(248, 250, 252, 0.12);
  color: #e2e8f0;
  cursor: pointer;
}
.kapi-modal {
  position: fixed;
  inset: 0;
  background: rgba(2, 6, 23, 0.72);
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 24px;
}
.kapi-dialog {
  width: min(420px, 100%);
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 20px;
  border-radius: 12px;
  background: #f8fafc;
  color: #0f172a;
  color-scheme: light;
}
.kapi-dialog h2 { margin: 0 0 4px; font-size: 1.25rem; }
.kapi-dialog label { display: flex; flex-direction: column; gap: 6px; font-size: 0.95rem; }
.kapi-dialog input, .kapi-dialog select {
  font: inherit;
  padding: 10px 12px;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  background: #fff;
  color: #0f172a;
  caret-color: #0f172a;
}
.kapi-actions { display: flex; justify-content: flex-end; gap: 8px; }
.kapi-actions button {
  font: inherit;
  padding: 8px 14px;
  border-radius: 8px;
  border: 0;
  cursor: pointer;
}
.kapi-actions button[type="submit"] { background: #0f172a; color: #f8fafc; }
.kapi-error { margin: 0; color: #b91c1c; }
</style>
