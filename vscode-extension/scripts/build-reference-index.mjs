// Builds reference-index.json -- every name in the online reference's A-Z
// page with the page that documents it -- for "Open the reference".
//   node scripts/build-reference-index.mjs   (run from vscode-extension/)
import { readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const here = dirname(fileURLToPath(import.meta.url));
const html = readFileSync(join(here, '..', '..', 'reference', 'a-z.html'), 'utf8');
const entry = /<li data-key="[^"]*"><a href="([^"]+)"><code>([^<]+)<\/code><\/a>\s*<span class="kind">([^<]+)<\/span>/g;

const index = {};
for (const [, href, name, kind] of html.matchAll(entry)) {
  index[name] ??= { href: `reference/${href}`, kind };
}
writeFileSync(join(here, '..', 'reference-index.json'), JSON.stringify(index, null, 1) + '\n');
console.log(`${Object.keys(index).length} names -> reference-index.json`);
