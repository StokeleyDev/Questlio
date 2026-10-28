const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const { spawn } = require('node:child_process');
const assert = require('node:assert/strict');

const UI_DIR = path.resolve(__dirname, 'ui');
const MIMES = {
  '.html': 'text/html; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.js': 'application/javascript; charset=utf-8',
  '.svg': 'image/svg+xml',
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
  async setViewport(width, height) {
    await this.send('Emulation.setDeviceMetricsOverride', {
      width,
      height,
      deviceScaleFactor: 1,
      mobile: false
    });
  }
  close() {
    this.ws.close();
  }
}

async function main() {
  const { server, port } = await startServer();
  const chromeTmp = fs.mkdtempSync(path.join(os.tmpdir(), 'chrome-tip-suite-'));
  const chromePort = 9500 + Math.floor(Math.random() * 400);
  
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
    if (!ver) throw new Error('Chrome not ready');

    const targetsRes = await fetch(`http://127.0.0.1:${chromePort}/json/list`);
    const targets = await targetsRes.json();
    const target = targets.find(t => t.type === 'page') || targets[0];
    cdp = new CDPClient(target.webSocketDebuggerUrl);
    await cdp.ready();
    await cdp.send('Page.enable');
    await cdp.send('Page.addScriptToEvaluateOnNewDocument', {
      source: `window.gaidenBootstrap = { version: '0.29.1', games: [], sessions: [], daily: [], emulators: [] };`
    });

    const targetUrl = `http://127.0.0.1:${port}/index.html`;
    await cdp.send('Page.navigate', { url: targetUrl });
    await new Promise(r => setTimeout(r, 600));

    const now = new Date();
    const fixture = {
      games: [{ id: 'g1', name: 'Hades', platform: 'PC', minutes: 240 }],
      sessions: [
        { id: 's1', gameId: 'g1', startedAt: now.toISOString(), activeSeconds: 7200 },
        { id: 's2', gameId: 'g1', startedAt: new Date(now.getTime() - 86400000).toISOString(), activeSeconds: 3600 },
        { id: 's3', gameId: 'g1', startedAt: new Date(now.getTime() - 2*86400000).toISOString(), activeSeconds: 1800 },
        { id: 's4', gameId: 'g1', startedAt: new Date(now.getTime() - 3*86400000).toISOString(), activeSeconds: 9000 },
        { id: 's5', gameId: 'g1', startedAt: new Date(now.getTime() - 4*86400000).toISOString(), activeSeconds: 5400 }
      ],
      daily: [],
      emulators: []
    };

    await cdp.eval(`
      window.testFixture = ${JSON.stringify(fixture)};
      GaidenUI.commitSnapshot(window.testFixture);
      navigate('stats');
    `);

    console.log('--- RUNNING STATISTICS TOOLTIP TEST SUITE ---');

    // 1. Check overlap on week chart (leftmost, rightmost, middle)
    const checkColumn = async (binIdx, label) => {
      return await cdp.eval(`(() => {
        const btn = document.querySelector('[data-stat-bin="${binIdx}"]');
        if (!btn) return { error: 'Button ' + ${binIdx} + ' not found' };
        btn.dispatchEvent(new PointerEvent('pointerover', { bubbles: true }));
        
        const tip = document.querySelector('#statistics-tooltip');
        const heading = document.querySelector('.period-heading');
        if (!tip) return { error: 'Tip not found for ' + ${binIdx} };
        if (!heading) return { error: 'Heading not found' };
        
        const tr = tip.getBoundingClientRect();
        const hr = heading.getBoundingClientRect();
        const br = btn.getBoundingClientRect();
        
        const xOverlap = Math.max(0, Math.min(tr.right, hr.right) - Math.max(tr.left, hr.left));
        const yOverlap = Math.max(0, Math.min(tr.bottom, hr.bottom) - Math.max(tr.top, hr.top));
        const overlapArea = xOverlap * yOverlap;
        
        const inViewport = tr.left >= 0 && tr.right <= window.innerWidth && tr.top >= 0 && tr.bottom <= window.innerHeight;
        const ariaCorrect = btn.getAttribute('aria-describedby') === tip.id && tip.getAttribute('role') === 'tooltip';
        
        return {
          bin: ${binIdx},
          label: '${label}',
          overlapArea,
          isOverlapping: overlapArea > 0,
          inViewport,
          ariaCorrect,
          tr: { left: Math.round(tr.left), right: Math.round(tr.right), top: Math.round(tr.top), bottom: Math.round(tr.bottom) },
          hr: { top: Math.round(hr.top), bottom: Math.round(hr.bottom) },
          br: { left: Math.round(br.left), right: Math.round(br.right), top: Math.round(br.top), bottom: Math.round(br.bottom) }
        };
      })()`);
    };

    // 1. Leftmost column (col 0)
    const resCol0 = await checkColumn(0, 'Leftmost column (col 0)');
    assert.equal(resCol0.isOverlapping, false, 'Col 0 must not overlap period-heading');
    assert.equal(resCol0.inViewport, true, 'Col 0 tip must be within viewport');
    assert.equal(resCol0.ariaCorrect, true, 'Col 0 aria attributes must be set');
    console.log('✓ PASS: Leftmost column (col 0) — no overlap, in viewport, aria correct');

    // 2. Rightmost column (col 6)
    const resColLast = await checkColumn(6, 'Rightmost column (col 6)');
    assert.equal(resColLast.isOverlapping, false, 'Col 6 must not overlap period-heading');
    assert.equal(resColLast.inViewport, true, 'Col 6 tip must be within viewport');
    assert.equal(resColLast.ariaCorrect, true, 'Col 6 aria attributes must be set');
    console.log('✓ PASS: Rightmost column (col 6) — no overlap, in viewport, aria correct');

    // 3. Pointerout
    const pointerOutRes = await cdp.eval(`(() => {
      const btn = document.querySelector('[data-stat-bin="0"]');
      btn.dispatchEvent(new PointerEvent('pointerout', { bubbles: true, relatedTarget: document.body }));
      return { tipExists: !!document.querySelector('#statistics-tooltip') };
    })()`);
    assert.equal(pointerOutRes.tipExists, false, 'Tip must be hidden on pointerout');
    console.log('✓ PASS: Mouse pointerout — tooltip hidden');

    // 4. Keyboard navigation & Escape
    const kbRes = await cdp.eval(`(() => {
      const col0 = document.querySelector('[data-stat-bin="0"]');
      col0.dispatchEvent(new FocusEvent('focusin', { bubbles: true }));
      const tipAfterFocus = !!document.querySelector('#statistics-tooltip');
      
      // Press ArrowRight
      col0.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }));
      const activeAfterRight = document.activeElement?.dataset?.statBin;
      const tipAfterRight = document.querySelector('#statistics-tooltip');
      const rightDesc = document.activeElement?.getAttribute('aria-describedby');
      
      // Press Escape
      document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
      const tipAfterEsc = !!document.querySelector('#statistics-tooltip');
      const descAfterEsc = document.activeElement?.getAttribute('aria-describedby');
      
      return {
        tipAfterFocus,
        activeAfterRight,
        tipAfterRightExists: !!tipAfterRight,
        rightDescMatches: rightDesc === 'statistics-tooltip',
        tipAfterEscExists: tipAfterEsc,
        descAfterEscCleared: !descAfterEsc
      };
    })()`);
    assert.equal(kbRes.tipAfterFocus, true, 'Tip must show on focusin');
    assert.equal(kbRes.activeAfterRight, '1', 'Focus must move to col 1');
    assert.equal(kbRes.tipAfterRightExists, true, 'Tip must exist on col 1');
    assert.equal(kbRes.rightDescMatches, true, 'aria-describedby must match');
    assert.equal(kbRes.tipAfterEscExists, false, 'Tip must hide on Escape');
    assert.equal(kbRes.descAfterEscCleared, true, 'aria-describedby must clear on Escape');
    console.log('✓ PASS: Keyboard navigation — focusin, ArrowRight, and Escape');

    // 5. Period switch
    const periodRes = await cdp.eval(`(() => {
      const col0 = document.querySelector('[data-stat-bin="0"]');
      col0.dispatchEvent(new PointerEvent('pointerover', { bubbles: true }));
      const tipBefore = !!document.querySelector('#statistics-tooltip');
      
      const monthBtn = document.querySelector('[data-stats-period="month"]');
      monthBtn.click();
      const tipAfter = !!document.querySelector('#statistics-tooltip');
      const newCols = document.querySelectorAll('[data-stat-bin]').length;
      
      const yearBtn = document.querySelector('[data-stats-period="year"]');
      yearBtn.click();
      const yearCols = document.querySelectorAll('[data-stat-bin]').length;
      
      document.querySelector('[data-stats-period="week"]').click();
      
      return { tipBefore, tipAfter, newCols, yearCols };
    })()`);
    assert.equal(periodRes.tipBefore, true, 'Tip before switch');
    assert.equal(periodRes.tipAfter, false, 'Tip hidden after switch');
    assert.ok(periodRes.newCols >= 28, 'Month columns >= 28');
    assert.equal(periodRes.yearCols, 12, 'Year columns === 12');
    console.log('✓ PASS: Period switch — cleans up tooltip across periods');

    // 6. Small viewport
    await cdp.setViewport(360, 480);
    const smallVpRes = await checkColumn(0, 'Small viewport (360x480)');
    assert.equal(smallVpRes.isOverlapping, false, 'Small viewport must not overlap heading');
    assert.equal(smallVpRes.inViewport, true, 'Small viewport tip must be in viewport');
    console.log('✓ PASS: Small viewport (360x480) — no overlap, in viewport');

    // 7. Scroll / resize behavior
    await cdp.setViewport(1024, 768);
    const scrollRes = await cdp.eval(`(() => {
      const col0 = document.querySelector('[data-stat-bin="0"]');
      col0.dispatchEvent(new PointerEvent('pointerover', { bubbles: true }));
      const tipBefore = !!document.querySelector('#statistics-tooltip');
      
      window.dispatchEvent(new Event('scroll'));
      const tipAfterScroll = document.querySelector('#statistics-tooltip');
      
      window.dispatchEvent(new Event('resize'));
      const tipAfterResize = document.querySelector('#statistics-tooltip');
      
      window.scrollTo(0, 5000);
      window.dispatchEvent(new Event('scroll'));
      const tipAfterFarScroll = !!document.querySelector('#statistics-tooltip');
      window.scrollTo(0, 0);
      
      return {
        tipBefore,
        tipAfterScrollExists: !!tipAfterScroll,
        tipAfterResizeExists: !!tipAfterResize,
        tipAfterFarScroll
      };
    })()`);
    assert.equal(scrollRes.tipBefore, true);
    assert.equal(scrollRes.tipAfterScrollExists, true);
    assert.equal(scrollRes.tipAfterResizeExists, true);
    assert.equal(scrollRes.tipAfterFarScroll, false, 'Tooltip hidden when scrolled away');
    console.log('✓ PASS: Scroll/resize — repositioned while visible, hidden when offscreen');

    // 8. Full cleanup
    const cleanupRes = await cdp.eval(`(() => {
      const col0 = document.querySelector('[data-stat-bin="0"]');
      col0.dispatchEvent(new PointerEvent('pointerover', { bubbles: true }));
      document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
      return {
        tipExists: !!document.querySelector('#statistics-tooltip'),
        ariaPresent: !!col0.getAttribute('aria-describedby')
      };
    })()`);
    assert.equal(cleanupRes.tipExists, false, 'Tip removed from DOM');
    assert.equal(cleanupRes.ariaPresent, false, 'aria-describedby removed');
    console.log('✓ PASS: Full cleanup verified');

    // 9. Unavailable space (minTop > innerHeight - th - 8): safely hide tooltip and clear aria
    await cdp.setViewport(800, 320);
    const unavailRes = await cdp.eval(`(() => {
      const col0 = document.querySelector('[data-stat-bin="0"]');
      col0.dispatchEvent(new PointerEvent('pointerover', { bubbles: true }));
      const tip = document.querySelector('#statistics-tooltip');
      const aria = col0.getAttribute('aria-describedby');
      return { tipExists: !!tip, ariaPresent: !!aria };
    })()`);
    assert.equal(unavailRes.tipExists, false, 'Tooltip must be hidden when space is unavailable (minTop > innerHeight - th - 8)');
    assert.equal(unavailRes.ariaPresent, false, 'aria-describedby must be cleared when space is unavailable');

    // Also test dynamically resizing down when tooltip is already open:
    await cdp.setViewport(1024, 768);
    const dynamicRes = await cdp.eval(`(() => {
      const col0 = document.querySelector('[data-stat-bin="0"]');
      col0.dispatchEvent(new PointerEvent('pointerover', { bubbles: true }));
      return { tipBefore: !!document.querySelector('#statistics-tooltip') };
    })()`);
    assert.equal(dynamicRes.tipBefore, true, 'Tooltip open at 1024x768');
    await cdp.setViewport(800, 320);
    const afterResizeRes = await cdp.eval(`(() => {
      window.dispatchEvent(new Event('resize'));
      const col0 = document.querySelector('[data-stat-bin="0"]');
      return {
        tipAfter: !!document.querySelector('#statistics-tooltip'),
        ariaAfter: !!col0.getAttribute('aria-describedby')
      };
    })()`);
    assert.equal(afterResizeRes.tipAfter, false, 'Tooltip must hide when resized to unavailable space');
    assert.equal(afterResizeRes.ariaAfter, false, 'aria must clear when resized to unavailable space');
    await cdp.setViewport(1024, 768);
    console.log('✓ PASS: Unavailable space — tooltip safely hidden and aria cleared');

    console.log('\n*** ALL TESTS PASSED (GREEN) ***');

  } finally {
    if (cdp) cdp.close();
    chrome.kill();
    server.close();
    try { fs.rmSync(chromeTmp, { recursive: true, force: true }); } catch (e) {}
  }
}

main().catch(err => {
  console.error('ERROR:', err);
  process.exit(1);
});
