#!/bin/sh
# Convert odak_egitimler legacy US/ISO datetime strings -> BSON Date (idempotent for already-Date values).
set -e
docker exec mongo mongosh --quiet -u admin -p admin123 --authenticationDatabase admin mng_odak --eval '
function toDate(v) {
  if (v == null) return null;
  if (v instanceof Date) return v;
  if (typeof v !== "string") return null;
  var d = new Date(v);
  return isNaN(d.getTime()) ? null : d;
}
var fields = ["gerceklesenTarih", "planlananTarih"];
var updated = 0;
fields.forEach(function(field) {
  db.odak_egitimler.find({ [field]: { $type: "string" } }).forEach(function(doc) {
    var d = toDate(doc[field]);
    if (!d) return;
    db.odak_egitimler.updateOne({ _id: doc._id }, { $set: { [field]: d } });
    updated++;
  });
});
print("updated fields:", updated);
print("2017 Tamamlandi (date range):", db.odak_egitimler.countDocuments({
  durum: "Tamamlandi",
  gerceklesenTarih: { $gte: ISODate("2017-01-01T00:00:00Z"), $lte: ISODate("2017-12-31T23:59:59.999Z") }
}));
print("sample:", JSON.stringify(db.odak_egitimler.findOne({ egitimNo: "EGTM2017/2" }, { gerceklesenTarih: 1, egitimNo: 1 })));
'
