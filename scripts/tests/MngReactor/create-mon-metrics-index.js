// mon_metrics Time Series koleksiyonu icin compound index
// Sorgu: { "$and": [{ "meta.assetId": "..." }, { "meta.collectibleCode": "..." }] }, sort: -timestamp
//
// Kullanim (domain DB secili olmali - ornek: mng_meral):
//   mongosh "mongodb://localhost:27017"
//   use mng_meral
//   load('scripts/tests/MngReactor/create-mon-metrics-index.js')
//
// Not: Reactor ilk ingest'te index'i otomatik olusturur. Bu script manuel kurulum icindir.

const coll = db.getCollection('mon_metrics');
const indexName = 'idx_assetId_collectibleCode_timestamp';
const indexSpec = { 'meta.assetId': 1, 'meta.collectibleCode': 1, 'timestamp': -1 };

const existing = coll.getIndexes().filter((i) => i.name === indexName);
if (existing.length > 0) {
  print('Index zaten mevcut: ' + indexName);
} else {
  coll.createIndex(indexSpec, { name: indexName });
  print('Index olusturuldu: ' + indexName + ' (db: ' + db.getName() + ')');
}
