const fs = require('node:fs');
const crypto = require('node:crypto');
const execSync = require('node:child_process').execSync;

const configPath = 'tailwind.config.js';
const configHashPath = 'tailwind.config.hash';
const indexPath = 'www/index.pcss';
const outputCssPath = 'www/index.css';
const cssHashFilePath = 'www/index_pcss.hash';

const calculateHash = filePath => { return crypto.createHash('md5').update(fs.readFileSync(filePath, 'utf-8')).digest('hex'); }
const updateHashFile = (path, hash) => { fs.writeFileSync(path, hash); }

if (fs.existsSync(cssHashFilePath) && fs.existsSync(configHashPath)) {
  const storedCSSHash = fs.readFileSync(cssHashFilePath, 'utf-8').trim();
  const currentCSSHash = calculateHash(indexPath);

  const storedConfigHash = fs.readFileSync(configHashPath, 'utf-8').trim();
  const currentConfigHash = calculateHash(configPath);

  if (storedCSSHash === currentCSSHash && storedConfigHash === currentConfigHash) {
    console.log('CSS hashes match. Skipping rebuild.');
  } else {
    console.log('CSS hash mismatch. Rebuilding CSS.')
    execSync(`tailwindcss -i ${indexPath} -o ${outputCssPath}`);
    updateHashFile(cssHashFilePath, currentCSSHash);
    updateHashFile(configHashPath, currentConfigHash);
    console.log('CSS rebuild completed.');
  }
} else {
  console.log('Hash file not found. Rebuilding CSS.');
  execSync(`tailwindcss -i ${indexPath} -o ${outputCssPath}`);
  updateHashFile(cssHashFilePath, calculateHash(indexPath));
  updateHashFile(configHashPath, calculateHash(configPath));
  console.log('CSS rebuild completed.');
}