const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const assert = require('node:assert/strict');
const { spawn } = require('node:child_process');

const UI_DIR = path.resolve(__dirname, 'ui');
const MIMES = {
  '.html': 'text/html; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.js': 'application/javascript; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.ttf': 'font/ttf',
  '.woff2': 'font/woff2',
  '.json': 'application/json'
};

function startServer() {
  return new Promise((resolve, reject) => {
    const server = http.createServer((req, res) => {
      const parsedUrl = new URL(req.url, 'http://localhost');
      let pathname = parsedUrl.pathname;
      if (pathname === '/') pathname = '/index.html';
      const filePath = path.join(UI_DIR, pathname);
      if (!filePath.startsWith(UI_DIR)) {
        res.writeHead(403); res.end('Forbidden'); return;
      }
      fs.readFile(filePath, (err, data) => {
        if (err) { res.writeHead(404); res.end('Not Found'); return; }
        const ext = path.extname(filePath).toLowerCase();
        res.writeHead(200, { 'Content-Type': MIMES[ext] || 'application/octet-stream' });
        res.end(data);
      });
    });
    server.listen(0, '127.0.0.1', () => {
      resolve({ server, port: server.address().port });
    });
    server.on('error', reject);
  });
}

class CDPClient {
  constructor(wsUrl) {
    this.ws = new WebSocket(wsUrl);
    this.id = 0;
    this.callbacks = new Map();
    this.ws.onmessage = (event) => {
      const msg = JSON.parse(event.data);
      if (msg.id && this.callbacks.has(msg.id)) {
        const cb = this.callbacks.get(msg.id);
        this.callbacks.delete(msg.id);
        if (msg.error) cb.reject(new Error(msg.error.message));
        else cb.resolve(msg.result);
      }
    };
  }
  ready() {
    return new Promise((resolve, reject) => {
      if (this.ws.readyState === WebSocket.OPEN) return resolve();
      this.ws.onopen = () => resolve();
      this.ws.onerror = reject;
    });
  }
  send(method, params = {}) {
    const id = ++this.id;
    return new Promise((resolve, reject) => {
      this.callbacks.set(id, { resolve, reject });
      this.ws.send(JSON.stringify({ id, method, params }));
    });
  }
  async eval(expr) {
    const res = await this.send('Runtime.evaluate', { expression: expr, returnByValue: true, awaitPromise: true });
    if (res.exceptionDetails) throw new Error(res.exceptionDetails.exception?.description || 'Eval error');
    return res.result?.value;
  }
  close() {
    this.ws.close();
  }
}

async function main() {
  const { server, port } = await startServer();
  const chromeTmp = fs.mkdtempSync(path.join(os.tmpdir(), 'chrome-search-test-'));
  const chromePort = 9444 + Math.floor(Math.random() * 500);

  const chrome = spawn('C:/Program Files/Google/Chrome/Application/chrome.exe', [
    '--headless=new',
    `--remote-debugging-port=${chromePort}`,
    `--user-data-dir=${chromeTmp}`,
    '--no-first-run',
    '--no-default-browser-check'
  ], { stdio: 'ignore' });

  let cdp;
  try {
    let ver;
    for (let i = 0; i < 40; i++) {
      try {
        const r = await fetch(`http://127.0.0.1:${chromePort}/json/version`);
        if (r.ok) { ver = await r.json(); break; }
      } catch {
        await new Promise(r => setTimeout(r, 150));
      }
    }
    if (!ver) throw new Error('Chrome remote debugging did not respond');

    const targetsRes = await fetch(`http://127.0.0.1:${chromePort}/json/list`);
    const targets = await targetsRes.json();
    const target = targets.find(t => t.type === 'page') || targets[0];
    cdp = new CDPClient(target.webSocketDebuggerUrl);
    await cdp.ready();
    await cdp.send('Page.enable');
    await cdp.send('Page.addScriptToEvaluateOnNewDocument', {
      source: `window.gaidenBootstrap = { version: '0.29.1', games: [], sessions: [], daily: [], emulators: [] };`
    });

    await cdp.send('Page.navigate', { url: `http://127.0.0.1:${port}/index.html` });
    await new Promise(r => setTimeout(r, 800));

    // Commit test game
    await cdp.eval(`
      window.testFixture = {
        games: [{
          id: 'game-check',
          name: 'Hero Check Game',
          platform: 'PC',
          minutes: 100,
          status: 'playing',
          launchAvailable: true
        }],
        sessions: [],
        daily: [],
        emulators: []
      };
      GaidenUI.commitSnapshot(window.testFixture);
      navigate('home');
    `);

    // Check 1: .hero-play border radius
    const heroPlay = await cdp.eval(`(() => {
      const el = document.querySelector('.hero-play');
      if (!el) return null;
      const cs = window.getComputedStyle(el);
      return { borderRadius: cs.borderRadius };
    })()`);

    console.log('[CHECK 1] .hero-play borderRadius:', heroPlay?.borderRadius);
    assert.ok(heroPlay, 'Element .hero-play must exist on home view');
    assert.equal(heroPlay.borderRadius, '999px', '.hero-play must have pill border-radius (999px)');

    // Navigate to library
    await cdp.eval(`navigate('library');`);

    // Check 2: .search-box on input focus (standard mode)
    // In Chromium headless, dispatch a keyboard Tab or key to set keyboard modality for :focus-visible
    await cdp.send('Input.dispatchKeyEvent', { type: 'keyDown', key: 'Tab', code: 'Tab', windowsVirtualKeyCode: 9 });
    await cdp.send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Tab', code: 'Tab', windowsVirtualKeyCode: 9 });

    const debugFocus = await cdp.eval(`(() => {
      const box = document.querySelector('.search-box');
      const input = document.querySelector('.search-box input');
      if (!box || !input) return { error: 'elements not found' };
      input.focus({ focusVisible: true });
      return {
        isFocused: document.activeElement === input,
        inputMatchesFv: input.matches(':focus-visible'),
        boxMatchesFv: box.matches(':has(:focus-visible)')
      };
    })()`);
    console.log('[DEBUG FOCUS]:', debugFocus);

    const searchFocused = await cdp.eval(`(() => {
      const box = document.querySelector('.search-box');
      const input = document.querySelector('.search-box input');
      if (!box || !input) return null;
      input.focus({ focusVisible: true });
      const boxCS = window.getComputedStyle(box);
      const inputCS = window.getComputedStyle(input);
      return {
        boxOutline: boxCS.outline,
        boxOutlineOffset: boxCS.outlineOffset,
        boxOutlineWidth: boxCS.outlineWidth,
        boxOutlineStyle: boxCS.outlineStyle,
        inputOutline: inputCS.outline,
        inputOutlineStyle: inputCS.outlineStyle
      };
    })()`);

    console.log('[CHECK 2] Standard focus:', searchFocused);
    assert.ok(searchFocused, 'Search box and input must exist');
    assert.equal(searchFocused.inputOutlineStyle, 'none', 'Input must have outline: none to avoid double ring');
    assert.equal(searchFocused.boxOutlineStyle, 'solid', 'Container must have solid outline');
    assert.equal(searchFocused.boxOutlineWidth, '2px', 'Container outline width must be 2px');
    assert.equal(searchFocused.boxOutlineOffset, '2px', 'Container outline offset must be 2px');

    // Check 3: Forced colors
    await cdp.send('Emulation.setEmulatedMedia', {
      features: [{ name: 'forced-colors', value: 'active' }]
    });

    const searchForced = await cdp.eval(`(() => {
      const box = document.querySelector('.search-box');
      const input = document.querySelector('.search-box input');
      input.focus();
      const boxCS = window.getComputedStyle(box);
      const inputCS = window.getComputedStyle(input);
      return {
        boxOutline: boxCS.outline,
        boxOutlineStyle: boxCS.outlineStyle,
        boxOutlineWidth: boxCS.outlineWidth,
        inputOutline: inputCS.outline,
        inputOutlineStyle: inputCS.outlineStyle
      };
    })()`);

    console.log('[CHECK 3] Forced-colors focus:', searchForced);
    assert.equal(searchForced.inputOutlineStyle, 'none', 'Input in forced-colors must have outline: none');
    assert.equal(searchForced.boxOutlineStyle, 'solid', 'Container in forced-colors must have solid outline');
    assert.equal(searchForced.boxOutlineWidth, '2px', 'Container in forced-colors must have 2px outline');

    console.log('ALL CHECKS PASSED SUCCESSFULLY!');
  } finally {
    if (cdp) cdp.close();
    try { chrome.kill(); } catch {}
    server.close();
    try { fs.rmSync(chromeTmp, { recursive: true, force: true }); } catch {}
  }
}

main().catch((err) => {
  console.error('FAIL:', err);
  process.exit(1);
});
