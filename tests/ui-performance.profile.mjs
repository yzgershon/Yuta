import {chromium} from '@playwright/test';
import http from 'node:http';
import {readFile,mkdir,writeFile} from 'node:fs/promises';
import path from 'node:path';

// A repeatable local browser diagnostic, not a claim about native desktop FPS.
const root=path.resolve('ui');
const types={'.js':'text/javascript','.css':'text/css','.html':'text/html','.svg':'image/svg+xml','.png':'image/png'};
const server=http.createServer(async(req,res)=>{
 try{const file=path.resolve(root,'.'+new URL(req.url,'http://localhost').pathname.replace(/\/$/,'/index.html'));
  if(!file.startsWith(root+path.sep)){res.writeHead(403).end();return;}
  const bytes=await readFile(file);res.writeHead(200,{'Content-Type':types[path.extname(file)]||'application/octet-stream'});res.end(bytes);
 }catch{res.writeHead(404).end();}
});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
let browser;
try{
 browser=await chromium.launch({channel:'msedge',headless:true});
 const page=await browser.newPage({viewport:{width:1280,height:960}});
 await page.goto(`http://127.0.0.1:${server.address().port}`);
 await page.getByRole('tab',{name:'Today',exact:true}).waitFor();
 const result=await page.evaluate(async()=>{
  const pause=ms=>new Promise(resolve=>setTimeout(resolve,ms));
  const root=document.querySelector('#yuta');
  const summary={environment:'Headless Edge, preview fixture, 1280x960',panelMounts:0,headerMounts:0,wallpaperMounts:0,longTasks:[],frameGaps:[]};
  const mutations=new MutationObserver(records=>{for(const r of records)for(const n of r.addedNodes){if(n.nodeType!==1)continue;for(const [selector,key] of [['.panel','panelMounts'],['.topbar','headerMounts'],['.wallpaper','wallpaperMounts']])summary[key]+=(n.matches(selector)?1:0)+n.querySelectorAll(selector).length;}});
  mutations.observe(root,{childList:true,subtree:true});
  const longTasks=new PerformanceObserver(list=>summary.longTasks.push(...list.getEntries().map(x=>Math.round(x.duration*10)/10)));
  longTasks.observe({type:'longtask',buffered:false});
  let sampling=true,last=performance.now();
  function frame(now){summary.frameGaps.push(now-last);last=now;if(sampling)requestAnimationFrame(frame);}
  requestAnimationFrame(frame);
  const start=performance.now();
  for(let i=0;i<3;i++)for(const name of ['Controls','Today','Stats','Clipboard','Apps','Wallpaper','Power']){
   document.querySelector(`[role=tab][data-tab="${name}"]`).click();await pause(50);
  }
  document.querySelector('[role=tab][data-tab="Wallpaper"]').click();await pause(80);
  const initialWallNodes=[...document.querySelectorAll('.wallpaper')];
  for(let i=0;i<24;i++){document.querySelector('[data-action=wallNext]').click();await pause(35);}
  summary.wallpaperNodesRetained=initialWallNodes.filter(node=>node.isConnected).length;
  summary.wallpaperNodesInitially=initialWallNodes.length;
  for(let i=0;i<5;i++){
   document.querySelector('[data-action=collapse]').click();await pause(25);
   document.querySelector('[data-action=expand]').click();await pause(35);
  }
  await pause(100);sampling=false;mutations.disconnect();longTasks.disconnect();
  summary.durationMs=Math.round(performance.now()-start);
  const gaps=summary.frameGaps.sort((a,b)=>a-b);
  summary.frames=gaps.length;summary.frameGapP95Ms=Math.round(gaps[Math.floor(gaps.length*.95)]*10)/10;
  summary.frameGapMaxMs=Math.round(Math.max(...gaps)*10)/10;delete summary.frameGaps;
  summary.errors=window.__yutaErrors;return summary;
 });
 const label=(process.argv[2]||'current').replace(/[^a-zA-Z0-9_-]/g,'');
 await mkdir('artifacts/performance',{recursive:true});
 await writeFile(`artifacts/performance/browser-${label}.json`,JSON.stringify(result,null,2)+'\n');
 console.log(JSON.stringify(result,null,2));
}finally{await browser?.close();await new Promise(resolve=>server.close(resolve));}
