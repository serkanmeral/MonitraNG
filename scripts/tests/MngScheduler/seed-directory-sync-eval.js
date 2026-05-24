const j = {
  jobId: 'system-directory-sync-all-domains',
  jobType: 0,
  name: 'Directory Sync (All Active Domains)',
  description: 'Periyodik Keycloak → Mongo directory sync; her Active domain için MngKeeper POST',
  cronExpression: '0/30 * * * * ?',
  endpointUrl: 'orchestration://directory-sync',
  httpMethod: 'POST',
  isActive: true,
  totalExecutionCount: 0,
  successfulExecutionCount: 0,
  failedExecutionCount: 0,
  timeoutSeconds: 600,
  createdAt: new Date(),
  createdBy: 'system'
};
const r = db.getCollection('@scheduled_jobs').updateOne(
  { jobId: j.jobId },
  { $set: j },
  { upsert: true }
);
print('upsert:');
printjson(r);
print('document:');
printjson(db.getCollection('@scheduled_jobs').findOne(
  { jobId: j.jobId },
  { jobId: 1, cronExpression: 1, isActive: 1, name: 1 }
));
