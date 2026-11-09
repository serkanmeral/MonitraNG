/** Kayıt klasörü adı. Boş parçalar düşer. Klasör ayracı ada girmez. */

export function afRecordFolderName(
  values: Record<string, unknown> | null | undefined,
  nameFields: string[]
): string {
  const parts: string[] = [];
  for (const fieldName of nameFields) {
    const value = values?.[fieldName];
    if (typeof value === 'string' || typeof value === 'number') {
      const text = String(value).trim();
      if (text) parts.push(text);
    }
  }
  return parts.join(' ').replace(/[\\/]/g, ' ').replace(/\s+/g, ' ').trim();
}
