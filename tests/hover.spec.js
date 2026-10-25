import {test,expect} from '@playwright/test';
import {useNativeBridge} from './native-bridge.fixture.js';

test('default Yuta notch opens on deliberate hover, cancels retraction on reentry, and closes after leaving',async({page})=>{
 await page.goto('/');await page.getByRole('tab',{name:'Today',exact:true}).click();
 await page.getByRole('button',{name:'Collapse menu',exact:true}).click();
 const root=page.locator('#yuta');
 const pill=page.getByRole('button',{name:'Open Yuta',exact:true});
 await expect(root).toHaveClass(/notch/);await expect(pill).toBeVisible();
 await pill.hover();await expect(root).toHaveAttribute('data-expanded','true');
 await root.hover({position:{x:80,y:20}});
 await page.mouse.move(5,850);
 await root.hover({position:{x:80,y:20}});
 await page.waitForTimeout(260);
 await expect(root).toHaveAttribute('data-expanded','true');
 await page.mouse.move(5,850);
 await expect(root).toHaveAttribute('data-expanded','false');
 await expect(pill).toBeVisible();
});

test('passing across the notch, brief pauses, and drags do not expand it',async({page})=>{
 await page.goto('/');await page.getByRole('tab',{name:'Today',exact:true}).click();
 await page.getByRole('button',{name:'Collapse menu',exact:true}).click();
 const root=page.locator('#yuta'),pill=page.getByRole('button',{name:'Open Yuta',exact:true});
 const box=await pill.boundingBox(),y=box.y+box.height/2;
 await page.mouse.move(box.x-10,y);
 for(let x=box.x+25;x<=box.x+110;x+=5){await page.mouse.move(x,y);await page.waitForTimeout(40);}
 await expect(root).toHaveAttribute('data-expanded','false');
 await page.mouse.move(box.x+box.width+10,y);
 await pill.hover();await page.waitForTimeout(250);
 await expect(root).toHaveAttribute('data-expanded','false');
 await page.mouse.move(box.x-10,y);await page.mouse.down();
 await pill.hover();await page.waitForTimeout(750);
 await expect(root).toHaveAttribute('data-expanded','false');
 await page.mouse.move(box.x-10,y);await page.mouse.up();
 await pill.click();await expect(root).toHaveAttribute('data-expanded','true');
});

test('click-only setting persists and ignores even a settled hover',async({page})=>{
 await page.goto('/');await page.getByRole('tab',{name:'Today',exact:true}).click();
 await page.getByRole('button',{name:'Settings',exact:true}).click();
 const setting=page.getByRole('switch',{name:'Open with deliberate hover'});
 await setting.click();await expect(setting).toHaveAttribute('aria-checked','false');
 await page.reload();
 await page.getByRole('button',{name:'Collapse menu',exact:true}).click();
 const pill=page.getByRole('button',{name:'Open Yuta',exact:true});
 await pill.hover();await page.waitForTimeout(800);
 await expect(page.locator('#yuta')).toHaveAttribute('data-expanded','false');
 await pill.click();await expect(page.locator('#yuta')).toHaveAttribute('data-expanded','true');
});

test('closing under the pointer stays closed until it leaves and deliberately returns',async({page})=>{
 await page.goto('/');await page.getByRole('tab',{name:'Today',exact:true}).click();
 const root=page.locator('#yuta'),box=await root.boundingBox();
 await page.mouse.move(box.x+box.width/2,box.y+10);
 await page.keyboard.press('Escape');await page.waitForTimeout(850);
 await expect(root).toHaveAttribute('data-expanded','false');
 await page.mouse.move(5,850);
 await page.getByRole('button',{name:'Open Yuta',exact:true}).hover();
 await expect(root).toHaveAttribute('data-expanded','true');
});

test('an open dialog and held pointer protect the expanded menu from retraction',async({page})=>{
 await page.goto('/');await page.getByRole('tab',{name:'Today',exact:true}).click();
 const root=page.locator('#yuta');
 await page.getByRole('button',{name:'Add event',exact:true}).click();
 await page.mouse.move(5,850);await page.waitForTimeout(300);
 await expect(page.getByRole('dialog')).toBeVisible();
 await expect(root).toHaveAttribute('data-expanded','true');
 await page.getByRole('button',{name:'Close dialog',exact:true}).click();
 await root.hover({position:{x:80,y:20}});
 await page.mouse.down();await page.mouse.move(5,850);await page.waitForTimeout(300);
 await expect(root).toHaveAttribute('data-expanded','true');
 await page.mouse.up();
 await expect(root).toHaveAttribute('data-expanded','false');
});

test('every section fits the compact 760px layout with internal scrolling and usable controls',async({page})=>{
 await page.setViewportSize({width:760,height:366});
 await useNativeBridge(page);
 const sections={Home:366,Today:366,Stats:366,Wallpaper:366,Routines:366,Settings:366};
 for(const [name,height] of Object.entries(sections)){
  await page.setViewportSize({width:760,height});
  await page.evaluate(name=>window.__nativeFixture.emit('navigate',name),name);
  await expect(page.getByRole('tabpanel',{name,exact:true})).toBeVisible();
  if(['Home','Today','Stats','Wallpaper'].includes(name)){
   await expect(page.getByRole('tab',{name,exact:true})).toHaveAttribute('aria-selected','true');
   await expect(page.locator('[role=tab][aria-selected=true]')).toHaveCount(1);
  }
  if(name==='Home'){
   await expect(page.getByRole('group',{name:'Connections'}).getByRole('button')).toHaveCount(6);
   await expect(page.getByRole('button',{name:'Audio settings',exact:true})).toBeEnabled();
   await expect(page.locator('#volume,#brightness,[data-page="notifications"],.app-footer,.battery')).toHaveCount(0);
   const player=await page.locator('.media-card').boundingBox();
   const transport=await page.locator('.media-transport').boundingBox();
   const info=await page.locator('.media-info').boundingBox();
   const clipboard=await page.locator('.home-clipboard').boundingBox();
   const apps=await page.locator('.home-apps').boundingBox();
   await expect(page.locator('.media-card')).toHaveCSS('width','322px');
   expect(player.height).toBeLessThanOrEqual(215);
   expect(player.x).toBeGreaterThan(clipboard.x+clipboard.width);
   expect(transport.y).toBeGreaterThan(info.y+info.height);
   expect(apps.y).toBeGreaterThan(player.y+player.height);
   expect(await page.locator('.home-app').nth(2).evaluate((el)=>el.offsetTop)).toBe(await page.locator('.home-app').first().evaluate((el)=>el.offsetTop));
   await page.screenshot({path:'artifacts/yuta-home-layout.png'});
  }
  if(name==='Stats'){
   await expect(page.locator('.process')).toHaveCount(4);
   await expect(page.locator('.gauge-group').first().locator('p')).toHaveAttribute('title','Snapdragon(R) X Elite - X1E80100 - Qualcomm(R) Oryon(TM) CPU');
  }
  if(name==='Wallpaper')await expect(page.locator('.wallpaper.selected')).toHaveCount(1);
  const layout=await page.evaluate(()=>{
   const panel=document.querySelector('.panel'),tabs=document.querySelector('.tabs'),root=document.querySelector('#yuta');
   return {pageWidth:document.documentElement.scrollWidth,pageHeight:document.documentElement.scrollHeight,panelWidth:panel.clientWidth,panelScrollWidth:panel.scrollWidth,panelHeight:panel.clientHeight,panelScrollHeight:panel.scrollHeight,panelBottom:panel.getBoundingClientRect().bottom,tabsTop:tabs.getBoundingClientRect().top,rootWidth:root.getBoundingClientRect().width,rootHeight:root.getBoundingClientRect().height};
  });
  expect(layout.rootWidth,`${name} menu width`).toBe(760);
  expect(layout.rootHeight,`${name} menu height`).toBe(366);
  expect(layout.pageWidth,`${name} page horizontal overflow`).toBeLessThanOrEqual(760);
  expect(layout.panelScrollWidth,`${name} panel horizontal overflow`).toBeLessThanOrEqual(layout.panelWidth+1);
  if(['Home','Today','Stats','Wallpaper'].includes(name))expect.soft(layout.panelScrollHeight,`${name} primary panel vertical overflow`).toBeLessThanOrEqual(layout.panelHeight+1);
  expect(layout.pageHeight,`${name} page vertical overflow`).toBeLessThanOrEqual(height);
  expect(layout.panelBottom,`${name} panel reachable`).toBeLessThanOrEqual(height+1);
  expect(layout.tabsTop,`${name} tabs at the top`).toBeLessThanOrEqual(12);
 }
 await page.evaluate(()=>window.__nativeFixture.emit('navigate','Today'));
 await page.setViewportSize({width:760,height:366});
 await page.getByLabel('New to-do',{exact:true}).fill('A compact task');
 await page.getByRole('button',{name:'Add task',exact:true}).click();
 await expect(page.getByText('A compact task',{exact:true})).toBeVisible();
 await page.screenshot({path:'artifacts/yuta-restored-today.png'});
 expect(await page.evaluate(()=>window.__yutaErrors)).toEqual([]);
});
