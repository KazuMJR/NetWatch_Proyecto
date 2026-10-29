const { test } = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const path = require('node:path');
const source = fs.readFileSync(path.resolve(__dirname, '../../src/NetWatch.Web/wwwroot/download.js'), 'utf8');

function environment(width = 1280, height = 800) {
    let rect = { left:280, top:180, right:1150, bottom:320, width:870, height:140 };
    const classes = new Set(), listeners = new Map();
    let pendingFrame;
    const cat = { classList:{ toggle:(name, enabled) => enabled ? classes.add(name) : classes.delete(name) } };
    const target = { isConnected:true, getBoundingClientRect:() => rect, scrollIntoView() {} };
    const nodes = {
        'netwatch-guide-card':{ style:{}, getBoundingClientRect:() => ({width:Math.min(410,width-28),height:360}) },
        'netwatch-guide-spotlight':{ style:{} },
        'netwatch-guide-nero':{ style:{}, offsetWidth:88, offsetHeight:98, querySelector:() => cat }
    };
    const window = { innerWidth:width, innerHeight:height,
        addEventListener:(name, cb) => listeners.set(name, cb), removeEventListener:name => listeners.delete(name),
        requestAnimationFrame:cb => { pendingFrame = cb; return 1; }, cancelAnimationFrame:() => { pendingFrame = null; },
        setTimeout:cb => cb(), netwatchMascot:{ motionEnabled:() => true } };
    const document = { querySelector:selector => selector === '#target' ? target : null, getElementById:id => nodes[id] };
    vm.runInNewContext(source, { window, document, Map });
    return { api:window.netwatchGuide, nodes, classes, target, listeners, window,
        changeRect:r => { rect = r; }, flush:() => { const cb=pendingFrame; pendingFrame=null; cb?.(); } };
}

function box(node, width, height) {
    const x = parseFloat(node.style.left), y = parseFloat(node.style.top);
    return { x,y,right:x+width,bottom:y+height };
}

test('tour cat is animated, visible near the highlight and outside the instruction card', async () => {
    const e = environment(); assert.equal(await e.api.show('#target', 1), true);
    const cat = box(e.nodes['netwatch-guide-nero'],88,98), card = box(e.nodes['netwatch-guide-card'],410,360);
    assert.equal(e.nodes['netwatch-guide-nero'].style.visibility,'visible');
    assert.ok(e.classes.has('motion-enabled'));
    assert.ok(cat.right <= card.x || cat.x >= card.right || cat.bottom <= card.y || cat.y >= card.bottom);
    assert.ok(cat.y < 450, 'stays near the highlighted section');
});

test('different steps move Nero around the focus; scroll and resize update his position', async () => {
    const e = environment(); await e.api.show('#target',0);
    const first = box(e.nodes['netwatch-guide-nero'],88,98);
    await e.api.show('#target',3);
    assert.notDeepEqual(box(e.nodes['netwatch-guide-nero'],88,98),first);
    e.changeRect({left:280,top:80,right:1150,bottom:220,width:870,height:140});
    e.listeners.get('scroll')(); e.flush();
    assert.equal(e.nodes['netwatch-guide-spotlight'].style.top,'72px');
    e.window.innerWidth=900; e.listeners.get('resize')(); e.flush();
    assert.ok(box(e.nodes['netwatch-guide-nero'],88,98).right <= 892);
});

test('on narrow screens Nero stays in the viewport and clear removes all guide listeners', async () => {
    const e = environment(375,667); await e.api.show('#target',4);
    const cat = box(e.nodes['netwatch-guide-nero'],88,98);
    assert.ok(cat.x>=8 && cat.y>=8 && cat.right<=367 && cat.bottom<=659);
    e.api.clear();
    assert.equal(e.listeners.size,0);
    assert.equal(e.nodes['netwatch-guide-nero'].style.visibility,'hidden');
    assert.equal(e.nodes['netwatch-guide-card'].style.visibility,'hidden');
});

test('missing guide targets fail gracefully and paused mascot animations stay paused in the guide', async () => {
    const e = environment(); assert.equal(await e.api.show('#missing',0),false);
    e.window.netwatchMascot.motionEnabled=() => false;
    await e.api.show('#target',0);
    assert.ok(e.classes.has('motion-disabled'));
});
