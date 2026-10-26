import {test,expect} from '@playwright/test';
import {useNativeBridge} from './native-bridge.fixture.js';

test('Home is centered, compact, icon-only, and keeps direct power actions',async({page})=>{
 await page.setViewportSize({width:760,height:366});
 await useNativeBridge(page,{startSection:'Home'});
 await expect(page.getByRole('tab')).toHaveText(['Home','Darch','Stats','Wallpaper']);
 await expect(page.locator('.topbar button').first()).toHaveAccessibleName('Home');
 await expect(page.locator('.home-app img')).toHaveCount(6);
 await expect(page.getByRole('group',{name:'Connections'}).getByRole('button')).toHaveCount(6);
 await expect(page.getByRole('button',{name:'Quick note',exact:true})).toHaveCount(0);
 await expect(page.getByRole('button',{name:'All apps',exact:true})).toHaveCount(0);
 await expect(page.getByRole('searchbox',{name:'Search clipboard'})).toHaveCount(0);
 await page.evaluate(()=>Promise.all(document.getAnimations().filter(a=>Number.isFinite(a.effect?.getComputedTiming().iterations)).map(a=>a.finished.catch(()=>{}))));
 const geometry=await page.evaluate(()=>{
  const box=s=>document.querySelector(s).getBoundingClientRect(),root=box('#yuta'),tabs=box('.tabs');
  const clipboard=box('.home-clipboard'),connections=box('.connection-tools'),media=box('.media-card'),dock=box('.home-app-grid');
  return {center:Math.abs(tabs.x+tabs.width/2-root.x-root.width/2),overflow:document.querySelector('#panel').scrollHeight-document.querySelector('#panel').clientHeight,power:[...document.querySelectorAll('.top-power button')].map(el=>{const r=el.getBoundingClientRect();return {x:r.x,y:r.y,right:r.right};}),home:box('[role=tab]').left,media:media.width,root:root.width,leftStack:connections.right<clipboard.left&&clipboard.right<media.left,rightGap:root.right-media.right,bottomGap:root.bottom-dock.bottom,connectionRects:[...document.querySelectorAll('.connection-split')].map(el=>{const r=el.getBoundingClientRect();return {x:r.x,y:r.y,width:r.width,height:r.height};})};
 });
 expect(geometry.center).toBeLessThan(1);expect(geometry.overflow).toBeLessThan(3);expect(geometry.media).toBeLessThan(geometry.root/2);expect(geometry.power.at(-1).right).toBeLessThan(geometry.home);
 expect(geometry.power.every(p=>p.y===geometry.power[0].y)).toBe(true);
 expect(geometry.leftStack).toBe(true);expect(geometry.rightGap).toBeGreaterThanOrEqual(35);expect(geometry.rightGap).toBeLessThan(40);expect(geometry.bottomGap).toBeGreaterThanOrEqual(12);expect(geometry.bottomGap).toBeLessThan(25);
 const [wifi,bluetooth,audio]=geometry.connectionRects;
 expect(wifi.x).toBe(bluetooth.x);expect(wifi.x).toBe(audio.x);
 expect(bluetooth.y).toBeGreaterThan(wifi.y+wifi.height);expect(audio.y).toBeGreaterThan(bluetooth.y+bluetooth.height);
 expect(wifi.width).toBeLessThan(100);expect(wifi.height).toBeLessThanOrEqual(40);
 for(const [label,action] of [['Lock','lock'],['Sleep','sleep'],['Restart','restart'],['Shut down','shutdown']]){
  await page.getByRole('button',{name:label,exact:true}).click();
  await expect.poll(()=>page.evaluate(()=>window.__nativeFixture.calls.filter(c=>c.method==='power').at(-1)?.args.action)).toBe(action);
  await expect(page.getByRole('dialog')).not.toBeVisible();
 }
 expect(await page.evaluate(()=>window.__yutaErrors)).toEqual([]);
 await page.screenshot({path:'artifacts/yuta-home-refined.png'});
});

test('the pencil edits pins, saves their order and launches the chosen icon',async({page})=>{
 await page.setViewportSize({width:760,height:366});
 await useNativeBridge(page,{startSection:'Home'});
 await page.getByRole('button',{name:'Edit pinned apps'}).click();
 await page.getByRole('textbox',{name:'Find an app to pin'}).fill('Application 9');
 await page.locator('.pin-choice').filter({hasText:/^Application 9$/}).click();
 for(let i=0;i<6;i++)await page.getByRole('button',{name:'Move Application 9 left',exact:true}).click();
 await page.getByRole('button',{name:'Save pins',exact:true}).click();
 await expect(page.locator('.home-app').first()).toHaveAccessibleName('Open Application 9');
 expect(await page.locator('.home-app').last().evaluate(el=>el.offsetTop)).toBe(await page.locator('.home-app').first().evaluate(el=>el.offsetTop));
 await page.locator('.home-app').first().click();
 expect(await page.evaluate(()=>window.__nativeFixture.calls.find(c=>c.method==='launch')?.args.target)).toBe('C:\\Apps\\app-9.exe');
 await page.getByRole('button',{name:'Edit pinned apps'}).click();
 await page.getByRole('button',{name:'Unpin Application 9',exact:true}).click();
 await page.getByRole('button',{name:'Save pins',exact:true}).click();
 await expect(page.getByRole('button',{name:'Open Application 9',exact:true})).toHaveCount(0);
 await page.keyboard.press('Control+k');await expect(page.getByRole('textbox',{name:'Find an app to pin'})).toBeFocused();
 expect(await page.evaluate(()=>window.__yutaErrors)).toEqual([]);
});

test('clipboard renders images and full history, preserves scrolling, and pastes without a detail page',async({page})=>{
 await page.setViewportSize({width:760,height:366});
 await useNativeBridge(page,{startSection:'Home',delays:{clipboard:350}});
 await page.evaluate(()=>{const f=window.__nativeFixture;f.clips=Array.from({length:18},(_,i)=>({id:'clip-'+i,type:i===0?'image':'text',image:i===0?f.media.artwork:null,width:500,height:228,label:i===0?'Design reference':'Note '+i,text:i===17?'The release plan is ready.':'A useful paragraph, kept in your clipboard. '+i,pinned:false,copied:new Date(Date.now()-i*60000).toISOString()}));f.emit('clipboard',f.clips);});
 await expect(page.locator('.home-clip')).toHaveCount(18);
 await page.waitForTimeout(400);await expect(page.locator('.home-clip')).toHaveCount(18);
 await expect(page.locator('.clip-thumbnail')).toBeVisible();
 await page.locator('.clip-tools [data-action=homePaste]').first().click();
 expect(await page.evaluate(()=>window.__nativeFixture.calls.filter(c=>c.method==='clipPaste').at(-1).args.id)).toBe('clip-0');
 await expect(page.getByRole('tabpanel',{name:'Home',exact:true})).toBeVisible();
 expect(await page.evaluate(()=>window.__nativeFixture.calls.filter(c=>c.method==='clipCopy').length)).toBe(0);
 await page.locator('.home-clips').evaluate(el=>el.scrollTop=280);
 const before=await page.locator('.home-clips').evaluate(el=>el.scrollTop);
 await page.evaluate(()=>{const f=window.__nativeFixture;f.emit('media',{...f.media,title:'A new track',revision:2});});
 expect(await page.locator('.home-clips').evaluate(el=>el.scrollTop)).toBe(before);
 await expect(page.getByRole('searchbox',{name:'Search clipboard'})).toHaveCount(0);
 await page.evaluate(()=>window.__nativeFixture.emit('clipboard',window.__nativeFixture.clips));
 expect(await page.locator('.home-clips').evaluate(el=>el.scrollTop)).toBe(before);
 await page.locator('.home-clip').filter({hasText:'The release plan is ready.'}).hover();await page.getByRole('button',{name:'Delete Note 17',exact:true}).click();
 await expect(page.locator('.home-clip')).toHaveCount(17);
 await expect(page.getByRole('dialog')).not.toBeVisible();
 await expect(page.getByRole('button',{name:'Delete Note 17',exact:true})).toHaveCount(0);
 await page.screenshot({path:'artifacts/yuta-home-history.png'});
 expect(await page.evaluate(()=>window.__yutaErrors)).toEqual([]);
});

test('split controls keep settings separate, reject duplicate toggles and recover from failure',async({page})=>{
 await useNativeBridge(page,{startSection:'Home',delays:{connectionSet:500}});
 const wifi=page.locator('[data-action=connectionToggle][data-kind=wifi]');
 await expect(wifi).toHaveAttribute('aria-pressed','true');
 await wifi.click();await expect(wifi).toBeDisabled();
 await expect(wifi).toHaveAttribute('aria-pressed','false');await expect(wifi).toBeEnabled();
 await page.getByRole('button',{name:'Wi-Fi settings',exact:true}).click();
 expect(await page.evaluate(()=>window.__nativeFixture.calls.filter(c=>c.method==='connectionSet').length)).toBe(1);
 expect(await page.evaluate(()=>window.__nativeFixture.calls.filter(c=>c.method==='settings').at(-1).args.page)).toBe('network-wifi');
 await page.evaluate(()=>window.__nativeFixture.connectionFailure=true);
 await wifi.click();await expect(page.locator('#toast')).toContainText('could not change');
 await expect(wifi).toHaveAttribute('aria-pressed','false');await expect(wifi).toBeEnabled();
 expect(await page.evaluate(()=>window.__yutaErrors)).toEqual([]);
});

test('a pending power request blocks duplicate clicks without a confirmation',async({page})=>{
 await useNativeBridge(page,{startSection:'Home',delays:{power:600}});
 await page.getByRole('button',{name:'Shut down',exact:true}).click();
 await expect(page.getByRole('button',{name:'Restart',exact:true})).toBeDisabled();
 await expect(page.getByRole('dialog')).not.toBeVisible();
 await expect(page.getByRole('button',{name:'Restart',exact:true})).toBeEnabled();
 expect(await page.evaluate(()=>window.__nativeFixture.calls.filter(c=>c.method==='power').length)).toBe(1);
});
