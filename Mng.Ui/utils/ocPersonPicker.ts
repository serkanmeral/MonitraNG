import type { User } from '@/stores/apps/user';

export const OC_PERSON_PICKER_PAGE_SIZE = 25;
export const OC_PERSON_PICKER_LOAD_MORE_VALUE = '__oc_person_load_more__';

export type OcPersonPickerItem = {
  title: string;
  subtitle: string;
  value: string;
};

/** Keeper listesi / seçim modeli ile aynı kimlik (id veya userId). */
export function resolveUserPickerValue(user: User): string {
  return (user.id || user.userId || '').trim();
}

export function buildOcPersonPickerTitle(user: User): string {
  const name = `${user.firstName ?? ''} ${user.lastName ?? ''}`.trim();
  if (name) return name;

  const username = user.username?.trim();
  if (username) return username;

  const email = user.email?.trim();
  if (email) return email;

  const src = (user.provisioningSource || '').trim().toLowerCase();
  if (src === 'local') {
    return username || email || 'Sistem kullanıcısı';
  }
  if (src && src !== 'directory') {
    return username || email || user.provisioningSource || 'Sistem kullanıcısı';
  }

  const id = resolveUserPickerValue(user);
  return username || email || id;
}

export function buildOcPersonPickerSubtitle(user: User): string {
  const parts: string[] = [];
  const username = user.username?.trim();
  const email = user.email?.trim();
  const dept = user.department?.trim();
  const jobTitle = user.title?.trim();
  const src = (user.provisioningSource || '').trim();

  if (username) parts.push(`@${username}`);
  if (email) parts.push(email);
  if (dept) parts.push(dept);
  if (jobTitle) parts.push(jobTitle);
  if (src) {
    parts.push(src.toLowerCase() === 'directory' ? 'LDAP' : src);
  }

  const id = resolveUserPickerValue(user);
  return parts.join(' · ') || id;
}

export function mapUserToOcPersonPickerItem(user: User): OcPersonPickerItem | null {
  const value = resolveUserPickerValue(user);
  if (!value) return null;
  return {
    value,
    title: buildOcPersonPickerTitle(user),
    subtitle: buildOcPersonPickerSubtitle(user),
  };
}

export function collectPersonIdsFromValue(value: unknown): string[] {
  if (value == null || value === '') return [];
  if (Array.isArray(value)) {
    return value.map((v) => String(v ?? '').trim()).filter(Boolean);
  }
  const id = String(value).trim();
  return id ? [id] : [];
}

export function collectPersonIdsFromFormModel(
  model: Record<string, unknown> | null | undefined,
  fieldKeys: string[]
): string[] {
  if (!model) return [];
  const ids = new Set<string>();
  for (const key of fieldKeys) {
    const raw = model[key];
    if (raw == null || raw === '') continue;
    if (Array.isArray(raw)) {
      for (const v of raw) {
        const id = String(v ?? '').trim();
        if (id) ids.add(id);
      }
    } else {
      const id = String(raw).trim();
      if (id) ids.add(id);
    }
  }
  return [...ids];
}
