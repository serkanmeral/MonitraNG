import assert from 'node:assert/strict';
import test from 'node:test';
import {
  applyMergeScopeToConfig,
  buildAlarmDedupTemplate,
  inferMergeScope,
  normalizeAlarmDedup,
} from './alarmOutputMerge.ts';
import type { ScenarioNodeConfig } from '../../types/apps/scenario.ts';

test('normalizeAlarmDedup defaults mergeEnabled to true', () => {
  const dedup = normalizeAlarmDedup({ keyTemplate: '{ruleId}:{key}', cooldownSeconds: 120 });
  assert.equal(dedup.mergeEnabled, true);
  assert.equal(dedup.cooldownSeconds, 120);
});

test('merge scope host builds groupBy and template', () => {
  const config: ScenarioNodeConfig = {
    groupBy: [],
    settleAfterSeconds: 0,
    severity: 7,
    dedup: normalizeAlarmDedup(),
  };
  const next = applyMergeScopeToConfig(config, 'host');
  assert.equal(next.dedup?.mergeEnabled, true);
  assert.deepEqual(next.groupBy, ['sourceHost']);
  assert.equal(next.dedup?.keyTemplate, buildAlarmDedupTemplate(['sourceHost']));
  assert.equal(inferMergeScope(true, next.groupBy), 'host');
});

test('merge scope none disables merge', () => {
  const config: ScenarioNodeConfig = {
    groupBy: ['sourceHost'],
    settleAfterSeconds: 0,
    dedup: normalizeAlarmDedup({ mergeEnabled: true, cooldownSeconds: 300 }),
  };
  const next = applyMergeScopeToConfig(config, 'none');
  assert.equal(next.dedup?.mergeEnabled, false);
  assert.deepEqual(next.groupBy, []);
  assert.equal(inferMergeScope(false, next.groupBy), 'none');
});
