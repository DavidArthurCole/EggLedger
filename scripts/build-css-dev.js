const execSync = require('node:child_process').execSync;
execSync(`tailwindcss -i www/index.pcss -o www/index.css --watch`);