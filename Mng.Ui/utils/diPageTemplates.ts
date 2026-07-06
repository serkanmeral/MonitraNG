/** Sayfa (markdown) oluşturma şablonları — Faz P-E P1 */

export type DiPageTemplateId = 'blank' | 'runbook' | 'setup' | 'releaseNotes';

export interface DiPageTemplateDefinition {
  id: DiPageTemplateId;
  labelKey: string;
  content: string;
}

export const DI_PAGE_TEMPLATE_DEFINITIONS: DiPageTemplateDefinition[] = [
  {
    id: 'blank',
    labelKey: 'documentIntelligence.templates.blank',
    content: '',
  },
  {
    id: 'runbook',
    labelKey: 'documentIntelligence.templates.runbook',
    content: `# Runbook: [Başlık]

## Özet
Kısa açıklama ve etki alanı.

## Ön koşullar
- Erişim / yetki:
- Araçlar:

## Adımlar
1. 
2. 

## Doğrulama
- [ ] Beklenen sonuç doğrulandı

## Geri alma
Sorun çıkarsa:

## İlgili kaynaklar
- 
`,
  },
  {
    id: 'setup',
    labelKey: 'documentIntelligence.templates.setup',
    content: `# Kurulum: [Ürün veya servis]

## Gereksinimler
| Bileşen | Minimum sürüm | Not |
| --- | --- | --- |
| | | |

## Kurulum adımları

### 1. Hazırlık


### 2. Kurulum


### 3. Yapılandırma


## Doğrulama
- [ ] Servis erişilebilir
- [ ] Log / health kontrolü OK

## Sorun giderme
| Belirti | Olası neden | Çözüm |
| --- | --- | --- |
| | | |
`,
  },
  {
    id: 'releaseNotes',
    labelKey: 'documentIntelligence.templates.releaseNotes',
    content: `# Sürüm [X.Y.Z] — [Tarih]

## Yenilikler
- 

## İyileştirmeler
- 

## Düzeltmeler
- 

## Bilinen sorunlar
- 

## Yükseltme notları
- 
`,
  },
];

export function getDiPageTemplateById(id: DiPageTemplateId): DiPageTemplateDefinition | undefined {
  return DI_PAGE_TEMPLATE_DEFINITIONS.find((item) => item.id === id);
}

export function getDiPageTemplateContent(id: DiPageTemplateId): string {
  return getDiPageTemplateById(id)?.content ?? '';
}
