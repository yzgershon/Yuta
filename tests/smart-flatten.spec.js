import {test,expect} from '@playwright/test';
test('smart flattening defaults on and an explicit off choice persists alongside other settings',async({page})=>{
 await page.goto('/');await page.getByRole('button',{name:'Settings',exact:true}).click();
 const toggle=page.getByRole('switch',{name:'Flatten over covered controls',exact:true});
 await expect(toggle).toHaveAttribute('aria-checked','true');await toggle.click();await expect(toggle).toHaveAttribute('aria-checked','false');
 await page.reload();await page.getByRole('button',{name:'Settings',exact:true}).click();await expect(toggle).toHaveAttribute('aria-checked','false');
 await expect(page.getByRole('switch',{name:'Open with deliberate hover',exact:true})).toHaveAttribute('aria-checked','true');
 await toggle.click();await expect(toggle).toHaveAttribute('aria-checked','true');expect(await page.evaluate(()=>window.__yutaErrors)).toEqual([]);
});
