// Build normalized side menu summary from mongoexport JSON array (stdin path argv[2])
const fs = require('fs');

function isHeader(doc) {
  return doc.itemType === 'header' || (typeof doc.header === 'string' && doc.header.length > 0);
}

function stableKey(doc) {
  if (doc.pageCode) return 'pc:' + doc.pageCode;
  if (doc.to) return 'to:' + doc.to;
  if (isHeader(doc)) return 'hdr:' + (doc.header || doc.title || doc.pageCode || '');
  if (doc.__dataId) return 'id:' + doc.__dataId;
  return null;
}

function normGroups(groups) {
  if (!groups) return null;
  if (Array.isArray(groups)) return groups.slice().sort();
  if (typeof groups === 'object') {
    const out = {};
    for (const k of Object.keys(groups).sort((a, b) => a.localeCompare(b))) {
      out[k.toLowerCase()] = groups[k];
    }
    return out;
  }
  return groups;
}

const inputPath = process.argv[2];
const docs = JSON.parse(fs.readFileSync(inputPath, 'utf8'));
const lines = [];
for (const doc of docs) {
  lines.push(JSON.stringify({
    key: stableKey(doc),
    pageCode: doc.pageCode || null,
    to: doc.to || null,
    header: doc.header || null,
    title: doc.title || null,
    order: doc.order,
    pageType: doc.pageType || null,
    disabled: !!doc.disabled,
    parentId: doc.parentId || null,
    permissions: doc.permissions ? { groups: normGroups(doc.permissions.groups) } : null,
  }));
}
process.stdout.write(lines.join('\n'));
