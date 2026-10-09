import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

// Runs after all three production builds in the Staff container stage.
// Development-only login help must be eliminated, not merely hidden with CSS.
for (const role of ['chef', 'cashier', 'admin']) {
  test(`${role} production bundle excludes demo login credentials`, () => {
    const assets = join(role, 'dist', 'assets');
    const scripts = readdirSync(assets).filter(file => file.endsWith('.js'));
    assert.ok(scripts.length > 0);
    for (const file of scripts) {
      const bundle = readFileSync(join(assets, file), 'utf8');
      assert.equal(bundle.includes('chef_hung'), false, 'Demo login help leaked');
      assert.equal(bundle.includes('cashier_lan'), false, 'Demo login help leaked');
      assert.equal(/sb_secret_[A-Za-z0-9_-]{15,}/.test(bundle), false, 'Service key leaked');
    }
  });
}
