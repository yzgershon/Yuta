import {test,expect} from '@playwright/test';
import {useNativeBridge} from './native-bridge.fixture.js';

test('populated views keep their frame and scroll long content without hiding actions',async({page})=>{
 await page.setViewportSize({width:760,height:366});
 const now=new Date(),date=[now.getFullYear(),String(now.getMonth()+1).padStart(2,'0'),String(now.getDate()).padStart(2,'0')].join('-');
 await useNativeBridge(page,{startSection:'Today',initialState:{
  todos:Array.from({length:30},(_,i)=>({id:'task-'+i,text:`Review ${i+1}: a longer task with details that should wrap inside the list`,done:false})),
  events:Array.from({length:12},(_,i)=>({id:'event-'+i,date,time:`${String(i+8).padStart(2,'0')}:00`,title:`Event ${i+1}: a longer appointment title with useful details`})),
  routines:Array.from({length:12},(_,i)=>({id:'routine-'+i,name:`Routine ${i+1}: a saved workspace with a longer descriptive name`,targets:['https://example.com']}))
 }});
 await expect(page.locator('#darch-frame')).toHaveCount(0);
 expect((await page.locator('.darch-stage').boundingBox()).width).toBeGreaterThanOrEqual(700);
 const connect=await page.getByRole('button',{name:'Connect Darch',exact:true}).boundingBox();
 expect(connect.y).toBeGreaterThan(56);expect(connect.y+connect.height).toBeLessThanOrEqual(366);
 expect(await page.locator('#panel').evaluate(el=>el.scrollTop)).toBe(0);
 await page.screenshot({path:'artifacts/equal-panels/darch-unlinked.png'});

 await page.getByRole('button',{name:'Routines',exact:true}).click();
 await expect(page.locator('.routine-card')).toHaveCount(12);
 await page.locator('.routine-card').last().scrollIntoViewIfNeeded();
 expect(await page.locator('.routine-list').evaluate(el=>el.scrollTop)).toBeGreaterThan(0);
 for(const label of ['New routine','Schoolwork']){
  const b=await page.getByRole('button',{name:label,exact:true}).boundingBox();
  expect(b.y).toBeGreaterThan(56);expect(b.y+b.height).toBeLessThanOrEqual(366);
 }
 await page.getByRole('button',{name:'Settings',exact:true}).click();
 await page.getByRole('button',{name:'°C',exact:true}).scrollIntoViewIfNeeded();
 expect(await page.locator('.settings-list').evaluate(el=>el.scrollTop)).toBeGreaterThan(0);
 const temperature=await page.getByRole('button',{name:'°C',exact:true}).boundingBox();
 expect(temperature.y+temperature.height).toBeLessThanOrEqual(366);
 const result=await page.evaluate(()=>({width:document.querySelector('#yuta').offsetWidth,height:document.querySelector('#yuta').offsetHeight,panelOverflow:document.querySelector('#panel').scrollWidth>document.querySelector('#panel').clientWidth,resizeCalls:window.__nativeFixture.calls.filter(c=>c.method==='resize').map(c=>c.args)}));
 expect(result.width).toBe(760);expect(result.height).toBe(366);expect(result.panelOverflow).toBe(false);
 expect(result.resizeCalls.every(c=>c.width===760&&c.height===366)).toBe(true);
 expect(await page.evaluate(()=>window.__yutaErrors)).toEqual([]);
});
