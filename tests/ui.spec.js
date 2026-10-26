import {test,expect} from '@playwright/test';
test('legacy preview data survives the approved accent update',async({page})=>{
 const legacy={notes:'A note from the original app.',todos:[{id:'legacy-task',text:'Keep my task',done:false}],routines:[{id:'legacy-routine',name:'My routine',targets:['https://github.com']}],settings:{shape:'pill',accent:'rose',monitor:0,clipboardEnabled:false,city:'',fahrenheit:false,openOnHover:false}};
 await page.addInitScript(value=>localStorage.setItem('perch-preview',JSON.stringify(value)),legacy);
 await page.goto('/');await page.getByRole('tab',{name:'Darch',exact:true}).click();await expect(page.locator('#yuta')).toHaveClass(/expanded/);await expect(page.locator('#yuta')).not.toHaveClass(/notch/);
 await page.getByRole('button',{name:'Settings',exact:true}).click();await expect(page.getByRole('button',{name:'Violet',exact:true})).toHaveAttribute('aria-pressed','true');await expect(page.getByRole('switch',{name:'Open with deliberate hover'})).toHaveAttribute('aria-checked','false');
 const migrated=await page.evaluate(()=>JSON.parse(localStorage.getItem('yuta-preview')));expect(migrated.notes).toBe(legacy.notes);expect(migrated.todos).toEqual(legacy.todos);expect(migrated.routines).toEqual(legacy.routines);expect(migrated.settings).toEqual({...legacy.settings,accent:'custom',customAccent:'#9b80ff',hotkey:'Alt+Space'});
 await expect(page.getByRole('button',{name:'Quick note',exact:true})).toHaveCount(0);
 await page.reload();expect(await page.evaluate(()=>JSON.parse(localStorage.getItem('yuta-preview')).notes)).toBe(legacy.notes);
 expect(await page.evaluate(()=>window.__yutaErrors)).toEqual([]);
});
test('Darch section choices persist and the preview explains real linking',async({page})=>{
 await page.goto('/');await page.getByRole('tab',{name:'Darch',exact:true}).click();
 await page.getByRole('button',{name:'Connect Darch',exact:true}).click();
 await expect(page.locator('#toast')).toContainText('Windows app');
 await page.getByRole('button',{name:'Settings',exact:true}).click();
 await page.getByRole('switch',{name:'Show Calendar in Darch',exact:true}).click();
 await page.getByRole('button',{name:'Move Focus up',exact:true}).click();
 await page.reload();await page.getByRole('tab',{name:'Darch',exact:true}).click();
 await expect(page.getByRole('navigation',{name:'Darch sections'}).getByRole('button',{name:'Calendar',exact:true})).toHaveCount(0);
 await page.getByRole('button',{name:'Settings',exact:true}).click();
 await expect(page.getByRole('switch',{name:'Show Calendar in Darch',exact:true})).toHaveAttribute('aria-checked','false');
});

test('routines validate destinations and persist between sections and reload',async({page})=>{
 await page.goto('/');await page.getByRole('tab',{name:'Darch',exact:true}).click();await page.getByRole('button',{name:'Routines',exact:true}).click();await page.getByRole('button',{name:'New routine',exact:true}).click();await page.getByLabel('Name',{exact:true}).fill('Start coding');await page.getByLabel('Websites, apps, or folders').fill('powershell -Command echo');await page.getByRole('button',{name:'Create routine',exact:true}).click();await expect(page.locator('#form-error')).toContainText('full Windows path');await page.getByLabel('Websites, apps, or folders').fill('https://github.com\nC:\\Dev');await page.getByRole('button',{name:'Create routine',exact:true}).click();await expect(page.locator('.routine-card h3')).toHaveText('Start coding');
 await page.reload();await page.getByRole('button',{name:'Routines',exact:true}).click();await expect(page.locator('.routine-card h3')).toHaveText('Start coding');
});
test('Home clipboard hover removal keeps the single history view without search',async({page})=>{
 await page.goto('/');await expect(page.getByRole('button',{name:'History',exact:true})).toHaveCount(0);
 await expect(page.getByRole('searchbox',{name:'Search clipboard'})).toHaveCount(0);
 const count=await page.locator('.home-clip').count();expect(count).toBeGreaterThan(0);
 const row=page.locator('.home-clip').first();await row.hover();
 await row.locator('[data-action=homeDelete]').click();await expect(page.locator('.home-clip')).toHaveCount(count-1);
 await expect(page.getByRole('tabpanel',{name:'Home',exact:true})).toBeVisible();
});

test('all sections, keyboard navigation, escaping and responsive layouts',async({page})=>{
 const errors=[];page.on('pageerror',e=>errors.push(e.message));await page.goto('/');await page.getByRole('tab',{name:'Darch',exact:true}).click();
 for(const name of ['Home','Stats','Wallpaper','Today']){await page.getByRole('tab',{name:name==='Today'?'Darch':name,exact:true}).click();await expect(page.getByRole('tabpanel',{name})).toBeVisible();}
 await expect(page.getByRole('button',{name:'Connect Darch',exact:true})).toBeVisible();
 await page.getByRole('tab',{name:'Darch',exact:true}).focus();await page.keyboard.press('ArrowRight');await expect(page.getByRole('tab',{name:'Stats',exact:true})).toHaveAttribute('aria-selected','true');
 await page.getByRole('tab',{name:'Darch',exact:true}).click();await page.screenshot({path:'artifacts/preview-desktop.png',fullPage:true,animations:'disabled'});
 await page.setViewportSize({width:375,height:812});await page.screenshot({path:'artifacts/preview-narrow.png',fullPage:true});expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
 await page.emulateMedia({reducedMotion:'reduce'});await page.mouse.move(0,800);await expect(page.getByRole('button',{name:'Open Yuta',exact:true})).toBeVisible();await page.getByRole('button',{name:'Open Yuta',exact:true}).click();await page.getByRole('tab',{name:'Wallpaper',exact:true}).click();await page.getByRole('button',{name:'Next wallpaper'}).click();await expect(page.locator('.wallpaper.selected')).toHaveCount(1);expect(errors).toEqual([]);
});
