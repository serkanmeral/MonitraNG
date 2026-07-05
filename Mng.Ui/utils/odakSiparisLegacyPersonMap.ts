/**
 * Legacy kalite users/employees -> Keeper userId eslemesi.
 * Uretim: scripts/tests/MngKeeper/users/sync-legacy-person-map-to-ui.ps1
 * Kaynak: docs/odak/eskiapp/reports/legacy-kalite-user-id-map.json
 */
export const LEGACY_PERSON_KEEPER_ID_BY_LEGACY_KEY: Record<string, string> = {
  '10': '6a4a7623647fce4a6fd43245',
  '101': '6a4a7624647fce4a6fd4324a',
  '11': '6a4a7624647fce4a6fd43248',
  '118': '6a2257d16723c2bd54eec3b2',
  '12': '6a4a7624647fce4a6fd43249',
  '123': '6a4a7625647fce4a6fd4324e',
  '160': '6a4a7623647fce4a6fd43246',
  '17': '6a4a7624647fce4a6fd4324b',
  '18': '6a4a7625647fce4a6fd4324c',
  '180': '6a4a7624647fce4a6fd43247',
  '220': '6a4a77d9647fce4a6fd4324f',
  '225': '6a2257d16723c2bd54eec39a',
  '250': '6a4a77d9647fce4a6fd43250',
  '3': '6a2257d26723c2bd54eec3b4',
  '9': '6a4a7623647fce4a6fd43244',
  '97': '6a4a7625647fce4a6fd4324d',
};

export const LEGACY_PERSON_LABEL_BY_LEGACY_KEY: Record<string, string> = {
  '10': 'Engin Berk Oğuz',
  '101': 'Aylin Güneş',
  '11': 'Murat Tarcan',
  '118': 'Murat Küçük',
  '12': 'Emin Demiroğlu',
  '123': 'Yemliha Gülyazı',
  '160': 'Gamze Yıldız',
  '17': 'Osman Karakadıoğlu',
  '18': 'İlhan Uzun',
  '180': 'Gülce Karakuş',
  '220': 'Alihan Köse',
  '225': 'Ahmet Emin Gezer',
  '250': 'Ahmet Şamil Ortaç',
  '3': 'Osman Karagül',
  '9': 'Halil Şendil',
  '97': 'Burak Taban',
};

export function legacyPersonLabel(
  legacyId: string | undefined,
  personLabels: Record<string, string> = {}
): string {
  const id = String(legacyId ?? '').trim();
  if (!id) return '';
  const keeperId = LEGACY_PERSON_KEEPER_ID_BY_LEGACY_KEY[id];
  if (keeperId && personLabels[keeperId]) return personLabels[keeperId];
  return LEGACY_PERSON_LABEL_BY_LEGACY_KEY[id] || '';
}

export function collectLegacyPersonKeeperIds(legacyIds: string[]): string[] {
  const out = new Set<string>();
  for (const raw of legacyIds) {
    const id = String(raw ?? '').trim();
    if (!id) continue;
    const keeperId = LEGACY_PERSON_KEEPER_ID_BY_LEGACY_KEY[id];
    if (keeperId) out.add(keeperId);
  }
  return [...out];
}