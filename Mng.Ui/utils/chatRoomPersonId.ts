import { useAuthStore } from '@/stores/auth';
import { useUserStore, type User } from '@/stores/apps/user';

function addLower(set: Set<string>, v: string | undefined | null) {
  if (v == null) return;
  const t = String(v).trim().toLowerCase();
  if (t) set.add(t);
}

/**
 * Sohbet / DM için bir kişiye ait olası id'ler (JWT sub, Keeper @users __dataId, keycloakUserId, userId).
 * Kullanıcı listesi yüklenmemişse yalnızca `personId` döner.
 */
export function chatPersonAliases(personId: string): Set<string> {
  const s = new Set<string>();
  addLower(s, personId);
  try {
    const auth = useAuthStore();
    const sub = String(auth.userInfo?.sub ?? '').trim();
    const mp = String(auth.userInfo?.mng_person_id ?? '').trim();
    const pl = personId.trim().toLowerCase();
    if (sub && pl === sub.toLowerCase()) {
      if (mp) addLower(s, mp);
    }
    if (mp && pl === mp.toLowerCase()) {
      if (sub) addLower(s, sub);
    }
    const u = useUserStore().getUserById(personId.trim());
    if (u) {
      addLower(s, u.id);
      addLower(s, u.userId);
      addLower(s, u.keycloakUserId);
    }
  } catch {
    /* store dışı / SSR */
  }
  return s;
}

export function participantMatchesAnyAlias(storedParticipantId: string, personId: string): boolean {
  const p = String(storedParticipantId ?? '').trim().toLowerCase();
  if (!p) return false;
  return chatPersonAliases(personId).has(p);
}

function mergeAliasSet(target: Set<string>, source: Set<string>) {
  for (const x of source) {
    if (x) target.add(x.toLowerCase());
  }
}

/**
 * Oturum kullanıcısının `cht_messages.authorPersonId` ile eşleşebilecek tüm id’ler (sub, mng_person_id,
 * yüklü kullanıcı kaydındaki id’ler, listede sub ile eşleşen keycloakUserId vb.).
 * @param additionalIds — örn. `myDmParticipantId` (store’dan); döngüsel import olmaması için dışarıdan.
 */
export function sessionMessageAuthorAliasSet(additionalIds?: Iterable<string>): Set<string> {
  const s = new Set<string>();
  const add = (v: string | undefined | null) => {
    const t = String(v ?? '').trim().toLowerCase();
    if (t) s.add(t);
  };
  try {
    const auth = useAuthStore();
    const sub = String(auth.userInfo?.sub ?? '').trim();
    const mp = String(auth.userInfo?.mng_person_id ?? '').trim();
    add(sub);
    add(mp);
    if (sub) mergeAliasSet(s, chatPersonAliases(sub));
    if (mp) mergeAliasSet(s, chatPersonAliases(mp));

    const us = useUserStore();
    const c = us.currentUser;
    if (c) {
      add(c.id);
      add(c.userId);
      add(c.keycloakUserId);
    }
    for (const seed of [sub, mp]) {
      if (!seed) continue;
      const u = us.getUserById(seed);
      if (u) {
        add(u.id);
        add(u.userId);
        add(u.keycloakUserId);
      }
    }
    if (sub) {
      const tl = sub.toLowerCase();
      for (const u of us.users) {
        if (u.keycloakUserId && u.keycloakUserId.toLowerCase() === tl) {
          add(u.id);
          add(u.userId);
          add(u.keycloakUserId);
        }
        if (u.userId && u.userId.toLowerCase() === tl) {
          add(u.id);
          add(u.keycloakUserId);
        }
      }
    }
    if (mp) {
      const ml = mp.toLowerCase();
      for (const u of us.users) {
        if (u.id && u.id.toLowerCase() === ml) {
          add(u.id);
          add(u.userId);
          add(u.keycloakUserId);
        }
      }
    }
    if (additionalIds) {
      for (const x of additionalIds) add(x);
    }
  } catch {
    /* SSR / store dışı */
  }
  return s;
}

/**
 * `cht_messages.authorPersonId` ile oturum kullanıcısı eşlemesi.
 * `additionalIds` içine Pinia’dan `myDmParticipantId` geçirilebilir (workspace → döngü yok).
 */
export function messageAuthorIsCurrentUser(authorPersonId: string, additionalIds?: Iterable<string>): boolean {
  const a = String(authorPersonId ?? '').trim().toLowerCase();
  if (!a) return false;
  return sessionMessageAuthorAliasSet(additionalIds).has(a);
}

/**
 * DM `participant*` kaydı: önce Keeper Mongo kullanıcı id (`User.id` ≈ @users __dataId).
 * Böylece localStorage / ortamda Keycloak id görünmese bile aynı id uzayından eşleşme kolaylaşır.
 * Mesaj `authorPersonId` için JWT `sub` kullanılmaya devam eder (DG doğrulaması).
 */
export function keeperPrimaryChatPersonId(u: User): string {
  const mongo = String(u.id ?? '').trim();
  if (mongo) return mongo;
  const uid = String(u.userId ?? '').trim();
  if (uid) return uid;
  const k = u.keycloakUserId?.trim();
  if (k) return k;
  return '';
}

/**
 * DM `participant*`: yalnızca Keeper `@users` Mongo id (JWT’deki `mng_person_id` ile aynı alan).
 * Çözümlenemezse boş döner — ham `sub`/Keycloak id participant olarak yazılmaz.
 */
export function normalizePeerIdForDirectChat(raw: string): string {
  const t = String(raw ?? '').trim();
  if (!t) return '';
  try {
    const u = useUserStore().getUserById(t);
    const id = u?.id?.trim();
    if (id) return id;
  } catch {
    /* ignore */
  }
  return '';
}

/** Oturum kullanıcısına göre DM satırındaki karşı tarafın kayıtlı id'si (gösterim için). */
export function directConversationPeerStoredId(row: { participantAId: string; participantBId: string }, sessionSub: string): string {
  const me = String(sessionSub ?? '').trim();
  if (!me) return row.participantBId;
  if (participantMatchesAnyAlias(row.participantAId, me)) return row.participantBId;
  if (participantMatchesAnyAlias(row.participantBId, me)) return row.participantAId;
  return row.participantBId;
}

/** DM listesi / başlık: ham id yerine ad soyad veya kullanıcı adı. */
export function displayNameForStoredPersonId(storedId: string): string {
  const raw = String(storedId ?? '').trim();
  if (!raw) return '—';
  try {
    const u = useUserStore().getUserById(raw);
    if (u) {
      const n = `${u.firstName ?? ''} ${u.lastName ?? ''}`.trim();
      if (n) return n;
      if (u.username?.trim()) return u.username.trim();
      if (u.email?.trim()) return u.email.trim();
    }
  } catch {
    /* ignore */
  }
  return raw.length > 22 ? `${raw.slice(0, 10)}…${raw.slice(-6)}` : raw;
}

export function directConversationLinksPair(
  row: { participantAId: string; participantBId: string },
  mePersonId: string,
  otherPersonId: string
): boolean {
  const meH = chatPersonAliases(mePersonId);
  const othH = chatPersonAliases(otherPersonId);
  const a = row.participantAId.trim().toLowerCase();
  const b = row.participantBId.trim().toLowerCase();
  const aMe = [...meH].some((x) => x === a);
  const bMe = [...meH].some((x) => x === b);
  const aO = [...othH].some((x) => x === a);
  const bO = [...othH].some((x) => x === b);
  return (aMe && bO) || (bMe && aO);
}
