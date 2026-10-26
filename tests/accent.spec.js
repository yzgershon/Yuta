import {test,expect} from '@playwright/test';
import {useNativeBridge} from './native-bridge.fixture.js';

const choices=[
 ['custom','Violet','rgb(155, 128, 255)','rgb(42, 34, 67)'],
 ['lavender','Lavender','rgb(194, 182, 236)','rgb(42, 34, 67)'],
 ['ice','Ice blue','rgb(172, 212, 245)','rgb(23, 42, 59)'],
 ['rose','Rose','rgb(228, 178, 194)','rgb(64, 35, 48)'],
 ['amber','Amber','rgb(223, 193, 142)','rgb(56, 43, 24)'],
 ['porcelain','Porcelain','rgb(216, 217, 211)','rgb(41, 45, 43)'],
];

test('personal accents persist while all connections remain blue',async({page})=>{
 await page.goto('/');await page.getByRole('tab',{name:'Darch',exact:true}).click();
 await page.getByRole('button',{name:'Settings',exact:true}).click();
 const accents=page.getByRole('group',{name:'Accent color',exact:true});
 await expect(accents.getByRole('button',{name:'Violet',exact:true})).toHaveAttribute('aria-pressed','true');
 for(const [id,name,color,ink] of choices){
  await page.getByRole('button',{name:'Settings',exact:true}).click();
  await accents.getByRole('button',{name,exact:true}).click();
  await expect(accents.getByRole('button',{name,exact:true})).toHaveAttribute('aria-pressed','true');
  await page.getByRole('tab',{name:'Home',exact:true}).click();
  await expect(page.getByRole('button',{name:'Wi-Fi settings',exact:true})).toBeEnabled();
  await expect(page.getByRole('tab',{name:'Home',exact:true})).toHaveCSS('color',color);
  await expect(page.getByRole('button',{name:'Wi-Fi settings',exact:true})).toHaveCSS('color','rgb(18, 26, 35)');
  const groups=await page.locator('.connection-split.is-on').evaluateAll(els=>els.map(el=>getComputedStyle(el).backgroundColor));
  expect(new Set(groups).size).toBe(1);expect(groups).toHaveLength(3);
  for(const group of await page.locator('.connection').all())expect(await group.evaluate(el=>getComputedStyle(el).getPropertyValue('--connection').trim())).toBe('#8cc9ed');
  await page.getByRole('tab',{name:'Wallpaper',exact:true}).click();
  await expect(page.getByRole('button',{name:'Set wallpaper',exact:true})).toHaveCSS('background-color',color);
  await expect(page.getByRole('button',{name:'Set wallpaper',exact:true})).toHaveCSS('color',ink);
  expect(await page.evaluate(()=>JSON.parse(localStorage.getItem('yuta-preview')).settings.accent)).toBe(id);
 }
 await page.reload();
 await page.getByRole('tab',{name:'Home',exact:true}).click();
 await expect(page.getByRole('tab',{name:'Home',exact:true})).toHaveCSS('color',choices.at(-1)[2]);
 await page.getByRole('button',{name:'Settings',exact:true}).click();
 await expect(accents.getByRole('button',{name:'Porcelain',exact:true})).toHaveAttribute('aria-pressed','true');
 expect(await page.evaluate(()=>window.__yutaErrors)).toEqual([]);
});

test('accent choices and unavailable-shortcut warning fit inside full-size native Settings',async({page})=>{
 await page.setViewportSize({width:760,height:366});
 await useNativeBridge(page,{hotkey:false});
 await page.getByRole('button',{name:'Settings',exact:true}).click();
 const accents=page.getByRole('group',{name:'Accent color',exact:true});
 await expect(page.getByText('The shortcut is busy. Use the notch or system tray.',{exact:false})).toBeVisible();
 await expect(accents.getByRole('button')).toHaveCount(6);
 for(const [,name] of choices){
  const swatch=accents.getByRole('button',{name,exact:true});
  await expect(swatch).toBeVisible();
  const bounds=await swatch.boundingBox();
  expect(bounds.x).toBeGreaterThanOrEqual(0);expect(bounds.x+bounds.width).toBeLessThanOrEqual(760);
  expect(bounds.y+bounds.height).toBeLessThanOrEqual(366);
 }
 const layout=await page.locator('.panel:not([hidden])').evaluate(panel=>({width:panel.clientWidth,scrollWidth:panel.scrollWidth,height:panel.clientHeight,scrollHeight:panel.scrollHeight}));
 expect(layout.scrollWidth).toBeLessThanOrEqual(layout.width+1);
 expect(layout.scrollHeight).toBeLessThanOrEqual(layout.height+1);
 await page.screenshot({path:'artifacts/yuta-accent-settings.png'});
});
