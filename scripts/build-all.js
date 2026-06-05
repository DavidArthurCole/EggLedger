const path = require('node:path');
const { spawn } = require('node:child_process');

// Orchestrates the full local build: CSS -> Vite -> go mod vendor -> go build.
// The two go steps produce no stdout, so this wraps them with a live spinner +
// elapsed-time heartbeat so they never look frozen.

const repoRoot = path.join(__dirname, '..');

// The CSS and Vite steps are plain node scripts. Spawning node directly (via
// process.execPath) avoids npm.cmd, which Windows cannot spawn without
// shell: true - and shell: true triggers the DEP0190 arg-escaping warning.
const nodeExe = process.execPath;

// Runs a command, streaming its stdio straight through. Used for the node
// build steps that already print their own progress.
function runInherited(label, command, args) {
  return new Promise((resolve, reject) => {
    process.stdout.write(`\n${label}\n`);
    const child = spawn(command, args, {
      cwd: repoRoot,
      stdio: 'inherit',
    });
    child.on('error', reject);
    child.on('close', (code) => {
      if (code === 0) resolve();
      else reject(new Error(`${label} failed (exit ${code})`));
    });
  });
}

const spinnerFrames = ['|', '/', '-', '\\'];

// Runs a quiet command with a spinner + elapsed seconds on a single rewritten
// line. Captures output and only prints it if the command fails, so success
// stays clean but failures stay debuggable.
function runWithSpinner(label, command, args) {
  return new Promise((resolve, reject) => {
    const start = Date.now();
    let frame = 0;
    const interactive = process.stdout.isTTY;
    let captured = '';

    process.stdout.write(`\n${label}\n`);

    const tick = () => {
      const elapsed = ((Date.now() - start) / 1000).toFixed(1);
      const f = spinnerFrames[frame % spinnerFrames.length];
      frame += 1;
      if (interactive) {
        process.stdout.write(`\r  ${f} working... ${elapsed}s`);
      } else {
        process.stdout.write(`  working... ${elapsed}s\n`);
      }
    };
    tick();
    const timer = setInterval(tick, interactive ? 120 : 5000);

    const child = spawn(command, args, {
      cwd: repoRoot,
      stdio: ['ignore', 'pipe', 'pipe'],
    });
    child.stdout.on('data', (d) => { captured += d; });
    child.stderr.on('data', (d) => { captured += d; });

    const finish = (err) => {
      clearInterval(timer);
      const elapsed = ((Date.now() - start) / 1000).toFixed(1);
      if (interactive) process.stdout.write('\r');
      if (err) {
        process.stdout.write(`  ${label} failed after ${elapsed}s\n`);
        if (captured.trim()) process.stdout.write(captured.endsWith('\n') ? captured : captured + '\n');
        reject(err);
      } else {
        process.stdout.write(`  done in ${elapsed}s${interactive ? '          ' : ''}\n`);
        resolve();
      }
    };

    child.on('error', finish);
    child.on('close', (code) => {
      if (code === 0) finish(null);
      else finish(new Error(`${label} failed (exit ${code})`));
    });
  });
}

async function main() {
  const total = 4;
  const overallStart = Date.now();
  await runInherited(`[1/${total}] Building CSS`, nodeExe, ['scripts/build-css.js']);
  await runInherited(`[2/${total}] Building frontend (Vite)`, nodeExe, ['scripts/build-vite.js']);
  await runWithSpinner(`[3/${total}] go mod vendor`, 'go', ['mod', 'vendor']);
  await runWithSpinner(`[4/${total}] go build .`, 'go', ['build', '.']);
  const elapsed = ((Date.now() - overallStart) / 1000).toFixed(1);
  process.stdout.write(`\nBuild complete in ${elapsed}s.\n`);
}

main().catch((err) => {
  process.stderr.write(`\n${err.message}\n`);
  process.exit(1);
});
