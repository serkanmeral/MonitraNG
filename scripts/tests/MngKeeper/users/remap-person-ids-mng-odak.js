// Remap persons/personGroups in mng_odak using /tmp/user-id-map.json and /tmp/group-id-map.json
// Map files: [ { oldId, newId }, ... ]
// whatIf via whatIfFlag from --eval

const fs = require('fs');
const whatIf = typeof whatIfFlag !== 'undefined' ? !!whatIfFlag : false;

function loadMap(path) {
  const raw = fs.readFileSync(path, 'utf8');
  const arr = JSON.parse(raw);
  const map = {};
  for (const row of arr) {
    if (row.oldId && row.newId) map[String(row.oldId)] = String(row.newId);
  }
  return map;
}

const userMap = loadMap('/tmp/user-id-map.json');
const groupMap = loadMap('/tmp/group-id-map.json');
const d = db.getSiblingDB('mng_odak');

function remapOne(val, map) {
  if (val === null || val === undefined) return { value: val, changed: false };
  if (Array.isArray(val)) {
    let changed = false;
    const next = val.map((x) => {
      if (x === null || x === undefined) return x;
      const s = String(x);
      if (Object.prototype.hasOwnProperty.call(map, s)) {
        changed = true;
        return map[s];
      }
      return x;
    });
    return { value: next, changed };
  }
  const s = String(val);
  if (Object.prototype.hasOwnProperty.call(map, s)) return { value: map[s], changed: true };
  return { value: val, changed: false };
}

const datasets = d.getCollection('@datasets').find({}).toArray();
const targets = [];
for (const doc of datasets) {
  const coll = doc.name || doc.__dataId;
  if (!coll) continue;
  for (const f of doc.fields || []) {
    if (f.fieldType === 'persons' || f.fieldType === 'personGroups') {
      targets.push({
        collection: coll,
        field: f.name,
        type: f.fieldType,
        mapKind: f.fieldType === 'persons' ? 'user' : 'group',
      });
    }
  }
}

print('userMapKeys=' + Object.keys(userMap).length);
print('groupMapKeys=' + Object.keys(groupMap).length);
print('targets=' + targets.length);
print('whatIf=' + whatIf);

const report = {
  whatIf,
  fields: [],
  totals: { docsScanned: 0, docsUpdated: 0 },
};

const names = d.getCollectionNames();
for (const t of targets) {
  if (names.indexOf(t.collection) < 0) {
    report.fields.push({ collection: t.collection, field: t.field, skipped: 'collection missing' });
    continue;
  }
  const coll = d.getCollection(t.collection);
  const map = t.mapKind === 'user' ? userMap : groupMap;
  const filter = {};
  filter[t.field] = { $exists: true, $ne: null };
  const cursor = coll.find(filter);
  let scanned = 0;
  let updated = 0;
  while (cursor.hasNext()) {
    const doc = cursor.next();
    scanned++;
    const r = remapOne(doc[t.field], map);
    if (!r.changed) continue;
    if (!whatIf) {
      const setDoc = { $set: {} };
      setDoc.$set[t.field] = r.value;
      coll.updateOne({ _id: doc._id }, setDoc);
    }
    updated++;
  }
  report.fields.push({
    collection: t.collection,
    field: t.field,
    type: t.type,
    scanned,
    updated,
  });
  report.totals.docsScanned += scanned;
  report.totals.docsUpdated += updated;
  print(t.collection + '.' + t.field + ' scanned=' + scanned + ' updated=' + updated);
}

print('REPORT_JSON_BEGIN');
print(JSON.stringify(report));
print('REPORT_JSON_END');
