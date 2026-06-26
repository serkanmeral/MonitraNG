import type { DiTemplatePageLayout } from '@/types/apps/documentIntelligence';

/** Word twips per centimetre (ODK reference). */
export const DI_TWIPS_PER_CM = 567;

export const DI_DEFAULT_PAGE_LAYOUT: DiTemplatePageLayout = {
  marginTopTwips: 1440,
  marginRightTwips: 1797,
  marginBottomTwips: 1440,
  marginLeftTwips: 1797,
  headerDistanceTwips: 709,
  footerDistanceTwips: 658,
  footerLeftIndentTwips: -567,
};

export function diTwipsToCm(twips: number): number {
  return Math.round((twips / DI_TWIPS_PER_CM) * 100) / 100;
}

export function diCmToTwips(cm: number): number {
  return Math.round(cm * DI_TWIPS_PER_CM);
}

export function diCreateDefaultPageLayout(): DiTemplatePageLayout {
  return { ...DI_DEFAULT_PAGE_LAYOUT };
}

export function diNormalizePageLayout(raw: Partial<DiTemplatePageLayout> | null | undefined): DiTemplatePageLayout {
  const defaults = diCreateDefaultPageLayout();
  if (!raw) return defaults;
  return {
    marginTopTwips: raw.marginTopTwips ?? defaults.marginTopTwips,
    marginRightTwips: raw.marginRightTwips ?? defaults.marginRightTwips,
    marginBottomTwips: raw.marginBottomTwips ?? defaults.marginBottomTwips,
    marginLeftTwips: raw.marginLeftTwips ?? defaults.marginLeftTwips,
    headerDistanceTwips: raw.headerDistanceTwips ?? defaults.headerDistanceTwips,
    footerDistanceTwips: raw.footerDistanceTwips ?? defaults.footerDistanceTwips,
    footerLeftIndentTwips: raw.footerLeftIndentTwips ?? defaults.footerLeftIndentTwips,
  };
}
