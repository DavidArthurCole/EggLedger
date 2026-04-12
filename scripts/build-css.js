const fs = require('node:fs');
const crypto = require('node:crypto');
const execSync = require('node:child_process').execSync;

const configPath = 'www/tailwind.config.js';
const configHashPath = 'tailwind.config.hash';
const indexPath = 'www/src/index.css';
const cssHashFilePath = 'www/src/index_css.hash';

const calculateHash = filePath => crypto.createHash('md5').update(fs.readFileSync(filePath, 'utf-8')).digest('hex');
const updateHashFile = (path, hash) => fs.writeFileSync(path, hash);

if (fs.existsSync(cssHashFilePath) && fs.existsSync(configHashPath)) {
  const storedCSSHash = fs.readFileSync(cssHashFilePath, 'utf-8').trim();
  const currentCSSHash = calculateHash(indexPath);

  const storedConfigHash = fs.readFileSync(configHashPath, 'utf-8').trim();
  const currentConfigHash = calculateHash(configPath);

  if (storedCSSHash === currentCSSHash && storedConfigHash === currentConfigHash) {
    console.log('CSS hashes match. Skipping rebuild.');
  } else {
    console.log('CSS hash mismatch. Rebuilding...');
    execSync('npm run build:force', { stdio: 'inherit' });
    updateHashFile(cssHashFilePath, currentCSSHash);
    updateHashFile(configHashPath, currentConfigHash);
    console.log('Rebuild completed.');
  }
} else {
  console.log('Hash file not found. Rebuilding...');
  execSync('npm run build:force', { stdio: 'inherit' });
  updateHashFile(cssHashFilePath, calculateHash(indexPath));
  updateHashFile(configHashPath, calculateHash(configPath));
  console.log('Rebuild completed.');
}
