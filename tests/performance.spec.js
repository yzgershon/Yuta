import {test,expect} from '@playwright/test';
import {useNativeBridge} from './native-bridge.fixture.js';

test('rapid navigation stays on the last requested section when native reads finish late',async({page})=>{
 await useNativeBridge(page,{delays:{controls:600,media:400,stats:500,wallpapers:700,clipboard:300}});
 await page.evaluate(()=>{
  window.__initialNavigation=document.querySelector('.tabs');
  for(const name of ['Home','Stats','Wallpaper','Today'])document.querySelector(`[role=tab][data-tab="${name}"]`).click();
 });
 await expect(page.getByRole('tabpanel',{name:'Today',exact:true})).toBeVisible();
 await page.waitForTimeout(850);
 await expect(page.getByRole('tabpanel',{name:'Today',exact:true})).toBeVisible();
 await expect(page.getByRole('tab',{name:'Today',exact:true})).toHaveAttribute('aria-selected','true');
 expect(await page.evaluate(()=>window.__initialNavigation===document.querySelector('.tabs'))).toBe(true);
 expect(await page.evaluate(()=>window.__yutaErrors)).toEqual([]);
});

test('keyboard tab focus moves immediately while a native read is pending',async({page})=>{
 await useNativeBridge(page,{delays:{stats:5000,clipboard:5000}});
 await page.getByRole('tab',{name:'Today',exact:true}).focus();
 await page.keyboard.press('ArrowRight');
 await expect(page.getByRole('tab',{name:'Stats',exact:true})).toBeFocused({timeout:1000});
 await page.keyboard.press('ArrowRight');
 await expect(page.getByRole('tab',{name:'Wallpaper',exact:true})).toBeFocused({timeout:1000});
 await page.keyboard.press('ArrowLeft');
 await expect(page.getByRole('tab',{name:'Stats',exact:true})).toBeFocused({timeout:1000});
 await expect(page.getByRole('tabpanel',{name:'Stats',exact:true})).toBeVisible();
 expect(await page.evaluate(()=>window.__nativeFixture.calls.filter(x=>x.method==='stats').length)).toBe(1);
});

test('picture browsing retains decoded images and selection through quick reopens',async({page})=>{
 await useNativeBridge(page,{wallpaperCount:50});
 await page.getByRole('tab',{name:'Wallpaper',exact:true}).click();
 await expect(page.locator('.wallpaper.selected')).toHaveAttribute('data-index','0');
 await page.evaluate(async()=>{
  window.__firstPicture=document.querySelector('.wallpaper.selected img');
  await window.__firstPicture.decode();
  window.__gallery=document.querySelector('.wallpaper-carousel');
 });
 await page.getByRole('button',{name:'Next wallpaper',exact:true}).click();
 expect(await page.evaluate(()=>window.__firstPicture.isConnected&&window.__firstPicture===document.querySelector('.wallpaper[data-index="0"] img'))).toBe(true);
 await page.evaluate(()=>{for(let i=0;i<23;i++)document.querySelector('[data-action=wallNext]').click();});
 await expect(page.locator('.wallpaper.selected')).toHaveAttribute('data-index','24');
 for(let i=0;i<5;i++){
  await page.evaluate(()=>{window.__nativeFixture.emit('expanded',false);window.__nativeFixture.emit('expanded',true);});
 }
 await expect(page.locator('.wallpaper.selected')).toHaveAttribute('data-index','24');
 expect(await page.evaluate(()=>window.__gallery===document.querySelector('.wallpaper-carousel'))).toBe(true);
 expect(await page.evaluate(()=>window.__nativeFixture.calls.filter(x=>x.method==='wallpapers').length)).toBe(1);
 expect(await page.evaluate(()=>window.__nativeFixture.calls.filter(x=>x.method==='save'&&x.args.key==='notes').length)).toBe(0);
 expect(await page.evaluate(()=>window.__yutaErrors)).toEqual([]);
});

test('background app refresh preserves the active search and text selection',async({page})=>{
 await useNativeBridge(page);
 await page.getByRole('tab',{name:'Home',exact:true}).click();await page.getByRole('button',{name:'All apps',exact:true}).click();
 const search=page.locator('#search');
 await search.fill('Visual');
 await search.evaluate(input=>{input.setSelectionRange(2,4);window.__searchInput=input;});
 await page.evaluate(()=>window.__nativeFixture.emit('apps',[...window.__nativeFixture.apps,{id:'new',name:'Visual companion',target:'C:\\Apps\\companion.exe',kind:'Application'}]));
 await expect(search).toBeFocused();await expect(search).toHaveValue('Visual');
 expect(await search.evaluate(input=>({same:input===window.__searchInput,start:input.selectionStart,end:input.selectionEnd}))).toEqual({same:true,start:2,end:4});
 await expect(page.locator('.launcher-row')).toHaveCount(2);
 await page.keyboard.type('XX');await expect(search).toHaveValue('ViXXal');
 expect(await page.evaluate(()=>window.__yutaErrors)).toEqual([]);
});

test('an unsaved to-do survives unrelated updates after focus moves away',async({page})=>{
 await page.goto('/');await page.getByRole('tab',{name:'Today',exact:true}).click();
 const draft=page.getByLabel('New to-do',{exact:true});
 await draft.fill('Keep this thought while I check next week');
 await page.getByRole('button',{name:'Next week',exact:true}).click();
 await expect(draft).not.toBeFocused();
 await expect(draft).toHaveValue('Keep this thought while I check next week');
 await page.getByRole('tab',{name:'Home',exact:true}).click();await page.getByRole('button',{name:'All apps',exact:true}).click();
 await page.getByRole('tab',{name:'Today',exact:true}).click();
 await expect(draft).toHaveValue('Keep this thought while I check next week');
 await page.getByRole('button',{name:'Add task',exact:true}).click();
 await expect(page.getByText('Keep this thought while I check next week',{exact:true})).toBeVisible();
 await expect(draft).toHaveValue('');
});

test('connection icons launch settings and retain focus across media refresh',async({page})=>{
 await useNativeBridge(page,{delays:{controls:250,media:400}});
 await page.getByRole('tab',{name:'Home',exact:true}).click();
 const audio=page.getByRole('button',{name:'Audio settings',exact:true});
 await expect(page.locator('.battery')).toHaveCount(0);
 await expect(page.locator('.media-info strong')).toHaveText('Fixture track');
 for(const name of ['Wi-Fi settings','Bluetooth settings','Audio settings'])await page.getByRole('button',{name,exact:true}).click();
 expect(await page.evaluate(()=>window.__nativeFixture.calls.filter(x=>x.method==='settings').map(x=>x.args.page))).toEqual(['network-wifi','bluetooth','sound']);
 await audio.evaluate(button=>window.__connectionButton=button);
 await page.waitForTimeout(2100);
 // Reopening refreshes the hardware snapshot while retaining the active panel.
 await page.evaluate(()=>{window.__nativeFixture.track='Next fixture track';window.__nativeFixture.emit('expanded',false);window.__nativeFixture.emit('expanded',true);});
 await audio.focus();
 await expect(page.locator('.media-info strong')).toHaveText('Next fixture track');
 expect(await page.evaluate(()=>window.__nativeFixture.calls.filter(x=>x.method==='controls').length)).toBe(0);
 await expect(audio).toBeFocused();
 expect(await audio.evaluate(button=>button===window.__connectionButton)).toBe(true);
 expect(await page.evaluate(()=>window.__yutaErrors)).toEqual([]);
});
