/** Keeper liste gelişmiş filtre seçenekleri */

export function keeperProvisioningSourceSelectItems(
  t: (key: string) => string
): { value: string; title: string }[] {
  return [
    { value: '0', title: t('users.source.local') },
    { value: '1', title: t('users.source.directory') },
  ];
}
