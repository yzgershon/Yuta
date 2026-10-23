// Render the real UI with synthetic data. This never reads a Windows profile.
import {chromium} from '@playwright/test';
import http from 'node:http';
import {readFile,mkdir} from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const ui=path.join(root,'ui'),out=path.join(root,'docs/images');
const types={'.js':'text/javascript','.css':'text/css','.html':'text/html','.svg':'image/svg+xml'};
const server=http.createServer(async(req,res)=>{
 try{const url=new URL(req.url,'http://localhost'),file=path.resolve(ui,'.'+decodeURIComponent(url.pathname==='/'?'/index.html':url.pathname));if(!file.startsWith(ui+path.sep)){res.writeHead(403).end();return;}const data=await readFile(file);res.writeHead(200,{'Content-Type':types[path.extname(file)]||'application/octet-stream'});res.end(data);}catch{res.writeHead(404).end();}
});
await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
await mkdir(out,{recursive:true});
const browser=await chromium.launch({channel:'msedge'});
try{
 const context=await browser.newContext({viewport:{width:900,height:452},deviceScaleFactor:2,reducedMotion:'reduce'});
 const page=await context.newPage();
 await page.clock.install({time:new Date('2026-10-22T09:41:00')});
 // Original, procedural sample artwork; no commercial album covers or personal files.
 const cover=await page.evaluate(()=>{const c=document.createElement('canvas');c.width=c.height=256;const g=c.getContext('2d');const sky=g.createLinearGradient(0,0,0,256);sky.addColorStop(0,'#352a5b');sky.addColorStop(.65,'#c17e9c');sky.addColorStop(1,'#edbd94');g.fillStyle=sky;g.fillRect(0,0,256,256);g.fillStyle='#f5ceb0';g.beginPath();g.arc(165,85,43,0,Math.PI*2);g.fill();for(const [y,color]of[[165,'#55466c'],[195,'#282b45'],[230,'#171c31']]){g.fillStyle=color;g.beginPath();g.moveTo(0,y);g.bezierCurveTo(75,y-60,155,y+48,256,y-20);g.lineTo(256,256);g.lineTo(0,256);g.fill();}return c.toDataURL();});
 await page.addInitScript(({cover})=>{
  const now=new Date(),date=[now.getFullYear(),String(now.getMonth()+1).padStart(2,'0'),String(now.getDate()).padStart(2,'0')].join('-');
  const apps=[['Google Chrome','chrome'],['Spotify','spotify'],['Files','files'],['VS Code','code'],['Terminal','terminal'],['Settings','settings']].map(([name,id])=>({id,name,target:id,kind:'Application'}));
  const clips=[{id:'ideas',type:'text',label:'Ideas for the week',text:'Keep the essentials close. Finish the first draft. Make room for a walk.',pinned:true,copied:now.toISOString()},{id:'workspace',type:'link',label:'example.com / Workspace',text:'https://example.com/workspace',pinned:false,copied:now.toISOString()},{id:'notes',type:'text',label:'Project notes',text:'A few useful things, always within reach.',pinned:false,copied:now.toISOString()}];
  const state={todos:[{id:'1',text:'Finish the next chapter',done:false},{id:'2',text:'Review project notes',done:false},{id:'3',text:'Make time for a walk',done:true}],events:[{id:'e',title:'Project review',date,time:'14:30'}],routines:[],snippets:[],favorites:['chrome','spotify','files','code','terminal','settings'],recent:[],files:[],notes:'Ideas for the week\n\nKeep the essentials close.',settings:{hotkey:'Alt+Space',shape:'notch',monitor:0,clipboardEnabled:true,city:'London',fahrenheit:false,openOnHover:true,smartFlatten:true,accent:'custom',customAccent:'#9b80ff'},timer:{remaining:1500,duration:1500,running:false}};
  const media={available:true,title:'Midnight Drive',artist:'Studio North',album:'Sample artwork',source:'Spotify.exe',sourceName:'Spotify',artwork:cover,playing:true,position:49,duration:227,updatedAt:Date.now(),playbackRate:0,canPlay:true,canPause:true,canToggle:true,canNext:true,canPrevious:true,canSeek:true,revision:1};
  const handlers=[],emit=(event,data)=>handlers.forEach(fn=>fn({data:{event,data}}));
  window.__docs={emit};window.chrome||={};
  window.chrome.webview={addEventListener(type,fn){if(type==='message')handlers.push(fn);},postMessage({id,method,args}){
   let result;
   switch(method){
    case 'bootstrap':result={state,apps,clips,monitors:[{id:0,name:'Main display'}],hotkey:true};break;
    case 'media':result=media;break;
    case 'clipboard':result=clips;break;
    case 'apps':result=apps;break;
    case 'weather':result={temperature:18,code:2,unit:'°C',city:'London'};break;
    case 'resize':case 'dialogState':result=true;break;
    case 'expand':emit('expanded',args.value);result=true;break;
    default:throw new Error('Unexpected documentation fixture call: '+method);
   }
   queueMicrotask(()=>handlers.forEach(fn=>fn({data:{id,result:structuredClone(result)}})));
  }};
 },{cover});
 await page.goto(`http://127.0.0.1:${server.address().port}`);
 await page.waitForSelector('[role=tab]');
 await page.evaluate(()=>window.__docs.emit('expanded',true));
 await page.getByText('Midnight Drive',{exact:true}).waitFor();
 await page.screenshot({path:path.join(out,'home.png'),animations:'disabled'});
 await page.setViewportSize({width:900,height:450});
 await page.getByRole('tab',{name:'Today',exact:true}).click();
 await page.locator('.weather strong').filter({hasText:'18'}).waitFor();
 await page.screenshot({path:path.join(out,'today.png'),animations:'disabled'});
 const errors=await page.evaluate(()=>window.__yutaErrors);
 if(errors.length)throw new Error(errors.join('\n'));
 console.log('Saved real UI captures with sample data: docs/images/home.png, today.png');
}finally{await browser.close();await new Promise(resolve=>server.close(resolve));}
