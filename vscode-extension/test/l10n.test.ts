// "Everything in English and Spanish": every string the extension shows
// has a Spanish translation, and every %key% in package.json exists in both
// languages.
import { test } from 'node:test';
import * as assert from 'node:assert/strict';
import * as fs from 'node:fs';
import * as path from 'node:path';

const root = path.join(__dirname, '..', '..');
const read = (f: string) => fs.readFileSync(path.join(root, f), 'utf8');

test('every runtime string has a Spanish translation', () => {
  const es: Record<string, string> = JSON.parse(read('l10n/bundle.l10n.es.json'));
  const src = read('src/extension.ts');
  const used = [...src.matchAll(/\bt\(\s*'((?:[^'\\]|\\.)*)'/g)].map(m => m[1].replace(/\\'/g, "'"));
  assert.ok(used.length > 30);
  const missing = used.filter(s => !es[s]);
  assert.deepEqual(missing, [], 'missing from l10n/bundle.l10n.es.json');
  const unused = Object.keys(es).filter(k => !used.includes(k));
  assert.deepEqual(unused, [], 'translations nothing uses');
  for (const [en, sp] of Object.entries(es)) {
    const holes = (s: string) => (s.match(/\{\d\}/g) ?? []).sort().join();
    assert.equal(holes(sp), holes(en), `placeholders differ: ${en}`);
  }
});

test('package.json is localized in both languages', () => {
  const keys = [...read('package.json').matchAll(/"%([^%"]+)%"/g)].map(m => m[1]);
  const en = JSON.parse(read('package.nls.json'));
  const es = JSON.parse(read('package.nls.es.json'));
  assert.ok(keys.length > 20);
  assert.deepEqual(keys.filter(k => !(k in en)), [], 'missing from package.nls.json');
  assert.deepEqual(keys.filter(k => !(k in es)), [], 'missing from package.nls.es.json');
  assert.deepEqual(Object.keys(en).sort(), Object.keys(es).sort());
});
