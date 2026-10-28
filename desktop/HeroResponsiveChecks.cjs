const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const { spawn } = require('node:child_process');

const UI_DIR = path.resolve(__dirname, 'ui');
const ARTIFACTS_DIR = path.resolve(__dirname, 'hero-test-artifacts');
if (!fs.existsSync(ARTIFACTS_DIR)) fs.mkdirSync(ARTIFACTS_DIR, { recursive: true });

// Simple MIME lookup
const MIMES = {
  '.html': 'text/html; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.js': 'application/javascript; charset=utf-8',
  '.svg': 'image/svg+xml',
  '.ttf': 'font/ttf',
  '.woff2': 'font/woff2',
  '.json': 'application/json',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg'
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
  async screenshot(filePath) {
    const res = await this.send('Page.captureScreenshot', { format: 'png' });
    fs.writeFileSync(filePath, Buffer.from(res.data, 'base64'));
  }
  close() {
    this.ws.close();
  }
}

async function main() {
  const { server, port } = await startServer();
  const chromeTmp = fs.mkdtempSync(path.join(os.tmpdir(), 'chrome-hero-test-'));
  const chromePort = 9333 + Math.floor(Math.random() * 500);
  
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

    const targetUrl = `http://127.0.0.1:${port}/index.html`;
    await cdp.send('Page.navigate', { url: targetUrl });
    await new Promise(r => setTimeout(r, 600));

    // Isolated test fixture with long title and all hero types
    const fixture = {
      games: [
        {
          id: 'game-long-poster',
          name: 'The Legend of Heroes: Trails of Cold Steel IV - THE END OF SAGA (Digital Deluxe Edition with All DLC Packages Included)',
          platform: 'PC',
          minutes: 420,
          status: 'playing',
          lastPlayedAt: new Date().toISOString(),
          steam: { appId: 100, minutes: 1200 }
        },
        {
          id: 'game-full',
          name: 'Cyberpunk 2077: Phantom Liberty',
          platform: 'PC',
          minutes: 360,
          status: 'playing',
          lastPlayedAt: new Date().toISOString(),
          customCover: 'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="800" height="400"><rect width="100%" height="100%" fill="%23224488"/></svg>'
        },
        {
          id: 'game-compact',
          name: 'Hades II',
          platform: 'PC',
          minutes: 180,
          status: 'playing',
          lastPlayedAt: new Date().toISOString(),
          customCover: 'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="300" height="180"><rect width="100%" height="100%" fill="%23882244"/></svg>'
        },
        {
          id: 'game-icon',
          name: 'Diablo II: Resurrected',
          platform: 'PC',
          minutes: 90,
          status: 'playing',
          lastPlayedAt: new Date().toISOString(),
          exeIcon: 'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64"><rect width="100%" height="100%" fill="%23aa4422"/></svg>'
        }
      ],
      sessions: [],
      daily: [],
      emulators: []
    };

    await cdp.eval(`
      window.testFixture = ${JSON.stringify(fixture)};
      GaidenUI.commitSnapshot(window.testFixture);
      navigate('home');
    `);
    const mainHtml = await cdp.eval(`document.querySelector('#main')?.innerHTML || ''`);
    console.log('MAIN HTML length:', mainHtml.length, 'HAS HERO:', mainHtml.includes('hero'));

    const widths = [320, 390, 820, 1280, 1680];
    const expected = {
      320: { minHeight: '216px', heroContentMinHeight: '216px', fontSize: '22px', padding: '16px' },
      390: { minHeight: '216px', heroContentMinHeight: '216px', fontSize: '22px', padding: '16px' },
      820: { minHeight: '216px', heroContentMinHeight: '216px', fontSize: '24px', padding: '16px 20px' },
      1280: { minHeight: '228px', heroContentMinHeight: '228px', fontSize: '28px', padding: '22px 28px' },
      1680: { minHeight: '260px', heroContentMinHeight: '260px', fontSize: '28px', padding: '22px 28px' }
    };

    async function evaluateOverviewMetrics(winWidth) {
      return await cdp.eval(`(() => {
        const row = document.querySelector('.overview-metrics');
        if (!row) return { exists: false, allPass: false, reason: 'overview-metrics not found' };
        row.scrollIntoView();
        const rowRect = row.getBoundingClientRect();
        const rowStyle = getComputedStyle(row);
        const metrics = Array.from(row.querySelectorAll('.metric'));
        const docScrollWidth = document.documentElement.scrollWidth;
        const winW = window.innerWidth;
        const viewportWidthMatches = (winW === ${winWidth});
        const hScroll = docScrollWidth > winW + 1;
        const rowScrollOverflow = row.scrollWidth > row.clientWidth + 1;

        const cols = rowStyle.gridTemplateColumns.split(' ').filter(Boolean).length;
        const columnRulePass = winW <= 640 ? (cols === 1 || cols === 2) : true;

        const cards = metrics.map((m, idx) => {
          m.scrollIntoView();
          const mRect = m.getBoundingClientRect();
          const label = m.querySelector('.metric-label');
          const val = m.querySelector('.metric-value');
          const smalls = Array.from(m.querySelectorAll('small'));

          function measureInline(el) {
            if (!el) return { exists: false, pass: true };
            const text = el.textContent.trim();
            if (!text) return { exists: true, text: '', pass: true };
            const range = document.createRange();
            range.selectNodeContents(el);
            const rects = Array.from(range.getClientRects());
            const bRect = range.getBoundingClientRect();

            // 0 размеров — FAIL
            const zeroSize = bRect.width === 0 || bRect.height === 0 || rects.length === 0;

            // сравнивать границы с карточкой
            const clippedByCard = bRect.right > mRect.right + 2 ||
                                  bRect.left < mRect.left - 2 ||
                                  rects.some(r => r.right > mRect.right + 2 || r.left < mRect.left - 2);

            return {
              text,
              zeroSize,
              clippedByCard,
              width: Math.round(bRect.width),
              height: Math.round(bRect.height),
              rectsCount: rects.length,
              pass: !zeroSize && !clippedByCard
            };
          }

          const labelCheck = measureInline(label);
          const valCheck = measureInline(val);
          const smallsData = smalls.map(s => measureInline(s));

          const labelOverflow = label ? (label.scrollWidth > label.clientWidth + 1) : false;
          const valOverflow = val ? (val.scrollWidth > val.clientWidth + 1) : false;

          const anySmallFailed = smallsData.some(s => !s.pass);
          const labelPass = labelCheck.pass && !labelOverflow;
          const valPass = valCheck.pass && !valOverflow;

          const cardScrollOverflow = m.scrollWidth > m.clientWidth + 1;
          const cardOutOfBounds = mRect.right > winW + 1 || mRect.left < 0;
          const minHeightPass = mRect.height >= 44;

          const cardPass = labelPass && valPass && !anySmallFailed &&
                           !cardScrollOverflow && !cardOutOfBounds && minHeightPass;

          return {
            index: idx,
            label: label?.textContent.trim(),
            value: val?.textContent.trim(),
            width: Math.round(mRect.width),
            height: Math.round(mRect.height),
            labelCheck,
            valCheck,
            smalls: smallsData,
            cardScrollOverflow,
            cardOutOfBounds,
            minHeightPass,
            cardPass
          };
        });

        const allCardsPass = cards.length > 0 && cards.every(c => c.cardPass);
        const allPass = viewportWidthMatches && !hScroll && !rowScrollOverflow && columnRulePass && allCardsPass;

        return {
          exists: true,
          viewportWidth: winW,
          viewportWidthMatches,
          columns: cols,
          columnRulePass,
          hScroll,
          rowScrollOverflow,
          allCardsPass,
          allPass,
          cards
        };
      })()`);
    }

    console.log('=== HERO RESPONSIVE TEST RUN ===');
    let totalFailed = 0;
    let totalPassed = 0;
    const results = [];

    for (const w of widths) {
      await cdp.setViewport(w, 800);
      await new Promise(r => setTimeout(r, 200));

      // Test poster / full hero layout
      await cdp.eval(`(() => {
        const hero = document.querySelector('.hero');
        if (hero) hero.dataset.artLayout = 'poster';
      })()`);

      const metrics = await cdp.eval(`(() => {
        const hero = document.querySelector('.hero');
        const hc = hero?.querySelector('.hero-content');
        const h2 = hero?.querySelector('h2');
        const sHero = getComputedStyle(hero);
        const sHc = getComputedStyle(hc);
        const sH2 = getComputedStyle(h2);
        
        const heroRect = hero.getBoundingClientRect();
        const hcRect = hc.getBoundingClientRect();
        const h2Rect = h2.getBoundingClientRect();
        
        const hScroll = document.documentElement.scrollWidth > window.innerWidth + 1;
        let bad = [];
        if (hScroll) {
          for (const el of document.querySelectorAll('*')) {
            const r = el.getBoundingClientRect();
            if (r.right > window.innerWidth + 1) {
              bad.push({
                tag: el.tagName,
                cls: typeof el.className === 'string' ? el.className : String(el.className),
                id: el.id,
                text: (el.textContent || '').trim().slice(0, 40),
                parent: el.parentElement ? (el.parentElement.tagName + '.' + (typeof el.parentElement.className === 'string' ? el.parentElement.className : '')) : null,
                right: Math.round(r.right),
                width: Math.round(r.width)
              });
            }
          }
        }
        
        return {
          minHeight: sHero.minHeight,
          heroContentMinHeight: sHc.minHeight,
          fontSize: sH2.fontSize,
          paddingTop: sHc.paddingTop,
          paddingRight: sHc.paddingRight,
          paddingBottom: sHc.paddingBottom,
          paddingLeft: sHc.paddingLeft,
          hScroll,
          bad,
          h2Overflow: h2Rect.right > hcRect.right + 2,
          heroOverflow: heroRect.right > window.innerWidth + 1
        };
      })()`);

      const exp = expected[w];
      const actualPadding = `${metrics.paddingTop} ${metrics.paddingRight}` === `${metrics.paddingBottom} ${metrics.paddingLeft}` && metrics.paddingTop === metrics.paddingRight
        ? metrics.paddingTop
        : `${metrics.paddingTop} ${metrics.paddingRight}`;

      const minHeightPass = metrics.minHeight === exp.minHeight;
      const hcMinHeightPass = metrics.heroContentMinHeight === exp.heroContentMinHeight;
      const fontSizePass = metrics.fontSize === exp.fontSize;
      const paddingPass = actualPadding === exp.padding;
      const geometryPass = !metrics.hScroll && !metrics.h2Overflow && !metrics.heroOverflow;
      const heroPass = minHeightPass && hcMinHeightPass && fontSizePass && paddingPass && geometryPass;

      // Check overview metrics on standard fixture
      const ovCheck = await evaluateOverviewMetrics(w);
      const allPass = heroPass && ovCheck.allPass;
      if (allPass) totalPassed++; else totalFailed++;

      results.push({
        width: w,
        allPass,
        heroPass,
        overviewPass: ovCheck.allPass,
        minHeight: { actual: metrics.minHeight, expected: exp.minHeight, pass: minHeightPass },
        hcMinHeight: { actual: metrics.heroContentMinHeight, expected: exp.heroContentMinHeight, pass: hcMinHeightPass },
        fontSize: { actual: metrics.fontSize, expected: exp.fontSize, pass: fontSizePass },
        padding: { actual: actualPadding, expected: exp.padding, pass: paddingPass },
        geometry: { pass: geometryPass, details: { hScroll: metrics.hScroll, bad: metrics.bad, h2Overflow: metrics.h2Overflow, heroOverflow: metrics.heroOverflow } },
        overviewMetrics: ovCheck
      });

      // Screenshot
      const shotPath = path.join(ARTIFACTS_DIR, `hero-${w}.png`);
      await cdp.screenshot(shotPath);
    }

    // Check all kinds of hero (icon/compact/poster/full) on 320px and 390px with 80-char name
    console.log('=== TESTING HERO LAYOUTS (80-CHAR NAME ON 320px & 390px) ===');
    const LONG_NAME_80 = 'The Legend of Heroes: Trails of Cold Steel IV - The End of Saga (Deluxe Edition)';
    const hero80Games = {
      icon: {
        id: 'game-80-icon',
        name: LONG_NAME_80,
        platform: 'PC',
        minutes: 120,
        status: 'playing',
        lastPlayedAt: new Date().toISOString(),
        exeIcon: 'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64"><rect width="100%" height="100%" fill="%23aa4422"/></svg>'
      },
      compact: {
        id: 'game-80-compact',
        name: LONG_NAME_80,
        platform: 'PC',
        minutes: 180,
        status: 'playing',
        lastPlayedAt: new Date().toISOString(),
        customCover: 'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="300" height="180"><rect width="100%" height="100%" fill="%23882244"/></svg>'
      },
      poster: {
        id: 'game-80-poster',
        name: LONG_NAME_80,
        platform: 'PC',
        minutes: 420,
        status: 'playing',
        lastPlayedAt: new Date().toISOString(),
        customCover: 'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="300" height="450"><rect width="100%" height="100%" fill="%23228844"/></svg>'
      },
      full: {
        id: 'game-80-full',
        name: LONG_NAME_80,
        platform: 'PC',
        minutes: 360,
        status: 'playing',
        lastPlayedAt: new Date().toISOString(),
        customCover: 'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="800" height="400"><rect width="100%" height="100%" fill="%23224488"/></svg>'
      }
    };

    const layouts = ['icon', 'compact', 'poster', 'full'];
    const narrowWidths = [320, 390];
    const layout80Results = [];

    for (const layout of layouts) {
      for (const nw of narrowWidths) {
        await cdp.setViewport(nw, 800);
        await new Promise(r => setTimeout(r, 100));
        await cdp.eval(`(() => {
          const g = ${JSON.stringify(hero80Games[layout])};
          window.testHeroFixture = { games: [g], sessions: [], daily: [], emulators: [] };
          GaidenUI.commitSnapshot(window.testHeroFixture);
          navigate('home');
        })()`);
        await new Promise(r => setTimeout(r, 200));

        if (layout === 'poster') {
          await cdp.eval(`(() => {
            const hero = document.querySelector('.hero');
            const img = hero?.querySelector('img.game-art');
            if (img) {
              img.classList.add('steam-poster');
              hero.dataset.artKey = '';
              GaidenUI.emit('rendered');
            }
          })()`);
          await new Promise(r => setTimeout(r, 100));
        }

        const heroMetrics = await cdp.eval(`(() => {
          const hero = document.querySelector('.hero');
          const hc = hero?.querySelector('.hero-content');
          const h2 = hero?.querySelector('h2');
          const iconTile = hero?.querySelector('.hero-icon-tile');
          const clearArt = hero?.querySelector('.hero-clear-art');
          const winW = window.innerWidth;
          const docScrollW = document.documentElement.scrollWidth;

          const hScroll = docScrollW > winW + 1;
          const heroRect = hero ? hero.getBoundingClientRect() : null;
          const hcRect = hc ? hc.getBoundingClientRect() : null;
          const h2Rect = h2 ? h2.getBoundingClientRect() : null;
          const tileRect = iconTile ? iconTile.getBoundingClientRect() : null;
          const clearArtRect = clearArt ? clearArt.getBoundingClientRect() : null;

          const h2Style = h2 ? getComputedStyle(h2) : null;
          const hcStyle = hc ? getComputedStyle(hc) : null;

          let lineRects = [];
          if (h2) {
            const range = document.createRange();
            range.selectNodeContents(h2);
            lineRects = Array.from(range.getClientRects()).map(r => ({
              width: Math.round(r.width),
              height: Math.round(r.height),
              top: Math.round(r.top),
              bottom: Math.round(r.bottom),
              left: Math.round(r.left),
              right: Math.round(r.right)
            }));
          }

          const text = (h2?.textContent || '').trim();
          const words = text ? text.split(' ').map(w => w.trim()).filter(Boolean) : [];
          const wordsCount = words.length;
          const linesCount = lineRects.length;
          const avgWordsPerLine = linesCount > 0 ? (wordsCount / linesCount) : 0;

          // If linesCount >= wordsCount * 0.7 or avgWordsPerLine < 1.6 for an 80-char/multi-word title, it's crushed to one word per line!
          const isOneWordPerLine = wordsCount >= 6 && (linesCount >= wordsCount * 0.7 || avgWordsPerLine < 1.6);

          // Verify padding right is reduced on narrow screens (<= 24px) so text has full width
          const paddingRight = hcStyle ? parseFloat(hcStyle.paddingRight || 0) : 0;
          const paddingRightPass = paddingRight <= 24;

          // Check h2 width: must occupy at least 70% of hero-content width
          const h2WidthRatio = hcRect && hcRect.width > 0 && h2Rect ? (h2Rect.width / (hcRect.width - 40)) : 0;
          const fullWidthPass = h2WidthRatio >= 0.70;

          const h2OverflowsHc = h2Rect && hcRect ? (h2Rect.right > hcRect.right + 2 || h2Rect.left < hcRect.left - 2) : true;
          const heroOverflow = heroRect ? (heroRect.right > winW + 1 || heroRect.left < -1) : true;
          const h2Visible = h2Rect ? (h2Rect.width > 0 && h2Rect.height > 0 && h2Style.display !== 'none' && h2Style.visibility !== 'hidden') : false;

          let tileCollidesWithH2 = false;
          if (iconTile && h2Rect && tileRect && tileRect.width > 0 && tileRect.height > 0) {
            const horizontalOverlap = tileRect.right > h2Rect.left + 2 && tileRect.left < h2Rect.right - 2;
            const verticalOverlap = tileRect.bottom > h2Rect.top + 2 && tileRect.top < h2Rect.bottom - 2;
            tileCollidesWithH2 = horizontalOverlap && verticalOverlap;
          }

          const viewportPass = winW === ${nw};
          const pass = viewportPass &&
                       !hScroll &&
                       !heroOverflow &&
                       !h2OverflowsHc &&
                       h2Visible &&
                       !isOneWordPerLine &&
                       fullWidthPass &&
                       paddingRightPass &&
                       !tileCollidesWithH2;

          return {
            layout: '${layout}',
            width: ${nw},
            h2Text: h2?.textContent,
            actualWinW: winW,
            viewportPass,
            hScroll,
            heroOverflow,
            h2OverflowsHc,
            h2Visible,
            isOneWordPerLine,
            fullWidthPass,
            tileCollidesWithH2,
            wordsCount,
            linesCount,
            avgWordsPerLine: Number(avgWordsPerLine.toFixed(2)),
            h2Width: h2Rect ? Math.round(h2Rect.width) : 0,
            h2Height: h2Rect ? Math.round(h2Rect.height) : 0,
            hcWidth: hcRect ? Math.round(hcRect.width) : 0,
            h2WidthRatio: Number(h2WidthRatio.toFixed(2)),
            fontSize: h2Style?.fontSize,
            tileRect: tileRect ? { width: Math.round(tileRect.width), height: Math.round(tileRect.height), top: Math.round(tileRect.top), right: Math.round(tileRect.right) } : null,
            pass
          };
        })()`);

        const shotPath = path.join(ARTIFACTS_DIR, `hero-${nw}-${layout}.png`);
        await cdp.screenshot(shotPath);

        if (heroMetrics.pass) totalPassed++; else totalFailed++;
        layout80Results.push(heroMetrics);
      }
    }

    // Theme test (light vs dark) at 390px with icon layout
    console.log('=== TESTING DARK THEME AT 390px ===');
    await cdp.setViewport(390, 800);
    await cdp.eval(`document.documentElement.dataset.theme = 'dark';`);
    await new Promise(r => setTimeout(r, 200));
    const ovDark = await evaluateOverviewMetrics(390);
    if (ovDark.allPass) totalPassed++; else totalFailed++;
    const shotPathDark = path.join(ARTIFACTS_DIR, `hero-390-dark.png`);
    await cdp.screenshot(shotPathDark);
    await cdp.eval(`document.documentElement.dataset.theme = 'light';`);

    console.log(JSON.stringify({ totalPassed, totalFailed, results, layout80Results, darkThemeResult: { width: 390, pass: ovDark.allPass, check: ovDark } }, null, 2));
    if (totalFailed > 0) {
      process.exit(1);
    }
  } finally {
    if (cdp) cdp.close();
    chrome.kill();
    server.close();
    try { fs.rmSync(chromeTmp, { recursive: true, force: true }); } catch {}
  }
}

main().catch(err => {
  console.error(err);
  process.exit(1);
});
