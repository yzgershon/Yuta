import {test,expect} from '@playwright/test';
test('legacy preview data survives the approved accent update',async({page})=>{
 const legacy={notes:'A note from the original app.',todos:[{id:'legacy-task',text:'Keep my task',done:false}],routines:[{id:'legacy-routine',name:'My routine',targets:['https://github.com']}],settings:{shape:'pill',accent:'rose',monitor:0,clipboardEnabled:false,city:'',fahrenheit:false,openOnHover:false}};
 await page.addInitScript(value=>localStorage.setItem('perch-preview',JSON.stringify(value)),legacy);
 await page.goto('/');await page.getByRole('tab',{name:'Today',exact:true}).click();await expect(page.locator('#yuta')).toHaveClass(/expanded/);await expect(page.locator('#yuta')).not.toHaveClass(/notch/);
 await page.getByRole('button',{name:'Settings',exact:true}).click();await expect(page.getByRole('button',{name:'Violet',exact:true})).toHaveAttribute('aria-pressed','true');await expect(page.getByRole('switch').first()).toHaveAttribute('aria-checked','false');
 const migrated=await page.evaluate(()=>JSON.parse(localStorage.getItem('yuta-preview')));expect(migrated.notes).toBe(legacy.notes);expect(migrated.todos).toEqual(legacy.todos);expect(migrated.routines).toEqual(legacy.routines);expect(migrated.settings).toEqual({...legacy.settings,accent:'custom',customAccent:'#9b80ff',hotkey:'Alt+Space'});
 await expect(page.getByRole('button',{name:'Quick note',exact:true})).toHaveCount(0);
 await page.reload();expect(await page.evaluate(()=>JSON.parse(localStorage.getItem('yuta-preview')).notes)).toBe(legacy.notes);
 expect(await page.evaluate(()=>window.__yutaErrors)).toEqual([]);
});
test('daily tasks persist, calendar edits persist, timer survives collapsing',async({page})=>{
 await page.goto('/');await page.getByRole('tab',{name:'Today',exact:true}).click();await page.getByLabel('New to-do').fill('Finish the first Yuta build');await page.getByRole('button',{name:'Add task',exact:true}).click();await expect(page.getByText('Finish the first Yuta build',{exact:true})).toBeVisible();
 await page.reload();await page.getByRole('tab',{name:'Today',exact:true}).click();await expect(page.getByText('Finish the first Yuta build',{exact:true})).toBeVisible();await page.getByRole('button',{name:'Complete Finish the first Yuta build'}).click();await expect(page.locator('.counter')).toHaveText('1/1');
 await page.getByRole('button',{name:'Add event',exact:true}).click();await page.getByLabel('Event',{exact:true}).fill('Review Yuta');await page.getByRole('button',{name:'Save event'}).click();await expect(page.getByText('Review Yuta',{exact:true})).toBeVisible();
 await page.getByRole('button',{name:'1m',exact:true}).click();await page.getByRole('button',{name:'Start',exact:true}).click();await page.getByRole('button',{name:'Collapse menu'}).click();await expect(page.getByRole('button',{name:'Open Yuta'})).toBeVisible();await page.waitForTimeout(1200);await page.getByRole('button',{name:'Open Yuta'}).click();await expect(page.getByRole('button',{name:'Pause',exact:true})).toBeVisible();expect(await page.locator('#timer-value').innerText()).not.toBe('01:00');
});
test('routines validate destinations and persist between sections and reload',async({page})=>{
 await page.goto('/');await page.getByRole('tab',{name:'Today',exact:true}).click();await page.getByRole('button',{name:'Routines',exact:true}).click();await page.getByRole('button',{name:'New routine',exact:true}).click();await page.getByLabel('Name',{exact:true}).fill('Start coding');await page.getByLabel('Websites, apps, or folders').fill('powershell -Command echo');await page.getByRole('button',{name:'Create routine',exact:true}).click();await expect(page.locator('#form-error')).toContainText('full Windows path');await page.getByLabel('Websites, apps, or folders').fill('https://github.com\nC:\\Dev');await page.getByRole('button',{name:'Create routine',exact:true}).click();await expect(page.locator('.routine-card h3')).toHaveText('Start coding');
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
 const errors=[];page.on('pageerror',e=>errors.push(e.message));await page.goto('/');await page.getByRole('tab',{name:'Today',exact:true}).click();
 for(const name of ['Home','Stats','Wallpaper','Today']){await page.getByRole('tab',{name,exact:true}).click();await expect(page.getByRole('tabpanel',{name})).toBeVisible();}
 await page.getByLabel('New to-do').fill('<img src=x onerror=alert(1)>');await page.getByRole('button',{name:'Add task',exact:true}).click();expect(await page.locator('.task img').count()).toBe(0);
 await page.getByRole('tab',{name:'Today',exact:true}).focus();await page.keyboard.press('ArrowRight');await expect(page.getByRole('tab',{name:'Stats',exact:true})).toHaveAttribute('aria-selected','true');
 await page.getByRole('tab',{name:'Today',exact:true}).click();await page.screenshot({path:'artifacts/preview-desktop.png',fullPage:true,animations:'disabled'});
 await page.setViewportSize({width:375,height:812});await page.screenshot({path:'artifacts/preview-narrow.png',fullPage:true});expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
 await page.emulateMedia({reducedMotion:'reduce'});await page.mouse.move(0,800);await expect(page.getByRole('button',{name:'Open Yuta',exact:true})).toBeVisible();await page.getByRole('button',{name:'Open Yuta',exact:true}).click();await page.getByRole('tab',{name:'Wallpaper',exact:true}).click();await page.getByRole('button',{name:'Next wallpaper'}).click();await expect(page.locator('.wallpaper.selected')).toHaveCount(1);expect(errors).toEqual([]);
});
