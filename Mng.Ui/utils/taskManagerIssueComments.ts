import type { TmIssueComment } from '@/types/apps/taskManager';

/** Görev yorumu metninde mention: `@[userId]` (userId köşeli parantez içinde, boşluk yok). */
export const TM_COMMENT_MENTION_PATTERN = /@\[([^\]]+)\]/g;

export type CommentBodySegment =
  | { type: 'text'; text: string }
  | { type: 'mention'; userId: string };

/**
 * Yorum gövdesini güvenli parçalara ayırır (HTML üretmez; şablon tarafında escape).
 */
export function parseCommentBodySegments(body: string): CommentBodySegment[] {
  if (!body) return [{ type: 'text', text: '' }];
  const out: CommentBodySegment[] = [];
  let last = 0;
  const re = new RegExp(TM_COMMENT_MENTION_PATTERN.source, 'g');
  let m: RegExpExecArray | null;
  while ((m = re.exec(body)) !== null) {
    if (m.index > last) {
      out.push({ type: 'text', text: body.slice(last, m.index) });
    }
    const userId = (m[1] ?? '').trim();
    if (userId) out.push({ type: 'mention', userId });
    last = m.index + m[0].length;
  }
  if (last < body.length) out.push({ type: 'text', text: body.slice(last) });
  if (!out.length) return [{ type: 'text', text: body }];
  return out;
}

/** `@[userId]` ekle (mention seçiciden). */
export function mentionTokenForUserId(userId: string): string {
  const id = String(userId ?? '').trim();
  return id ? `@[${id}]` : '';
}

export interface TmIssueCommentWithDepth {
  comment: TmIssueComment;
  depth: number;
}

/** Üst yorum zinciri uzunluğu (kök = 0, bir yanıt = 1, …). */
export function replyDepthFor(comment: TmIssueComment, byId: Map<string, TmIssueComment>): number {
  let d = 0;
  let cur: TmIssueComment | undefined = comment;
  for (let guard = 0; guard < 40 && cur?.parentCommentId; guard++) {
    d++;
    cur = byId.get(cur.parentCommentId);
  }
  return d;
}

/** Zaman sırası + girinti (çok seviyeli yanıt). */
export function flattenCommentsWithDepth(comments: TmIssueComment[]): TmIssueCommentWithDepth[] {
  if (!comments.length) return [];
  const byId = new Map(comments.map((c) => [c.__dataId, c]));
  const sorted = [...comments].sort((a, b) => String(a.createdAt ?? '').localeCompare(String(b.createdAt ?? '')));
  return sorted.map((comment) => ({
    comment,
    depth: replyDepthFor(comment, byId),
  }));
}
