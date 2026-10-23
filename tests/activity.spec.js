import {test,expect} from '@playwright/test';

test('music widens lengthwise, retains a focus timer, and pauses its animation when closed',async({page})=>{
 await page.goto('/');await page.getByRole('tab',{name:'Today',exact:true}).click();
 await page.getByRole('button',{name:'1m',exact:true}).click();
 await page.getByRole('button',{name:'Start',exact:true}).click();
 const examples=page.getByRole('group',{name:'Preview activity'});
 await examples.getByRole('button',{name:'Music',exact:true}).click();
 const pill=page.getByRole('button',{name:'Open Yuta',exact:true});
 await expect(pill).toHaveClass(/is-music/);
 await expect(pill.locator('#pill-label')).toHaveText('FOCUS');
 await expect.poll(async()=>(await pill.boundingBox()).width).toBe(224);
 expect((await pill.boundingBox()).height).toBe(28);
 const frames=await pill.locator('.idle-wave i').first().evaluate(async el=>{
  const before=getComputedStyle(el).transform;await new Promise(r=>setTimeout(r,140));return [before,getComputedStyle(el).transform];
 });expect(frames[0]).not.toBe(frames[1]);
 await page.screenshot({path:'artifacts/activity-preview-timer.png'});
 await pill.click();await expect(pill).toBeHidden();
 expect(await page.locator('.preview-pill .idle-wave i').first().evaluate(el=>getComputedStyle(el).animationPlayState)).toBe('paused');
 await examples.getByRole('button',{name:'Clock',exact:true}).click();
 await expect.poll(async()=>(await pill.boundingBox()).width).toBe(224);
 expect(await page.evaluate(()=>window.__yutaErrors)).toEqual([]);
});

test('preview notices use distinct status content and reduced motion is static',async({page})=>{
 await page.emulateMedia({reducedMotion:'reduce'});
 await page.goto('/');await page.getByRole('tab',{name:'Today',exact:true}).click();
 const examples=page.getByRole('group',{name:'Preview activity'}),pill=page.getByRole('button',{name:'Open Yuta',exact:true});
 await examples.getByRole('button',{name:'Music',exact:true}).click();
 await expect(pill.locator('.idle-title')).toHaveText('Rover');
 await expect(pill).toHaveAttribute('title','Rover (feat. Lil Tecca)');
 await expect(pill.locator('.idle-title')).toBeVisible();
 await expect(pill.locator('.idle-cover')).toBeVisible();
 expect((await pill.boundingBox()).width).toBe(224);
 expect(await pill.locator('.idle-wave i').first().evaluate(el=>getComputedStyle(el).animationName)).toBe('none');
 await examples.getByRole('button',{name:'Charging',exact:true}).click();
 await expect(pill.locator('.idle-title')).toHaveText('Charging');
 await expect(pill.locator('.idle-percent')).toHaveText('48%');
 await expect(pill.locator('.idle-wave')).toBeHidden();
 await examples.getByRole('button',{name:'Headphones',exact:true}).click();
 await expect(pill.locator('.idle-title')).toHaveText('My headphones');
 await expect(pill.locator('.idle-percent')).toBeEmpty();
 const box=await pill.boundingBox();expect(box.width).toBe(224);expect(box.height).toBe(28);
 expect(await page.evaluate(()=>window.__yutaErrors)).toEqual([]);
});
