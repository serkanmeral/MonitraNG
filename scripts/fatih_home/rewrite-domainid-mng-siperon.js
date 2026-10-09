// Rewrite domainId in mng_siperon to the siperon domain on 10.0.10.52.
// Skips @users and @groups (already created for siperon).
const NEW_ID = '6a89c0999b3629cdd970dce6';
const SKIP = { '@users': true, '@groups': true };
const d = db.getSiblingDB('mng_siperon');

function idKey(v) {
  if (v === null || v === undefined) return '';
  if (typeof v === 'object' && typeof v.toHexString === 'function') return v.toHexString();
  return String(v);
}

print('NEW_ID=' + NEW_ID);

const before = {};
const afterPlan = [];
let matched = 0;
let modified = 0;

d.getCollectionNames().forEach(function (name) {
  if (SKIP[name]) return;
  const c = d.getCollection(name);
  const distinct = c.distinct('domainId');
  if (!distinct || distinct.length === 0) return;
  before[name] = distinct.map(idKey);
  distinct.forEach(function (oldVal) {
    const key = idKey(oldVal);
    if (!key || key === NEW_ID) return;
    const r = c.updateMany({ domainId: oldVal }, { $set: { domainId: NEW_ID } });
    matched += r.matchedCount;
    modified += r.modifiedCount;
    afterPlan.push({
      collection: name,
      from: key,
      matched: r.matchedCount,
      modified: r.modifiedCount,
    });
  });
});

print('DOMAINID_BEFORE=' + JSON.stringify(before));
print('DOMAINID_UPDATES=' + JSON.stringify(afterPlan));
print('domainId matched=' + matched + ' modified=' + modified);

const after = {};
d.getCollectionNames().forEach(function (name) {
  if (SKIP[name]) return;
  const vals = d.getCollection(name).distinct('domainId');
  if (vals && vals.length) after[name] = vals.map(idKey);
});
print('DOMAINID_AFTER=' + JSON.stringify(after));

const userSample = d.getCollection('@users').findOne({}, { username: 1, domainId: 1 });
print('USERS_SAMPLE username=' + (userSample && userSample.username) + ' domainId=' + idKey(userSample && userSample.domainId));
const dsSample = d.getCollection('@datasets').findOne({ domainId: { $exists: true } }, { name: 1, domainId: 1 });
print('DATASET_SAMPLE name=' + (dsSample && dsSample.name) + ' domainId=' + idKey(dsSample && dsSample.domainId));
