import {test,expect} from '@playwright/test';
import {useNativeBridge} from './native-bridge.fixture.js';
const snapshot={account:{displayName:'Sample Student'},tabs:['today','assignments','calendar','focus','timelog','grades','games','settings'].map(id=>({id,label:{today:'Today',assignments:'Assignments',calendar:'Calendar',focus:'Focus',timelog:'Time log',grades:'Grades',games:'Brain games',settings:'Settings'}[id]}))};
async function connected(page){
 let requests=0;
 await page.route('https://darch-sepia.vercel.app/**',async route=>{
  requests++;
  await route.fulfill({contentType:'text/html',body:`<!doctype html><html><body><nav aria-label="Darch sections">${snapshot.tabs.map(t=>`<button data-section="${t.id}">${t.label}</button>`).join('')}</nav><h1 id="section">today</h1><label>Draft<input id="draft"></label><button id="modal">Open edit dialog</button><script>
  parent.postMessage({source:'darch-yuta',type:'ready'},'http://127.0.0.1:4179');
  addEventListener('message',e=>{if(e.origin==='http://127.0.0.1:4179'&&e.data.type==='section'){
   for(const link of document.querySelectorAll('[data-section]'))link.hidden=!e.data.tabs.includes(link.dataset.section);
   document.querySelector('#section').textContent=e.data.tabs.includes(e.data.section)?e.data.section:e.data.tabs[0];
  }});
  document.querySelector('nav').onclick=e=>{const section=e.target.dataset.section;if(section){document.querySelector('#section').textContent=section;parent.postMessage({source:'darch-yuta',type:'section',section},'http://127.0.0.1:4179');}};
  document.querySelector('#modal').onclick=()=>parent.postMessage({source:'darch-yuta',type:'dialog',open:true},'http://127.0.0.1:4179');
  </script></body></html>`});
 });
 await useNativeBridge(page,{darch:{status:'connected',snapshot},startSection:'Today'});
 await expect(page.frameLocator('#darch-frame').getByRole('heading')).toHaveText('today');
 return ()=>requests;
}
test('Darch keeps the live workspace and drafts mounted across Yuta tabs and server updates',async({page})=>{
 await page.setViewportSize({width:760,height:366});const count=await connected(page);
 const frame=page.frameLocator('#darch-frame');await frame.getByLabel('Draft').fill('Unsaved assignment draft');
 await page.getByRole('tab',{name:'Home',exact:true}).click();
 await page.getByRole('tab',{name:'Darch',exact:true}).click();
 await expect(frame.getByLabel('Draft')).toHaveValue('Unsaved assignment draft');expect(count()).toBe(1);
 await page.evaluate(snapshot=>window.__nativeFixture.emit('darch',{status:'connected',snapshot}),snapshot);
 await expect(frame.getByLabel('Draft')).toHaveValue('Unsaved assignment draft');expect(count()).toBe(1);
 await expect(page.getByRole('navigation',{name:'Darch sections'})).toHaveCount(0);
 await frame.getByRole('navigation',{name:'Darch sections'}).getByRole('button',{name:'Focus',exact:true}).click();
 await expect(frame.getByRole('heading')).toHaveText('focus');
 await frame.getByRole('button',{name:'Open edit dialog'}).click();expect(await page.evaluate(()=>window.__nativeFixture.dialogOpen)).toBe(true);
 const dimensions=await page.locator('#darch-frame').boundingBox();expect(dimensions.width).toBeGreaterThanOrEqual(700);expect(dimensions.x+dimensions.width).toBeLessThanOrEqual(760);expect(dimensions.y+dimensions.height).toBeLessThanOrEqual(366);
});
test('hidden sections do not grant SAT or admin access and disconnect clears the frame immediately',async({page})=>{
 await connected(page);const nav=page.frameLocator('#darch-frame').getByRole('navigation',{name:'Darch sections'});
 await expect(nav.getByRole('button',{name:'SAT practice'})).toHaveCount(0);await expect(nav.getByRole('button',{name:'Admin',exact:true})).toHaveCount(0);
 await page.getByRole('button',{name:'Settings',exact:true}).click();
 await page.getByRole('switch',{name:'Show Grades in Darch'}).click();
 await page.getByRole('tab',{name:'Darch',exact:true}).click();await expect(nav.getByRole('button',{name:'Grades',exact:true})).toHaveCount(0);
 await page.getByRole('button',{name:'Settings',exact:true}).click();await page.getByRole('button',{name:'Disconnect',exact:true}).click();
 await expect(page.locator('#darch-frame')).toHaveCount(0);
 await page.getByRole('tab',{name:'Darch',exact:true}).click();await expect(page.getByRole('button',{name:'Connect Darch',exact:true})).toBeVisible();
});
test('messages from the parent or an unrelated frame cannot invoke native actions',async({page})=>{
 await connected(page);
 const before=await page.evaluate(()=>window.__nativeFixture.calls.length);
 await page.evaluate(()=>window.postMessage({source:'darch-yuta',type:'open'},location.origin));
 await page.waitForTimeout(50);
 expect(await page.evaluate(before=>window.__nativeFixture.calls.slice(before).some(x=>x.method==='darchOpen'),before)).toBe(false);
});
