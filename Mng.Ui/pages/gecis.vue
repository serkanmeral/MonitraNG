<script setup lang="ts">
import {
  clearPhone,
  confirmPassage,
  createPhoneKey,
  domainFromToken,
  enrollPhone,
  loginPassage,
  previewPassage,
  readPassageNonce,
  readPhone,
  readPhoneStatus,
  refreshPassage,
  signNonce,
  tokenLeftSeconds,
  writePhone,
  type PassagePreview,
  type PassageResult,
  type PhoneVault,
} from '@/utils/pdksPhone';

definePageMeta({ layout: 'blank' });

useHead({
  title: 'Geçiş',
  meta: [
    { name: 'apple-mobile-web-app-capable', content: 'yes' },
    { name: 'mobile-web-app-capable', content: 'yes' },
    { name: 'theme-color', content: '#f8fafc' },
  ],
});

type Phase = 'boot' | 'login' | 'pending' | 'home' | 'scan' | 'decision';
type Facing = 'environment' | 'user';

const route = useRoute();
const router = useRouter();
const config = useRuntimeConfig();

const phase = ref<Phase>('boot');
const phone = ref<PhoneVault | null>(null);
const personName = ref('');
const preview = ref<PassagePreview | null>(null);
const result = ref<PassageResult | null>(null);
const nonce = ref('');
const errorText = ref('');
const busy = ref(false);
const cameraError = ref('');
const domain = ref('');
const username = ref('');
const password = ref('');
const now = ref('');

const video = ref<HTMLVideoElement | null>(null);
let stream: MediaStream | null = null;
let scanTimer = 0;
let leaveTimer = 0;
let clockTimer = 0;
let detector: { detect: (source: HTMLVideoElement) => Promise<Array<{ rawValue?: string }>> } | null = null;
let scanCanvas: HTMLCanvasElement | null = null;
type QrRead = (
  data: Uint8ClampedArray,
  width: number,
  height: number,
  options?: { inversionAttempts?: 'dontInvert' },
) => { data: string } | null;
let qrRead: QrRead | null = null;

const clock = new Intl.DateTimeFormat('tr-TR', {
  timeZone: 'Europe/Istanbul',
  hour: '2-digit',
  minute: '2-digit',
  second: '2-digit',
});

function extensionsBase() {
  const gateway = String(config.public.gatewayUrl || '').replace(/\/$/, '');
  return import.meta.dev ? 'http://127.0.0.1:5092' : `${gateway}/extensions`;
}

function directionLabel(value?: string | null) {
  if (value === 'in') return 'Giriş';
  if (value === 'out') return 'Çıkış';
  if (value === 'break') return 'Mola';
  if (value === 'return') return 'Dönüş';
  return '';
}

function resultText(item: PassageResult) {
  const greeting = item.greeting?.trim();
  if (greeting) return greeting;
  const label = directionLabel(item.direction);
  return label ? `${label} kaydedildi` : 'Geçiş kaydedildi';
}

function failureCopy(item: PassageResult) {
  const which = directionLabel(item.direction) || 'Geçiş';
  if (item.result === 'ignored') {
    return {
      title: `${which} yazılmadı`,
      detail: 'Aynı işlem otuz saniye içinde yeniden sayılmaz.',
    };
  }
  return {
    title: `${which} kaydedilemedi`,
    detail: item.message || 'Tekrar deneyin.',
  };
}

const action = computed(() => directionLabel(preview.value?.direction));
const breakLabel = computed(() => directionLabel(preview.value?.breakAction));
const closed = computed(() => preview.value?.status === 'complete');
const saved = computed(() => result.value?.result === 'accepted');
const outcome = computed(() => {
  if (saved.value && result.value) return resultText(result.value);
  if (closed.value) return preview.value?.message || 'Bugünkü geçiş tamam.';
  return '';
});
const failure = computed(() => (result.value && !saved.value ? failureCopy(result.value) : null));
const showFront = computed(() => preview.value?.photoRequired === true && result.value == null && !closed.value);
const place = computed(() => [preview.value?.locationTitle, preview.value?.doorTitle].filter(Boolean).join(' · '));

function stopCamera() {
  window.clearTimeout(scanTimer);
  scanTimer = 0;
  stream?.getTracks().forEach((track) => track.stop());
  stream = null;
  if (video.value) video.value.srcObject = null;
}

async function openCamera(facing: Facing) {
  stopCamera();
  cameraError.value = '';
  if (!navigator.mediaDevices?.getUserMedia) {
    cameraError.value = 'Bu tarayıcı kamerayı açamıyor.';
    return;
  }
  try {
    stream = await navigator.mediaDevices.getUserMedia({
      audio: false,
      video: { facingMode: { ideal: facing } },
    });
    await nextTick();
    if (!video.value) return;
    video.value.srcObject = stream;
    video.value.muted = true;
    await video.value.play();
    if (facing === 'environment') scheduleScan();
  } catch {
    cameraError.value = facing === 'user'
      ? 'Geçiş karesi için kamera gerekli. Ayarlardan açabilirsiniz.'
      : 'Kapı kodu için kamera gerekli. Ayarlardan açabilirsiniz.';
  }
}

function scheduleScan() {
  window.clearTimeout(scanTimer);
  scanTimer = window.setTimeout(() => { void scanFrame(); }, 400);
}

async function nativeDetector() {
  if (detector) return detector;
  const ctor = (globalThis as { BarcodeDetector?: new (options: { formats: string[] }) => typeof detector }).BarcodeDetector;
  if (!ctor) return null;
  try {
    detector = new ctor({ formats: ['qr_code'] });
    return detector;
  } catch {
    detector = null;
    return null;
  }
}

async function readQr(element: HTMLVideoElement) {
  const native = await nativeDetector();
  if (native) {
    const found = await native.detect(element);
    return found.find((item) => item.rawValue)?.rawValue || '';
  }
  if (!qrRead) qrRead = (await import('jsqr')).default as QrRead;
  const sourceWidth = element.videoWidth;
  const sourceHeight = element.videoHeight;
  if (sourceWidth < 2 || sourceHeight < 2) return '';
  const scale = Math.min(1, 720 / sourceWidth);
  const canvas = scanCanvas ?? (scanCanvas = document.createElement('canvas'));
  canvas.width = Math.max(2, Math.round(sourceWidth * scale));
  canvas.height = Math.max(2, Math.round(sourceHeight * scale));
  const context = canvas.getContext('2d', { willReadFrequently: true });
  if (!context) return '';
  context.drawImage(element, 0, 0, canvas.width, canvas.height);
  const image = context.getImageData(0, 0, canvas.width, canvas.height);
  return qrRead(image.data, image.width, image.height, { inversionAttempts: 'dontInvert' })?.data || '';
}

async function scanFrame() {
  const element = video.value;
  if (phase.value !== 'scan' || !element || element.readyState < 2) {
    if (phase.value === 'scan') scheduleScan();
    return;
  }
  try {
    const raw = await readQr(element);
    const next = raw ? readPassageNonce(raw, window.location.origin) : null;
    if (raw && !next) {
      errorText.value = 'Kodun adresi bu sunucuya ait değil.';
    } else if (next && next !== nonce.value) {
      await claim(next);
      if (phase.value === 'scan') scheduleScan();
      return;
    } else if (cameraError.value) {
      cameraError.value = '';
    }
  } catch {
    cameraError.value = 'Kod okunamadı.';
  }
  if (phase.value === 'scan') scheduleScan();
}

async function usableToken(): Promise<string> {
  const current = phone.value;
  if (!current) throw new Error('Oturum kapandı. Yeniden giriş yapın.');
  if (current.accessToken && tokenLeftSeconds(current.accessToken) > 30) return current.accessToken;
  const renewed = await refreshPassage(current.refreshToken, current.domain);
  const next = { ...current, accessToken: renewed.accessToken, refreshToken: renewed.refreshToken };
  phone.value = next;
  await writePhone(next);
  return next.accessToken;
}

function leaveWaiting() {
  if (phase.value === 'boot' || phase.value === 'pending' || phase.value === 'login') phase.value = 'home';
}

async function claim(nextNonce: string) {
  if (!phone.value) return;
  const ownsBusy = !busy.value;
  busy.value = true;
  errorText.value = '';
  result.value = null;
  try {
    const token = await usableToken();
    const view = await previewPassage(extensionsBase(), token, phone.value.publicKey, nextNonce);
    if (view.status === 'unapproved') {
      phase.value = 'pending';
      stopCamera();
      return;
    }
    if (view.status !== 'ready' && view.status !== 'complete') {
      errorText.value = view.message || 'Kod ayrılamadı.';
      leaveWaiting();
      return;
    }
    nonce.value = nextNonce;
    preview.value = view;
    phase.value = 'decision';
    stopCamera();
    if (view.photoRequired && view.status === 'ready') await openCamera('user');
  } catch (error) {
    errorText.value = error instanceof Error ? error.message : 'Kod ayrılamadı.';
    if (errorText.value.includes('Yeniden giriş')) {
      await dropSession();
      return;
    }
    leaveWaiting();
  } finally {
    if (ownsBusy) busy.value = false;
  }
}

function captureFrame(): string | null {
  const element = video.value;
  if (!element || element.videoWidth < 2) return null;
  const scale = Math.min(1, 640 / element.videoWidth);
  const canvas = document.createElement('canvas');
  canvas.width = Math.round(element.videoWidth * scale);
  canvas.height = Math.round(element.videoHeight * scale);
  const context = canvas.getContext('2d');
  if (!context) return null;
  context.drawImage(element, 0, 0, canvas.width, canvas.height);
  const url = canvas.toDataURL('image/jpeg', 0.5);
  const marker = url.indexOf(',');
  return marker >= 0 ? url.slice(marker + 1) : null;
}

async function confirm(chosen?: string | null) {
  if (!phone.value || !nonce.value || !preview.value || busy.value || closed.value || !chosen) return;
  busy.value = true;
  errorText.value = '';
  try {
    let photo: string | null = null;
    if (preview.value.photoRequired) {
      photo = captureFrame();
      if (!photo) {
        errorText.value = 'Fotoğraf alınamadı.';
        return;
      }
    }
    const token = await usableToken();
    const signature = await signNonce(phone.value.privateKey, nonce.value);
    result.value = await confirmPassage(
      extensionsBase(),
      token,
      phone.value.publicKey,
      nonce.value,
      signature,
      photo,
      chosen,
    );
  } catch (error) {
    errorText.value = error instanceof Error ? error.message : 'Geçiş kaydedilemedi.';
    if (errorText.value.includes('Yeniden giriş')) await dropSession();
  } finally {
    busy.value = false;
  }
}

async function dropSession() {
  const current = phone.value;
  if (!current) {
    phase.value = 'login';
    return;
  }
  const next = { ...current, accessToken: '', refreshToken: '' };
  phone.value = next;
  await writePhone(next);
  stopCamera();
  phase.value = 'login';
}

async function forgetPhone() {
  await clearPhone();
  phone.value = null;
  personName.value = '';
  stopCamera();
  phase.value = 'login';
}

async function enter() {
  errorText.value = '';
  let selectedDomain = domain.value.trim();
  let selectedUsername = username.value.trim();
  if (selectedUsername.includes('@')) {
    const parts = selectedUsername.split('@');
    if (parts.length === 2 && parts[1]) {
      if (!selectedDomain) selectedDomain = parts[0];
      selectedUsername = parts[1];
    }
  }
  if (!selectedUsername || !password.value) {
    errorText.value = 'Kullanıcı adı ve şifre gerekli.';
    return;
  }
  busy.value = true;
  try {
    const tokens = await loginPassage(selectedUsername, password.value, selectedDomain);
    password.value = '';
    const existing = phone.value?.privateKey && phone.value.publicKey ? phone.value : null;
    const key = existing
      ? { publicKey: existing.publicKey, privateKey: existing.privateKey }
      : await createPhoneKey();
    const next: PhoneVault = {
      publicKey: key.publicKey,
      privateKey: key.privateKey,
      accessToken: tokens.accessToken,
      refreshToken: tokens.refreshToken,
      domain: selectedDomain || domainFromToken(tokens.accessToken),
    };
    phone.value = next;
    await writePhone(next);
    const enrolled = await enrollPhone(tokens.accessToken, next.publicKey);
    await applyStatus(enrolled.status, enrolled.personName || '');
  } catch (error) {
    errorText.value = error instanceof Error ? error.message : 'Giriş başarısız.';
  } finally {
    busy.value = false;
  }
}

async function applyStatus(status: string, name: string) {
  personName.value = name;
  if (status === 'none') {
    await forgetPhone();
    return;
  }
  if (status === 'pending') {
    phase.value = 'pending';
    stopCamera();
    return;
  }
  if (status !== 'valid') {
    errorText.value = 'Anahtar okunamadı.';
    phase.value = 'pending';
    return;
  }
  const arrived = String(route.query.n || '').trim();
  if (arrived) {
    await claim(arrived);
    if (phase.value === 'boot' || phase.value === 'login') phase.value = 'home';
    return;
  }
  phase.value = 'home';
}

async function checkApproval() {
  if (!phone.value) return;
  busy.value = true;
  errorText.value = '';
  try {
    const token = await usableToken();
    const view = await readPhoneStatus(token, phone.value.publicKey);
    await applyStatus(view.status, view.personName || '');
  } catch (error) {
    errorText.value = error instanceof Error ? error.message : 'Onay okunamadı.';
    if (errorText.value.includes('Yeniden giriş')) await dropSession();
  } finally {
    busy.value = false;
  }
}

async function goHome() {
  window.clearTimeout(leaveTimer);
  result.value = null;
  preview.value = null;
  nonce.value = '';
  errorText.value = '';
  cameraError.value = '';
  stopCamera();
  if (route.query.n) await router.replace({ path: '/gecis' });
  phase.value = 'home';
}

const chromeHandoff = computed(() => {
  if (!import.meta.client) return false;
  const ua = navigator.userAgent;
  if (!/Android/i.test(ua)) return false;
  if (String(route.query.hop || '') === '1') return false;
  const otherBrowser = /SamsungBrowser|EdgA|OPR\/|Opera|UCBrowser|YaBrowser|MiuiBrowser|HeyTap|HuaweiBrowser|Vivaldi|DuckDuckGo|Firefox|wv\)/i.test(ua);
  const chrome = /Chrome\/\d+/i.test(ua) && !otherBrowser;
  return !chrome;
});

function openChrome() {
  const target = new URL(window.location.href);
  target.searchParams.set('hop', '1');
  const body = `${target.host}${target.pathname}${target.search}`;
  const fallback = encodeURIComponent(target.toString());
  window.location.href = `intent://${body}#Intent;scheme=https;package=com.android.chrome;S.browser_fallback_url=${fallback};end`;
}

async function openPassage() {
  errorText.value = '';
  cameraError.value = '';
  phase.value = 'scan';
  await openCamera('environment');
}

async function boot() {
  now.value = clock.format(new Date());
  clockTimer = window.setInterval(() => { now.value = clock.format(new Date()); }, 1000);
  if (chromeHandoff.value) {
    openChrome();
    return;
  }
  try {
    phone.value = await readPhone();
  } catch {
    phone.value = null;
  }
  if (!phone.value?.refreshToken) {
    phase.value = 'login';
    return;
  }
  try {
    await usableToken();
    const view = await readPhoneStatus(phone.value.accessToken, phone.value.publicKey);
    await applyStatus(view.status, view.personName || '');
  } catch {
    phase.value = 'login';
  }
}

watch([saved, closed], ([isSaved, isClosed]) => {
  window.clearTimeout(leaveTimer);
  if (!isSaved && !isClosed) return;
  leaveTimer = window.setTimeout(() => { void goHome(); }, 3000);
});

onMounted(() => { void boot(); });
onUnmounted(() => {
  stopCamera();
  window.clearTimeout(leaveTimer);
  window.clearInterval(clockTimer);
});
</script>

<template>
  <main class="passage">
    <header class="bar">
      <button v-if="phase === 'scan' || phase === 'decision'" type="button" class="back" @click="goHome">Geri</button>
      <span v-else class="brand">
        <img src="/images/logos/monitra-horizontal.png" alt="MonitraNG" />
      </span>
      <time>{{ now }}</time>
    </header>

    <section v-if="phase === 'boot'" class="panel">
      <p class="title">{{ chromeHandoff ? 'Chrome açılıyor' : 'Hazırlanıyor' }}</p>
      <p v-if="chromeHandoff" class="muted">Telefon kaydı Chrome'da durur.</p>
      <button v-if="chromeHandoff" type="button" @click="openChrome">Chrome'da aç</button>
    </section>

    <form v-else-if="phase === 'login'" class="panel" @submit.prevent="enter">
      <p class="title">Geçiş</p>
      <p class="muted">Şifre bir kez sorulur. Sonraki geçişlerde oturum durur.</p>
      <label>Alan<input v-model="domain" autocomplete="organization" /></label>
      <label>Kullanıcı adı<input v-model="username" autocomplete="username" /></label>
      <label>Şifre<input v-model="password" type="password" autocomplete="current-password" /></label>
      <p v-if="errorText" class="danger">{{ errorText }}</p>
      <button type="submit" :disabled="busy">{{ busy ? 'Giriliyor' : 'Giriş' }}</button>
    </form>

    <section v-else-if="phase === 'pending'" class="panel">
      <p class="title">Onay bekleniyor</p>
      <p class="muted">İK telefonu onaylayınca geçiş açılır.</p>
      <p v-if="errorText" class="danger">{{ errorText }}</p>
      <button type="button" :disabled="busy" @click="checkApproval">{{ busy ? 'Bakılıyor' : 'Onayı kontrol et' }}</button>
    </section>

    <section v-else-if="phase === 'home'" class="home">
      <p class="headline">{{ personName || 'Merhaba' }}</p>
      <p v-if="errorText" class="danger home-note">{{ errorText }}</p>
      <button type="button" class="tile" @click="openPassage">
        <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 4h6v6H4V4zm10 0h6v6h-6V4zM4 14h6v6H4v-6zm10 2h2v2h-2v-2zm4 0h2v2h-2v-2zM14 18h2v2h-2v-2zm4 0h2v2h-2v-2z" /></svg>
        <span>
          <strong>PDKS</strong>
          <small>Kapı kodunu okut</small>
        </span>
      </button>
    </section>

    <section v-else-if="phase === 'scan'" class="scan">
      <video ref="video" autoplay muted playsinline />
      <div class="finder">
        <div class="frame">
          <i /><i /><i /><i />
        </div>
        <p>{{ busy ? 'Kod ayrılıyor' : 'Kodu çerçeveye alın' }}</p>
        <p v-if="cameraError || errorText" class="banner">{{ cameraError || errorText }}</p>
      </div>
    </section>

    <section v-else class="decision">
      <div class="identity">
        <div>
          <p class="name">{{ preview?.personName || personName }}</p>
          <p v-if="place" class="place">{{ place }}</p>
        </div>
        <div v-if="showFront" class="lens">
          <video ref="video" autoplay muted playsinline />
        </div>
      </div>
      <p v-if="outcome" class="title">{{ outcome }}</p>
      <template v-else-if="failure">
        <p class="danger title">{{ failure.title }}</p>
        <p class="danger">{{ failure.detail }}</p>
        <button type="button" class="ghost" @click="goHome">Geri</button>
      </template>
      <template v-else>
        <p v-if="cameraError || errorText" class="danger">{{ cameraError || errorText }}</p>
        <button v-if="action" type="button" :disabled="busy || (showFront && !!cameraError)" @click="confirm(preview?.direction)">
          {{ busy ? 'Kaydediliyor' : action }}
        </button>
        <button v-if="breakLabel" type="button" class="ghost" :disabled="busy || (showFront && !!cameraError)" @click="confirm(preview?.breakAction)">
          {{ busy ? 'Kaydediliyor' : breakLabel }}
        </button>
      </template>
    </section>
  </main>
</template>

<style scoped>
.passage {
  min-height: 100vh;
  background: #f8fafc;
  color: #0f172a;
  font-family: Inter, "Segoe UI", sans-serif;
}
.bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
  font-variant-numeric: tabular-nums;
}
.brand { display: flex; align-items: center; }
.brand img { height: 36px; width: auto; }
.back { border: 0; background: transparent; color: #1d4ed8; font: inherit; font-size: 16px; min-height: 40px; padding: 0; }
.home { padding: 28px 24px; }
.headline { margin: 0; font-size: 32px; font-weight: 650; letter-spacing: -0.6px; }
.home-note { margin-top: 16px; }
.tile {
  margin-top: 36px;
  width: 100%;
  min-height: 76px;
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 0 18px;
  border: 1px solid #e2e8f0;
  border-radius: 18px;
  background: white;
  color: #0f172a;
  text-align: left;
}
.tile svg { width: 22px; height: 22px; fill: #1d4ed8; flex: none; }
.tile strong, .tile small { display: block; }
.tile strong { font-size: 18px; }
.tile small { font-size: 14px; font-weight: 400; color: #64748b; }
.panel {
  display: flex;
  flex-direction: column;
  gap: 14px;
  padding: 12px 20px 32px;
}
.decision { flex: 1; justify-content: center; padding: 12px 24px 48px; }
.identity { display: flex; align-items: center; gap: 16px; }
.place { margin: 8px 0 0; color: #64748b; font-size: 18px; }
.title { margin: 0; font-size: 28px; font-weight: 650; line-height: 1.25; }
.name { margin: 8px 0 0; font-size: 32px; font-weight: 650; }
.muted { margin: 0; color: #64748b; font-size: 16px; }
.danger { margin: 0; color: #b91c1c; font-size: 16px; font-weight: 600; }
.danger.title { font-size: 26px; }
label { display: flex; flex-direction: column; gap: 6px; font-size: 14px; color: #334155; }
input {
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  background: white;
  color: #0f172a;
  font: inherit;
  font-size: 16px;
  padding: 12px 14px;
}
button {
  border: 0;
  border-radius: 28px;
  background: #1d4ed8;
  color: white;
  font: inherit;
  font-size: 18px;
  font-weight: 650;
  height: 56px;
  margin-top: 36px;
  padding: 0 18px;
}
button:disabled { opacity: 0.6; }
.back { margin: 0; height: 40px; border-radius: 0; background: transparent; color: #1d4ed8; font-size: 16px; font-weight: 500; }
.tile { margin-top: 36px; height: auto; min-height: 76px; border-radius: 18px; background: white; color: #0f172a; font-weight: 400; }
.ghost { margin-top: 12px; background: white; color: #1d4ed8; border: 1px solid #e2e8f0; }
.decision { display: flex; flex-direction: column; }
.scan { position: relative; height: calc(100dvh - 64px); overflow: hidden; background: #0f172a; }
.scan video, .lens video {
  position: absolute;
  inset: 0;
  width: 100%;
  height: 100%;
  object-fit: cover;
  background: #0f172a;
}
.finder {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  color: white;
  text-align: center;
}
.finder p { position: absolute; bottom: 36px; margin: 0; font-size: 16px; }
.frame { position: relative; width: min(72vw, 320px); aspect-ratio: 1; }
.frame i {
  position: absolute;
  width: 28px;
  height: 28px;
  border: 3px solid white;
}
.frame i:nth-child(1) { top: 0; left: 0; border-right: 0; border-bottom: 0; }
.frame i:nth-child(2) { top: 0; right: 0; border-left: 0; border-bottom: 0; }
.frame i:nth-child(3) { bottom: 0; left: 0; border-right: 0; border-top: 0; }
.frame i:nth-child(4) { right: 0; bottom: 0; border-left: 0; border-top: 0; }
.banner {
  bottom: 84px;
  left: 24px;
  right: 24px;
  background: white;
  color: #b91c1c;
  border-radius: 16px;
  padding: 14px 16px;
  font-weight: 650;
}
.lens {
  position: relative;
  width: 72px;
  height: 96px;
  overflow: hidden;
  border-radius: 16px;
  flex: none;
  background: #0f172a;
}
</style>
