import {test,expect} from '@playwright/test';
import {useNativeBridge} from './native-bridge.fixture.js';

async function openRecorder(page){
 await page.getByRole('button',{name:'Settings',exact:true}).click();
 const button=page.getByRole('button',{name:'Change keyboard shortcut',exact:true});
 await button.click();
 await expect(button).toHaveText('Press shortcut…');
 return button;
}

test('Alt Space is the default and a recorded shortcut updates labels and persists after reload',async({page})=>{
 await page.goto('/');await page.getByRole('tab',{name:'Today',exact:true}).click();
 await expect(page.locator('[data-preview-hotkey]')).toHaveText('Alt + Space');
 const button=await openRecorder(page);
 await page.keyboard.press('Control+Shift+KeyY');
 await expect(button).toHaveText('Ctrl + Shift + Y');
 await expect(button).toHaveAttribute('aria-pressed','false');
 await expect(page.locator('[data-preview-hotkey]')).toHaveText('Ctrl + Shift + Y');
 expect(await page.evaluate(()=>JSON.parse(localStorage.getItem('yuta-preview')).settings.hotkey)).toBe('Ctrl+Shift+Y');
 await page.reload();
 await expect(page.locator('[data-preview-hotkey]')).toHaveText('Ctrl + Shift + Y');
 await page.getByRole('button',{name:'Settings',exact:true}).click();
 await expect(page.getByRole('button',{name:'Change keyboard shortcut',exact:true})).toHaveText('Ctrl + Shift + Y');
 expect(await page.evaluate(()=>window.__yutaErrors)).toEqual([]);
});

test('the recorder rejects typing shortcuts and Escape cancels without collapsing or changing the binding',async({page})=>{
 await page.goto('/');await page.getByRole('tab',{name:'Today',exact:true}).click();
 const button=await openRecorder(page);
 await page.keyboard.press('Shift+KeyZ');
 await expect(page.locator('#hotkey-status')).toHaveText('Include Ctrl, Alt, or Win so normal typing stays available.');
 await expect(button).toHaveAttribute('aria-pressed','true');
 await page.keyboard.press('Escape');
 await expect(button).toHaveText('Alt + Space');
 await expect(page.locator('#yuta')).toHaveAttribute('data-expanded','true');
 await expect(page.locator('#hotkey-status')).toHaveText('Shortcut unchanged.');
 await button.click();
 await expect(button).toHaveText('Press shortcut…');
 await page.getByRole('button',{name:'Cancel',exact:true}).click();
 await expect(button).toHaveText('Alt + Space');
 expect(await page.evaluate(()=>JSON.parse(localStorage.getItem('yuta-preview')).settings.hotkey)).toBe('Alt+Space');
});

test('legacy default migrates once and an explicit later Ctrl Alt Space choice survives reopening',async({page})=>{
 await page.addInitScript(()=>{
  if(!localStorage.getItem('yuta-preview'))localStorage.setItem('yuta-preview',JSON.stringify({notes:'Preserve this note',todos:[{id:'saved',text:'Preserve this task',done:false}],settings:{hotkey:'Ctrl+Alt+Space',accent:'rose',city:'Boston'}}));
 });
 await page.goto('/');await page.getByRole('tab',{name:'Today',exact:true}).click();
 await expect(page.locator('[data-preview-hotkey]')).toHaveText('Alt + Space');
 const migrated=await page.evaluate(()=>JSON.parse(localStorage.getItem('yuta-preview')));
 expect(migrated.hotkeyVersion).toBe(1);expect(migrated.notes).toBe('Preserve this note');expect(migrated.todos[0].text).toBe('Preserve this task');expect(migrated.settings.city).toBe('Boston');
 const button=await openRecorder(page);
 await page.keyboard.press('Control+Alt+Space');
 await expect(button).toHaveText('Ctrl + Alt + Space');
 await page.reload();
 await expect(page.locator('[data-preview-hotkey]')).toHaveText('Ctrl + Alt + Space');
 expect(await page.evaluate(()=>JSON.parse(localStorage.getItem('yuta-preview')).notes)).toBe('Preserve this note');
});

test('a shortcut being saved cannot be cancelled into a false unchanged state',async({page})=>{
 await useNativeBridge(page,{delays:{hotkeySet:600}});
 const button=await openRecorder(page);
 await page.keyboard.press('Control+Shift+KeyY');
 await expect(button).toHaveText('Saving…');
 await expect(page.getByRole('button',{name:'Cancel',exact:true})).toBeDisabled();
 await page.keyboard.press('Escape');
 await expect(page.locator('#hotkey-status')).not.toHaveText('Shortcut unchanged.');
 await expect(page.locator('#yuta')).toHaveAttribute('data-expanded','true');
 await expect(button).toHaveText('Ctrl + Shift + Y');
 await expect(page.locator('#hotkey-status')).toHaveText('Shortcut updated.');
 expect(await page.evaluate(()=>window.__nativeFixture.calls.filter(x=>x.method==='hotkeySet').at(-1).args.shortcut)).toBe('Ctrl+Shift+Y');
 expect(await page.evaluate(()=>window.__nativeFixture.hotkeyRecording)).toBe(false);
});

test('native conflict keeps the previous shortcut and native key capture does not toggle the menu',async({page})=>{
 await page.setViewportSize({width:760,height:366});
 await useNativeBridge(page);
 const button=await openRecorder(page);
 await page.evaluate(()=>{window.__nativeFixture.hotkeyConflict=true;});
 await page.keyboard.press('Control+Shift+KeyY');
 await expect(page.locator('#hotkey-status')).toContainText('previous shortcut is unchanged');
 expect(await page.evaluate(()=>window.__nativeFixture.calls.filter(x=>x.method==='hotkeySet').length)).toBe(1);
 await expect(page.locator('#yuta')).toHaveAttribute('data-expanded','true');
 expect(await page.evaluate(()=>window.__nativeFixture.hotkeyRecording)).toBe(true);
 const layout=await page.locator('.panel').evaluate(el=>({height:el.clientHeight,scrollHeight:el.scrollHeight,width:el.clientWidth,scrollWidth:el.scrollWidth}));
 expect(layout.scrollWidth).toBeLessThanOrEqual(layout.width+1);
 expect(layout.scrollHeight).toBeLessThanOrEqual(layout.height+1);
 await page.evaluate(()=>{window.__nativeFixture.hotkeyConflict=false;window.__nativeFixture.emit('hotkeyCaptured','Alt+Space');window.__nativeFixture.emit('hotkeyCaptured','Alt+Space');});
 await expect(button).toHaveText('Alt + Space');
 await expect(button).toHaveAttribute('aria-pressed','false');
 await expect(page.locator('#yuta')).toHaveAttribute('data-expanded','true');
 expect(await page.evaluate(()=>window.__nativeFixture.hotkeyRecording)).toBe(false);
 const calls=await page.evaluate(()=>window.__nativeFixture.calls);
 expect(calls.filter(x=>x.method==='hotkeySet'&&x.args.shortcut==='Alt+Space')).toHaveLength(1);
 expect(calls.filter(x=>x.method==='expand'&&x.args.value===false)).toHaveLength(0);
 await button.click();await expect(button).toHaveText('Press shortcut…');
 await page.evaluate(()=>window.__nativeFixture.emit('hotkeyCancel',null));
 await expect(button).toHaveText('Alt + Space');
 await expect(page.locator('#yuta')).toHaveAttribute('data-expanded','true');
 expect(await page.evaluate(()=>window.__nativeFixture.hotkeyRecording)).toBe(false);
 expect(await page.evaluate(()=>window.__yutaErrors)).toEqual([]);
});
