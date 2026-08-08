import assert from 'node:assert/strict';
import test from 'node:test';
import type { ScenarioDefinitionV2, ScenarioDefinitionV3 } from '../../types/apps/scenario.ts';
import {
  ensureScenarioV3,
  scenarioToVueFlow,
  vueFlowToScenario,
} from './scenarioFlowMapper.ts';

// Mirrors U4 from tests/fixtures/siem/scenario_templates/packages/siem-product-v3/manifest.json.
const productV3Fixture = {
  schemaVersion: 3,
  graph: {
    nodes: [
      {
        id: 'source',
        type: 'source',
        config: {
          source: {
            kind: 'observation',
            matchKey: 'u4',
          },
        },
      },
      {
        id: 'threshold',
        type: 'threshold',
        config: {
          aggregation: { function: 'count', operator: 'gte', threshold: 2 },
          window: { durationSeconds: 300 },
          settleAfterSeconds: 10,
        },
      },
      {
        id: 'alarm',
        type: 'alarm-output',
        config: {
          severity: 7,
          dedup: {
            keyTemplate: '{scenarioId}:{outputNodeId}:{groupKey}',
            cooldownSeconds: 60,
          },
        },
      },
    ],
    edges: [
      { id: 'e1', from: 'source', to: 'threshold', fromPort: 'next', toPort: 'in' },
      { id: 'e2', from: 'threshold', to: 'alarm', fromPort: 'true', toPort: 'in' },
    ],
  },
} as unknown as ScenarioDefinitionV3;

test('V3 API JSON hydrates and serializes with backend field names', () => {
  const hydrated = scenarioToVueFlow(productV3Fixture);
  const serialized = vueFlowToScenario(hydrated.nodes, hydrated.edges, productV3Fixture);

  assert.equal(serialized.schemaVersion, 3);
  assert.equal(serialized.graph.nodes[1].type, 'threshold');
  assert.equal(serialized.graph.nodes[1].config.settleAfterSeconds, 10);
  assert.deepEqual(serialized.graph.nodes[1].config.window, {
    durationSeconds: 300,
  });
  assert.deepEqual(serialized.graph.edges[1], {
    id: 'e2',
    from: 'threshold',
    to: 'alarm',
    fromPort: 'true',
    toPort: 'in',
  });
  assert.equal('kind' in serialized.graph.nodes[0], false);
  assert.equal('source' in serialized.graph.edges[0], false);
  assert.equal('runtime' in serialized, false);
  assert.equal('layout' in serialized, false);
  assert.equal('legacyV2' in serialized, false);
});

test('V3 alarm-output without severity inherits version severity', () => {
  const definition = {
    schemaVersion: 3,
    graph: {
      nodes: [
        {
          id: 'source',
          type: 'source',
          config: { source: { kind: 'observation', matchKey: 'x' } },
        },
        {
          id: 'alarm',
          type: 'alarm-output',
          config: {
            dedup: { keyTemplate: '{ruleId}:{key}', cooldownSeconds: 60 },
          },
        },
      ],
      edges: [
        { id: 'e1', from: 'source', to: 'alarm', fromPort: 'next', toPort: 'in' },
      ],
    },
  } as unknown as ScenarioDefinitionV3;

  const hydrated = scenarioToVueFlow(definition, 9);
  assert.equal(hydrated.nodes.at(-1)?.data.config.severity, 9);

  const serialized = vueFlowToScenario(hydrated.nodes, hydrated.edges, definition, 9);
  assert.equal(serialized.graph.nodes.at(-1)?.config.severity, 9);
});

  const v2: ScenarioDefinitionV2 = {
    schemaVersion: 2,
    source: {
      kind: 'observation',
      observationKind: 'event',
      matchKey: 'login_failed',
      dependsOnScenarioIds: [],
      maxChainDepth: 5,
    },
    condition: {
      children: [],
      field: 'dimensions.userId',
      operator: 'exists',
      value: true,
      sustainedForSeconds: 15,
    },
    aggregation: { function: 'count', operator: 'gte', threshold: 5 },
    groupBy: ['userId', 'srcIp'],
    window: { durationSeconds: 300, stalenessSeconds: 0 },
    dedup: { keyTemplate: '{ruleId}:{groupKey}', cooldownSeconds: 600 },
    hysteresis: { raiseThreshold: 5, clearThreshold: 2, minimumStateSeconds: 60 },
    metadata: { owner: 'SOC' },
  };

  const projected = ensureScenarioV3(v2, 8);
  assert.equal(projected.schemaVersion, 3);
  for (const key of [
    'source', 'condition', 'aggregation', 'groupBy', 'window',
    'dedup', 'hysteresis', 'metadata',
  ] as const) {
    assert.deepEqual(projected[key], v2[key]);
  }
  assert.equal(projected.graph.nodes.at(-1)?.type, 'alarm-output');
  assert.equal(projected.graph.nodes.at(-1)?.config.severity, 8);
  assert.deepEqual(
    projected.graph.edges.map(edge => edge.fromPort),
    ['next', 'true', 'true'],
  );
  assert.equal('legacyV2' in projected, false);
});
