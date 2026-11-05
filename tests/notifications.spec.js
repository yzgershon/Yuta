import {test,expect} from '@playwright/test';
import {useNativeBridge} from './native-bridge.fixture.js';

test('portable notifications explain setup and show a dismissible preview without enabling access',async({page})=>{
 await page.goto('/');await page.getByRole('button',{name:'Settings',exact:true}).click();
 await expect(page.getByText('MSIX required',{exact:true})).toBeVisible();
 await page.getByRole('button',{name:'Try a preview',exact:true}).click();
 const sample=page.getByRole('button',{name:'Dismiss sample notification'});
 await expect(sample).toBeVisible();await expect(sample).toContainText('No inbox');
 await sample.click();await expect(sample).toHaveCount(0);
 await page.setViewportSize({width:280,height:900});
 await page.getByRole('button',{name:'Try a preview',exact:true}).click();
 const bounds=await sample.boundingBox();expect(bounds.x).toBeGreaterThanOrEqual(0);expect(bounds.x+bounds.width).toBeLessThanOrEqual(280);
 await sample.click();
 expect(await page.evaluate(()=>JSON.parse(localStorage.getItem('yuta-preview')||'{}').settings?.notificationPreviews)).not.toBe(true);
});

test('notification permission denial stays off and the Windows settings action is explicit',async({page})=>{
 await useNativeBridge(page,{startSection:'Home'});
 await page.evaluate(()=>{window.__nativeFixture.notificationAccess={state:'denied',enabled:false};});
 await page.getByRole('button',{name:'Settings',exact:true}).click();
 await page.getByRole('button',{name:'Windows settings',exact:true}).click();
 expect(await page.evaluate(()=>window.__nativeFixture.calls.filter(x=>x.method==='settings').at(-1).args.page)).toBe('privacy-notifications');
 expect(await page.evaluate(()=>window.__nativeFixture.calls.filter(x=>x.method==='notificationsEnable').length)).toBe(0);
});
