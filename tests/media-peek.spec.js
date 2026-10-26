import {test,expect} from '@playwright/test';
import {useNativeBridge} from './native-bridge.fixture.js';

async function mediaPeek(page){
 await useNativeBridge(page,{startSection:'Home'});
 await page.setViewportSize({width:400,height:144});
 await page.evaluate(()=>window.__nativeFixture.emit('expanded',{value:true,view:'media',epoch:12,media:window.__nativeFixture.media}));
 await expect(page.locator('#yuta')).toHaveAttribute('data-preparing','false');
 await expect(page.getByRole('tabpanel',{name:'Now playing'})).toBeVisible();
}

test('compact player keeps playback and seeking separate from the second expansion',async({page})=>{
 await mediaPeek(page);
 await expect(page.getByRole('tab')).toHaveCount(0);
 expect((await page.locator('.media-seek').boundingBox()).width).toBeGreaterThan(300);
 await expect(page.locator('#media-progress')).toHaveCount(1);
 await page.getByRole('button',{name:'Pause music',exact:true}).click();
 await expect(page.getByRole('button',{name:'Play music',exact:true})).toBeVisible();
 await expect(page.locator('#yuta')).toHaveAttribute('data-view','media');
 const slider=page.getByLabel('Track progress',{exact:true});await slider.focus();await page.keyboard.press('ArrowRight');
 await expect.poll(()=>page.evaluate(()=>window.__nativeFixture.calls.some(c=>c.method==='mediaAction'&&c.args.action==='seek'))).toBe(true);
 expect(await page.evaluate(()=>window.__nativeFixture.calls.filter(c=>c.method==='expand').length)).toBe(0);
 await page.locator('.album-art').click();
 await expect(page.locator('#yuta')).toHaveAttribute('data-view','full');
 await page.setViewportSize({width:760,height:366});
 await expect(page.getByRole('tab',{name:'Today',exact:true})).toBeVisible();
 await expect(page.locator('#media-progress')).toHaveCount(1);
 expect(await page.evaluate(()=>window.__nativeFixture.calls.filter(c=>c.method==='expand').at(-1)?.args.full)).toBe(true);
});

test('media-only layout fits long video titles and has a keyboard-accessible full menu button',async({page})=>{
 await mediaPeek(page);
 await page.evaluate(()=>window.__nativeFixture.emit('media',{...window.__nativeFixture.media,sourceName:'Google Chrome',title:'A long video title that must stay inside the compact player without moving any controls',artist:'Video channel',revision:5}));
 await page.waitForTimeout(500);
 const boxes=await page.evaluate(()=>{const p=document.querySelector('#panel'),b=p.getBoundingClientRect();return {overflow:p.scrollHeight-p.clientHeight,controls:[...p.querySelectorAll('button,input')].map(e=>{const r=e.getBoundingClientRect();return r.left>=0&&r.right<=400&&r.top>=0&&r.bottom<=144;}),transform:getComputedStyle(p).transform};});
 expect(boxes.overflow).toBe(0);expect(boxes.controls.every(Boolean)).toBe(true);expect(boxes.transform).toBe('none');
 const layout=await page.evaluate(()=>{
  const rect=selector=>document.querySelector(selector).getBoundingClientRect();
  const body=rect('.media-body'),transport=rect('.media-transport'),timeline=rect('.media-timeline'),play=rect('[data-command=toggle]'),title=rect('.media-info strong');
  return {separate:body.right<=transport.left,aligned:Math.abs((body.top+body.bottom)-(transport.top+transport.bottom))<2,timelineBelow:timeline.top>=Math.max(body.bottom,transport.bottom),playWidth:play.width,titleWidth:title.width,focusOrder:[...document.querySelector('.media-card').querySelectorAll('button,input')].map(e=>e.dataset.command||e.id)};
 });
 expect(layout.separate&&layout.aligned&&layout.timelineBelow).toBe(true);
 expect(layout.playWidth).toBeGreaterThanOrEqual(40);expect(layout.titleWidth).toBeGreaterThan(90);
 expect(layout.focusOrder).toEqual(['previous','toggle','next','media-progress']);
 await page.screenshot({path:'artifacts/media-peek/compact-video.png'});
 const full=page.getByRole('button',{name:'Open full menu',exact:true});await full.focus();await page.keyboard.press('Enter');
 await expect(page.locator('#yuta')).toHaveAttribute('data-view','full');
 expect(await page.evaluate(()=>window.__yutaErrors)).toEqual([]);
});

test('artwork tint follows each cover, handles missing artwork, and the seek wave stops when paused',async({page})=>{
 await mediaPeek(page);
 const paint=async(color,revision)=>page.evaluate(({color,revision})=>{
  const c=document.createElement('canvas');c.width=c.height=24;const ctx=c.getContext('2d');ctx.fillStyle=color;ctx.fillRect(0,0,24,24);
  const f=window.__nativeFixture;f.media={...f.media,artwork:c.toDataURL(),revision};f.emit('media',f.media);
 },{color,revision});
 await paint('#d44136',10);
 await expect(page.locator('.media-card')).toHaveCSS('--media-wash','rgb(74, 34, 36)');
 await paint('#2c6bdd',11);
 await expect(page.locator('.media-card')).toHaveCSS('--media-wash','rgb(27, 46, 83)');
 const wave=page.locator('.seek-wave');await expect(wave).toHaveCSS('animation-play-state','running');
 await page.getByRole('button',{name:'Pause music',exact:true}).click();await expect(wave).toHaveCSS('animation-play-state','paused');
 await page.evaluate(()=>window.__nativeFixture.emit('media',{available:false,revision:20}));
 await expect(page.locator('.album-art img')).toHaveCount(0);
 await expect(page.getByLabel('Track progress',{exact:true})).toBeDisabled();
 await expect(page.locator('.media-card')).toHaveCSS('--media-wash','rgb(44, 47, 67)');
 await page.emulateMedia({reducedMotion:'reduce'});await expect(wave).toHaveCSS('animation-name','none');
});

test('layout readiness is acknowledged before reveal and stale reveals cannot animate a closed player',async({page})=>{
 await mediaPeek(page);
 const frames=await page.locator('.panel').evaluate(el=>el.getAnimations().flatMap(a=>a.effect.getKeyframes()));
 expect(frames.every(f=>!String(f.transform).includes('scale'))).toBe(true);
 await page.keyboard.press('Escape');
 await page.evaluate(()=>window.__nativeFixture.emit('presentationShown',12));
 await expect(page.locator('#yuta')).toHaveAttribute('data-expanded','false');
 expect(await page.locator('.panel').evaluate(el=>el.getAnimations().length)).toBe(0);
});
