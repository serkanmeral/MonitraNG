import { diCreateFileResource } from '@/services/documentIntelligenceService';
import type { DiResource } from '@/types/apps/documentIntelligence';

/** Empty draw.io canvas. Saved as a DI file (`origin=upload`) and opened in the self-hosted editor. */
export const DI_EMPTY_DRAWIO_XML = `<?xml version="1.0" encoding="UTF-8"?>
<mxfile host="drawio" agent="MonitraNG" version="22.1.0" type="device">
  <diagram name="Page-1" id="page-1">
    <mxGraphModel dx="1200" dy="800" grid="1" gridSize="10" guides="1" tooltips="1" connect="1" arrows="1" fold="1" page="1" pageScale="1" pageWidth="1169" pageHeight="827" math="0" shadow="0">
      <root>
        <mxCell id="0"/>
        <mxCell id="1" parent="0"/>
      </root>
    </mxGraphModel>
  </diagram>
</mxfile>
`;

const EDITOR_QUERY =
  'embed=1&ui=atlas&spin=1&proto=json&libraries=1&saveAndExit=0&noExitBtn=1&offline=1&stealth=1&lockdown=1';

const VIEWER_QUERY =
  'embed=1&ui=min&spin=1&proto=json&chrome=0&lightbox=1&nav=1&layers=1&modified=0&saveAndExit=0&noSaveBtn=1&noExitBtn=1&offline=1&stealth=1&lockdown=1';

function stripSlash(value: string): string {
  return value.trim().replace(/\/+$/, '');
}

/** Browser origin of the self-hosted diagrams.net container (no trailing slash). */
export function getDiDrawioEmbedOrigin(): string {
  try {
    return stripSlash(String(useRuntimeConfig().public.drawioEmbedOrigin || ''));
  } catch {
    return '';
  }
}

export function getDiDrawioEditorSrc(): string {
  const origin = getDiDrawioEmbedOrigin();
  return origin ? `${origin}/?${EDITOR_QUERY}` : '';
}

export function getDiDrawioViewerSrc(): string {
  const origin = getDiDrawioEmbedOrigin();
  return origin ? `${origin}/?${VIEWER_QUERY}` : '';
}

export function isDiDrawioEmbedOrigin(origin: string): boolean {
  const expected = getDiDrawioEmbedOrigin();
  return Boolean(expected) && origin === expected;
}

export function utf8ToBase64(text: string): string {
  const bytes = new TextEncoder().encode(text);
  let binary = '';
  for (const b of bytes) binary += String.fromCharCode(b);
  return btoa(binary);
}

export function drawioFileName(title: string): string {
  const stem = title
    .trim()
    .replace(/[<>:"/\\|?*]+/g, ' ')
    .replace(/\s+/g, ' ')
    .slice(0, 80)
    .trim() || 'process';
  return stem.toLowerCase().endsWith('.drawio') ? stem : `${stem}.drawio`;
}

export async function diCreateBlankDrawio(
  parentId: string,
  title: string,
  options?: { tags?: string[] },
): Promise<DiResource> {
  const name = drawioFileName(title);
  return diCreateFileResource({
    parentId,
    name,
    originalFileName: name,
    mimeType: 'application/vnd.jgraph.mxfile',
    extension: 'drawio',
    kind: 'diagram',
    content: utf8ToBase64(DI_EMPTY_DRAWIO_XML),
    tags: options?.tags,
  });
}
