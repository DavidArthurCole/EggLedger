const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const execSync = require('node:child_process').execSync;

const hashFilePath = 'www/vite.hash';

// Recursively collect all non-.hash files under a directory.
function getAllFiles(dirPath) {
  const entries = fs.readdirSync(dirPath, { withFileTypes: true });
  const files = [];
  for (const entry of entries) {
    const fullPath = path.join(dirPath, entry.name).replaceAll('\\', '/');
    if (entry.isDirectory()) {
      files.push(...getAllFiles(fullPath));
    } else if (!entry.name.endsWith('.hash')) {
      files.push(fullPath);
    }
  }
  return files;
}

// Hash all source files that Vite consumes.
// Excludes *.hash files to avoid feedback loops with build-css.js.
function computeSourceHash() {
  const hash = crypto.createHash('md5');
  const dirs = ['www/src', 'www/public'];
  const extraFiles = ['www/vite.config.ts'];

  const files = [];
  for (const dir of dirs) {
    if (fs.existsSync(dir)) files.push(...getAllFiles(dir));
  }
  for (const f of extraFiles) {
    if (fs.existsSync(f)) files.push(f);
  }

  for (const file of files.toSorted((a, b) => a.localeCompare(b))) {
    hash.update(file);
    hash.update(fs.readFileSync(file));
  }
  return hash.digest('hex');
}

const forceRebuild = process.argv.includes('--force');
const currentHash = computeSourceHash();

function distHasContent() {
  const distDir = 'www/dist';
  try {
    return fs.readdirSync(distDir).length > 0;
  } catch {
    return false;
  }
}

if (forceRebuild) {
  console.log('Force rebuild requested.');
} else if (fs.existsSync(hashFilePath)) {
  const storedHash = fs.readFileSync(hashFilePath, 'utf-8').trim();
  if (storedHash === currentHash) {
    if (distHasContent()) {
      console.log('Vite sources unchanged. Skipping build.');
      process.exit(0);
    }
    console.log('Hash matches but dist is missing or empty. Rebuilding...');
  } else {
    console.log('Vite source hash mismatch. Rebuilding...');
  }
} else {
  console.log('Hash file not found. Rebuilding...');
}

const binPath = path.join(__dirname, '..', 'node_modules', '.bin');
const env = { ...process.env, PATH: binPath + path.delimiter + process.env.PATH };
execSync('vite build --config www/vite.config.ts', { stdio: 'inherit', env });
fs.writeFileSync(hashFilePath, currentHash);
console.log('Rebuild completed.');
