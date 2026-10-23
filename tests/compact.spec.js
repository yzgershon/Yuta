import {test,expect} from '@playwright/test';

for(const [shape,width] of [['pill',168],['notch',176]]){
 test(`compact ${shape} keeps its small bounds and opens with the keyboard`,async({page})=>{
  await page.addInitScript(shape=>localStorage.setItem('yuta-preview',JSON.stringify({appearanceVersion:2,settings:{shape,monitor:0,clipboardEnabled:false,city:'',fahrenheit:true,openOnHover:false}})),shape);
  await page.goto('/');await page.getByRole('tab',{name:'Today',exact:true}).click();
  await page.getByLabel('New to-do',{exact:true}).fill('A draft worth keeping');
  await page.getByRole('button',{name:'Collapse menu',exact:true}).click();
  const pill=page.getByRole('button',{name:'Open Yuta',exact:true});
  await expect(pill).toBeVisible();
  await expect(pill.locator('#pill-time')).toHaveText(/^\d{1,2}:\d{2}$/);
  await expect(pill.locator('#pill-label')).toHaveText(/^[AP]M$/);
  await expect(pill.locator('.pill-signal,.pill-idle-mark')).toHaveCount(0);
  await expect(page.getByRole('tablist',{name:'Sections'})).toBeHidden();
  const bounds=await pill.boundingBox();
  expect(bounds.width).toBe(width);expect(bounds.height).toBe(28);
  await pill.focus();await page.keyboard.press('Enter');
  await expect(page.getByRole('tabpanel',{name:'Today',exact:true})).toBeVisible();
  await expect(page.getByLabel('New to-do',{exact:true})).toHaveValue('A draft worth keeping');
  await page.getByRole('button',{name:'1m',exact:true}).click();
  await page.getByRole('button',{name:'Start',exact:true}).click();
  await page.getByRole('button',{name:'Collapse menu',exact:true}).click();
  await expect(pill.locator('#pill-time')).toBeVisible();
  await expect(pill.locator('#pill-label')).toHaveText('FOCUS');
  const runningBounds=await pill.boundingBox();
  expect(runningBounds.width).toBe(224);expect(runningBounds.height).toBe(28);
  await page.screenshot({path:`artifacts/compact-${shape}.png`});
  expect(await page.evaluate(()=>window.__yutaErrors)).toEqual([]);
 });
}
