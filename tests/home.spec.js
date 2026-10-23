import {test,expect} from '@playwright/test';
import {useNativeBridge} from './native-bridge.fixture.js';

test('Home combines launcher, clipboard, connections, player and direct power actions',async({page})=>{
 await page.setViewportSize({width:900,height:452});
 await useNativeBridge(page,{startSection:'Home'});
 await expect(page.getByRole('tab')).toHaveText(['Home','Today','Stats','Wallpaper']);
 await expect(page.getByRole('tab',{name:'Home',exact:true})).toHaveAttribute('aria-selected','true');
 await expect(page.locator('.home-app')).toHaveCount(6);
 await expect(page.getByRole('group',{name:'Connection settings'}).getByRole('button')).toHaveCount(3);
 await expect(page.getByRole('region',{name:'Music player'})).toBeVisible();
 for(const [label,action] of [['Lock','lock'],['Sleep','sleep'],['Restart','restart'],['Shut down','shutdown']]){
  await page.getByRole('button',{name:label,exact:true}).click();
  await expect.poll(()=>page.evaluate(()=>window.__nativeFixture.calls.filter(c=>c.method==='power').at(-1)?.args.action)).toBe(action);
  await expect(page.getByRole('dialog')).not.toBeVisible();
 }
 expect(await page.evaluate(()=>window.__nativeFixture.calls.filter(c=>c.method==='power').length)).toBe(4);
 expect(await page.evaluate(()=>window.__yutaErrors)).toEqual([]);
 await page.screenshot({path:'artifacts/yuta-home-dashboard.png'});
});

test('Home prioritizes favorites, launches shortcuts and keeps complete app search inside Home',async({page})=>{
 await useNativeBridge(page,{startSection:'Home'});
 await page.getByRole('button',{name:'All apps',exact:true}).click();
 await page.locator('.launcher-row').filter({has:page.getByText('Application 9',{exact:true})}).getByRole('button',{name:'Add favorite',exact:true}).click();
 await page.getByRole('button',{name:'Home',exact:true}).click();
 await expect(page.locator('.home-app').first()).toHaveAccessibleName('Open Application 9');
 await page.locator('.home-app').first().click();
 expect(await page.evaluate(()=>window.__nativeFixture.calls.find(c=>c.method==='launch')?.args.target)).toBe('C:\\Apps\\app-9.exe');
 await page.getByRole('button',{name:'All apps',exact:true}).click();
 await page.getByRole('textbox',{name:'Search apps, files and snippets'}).fill('Application 17');
 await expect(page.locator('.launcher-row')).toHaveCount(11);
 await expect(page.getByRole('tab',{name:'Home',exact:true})).toHaveAttribute('aria-selected','true');
 await page.getByRole('button',{name:'Home',exact:true}).click();
 await expect(page.getByRole('tabpanel',{name:'Home',exact:true})).toBeVisible();
 await page.keyboard.press('Control+k');
 await expect(page.getByRole('textbox',{name:'Search apps, files and snippets'})).toBeFocused();
});

test('recent clipboard copies immediately and opens the full selected item without losing Home',async({page})=>{
 await useNativeBridge(page,{startSection:'Home',delays:{clipboard:350}});
 await page.evaluate(()=>{const f=window.__nativeFixture;f.clips=[{id:'clip-1',type:'text',label:'Meeting notes',text:'Meeting notes',pinned:true,copied:new Date().toISOString()},{id:'clip-2',type:'text',label:'<img src=x onerror=alert(1)>',text:'Literal markup',pinned:false,copied:new Date().toISOString()}];f.emit('clipboard',f.clips);});
 await expect(page.locator('.home-clip')).toHaveCount(2);
 await page.waitForTimeout(400);
 await expect(page.locator('.home-clip')).toHaveCount(2);
 await page.getByRole('button',{name:'Copy Meeting notes',exact:true}).click();
 expect(await page.evaluate(()=>window.__nativeFixture.calls.filter(c=>c.method==='clipCopy').map(c=>c.args.id))).toEqual(['clip-1']);
 await expect(page.locator('.home-clip img')).toHaveCount(0);
 await page.locator('.home-clip-preview').first().click();
 await expect(page.getByRole('tabpanel',{name:'Clipboard',exact:true})).toBeVisible();
 await expect(page.locator('.clip-preview pre')).toHaveText('Meeting notes');
 await expect(page.getByRole('tab',{name:'Home',exact:true})).toHaveAttribute('aria-selected','true');
 await page.getByRole('button',{name:'Home',exact:true}).click();
 await expect(page.locator('.home-clip')).toHaveCount(2);
 expect(await page.evaluate(()=>window.__yutaErrors)).toEqual([]);
});

test('Home searches the full clipboard and retains its query and focus through updates and other tools',async({page})=>{
 await page.setViewportSize({width:900,height:452});
 await useNativeBridge(page,{startSection:'Home'});
 await page.evaluate(()=>{const f=window.__nativeFixture;f.clips=Array.from({length:8},(_,i)=>({id:'clip-'+i,type:'text',label:i===7?'Older project notes':'Recent '+i,text:i===7?'The release plan is ready.':'Recent '+i,pinned:false,copied:new Date(Date.now()-i*60000).toISOString()}));f.emit('clipboard',f.clips);});
 await expect(page.locator('.home-clip')).toHaveCount(3);
 await expect(page.getByText('Older project notes',{exact:true})).toHaveCount(0);
 await page.screenshot({path:'artifacts/yuta-clipboard-first-populated.png'});
 const search=page.getByRole('searchbox',{name:'Search clipboard',exact:true});
 await search.fill('release');
 await expect(page.locator('.home-clip')).toHaveCount(1);
 await expect(page.getByRole('button',{name:'Copy Older project notes',exact:true})).toBeVisible();
 await page.evaluate(()=>{const f=window.__nativeFixture;f.emit('media',{...f.media,title:'New track',revision:2});f.emit('clipboard',f.clips);});
 await expect(search).toBeFocused();
 await expect(search).toHaveValue('release');
 await page.getByRole('button',{name:'Copy Older project notes',exact:true}).click();
 expect(await page.evaluate(()=>window.__nativeFixture.calls.filter(c=>c.method==='clipCopy').at(-1).args.id)).toBe('clip-7');
 await search.fill('no matching phrase');
 await expect(page.getByText('No matching clipboard items.',{exact:true})).toBeVisible();
 await search.fill('release');
 await page.getByRole('button',{name:'All apps',exact:true}).click();
 await page.getByRole('textbox',{name:'Search apps, files and snippets'}).fill('Application 17');
 await page.getByRole('button',{name:'Home',exact:true}).click();
 await expect(search).toHaveValue('release');
 await expect(page.locator('.home-clip')).toHaveCount(1);
 await page.locator('.home-clip-preview').first().click();
 await expect(page.locator('.clip-preview pre')).toHaveText('The release plan is ready.');
 await page.getByRole('button',{name:'Home',exact:true}).click();
 await search.fill('');
 await expect(page.locator('.home-clip')).toHaveCount(3);
 await page.getByRole('button',{name:'Open quick note',exact:true}).click();
 await expect(page.getByRole('textbox',{name:'Quick note',exact:true})).toBeVisible();
 expect(await page.evaluate(()=>window.__yutaErrors)).toEqual([]);
});

test('a pending power request blocks duplicate clicks without opening a confirmation',async({page})=>{
 await useNativeBridge(page,{startSection:'Home',delays:{power:600}});
 await page.getByRole('button',{name:'Shut down',exact:true}).click();
 await expect(page.getByRole('button',{name:'Restart',exact:true})).toBeDisabled();
 await expect(page.getByRole('dialog')).not.toBeVisible();
 await expect(page.getByRole('button',{name:'Restart',exact:true})).toBeEnabled();
 expect(await page.evaluate(()=>window.__nativeFixture.calls.filter(c=>c.method==='power').length)).toBe(1);
});
