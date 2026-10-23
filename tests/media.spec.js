import {test,expect} from '@playwright/test';
import {useNativeBridge} from './native-bridge.fixture.js';

test('music card displays live artwork and timeline without the album row and keeps connection-button focus on track events',async({page})=>{
 await useNativeBridge(page);await page.getByRole('tab',{name:'Home',exact:true}).click();
 const player=page.getByRole('region',{name:'Music player'});
 await expect(player.locator('.media-source')).toHaveText('Spotify');
 await expect(player.locator('.media-info strong')).toHaveText('Fixture track');
 await expect(player.locator('.media-album')).toHaveCount(0);
 await expect(player.getByAltText('Fixture album cover')).toBeVisible();
 await expect(player.locator('.media-times')).toContainText('3:00');
 const connection=page.getByRole('button',{name:'Audio settings',exact:true});await connection.focus();
 await page.evaluate(()=>{window.__connectionNode=document.querySelector('[data-page="sound"]');window.__nativeFixture.emit('media',{...window.__nativeFixture.media,title:'Another song',album:'Another album',revision:2});});
 await expect(player.locator('.media-info strong')).toHaveText('Another song');await expect(connection).toBeFocused();
 expect(await page.evaluate(()=>window.__connectionNode===document.querySelector('[data-page="sound"]'))).toBe(true);
 await page.evaluate(()=>window.__nativeFixture.emit('media',{...window.__nativeFixture.media,title:'Stale response',revision:1}));
 await expect(player.locator('.media-info strong')).toHaveText('Another song');
});

test('pause responds immediately while Windows is pending and rapid play follows in order',async({page})=>{
 await useNativeBridge(page,{delays:{mediaAction:650}});await page.getByRole('tab',{name:'Home',exact:true}).click();
 await page.getByRole('button',{name:'Pause music',exact:true}).click();
 await expect(page.getByRole('button',{name:'Play music',exact:true})).toBeVisible({timeout:350});
 await page.getByRole('button',{name:'Play music',exact:true}).click();
 await expect(page.getByRole('button',{name:'Pause music',exact:true})).toBeVisible({timeout:350});
 await page.evaluate(()=>window.__nativeFixture.emit('media',{...window.__nativeFixture.media,playing:true}));
 await expect.poll(()=>page.evaluate(()=>window.__nativeFixture.calls.filter(x=>x.method==='mediaAction').map(x=>x.args.action))).toEqual(['pause','play']);
 await expect(page.locator('.media-status')).toHaveText('Playing');
 const commands=await page.evaluate(()=>window.__nativeFixture.calls.filter(x=>x.method==='mediaAction'));
 expect(commands.every(x=>x.args.source==='Spotify.exe')).toBe(true);
 expect(await page.evaluate(()=>window.__yutaErrors)).toEqual([]);
});

test('a delayed seek retains its position through old timeline events',async({page})=>{
 await useNativeBridge(page,{delays:{mediaAction:700}});await page.getByRole('tab',{name:'Home',exact:true}).click();
 const progress=page.getByLabel('Track progress',{exact:true});await expect(progress).toBeEnabled();
 await progress.evaluate(range=>{range.value='90';range.dispatchEvent(new Event('input',{bubbles:true}));range.dispatchEvent(new Event('change',{bubbles:true}));});
 await page.evaluate(()=>window.__nativeFixture.emit('media',{...window.__nativeFixture.media}));
 expect(Number(await progress.inputValue())).toBeGreaterThanOrEqual(90);
 await expect.poll(()=>page.evaluate(()=>window.__nativeFixture.media.position)).toBe(90);
 await expect(page.locator('.media-status')).toHaveText('Playing');
});

test('a track change while dragging cannot seek the new song to the old position',async({page})=>{
 await useNativeBridge(page);await page.getByRole('tab',{name:'Home',exact:true}).click();
 const progress=page.getByLabel('Track progress',{exact:true});await expect(progress).toBeEnabled();
 await progress.dispatchEvent('pointerdown');
 await page.evaluate(()=>{window.__nativeFixture.media={...window.__nativeFixture.media,title:'New song',position:0,revision:2};window.__nativeFixture.emit('media',window.__nativeFixture.media);});
 await progress.evaluate(range=>{range.value='90';range.dispatchEvent(new Event('input',{bubbles:true}));range.dispatchEvent(new Event('change',{bubbles:true}));});
 await expect(page.locator('#toast')).toHaveText('The track changed. Try seeking again.');
 expect(await page.evaluate(()=>window.__nativeFixture.calls.filter(x=>x.method==='mediaAction'&&x.args.action==='seek').length)).toBe(0);
});

test('rejected playback rolls back optimistic state and reports the actual failure',async({page})=>{
 await useNativeBridge(page);await page.getByRole('tab',{name:'Home',exact:true}).click();
 await expect(page.getByRole('button',{name:'Pause music',exact:true})).toBeVisible();
 await page.evaluate(()=>window.__nativeFixture.mediaFailure=true);
 await page.getByRole('button',{name:'Pause music',exact:true}).click();
 await expect(page.locator('#toast')).toHaveText('Music app rejected the action.');
 await expect(page.getByRole('button',{name:'Pause music',exact:true})).toBeVisible();
 await expect(page.locator('.media-status')).toHaveText('Playing');
});

test('seeking uses the selected track and unavailable sessions disable transport',async({page})=>{
 await useNativeBridge(page);await page.getByRole('tab',{name:'Home',exact:true}).click();
 const progress=page.getByLabel('Track progress',{exact:true});await expect(progress).toBeEnabled();
 await progress.evaluate(range=>{range.value='75';range.dispatchEvent(new Event('input',{bubbles:true}));range.dispatchEvent(new Event('change',{bubbles:true}));});
 await expect.poll(()=>page.evaluate(()=>window.__nativeFixture.calls.find(x=>x.method==='mediaAction'&&x.args.action==='seek')?.args.position)).toBe(75);
 await page.evaluate(()=>window.__nativeFixture.emit('media',{available:false,revision:99}));
 await expect(page.getByRole('button',{name:'Play music',exact:true})).toBeDisabled();
 await expect(page.getByRole('button',{name:'Next track',exact:true})).toBeDisabled();
 await expect(page.locator('.album-art img')).toHaveCount(0);
});
