// Run with: node --test tests/NetWatch.Web.Tests/mascot.test.cjs
const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const source = fs.readFileSync(path.resolve(__dirname, '../../src/NetWatch.Web/wwwroot/js/netwatch-mascot.js'), 'utf8');

function environment(reduced = false, waitForEntry = true, dock = 'free') {
    let now = 0, nextFrame = null;
    let timerId = 0;
    const timers = new Map();
    class Element {
        constructor() {
            this.dataset = {}; this.events = {}; this.values = {}; this.textContent = ''; this.inside = true;
            this.style = { setProperty: (key, value) => { this.values[key] = value; } };
            const classes = new Set();
            this.classList = { add: x => classes.add(x), remove: x => classes.delete(x), contains: x => classes.has(x),
                toggle: (x, yes) => yes ? classes.add(x) : classes.delete(x) };
        }
        addEventListener(type, handler) { (this.events[type] ||= []).push(handler); }
        removeEventListener(type, handler) { this.events[type] = (this.events[type] || []).filter(x => x !== handler); }
        fire(type, props = {}) {
            const event = { type, target: this, button: 0, pointerId: 1, clientX: 0, clientY: 0,
                preventDefault() {}, stopImmediatePropagation() {}, ...props };
            (this.events[type] || []).forEach(handler => handler(event));
        }
        querySelector() { return null; }
        querySelectorAll() { return []; }
        contains(target) { return target.inside; }
        matches() { return false; }
        setPointerCapture() { this.captured = true; }
        hasPointerCapture() { return !!this.captured; }
        releasePointerCapture() { this.captured = false; }
        setAttribute(key, value) { this.values[key] = value; }
    }
    const root = new Element(), cat = new Element(), character = new Element(), toggle = new Element(), status = new Element();
    const document = new Element(), window = new Element(), motion = new Element();
    document.documentElement = new Element();
    character.offsetWidth = 72; character.offsetHeight = 72;
    character.querySelector = selector => selector === '.pixel-cat' ? cat : null;
    root.querySelector = selector => ({ '.mascot-character': character, '.mascot-menu-toggle': toggle })[selector] || null;
    root.querySelectorAll = selector => selector === '.mascot-status, .mascot-control-status' ? [status] : [];
    motion.matches = reduced;
    window.matchMedia = () => motion;
    const storage = new Map(), local = new Map();
    if (dock) local.set('netwatch-nero-dock', dock);
    const context = { window, document, innerWidth: 1280, innerHeight: 720,
        performance: { now: () => now }, Math: Object.create(Math),
        setTimeout: (fn, ms) => { timers.set(++timerId, { fn, at:now + ms }); return timerId; },
        clearTimeout: id => timers.delete(id),
        requestAnimationFrame: fn => { nextFrame = fn; return 1; }, cancelAnimationFrame: () => { nextFrame = null; },
        sessionStorage: { getItem: key => storage.get(key), setItem: (key, value) => storage.set(key, value) },
        localStorage: { getItem: key => local.get(key), setItem: (key, value) => local.set(key, value) } };
    context.Math.random = () => .2;
    vm.runInNewContext(source, context);
    const api = window.netwatchMascot;
    api.attach(root, 'login');
    const position = () => ({ x: parseFloat(root.values['--mascot-x']), y: parseFloat(root.values['--mascot-y']) });
    const step = ms => { for (let time = 0; time < ms; time += 50) {
        now += 50;
        for (const [id, timer] of timers) if (timer.at <= now) { timers.delete(id); timer.fn(); }
        const frame = nextFrame; nextFrame = null; frame?.(now);
    } };
    // Reproduce real pointer capture/release, not only a synthetic click.
    const pet = () => {
        const p = position(), pointer = { clientX:p.x + 36, clientY:p.y + 36 };
        character.fire('pointerdown', pointer);
        window.fire('pointerup', pointer);
        character.fire('lostpointercapture', pointer);
        character.fire('click', pointer);
    };
    const point = (x, y) => window.fire('pointermove', { clientX: x, clientY: y });
    const drag = (x, y, cancel = false) => {
        const p = position(); character.fire('pointerdown', { clientX: p.x + 36, clientY: p.y + 36 });
        point(x + 36, y + 36);
        window.fire(cancel ? 'pointercancel' : 'pointerup', { clientX: x + 36, clientY: y + 36 });
        character.fire('click'); step(100);
    };
    if (waitForEntry) step(4000);
    return { root, cat, character, toggle, window, document, api, position, step, pet, point, drag, status, storage, local, Element };
}

test('first visit defaults to a quiet corner with no automatic tips, entrance or chase', () => {
    const e = environment(false, false, null), initial = e.position();
    assert.equal(e.api.getDockPreference(), 'bottom-right');
    assert.equal(e.api.getTipsEnabled(), false);
    assert.equal(e.root.classList.contains('is-docked'), true);
    assert.equal(e.root.classList.contains('is-entering'), false);
    for (let i = 0; i < 8; i++) e.pet();
    e.point(20, 20); e.step(180000); e.drag(100, 100);
    assert.deepEqual(e.position(), initial);
    assert.equal(e.root.classList.contains('is-chasing'), false);
});

test('every corner is reachable and its selection survives navigation and a new session', () => {
    const e = environment();
    for (const [corner, point] of [['top-left',{x:16,y:16}], ['top-right',{x:1192,y:16}], ['bottom-left',{x:16,y:632}], ['bottom-right',{x:1192,y:632}]]) {
        e.api.setDock(e.root, corner); assert.deepEqual(e.position(), point);
        e.api.attach(e.root, 'reports'); assert.deepEqual(e.position(), point);
        e.api.detach(e.root); e.storage.clear(); e.api.attach(e.root, 'devices');
        assert.deepEqual(e.position(), point); assert.equal(e.api.getDockPreference(), corner);
    }
});

test('free mode restores dragging and corner mode stops active games immediately', () => {
    const e = environment(false, true, null);
    e.api.setDock(e.root, 'free'); e.drag(200,200); assert.deepEqual(e.position(), {x:200,y:200});
    e.step(6000); for(let i=0;i<4;i++)e.pet();
    assert.equal(e.root.classList.contains('is-chasing'), true);
    e.api.setDock(e.root, 'bottom-right');
    assert.equal(e.root.classList.contains('is-chasing'), false);
    assert.equal(e.document.documentElement.classList.contains('nero-has-cursor'), false);
    e.api.command(e.root, 'explore'); e.step(70000);
    assert.deepEqual(e.position(), {x:1192,y:632});
});

test('normal pointer movement does not interrupt quiet activities or move Nero', () => {
    const e = environment(), original = e.position();
    e.point(10, 10); e.step(55000);
    assert.equal(e.root.dataset.mascotMood, 'sit', 'initial rest lasts a minute');
    e.step(6000);
    assert.deepEqual(e.position(), original);
    assert.equal(e.root.dataset.mascotMood, 'groom');
    e.point(600, 300); e.step(60000);
    assert.deepEqual(e.position(), original);
    assert.equal(e.root.dataset.mascotMood, 'nap');
});

test('chosen sitting, sleeping and playing remain in place', () => {
    const e = environment(), original = e.position();
    for (const action of ['sit', 'nap', 'groom', 'play', 'stretch']) {
        e.api.command(e.root, action); e.point(20, 20); e.step(20000);
        assert.equal(e.root.dataset.mascotMood, action);
        assert.deepEqual(e.position(), original);
    }
});

test('four quick pets trigger a bounded chase; isolated pets do not', () => {
    const e = environment();
    e.pet(); e.step(3000); e.pet(); e.step(3000);
    assert.equal(e.root.classList.contains('is-chasing'), false);
    for (let i = 0; i < 4; i++) { e.pet(); e.step(100); }
    assert.equal(e.root.classList.contains('is-chasing'), true);
    e.point(20, 20);
    const from = e.position(); e.step(2000); const to = e.position();
    const distance = Math.hypot(to.x - from.x, to.y - from.y);
    assert.ok(distance > 50 && distance <= 218, 'chase moves at a controlled pace');
    e.step(18000);
    assert.equal(e.root.classList.contains('is-chasing'), false);
    const stopped = e.position(); e.point(1000, 600); e.step(1000);
    assert.deepEqual(e.position(), stopped);
});

test('dragging reaches all four viewport corners, then parks without counting as petting', () => {
    const e = environment();
    for (const [x, y] of [[0, 0], [1208, 0], [0, 648], [1208, 648]]) {
        e.drag(x, y);
        assert.deepEqual(e.position(), { x, y });
        assert.equal(e.root.dataset.mascotMode, 'stay');
        assert.equal(e.root.dataset.mascotMood, 'sit');
        assert.equal(e.root.classList.contains('is-chasing'), false);
        e.step(1000);
    }
});

test('pointer cancellation releases the drag and keeps the mascot available', () => {
    const e = environment(); e.drag(0, 0, true);
    assert.equal(e.root.classList.contains('is-dragging'), false);
    assert.equal(e.character.hasPointerCapture(), false);
    e.step(600); e.api.command(e.root, 'play');
    assert.equal(e.root.dataset.mascotMood, 'play');
});

test('exploring alternates a slow stroll with a complete resting animation', () => {
    const e = environment(); e.api.command(e.root, 'explore');
    e.step(1600); const original = e.position(); e.step(1000);
    const moved = e.position();
    assert.ok(Math.hypot(moved.x - original.x, moved.y - original.y) <= 44);
    assert.equal(e.root.dataset.mascotMood, 'walk');
    e.step(7000);
    assert.notEqual(e.root.dataset.mascotMood, 'walk');
});

test('the requested animated companion starts animated even with OS reduced motion', () => {
    const e = environment(true);
    assert.equal(e.cat.classList.contains('motion-enabled'), true);
    e.point(1000, 500);
    for (let i = 0; i < 4; i++) { e.pet(); e.step(600); }
    const from = e.position(); e.step(1600);
    assert.notDeepEqual(e.position(), from, 'real click sequences start a moving chase');
});

test('the explicit pause choice persists across pages and new sessions', () => {
    const e = environment(true);
    e.api.setAnimation(e.root, false);
    assert.equal(e.cat.classList.contains('motion-disabled'), true);
    e.api.command(e.root, 'explore'); const original = e.position(); e.step(12000);
    assert.deepEqual(e.position(), original);
    assert.match(e.status.textContent, /pausadas/);
    e.api.detach(e.root); e.storage.clear(); e.api.attach(e.root, 'login');
    assert.equal(e.cat.classList.contains('motion-disabled'), true);
    e.api.setAnimation(e.root, true); e.api.detach(e.root); e.api.attach(e.root, 'reports');
    assert.equal(e.cat.classList.contains('motion-enabled'), true);
    assert.equal(e.local.get('netwatch-nero-animated'), 'true');
});

test('hovering the nearby menu toggle does not freeze an active chase', () => {
    const e = environment();
    for (let i = 0; i < 4; i++) e.pet();
    e.toggle.fire('pointerenter'); e.point(1000, 500);
    const original = e.position(); e.step(2000);
    assert.notDeepEqual(e.position(), original);
});

test('opening the controls stops travel and selecting an action cancels a chase', () => {
    const e = environment();
    for (let i = 0; i < 4; i++) e.pet();
    e.point(10, 10); e.root.dataset.menuOpen = 'true';
    const original = e.position(); e.step(2000);
    assert.deepEqual(e.position(), original);
    e.api.command(e.root, 'sit');
    assert.equal(e.root.classList.contains('is-chasing'), false);
    assert.equal(e.root.dataset.mascotMood, 'sit');
});

function capture(e) {
    const p = e.position();
    e.point(p.x + 36, p.y + 36);
    for (let i = 0; i < 4; i++) e.pet();
    e.step(700);
    assert.equal(e.root.classList.contains('is-carrying'), true);
    assert.equal(e.document.documentElement.classList.contains('nero-cursor-caught'), true);
}

test('catching swats the visual pointer before carrying it and restores the real cursor automatically', () => {
    const e = environment(); capture(e);
    const original = e.position(); e.step(1000);
    assert.deepEqual(e.position(), original, 'stops to give five quick paw swats');
    assert.equal(e.root.classList.contains('is-batting'), true);
    assert.equal(e.cat.classList.contains('is-batting'), true);
    assert.equal(e.root.dataset.mascotMood, 'play');
    e.step(1700);
    assert.equal(e.root.classList.contains('is-batting'), false);
    assert.notDeepEqual(e.position(), original, 'carries the pointer after swatting it');
    e.step(4000);
    assert.equal(e.root.classList.contains('is-carrying'), false);
    assert.equal(e.cat.classList.contains('is-batting'), false);
    assert.equal(e.document.documentElement.classList.contains('nero-cursor-caught'), false);
});

test('automatic advice preference is remembered independently from animations', () => {
    const e = environment();
    assert.equal(e.api.getTipsEnabled(), false);
    e.api.setTipsEnabled(false);
    e.api.detach(e.root); e.storage.clear(); e.api.attach(e.root, 'login');
    assert.equal(e.api.getTipsEnabled(), false);
    assert.equal(e.api.motionEnabled(), true);
    e.api.setTipsEnabled(true);
    assert.equal(e.api.getTipsEnabled(), true);
});

test('the login mascot still animates and handles clicks without a menu button', () => {
    const e = environment(); e.api.detach(e.root);
    const query = e.root.querySelector;
    e.root.querySelector = selector => selector === '.mascot-menu-toggle' ? null : query(selector);
    e.api.attach(e.root, 'login'); e.step(4000);
    assert.equal(e.cat.classList.contains('motion-enabled'), true);
    for (let i = 0; i < 4; i++) e.pet();
    e.point(1000, 100);
    const original = e.position(); e.step(1500);
    assert.notDeepEqual(e.position(), original);
});

test('cursor movement, Escape, blur, navigation and disabling motion all release the cursor', () => {
    const exits = [
        e => e.point(900, 400),
        e => e.window.fire('keydown', { key:'Escape' }),
        e => e.window.fire('blur'),
        e => e.api.attach(e.root, 'reports'),
        e => e.api.setAnimation(e.root, false),
        e => e.api.detach(e.root),
        e => { e.document.hidden = true; e.document.fire('visibilitychange'); }
    ];
    for (const leave of exits) {
        const e = environment(); capture(e); leave(e);
        assert.equal(e.document.documentElement.classList.contains('nero-cursor-caught'), false);
        assert.equal(e.root.classList.contains('is-batting'), false);
    }
});

test('the chase lasts eighteen seconds when the cursor keeps escaping', () => {
    const e = environment();
    for (let i = 0; i < 4; i++) e.pet();
    for (let i = 0; i < 17; i++) {
        const p = e.position();
        e.point(p.x < 640 ? 1240 : 20, p.y < 360 ? 680 : 20);
        e.step(1000);
    }
    assert.equal(e.root.classList.contains('is-chasing'), true);
    e.step(1200);
    assert.equal(e.root.classList.contains('is-chasing'), false);
});

test('a fresh opening enters from outside and avoids repeating its previous edge', () => {
    const e = environment(false, false);
    const side = e.root.dataset.mascotEntry;
    assert.ok(e.position().x < 0 || e.position().y < 0 || e.position().x >= 1280 || e.position().y >= 720);
    e.step(4000);
    assert.equal(e.root.classList.contains('is-entering'), false);
    assert.ok(e.position().x >= 0 && e.position().y >= 0);
    e.api.attach(e.root, 'reports');
    assert.equal(e.root.dataset.mascotEntry, side, 'in-app navigation does not replay the entrance');
    e.api.detach(e.root); e.api.attach(e.root, 'login');
    assert.notEqual(e.root.dataset.mascotEntry, side);
    e.step(4000);
    assert.equal(e.root.classList.contains('is-entering'), false);
});

test('Nero rests on a real card edge and follows its position when the page scrolls', () => {
    const e = environment();
    let top = 220;
    const card = new e.Element(); card.isConnected = true;
    card.getBoundingClientRect = () => ({ left:40, right:350, top, bottom:top + 300, width:310, height:300 });
    e.document.querySelectorAll = selector => selector === '.login-card' ? [card] : [];
    e.api.command(e.root, 'explore'); e.step(12000);
    assert.equal(e.root.classList.contains('is-perched'), true);
    const original = e.position(); top -= 20; e.step(500);
    assert.equal(e.position().y, original.y - 20);
    const mood = e.root.dataset.mascotMood; e.step(45000);
    assert.equal(e.root.dataset.mascotMood, mood, 'keeps the activity for a full minute');
    card.isConnected = false; e.step(500);
    assert.equal(e.root.classList.contains('is-perched'), false);
});
