import {test,expect} from '@playwright/test';

test('music widens lengthwise, retains a focus timer, and pauses its animation when closed',async({page})=>{
 await page.addInitScript(()=>localStorage.setItem('yuta-preview',JSON.stringify({timer:{duration:600,remaining:600,running:true,endsAt:Date.now()+600000}})));
 await page.goto('/');await page.getByRole('tab',{name:'Darch',exact:true}).click();
 const examples=page.getByRole('group',{name:'Preview activity'});
 await examples.getByRole('button',{name:'Music',exact:true}).click();
 const pill=page.getByRole('button',{name:'Open Yuta',exact:true});
 await expect(pill).toHaveClass(/is-music/);
 await expect(pill.locator('.idle-wave i')).toHaveCount(21);
 expect((await pill.locator('.idle-wave i').first().boundingBox()).width).toBeLessThan(1);
 expect((await pill.locator('.idle-wave').boundingBox()).width).toBeLessThan(35);
 await expect(pill.locator('.idle-wave i').first()).toHaveCSS('background-color','rgb(179, 148, 255)');
 await expect(pill.locator('.idle-wave i').last()).toHaveCSS('background-color','rgb(86, 182, 255)');
 await expect(pill.locator('#pill-label')).toHaveText('FOCUS');
 await expect.poll(async()=>(await pill.boundingBox()).width).toBe(224);
 expect((await pill.boundingBox()).height).toBe(28);
 const heights=await pill.locator('.idle-wave i').nth(10).evaluate(async el=>{
  const values=[];for(let i=0;i<120;i++){values.push(el.getBoundingClientRect().height);await new Promise(r=>setTimeout(r,50));}return values;
 });expect(Math.max(...heights)-Math.min(...heights)).toBeGreaterThan(3);
 expect(Math.max(...heights)).toBeLessThan(16);
 await page.screenshot({path:'artifacts/activity-preview-timer.png'});
 await pill.click();await expect(pill).toBeHidden();
 const paused=await page.locator('.preview-pill .idle-wave i').first().evaluate(async el=>{const before=el.style.getPropertyValue('--level');await new Promise(r=>setTimeout(r,120));return [before,el.style.getPropertyValue('--level')];});expect(paused[0]).toBe(paused[1]);
 await examples.getByRole('button',{name:'Clock',exact:true}).click();
 await expect.poll(async()=>(await pill.boundingBox()).width).toBe(224);
 expect(await page.evaluate(()=>window.__yutaErrors)).toEqual([]);
});

test('preview notices use distinct status content and reduced motion is static',async({page})=>{
 await page.emulateMedia({reducedMotion:'reduce'});
 await page.goto('/');await page.getByRole('tab',{name:'Darch',exact:true}).click();
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
 await expect(pill.locator('.idle-percent')).toHaveText('72%');
 await expect(pill.locator('.idle-wave')).toBeHidden();
 await expect(pill.locator('.idle-battery')).toBeVisible();
 const title=await pill.locator('.idle-title').boundingBox(),percent=await pill.locator('.idle-percent').boundingBox(),battery=await pill.locator('.idle-battery').boundingBox();
 expect(title.x+title.width).toBeLessThanOrEqual(percent.x+1);expect(percent.x+percent.width).toBeLessThan(battery.x);
 const fill=await pill.locator('.idle-battery b').boundingBox();expect(fill.width/battery.width).toBeGreaterThan(.5);expect(fill.width/battery.width).toBeLessThan(.73);
 await examples.getByRole('button',{name:'Headphones',exact:true}).click();
 await expect(pill.locator('.idle-title')).toHaveText('My headphones');
 await expect(pill.locator('.idle-percent')).toBeEmpty();
 await examples.getByRole('button',{name:'Call',exact:true}).click();
 await expect(pill.locator('.idle-title')).toHaveText('Mic \u00b7 Discord');await expect(pill.locator('.idle-wave i')).toHaveCount(21);await expect(pill.locator('.idle-battery')).toBeHidden();
 const box=await pill.boundingBox();expect(box.width).toBe(224);expect(box.height).toBe(28);
 expect(await page.evaluate(()=>window.__yutaErrors)).toEqual([]);
});
