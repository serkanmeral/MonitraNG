import assert from 'node:assert/strict';
import test from 'node:test';
import {
  buildLibraryGrants,
  decideLibrarySync,
  folderPermissionStamp,
  libraryPermissionStamp,
} from './ocWorkspaceLibraryPermissions.ts';

const groups = [
  { id: 'dev', name: 'Dev-CarbonIT' },
  { id: 'yon', name: 'Yonetim' },
];

test('view, edit and admin grants union on the same group', () => {
  const grants = buildLibraryGrants(['dev', 'Dev-CarbonIT'], ['dev'], ['yon'], groups);
  const dev = grants.find((row) => row.groupId === 'dev');
  const yon = grants.find((row) => row.groupId === 'yon');
  assert.deepEqual(dev?.permissions, ['create', 'delete', 'download', 'edit', 'move', 'upload', 'view']);
  assert.ok(yon?.permissions.includes('share'));
  assert.equal(grants.some((row) => row.groupName === 'owners'), false);
});

test('unknown tokens are skipped and an empty list stays open', () => {
  assert.deepEqual(buildLibraryGrants(['missing'], [], [], groups), []);
  assert.equal(libraryPermissionStamp([]), 'open');
});

test('folder stamp ignores order and an inherited folder is open', () => {
  assert.equal(
    folderPermissionStamp({
      inheritanceBroken: false,
      groups: [{ groupName: 'Yonetim', permissions: ['view'] }],
    }),
    'open',
  );
  const left = folderPermissionStamp({
    inheritanceBroken: true,
    groups: [{ groupName: 'Yonetim', permissions: ['download', 'view'] }],
  });
  const right = libraryPermissionStamp([
    { groupId: 'yon', groupName: 'Yonetim', permissions: ['view', 'download'] },
  ]);
  assert.equal(left, right);
});

test('first open folder is copied and a custom folder detaches', () => {
  const open = decideLibrarySync({
    storedStamp: null,
    detached: false,
    actualStamp: 'open',
    desiredStamp: 'dev',
    force: false,
  });
  assert.equal(open.write, true);
  assert.equal(open.detached, false);

  const custom = decideLibrarySync({
    storedStamp: null,
    detached: false,
    actualStamp: 'custom',
    desiredStamp: 'dev',
    force: false,
  });
  assert.equal(custom.write, false);
  assert.equal(custom.detached, true);
  assert.equal(custom.stamp, null);
});

test('a matching copy is refreshed and a diverged copy stays', () => {
  const refresh = decideLibrarySync({
    storedStamp: 'old',
    detached: false,
    actualStamp: 'old',
    desiredStamp: 'new',
    force: false,
  });
  assert.equal(refresh.write, true);
  assert.equal(refresh.stamp, 'new');

  const diverged = decideLibrarySync({
    storedStamp: 'old',
    detached: false,
    actualStamp: 'hand',
    desiredStamp: 'new',
    force: false,
  });
  assert.equal(diverged.write, false);
  assert.equal(diverged.detached, true);
  assert.equal(diverged.stamp, 'old');
});

test('force rewrite clears a previous detachment', () => {
  const forced = decideLibrarySync({
    storedStamp: null,
    detached: true,
    actualStamp: 'hand',
    desiredStamp: 'new',
    force: true,
  });
  assert.equal(forced.write, true);
  assert.equal(forced.detached, false);
  assert.equal(forced.stamp, 'new');
});
