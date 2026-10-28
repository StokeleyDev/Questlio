const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

// Lightweight virtual DOM implementation for testing UI components
class MockNode {
  constructor(tagName = 'div') {
    this.tagName = tagName.toUpperCase();
    this.nodeType = 1;
    this.children = [];
    this.parentNode = null;
    this.attributes = new Map();
    this.listeners = new Map();
    this.style = {
      setProperty: (p, v) => { this.style[p] = v; },
      removeProperty: (p) => { delete this.style[p]; }
    };
    this.dataset = {};
    this._value = '';
    this._innerHTML = '';
  }

  get id() { return this.getAttribute('id') || ''; }
  set id(v) { this.setAttribute('id', v); }

  get className() { return this.getAttribute('class') || ''; }
  set className(v) { this.setAttribute('class', v); }

  get classList() {
    const self = this;
    return {
      add: (...cls) => {
        const cur = new Set((self.className || '').split(/\s+/).filter(Boolean));
        cls.forEach(c => cur.add(c));
        self.className = [...cur].join(' ');
      },
      remove: (...cls) => {
        const cur = new Set((self.className || '').split(/\s+/).filter(Boolean));
        cls.forEach(c => cur.delete(c));
        self.className = [...cur].join(' ');
      },
      toggle: (c, force) => {
        const cur = new Set((self.className || '').split(/\s+/).filter(Boolean));
        const has = cur.has(c);
        const next = force !== undefined ? !!force : !has;
        if (next) cur.add(c); else cur.delete(c);
        self.className = [...cur].join(' ');
        return next;
      },
      contains: (c) => (self.className || '').split(/\s+/).includes(c)
    };
  }

  get value() { return this._value; }
  set value(v) { this._value = String(v); }

  getAttribute(n) { return this.attributes.has(n) ? this.attributes.get(n) : null; }
  setAttribute(n, v) { this.attributes.set(n, String(v)); }
  hasAttribute(n) { return this.attributes.has(n); }
  removeAttribute(n) { this.attributes.delete(n); }

  append(...nodes) {
    for (const n of nodes) {
      const node = typeof n === 'string' ? new MockTextNode(n) : n;
      node.parentNode = this;
      this.children.push(node);
    }
  }

  replaceChildren(...nodes) {
    this.children = [];
    this.append(...nodes);
  }

  get textContent() {
    return this.children.map(c => c.textContent || '').join('');
  }
  set textContent(v) {
    this.children = [new MockTextNode(v)];
  }

  get innerHTML() { return this._innerHTML || ''; }
  set innerHTML(html) {
    this._innerHTML = html;
    this.children = parseHTMLSimple(html, this);
  }

  addEventListener(ev, fn) {
    if (!this.listeners.has(ev)) this.listeners.set(ev, []);
    this.listeners.get(ev).push(fn);
  }
  removeEventListener(ev, fn) {
    const list = this.listeners.get(ev) || [];
    this.listeners.set(ev, list.filter(f => f !== fn));
  }
  dispatchEvent(ev) {
    ev.target = this;
    ev.currentTarget = this;
    const direct = this['on' + ev.type];
    if (typeof direct === 'function') direct.call(this, ev);
    const list = this.listeners.get(ev.type) || [];
    for (const fn of list) fn.call(this, ev);
    if (ev.bubbles && this.parentNode) {
      this.parentNode.dispatchEvent(ev);
    }
    return !ev.defaultPrevented;
  }

  focus() {
    globalThis.document.activeElement = this;
    this.dispatchEvent(new MockEvent('focus'));
  }
  blur() {
    if (globalThis.document.activeElement === this) {
      globalThis.document.activeElement = null;
    }
    this.dispatchEvent(new MockEvent('blur'));
  }
  select() { this.selected = true; }

  closest(sel) {
    let cur = this;
    while (cur) {
      if (cur.matches && cur.matches(sel)) return cur;
      cur = cur.parentNode;
    }
    return null;
  }

  matches(sel) {
    if (sel.startsWith('#')) return this.id === sel.slice(1);
    if (sel.startsWith('.')) return this.classList.contains(sel.slice(1));
    if (sel.includes('.')) {
      const [tag, ...cls] = sel.split('.');
      if (tag && this.tagName.toLowerCase() !== tag.toLowerCase()) return false;
      return cls.every(c => this.classList.contains(c));
    }
    if (sel.startsWith('[') && sel.endsWith(']')) {
      const [k, v] = sel.slice(1, -1).split('=');
      if (!v) return this.hasAttribute(k);
      return this.getAttribute(k) === v.replace(/['"]/g, '');
    }
    return this.tagName.toLowerCase() === sel.toLowerCase();
  }

  querySelector(sel) {
    return this.querySelectorAll(sel)[0] || null;
  }

  querySelectorAll(sel) {
    const results = [];
    const matchOne = (node, s) => {
      if (s.includes(' ')) {
        const parts = s.split(/\s+/);
        let cur = [node];
        for (const p of parts) {
          const next = [];
          for (const c of cur) {
            next.push(...collectAll(c).filter(n => n.matches(p)));
          }
          cur = next;
        }
        return cur;
      }
      return collectAll(node).filter(n => n.matches(s));
    };
    return matchOne(this, sel);
  }
}

class MockTextNode {
  constructor(text) {
    this.nodeType = 3;
    this.textContent = String(text);
    this.parentNode = null;
  }
  matches() { return false; }
}

function collectAll(root) {
  const all = [];
  const walk = (n) => {
    for (const c of n.children || []) {
      if (c.nodeType === 1) {
        all.push(c);
        walk(c);
      }
    }
  };
  walk(root);
  return all;
}

class MockEvent {
  constructor(type, opts = {}) {
    this.type = type;
    this.bubbles = !!opts.bubbles;
    this.defaultPrevented = false;
    this.propagationStopped = false;
  }
  preventDefault() { this.defaultPrevented = true; }
  stopPropagation() { this.propagationStopped = true; }
  stopImmediatePropagation() { this.propagationStopped = true; }
}

class MockKeyboardEvent extends MockEvent {
  constructor(type, opts = {}) {
    super(type, opts);
    this.key = opts.key || '';
    this.keyCode = opts.keyCode || 0;
  }
}

// Simple recursive HTML parser for our component templates
function parseHTMLSimple(html, parent) {
  const tokens = html.match(/<\/?[a-zA-Z0-9\-]+(?:\s+[^>]*)?>|[^<]+/g) || [];
  const nodes = [];
  const stack = [parent];

  for (const token of tokens) {
    if (token.startsWith('</')) {
      if (stack.length > 1) stack.pop();
    } else if (token.startsWith('<')) {
      const isSelfClosing = token.endsWith('/>') || /^<(img|input|hr|br|use)/i.test(token);
      const tagMatch = token.match(/^<([a-zA-Z0-9\-]+)/);
      if (!tagMatch) continue;
      const el = new MockNode(tagMatch[1]);
      const attrRe = /([a-zA-Z0-9\-_:]+)(?:="([^"]*)"|='([^']*)'|=([^\s>]+))?/g;
      let m;
      while ((m = attrRe.exec(token)) !== null) {
        const name = m[1];
        if (name.toLowerCase() === el.tagName.toLowerCase()) continue;
        const val = m[2] !== undefined ? m[2] : m[3] !== undefined ? m[3] : m[4] !== undefined ? m[4] : '';
        el.setAttribute(name, val);
        if (name.startsWith('data-')) {
          const dataKey = name.slice(5).replace(/-([a-z])/g, (_, l) => l.toUpperCase());
          el.dataset[dataKey] = val;
        }
      }
      el.parentNode = stack[stack.length - 1];
      stack[stack.length - 1].children.push(el);
      nodes.push(el);
      if (!isSelfClosing) {
        stack.push(el);
      }
    } else {
      const text = token;
      if (text) {
        const tNode = new MockTextNode(text);
        tNode.parentNode = stack[stack.length - 1];
        stack[stack.length - 1].children.push(tNode);
      }
    }
  }
  return parent.children;
}

// Setup testing environment
async function runTests() {
  console.log('=== Starting TDD: Detail Name Editing & Marker Check ===');
  const doc = new MockNode('html');
  doc.createElement = (tag) => new MockNode(tag);
  doc.createTextNode = (text) => new MockTextNode(text);
  globalThis.document = doc;
  globalThis.window = globalThis;
  globalThis.window.addEventListener = (ev, fn) => doc.addEventListener(ev, fn);
  globalThis.window.removeEventListener = (ev, fn) => doc.removeEventListener(ev, fn);
  globalThis.Event = MockEvent;
  globalThis.KeyboardEvent = MockKeyboardEvent;
  globalThis.PointerEvent = MockEvent;

  // Setup body and base dialogs
  const body = new MockNode('body');
  doc.append(body);
  
  const detailDialog = new MockNode('dialog');
  detailDialog.id = 'detail-dialog';
  detailDialog.open = true;
  const detailContent = new MockNode('div');
  detailContent.id = 'detail-content';
  detailDialog.append(detailContent);
  
  const card = new MockNode('div');
  card.className = 'game-card';
  card.dataset.game = 'test-game-1';
  const cardName = new MockNode('div');
  cardName.className = 'game-name';
  cardName.textContent = 'Original Title';
  card.append(cardName);
  
  const addDialog = new MockNode('dialog');
  addDialog.id = 'add-dialog';
  const addForm = new MockNode('form');
  addForm.id = 'add-form';
  addForm.elements = { name: { value: '' }, platform: { value: 'PC' } };
  const selectedExe = new MockNode('div');
  selectedExe.id = 'selected-exe';
  const addSubmit = new MockNode('button');
  addSubmit.setAttribute('type', 'submit');
  const addChip = new MockNode('span');
  addChip.className = 'add-file-chip';
  const addReason = new MockNode('span');
  addReason.id = 'add-reason';
  const addExeBtn = new MockNode('button');
  addExeBtn.setAttribute('data-add-exe', '');
  const addSteamBtn = new MockNode('button');
  addSteamBtn.setAttribute('data-steam-open', '');
  const addEmuBtn = new MockNode('button');
  addEmuBtn.setAttribute('data-add-emulator', '');
  addForm.append(selectedExe, addSubmit, addChip, addReason, addExeBtn, addSteamBtn, addEmuBtn);
  addDialog.append(addForm);
  
  body.append(detailDialog, card, addDialog);

  const postedMessages = [];
  globalThis.window.chrome = {
    webview: {
      postMessage: (msg) => postedMessages.push(msg)
    }
  };

  globalThis.games = [
    { id: 'test-game-1', name: 'Original Title', status: 'backlog', notes: '', platform: 'PC', minutes: 60, launchAvailable: true }
  ];
  globalThis.statusNames = { backlog: 'В планах', playing: 'Играю', done: 'Пройдена', abandoned: 'Заброшена' };
  
  const uiEvents = new Map();
  globalThis.GaidenUI = {
    detail: { featureSections: () => [] },
    components: { kit: { mount: () => {} } },
    formatDate: () => 'Сегодня',
    on: (ev, fn) => {
      if (!uiEvents.has(ev)) uiEvents.set(ev, []);
      uiEvents.get(ev).push(fn);
    },
    emit: (ev, ...args) => {
      for (const fn of uiEvents.get(ev) || []) fn(...args);
    }
  };
  globalThis.media = () => '<div class="mock-art"></div>';
  globalThis.fmt = (sec) => Math.round(sec / 60) + ' ч';
  globalThis.escapeHTML = (s) => String(s || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

  // Load detail-flow.js and detail.js
  const detailFlowCode = fs.readFileSync(path.join(__dirname, 'ui/detail-flow.js'), 'utf8');
  const detailCode = fs.readFileSync(path.join(__dirname, 'ui/detail.js'), 'utf8');
  
  vm.runInThisContext(detailFlowCode);
  vm.runInThisContext(detailCode);

  // TEST 1: Build detail view
  const g = globalThis.games[0];
  GaidenUI.detail.build(g);

  const input = detailContent.querySelector('#edit-game-name');
  assert.ok(input, 'FAIL: #edit-game-name must exist in detail card');
  assert.equal(input.value, 'Original Title', 'FAIL: initial input value must match game name');
  assert.equal(input.getAttribute('aria-label'), 'Название игры', 'FAIL: input must have accessible aria-label');

  const titleH2 = detailContent.querySelector('#detail-title');
  assert.ok(titleH2, 'FAIL: #detail-title must exist');
  assert.equal(titleH2.getAttribute('aria-label'), 'Original Title', 'FAIL: title h2 must have aria-label for dialog labelling');

  // TEST 2: Edit marker presence and accessibility
  const marker = detailContent.querySelector('.detail-edit-marker');
  assert.ok(marker, 'FAIL: .detail-edit-marker must exist next to the name field');
  assert.equal(marker.tagName, 'BUTTON', 'FAIL: marker must be an accessible button element');
  assert.equal(marker.getAttribute('type'), 'button', 'FAIL: marker button must have type="button"');
  assert.equal(marker.getAttribute('aria-label'), 'Изменить название игры', 'FAIL: marker button must have aria-label "Изменить название игры"');
  assert.notEqual(marker.getAttribute('aria-hidden'), 'true', 'FAIL: marker button must not have aria-hidden="true"');
  assert.ok(!titleH2.querySelector('.detail-edit-marker'), 'FAIL: edit button must be placed outside h2');
  assert.ok(marker.textContent.includes('Изменить') || marker.getAttribute('title') || marker.getAttribute('aria-label'), 'FAIL: marker must have visual text, title tooltip, or aria-label');

  // TEST 3: Click marker focuses input
  input.blur();
  marker.dispatchEvent(new MockEvent('click'));
  assert.equal(globalThis.document.activeElement, input, 'FAIL: clicking edit marker must focus the input');

  // TEST 4: Typing new name and pressing Enter saves non-empty name and blurs
  input.focus();
  input.value = 'Renamed Masterpiece';
  input.dispatchEvent(new MockKeyboardEvent('keydown', { key: 'Enter', keyCode: 13, bubbles: true }));

  assert.notEqual(globalThis.document.activeElement, input, 'FAIL: pressing Enter must blur input');
  assert.equal(postedMessages.length, 1, 'FAIL: pressing Enter must post update message');
  assert.equal(postedMessages[0].action, 'updateGame');
  assert.equal(postedMessages[0].name, 'Renamed Masterpiece');
  assert.equal(titleH2.getAttribute('aria-label'), 'Renamed Masterpiece', 'FAIL: title aria-label must update on save');
  assert.equal(cardName.textContent, 'Renamed Masterpiece', 'FAIL: library card name must update synchronously');

  // TEST 5: Escape reverts to original name without posting
  postedMessages.length = 0;
  input.focus();
  input.value = 'Accidental Change';
  input.dispatchEvent(new MockKeyboardEvent('keydown', { key: 'Escape', keyCode: 27, bubbles: true }));

  assert.equal(input.value, 'Renamed Masterpiece', 'FAIL: pressing Escape must revert input to previous name');
  assert.notEqual(globalThis.document.activeElement, input, 'FAIL: pressing Escape must blur input');
  assert.equal(postedMessages.length, 0, 'FAIL: pressing Escape must NOT post any update message');

  // TEST 6: Empty name on save restores original name
  postedMessages.length = 0;
  input.focus();
  input.value = '   ';
  input.dispatchEvent(new MockKeyboardEvent('keydown', { key: 'Enter', keyCode: 13, bubbles: true }));
  assert.equal(input.value, 'Renamed Masterpiece', 'FAIL: empty name must restore original name');
  assert.equal(postedMessages.length, 0, 'FAIL: empty name must not be sent to bridge');

  // TEST 7: Non-destructive update (snapshotApplied while open keeps input draft)
  input.value = 'Draft in progress';
  GaidenUI.emit('snapshotApplied', { games: [{ id: 'test-game-1', name: 'Renamed Masterpiece', status: 'backlog', sessions: 2 }] }, { games: [] });
  assert.equal(input.value, 'Draft in progress', 'FAIL: snapshotApplied must not overwrite active user draft');

  console.log('PASS: All Detail Name Editing tests passed successfully!');
}

runTests().catch(err => {
  console.error(err.message);
  process.exit(1);
});
