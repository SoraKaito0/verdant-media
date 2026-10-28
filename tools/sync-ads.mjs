import fs from 'node:fs';
import path from 'node:path';

const repoRoot = process.cwd();
const catalogPath = path.join(repoRoot, 'ads', 'ads.json');
const postersDir = path.join(repoRoot, 'ads', 'posters');

const supported = new Set(['.png', '.jpg', '.jpeg', '.webp']);
const catalog = JSON.parse(fs.readFileSync(catalogPath, 'utf8'));

catalog.schemaVersion ??= 1;
catalog.enabled ??= true;
catalog.defaultDisplaySeconds ??= 12;
catalog.items ??= [];
catalog.ignoredImages ??= [];

const ignored = new Set(
  catalog.ignoredImages
    .filter(value => typeof value === 'string')
    .map(value => value.replaceAll('\\\\', '/').toLowerCase())
);

const existingByImage = new Map(
  catalog.items
    .filter(item => item && typeof item.image === 'string')
    .map(item => [item.image.replaceAll('\\', '/').toLowerCase(), item])
);

const files = fs.readdirSync(postersDir)
  .filter(name => supported.has(path.extname(name).toLowerCase()))
  .sort((a, b) => a.localeCompare(b, undefined, { numeric: true }));

let added = 0;

for (const file of files) {
  const image = `ads/posters/${file}`;
  const key = image.toLowerCase();
  if (existingByImage.has(key) || ignored.has(key)) continue;

  const base = path.basename(file, path.extname(file));
  const pretty = base
    .replace(/^ad[-_ ]*/i, 'Ad ')
    .replace(/[-_]+/g, ' ')
    .replace(/\b\w/g, c => c.toUpperCase())
    .trim();

  catalog.items.push({
    id: base.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '') || `ad-${catalog.items.length + 1}`,
    enabled: true,
    label: 'COMMUNITY',
    title: pretty || 'Community Ad',
    image,
    displaySeconds: catalog.defaultDisplaySeconds
  });

  added++;
}

catalog.items.sort((a, b) =>
  String(a.image || '').localeCompare(String(b.image || ''), undefined, { numeric: true })
);

catalog.catalogVersion = Number(catalog.catalogVersion || 0) + (added > 0 ? 1 : 0);

fs.writeFileSync(catalogPath, JSON.stringify(catalog, null, 2) + '\n');

if (added > 0) {
  console.log(`Added ${added} new poster(s) to ads/ads.json.`);
} else {
  console.log('No missing ad posters found.');
}
