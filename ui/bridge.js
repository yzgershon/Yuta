import {uid} from './core.js';
import {migrateHotkey,normalizeHotkey,shortcutLabel} from './hotkeys.js';
export const native = Boolean(window.chrome?.webview);
const pending = new Map();
const listeners = new Map();
export function on(name,handler){if(!listeners.has(name))listeners.set(name,[]);listeners.get(name).push(handler);}
if(native)window.chrome.webview.addEventListener('message',({data})=>{if(data.event){for(const fn of listeners.get(data.event)||[])fn(data.data);return;}const p=pending.get(data.id);if(!p)return;clearTimeout(p.timeout);pending.delete(data.id);data.error?p.reject(new Error(data.error)):p.resolve(data.result);});
let demo;
const previewStorageKey='yuta-preview';
// Keep the old key readable so renaming the app does not strand saved work.
const legacyPreviewStorageKey='perch-preview';
function readPreviewState(key){
  try{const value=JSON.parse(localStorage.getItem(key)||'null');return value&&typeof value==='object'&&!Array.isArray(value)?value:null;}catch{return null;}
}
function demoState(){
  const defaults={appearanceVersion:2,todos:[],events:[],routines:[],snippets:[],favorites:[],recent:[],files:[],notes:'',settings:{shape:'notch',accent:'custom',customAccent:'#9b80ff',monitor:0,clipboardEnabled:false,city:'',fahrenheit:true,openOnHover:true},timer:{remaining:1500,duration:1500,running:false}};
  const current=readPreviewState(previewStorageKey),legacy=current?null:readPreviewState(legacyPreviewStorageKey),saved=current||legacy||{};
  const settings={...defaults.settings,...saved.settings},next={...defaults,...saved,appearanceVersion:2,settings};
  if(saved.workbenchDesignVersion!==1){settings.accent='custom';settings.customAccent='#9b80ff';next.workbenchDesignVersion=1;}
  migrateHotkey(next);
  if(saved.workbenchDesignVersion!==1||!current&&legacy||saved.hotkeyVersion!==1||saved.settings?.hotkey!==next.settings.hotkey){try{localStorage.setItem(previewStorageKey,JSON.stringify(next));}catch{/* Keep the loaded data usable if browser storage is unavailable. */}}
  return next;
}
const demoApps=[['Visual Studio Code','code','Application'],['Google Chrome','chrome','Application'],['Spotify','spotify','Application'],['File Explorer','explorer','Application'],['Windows Terminal','terminal','Application'],['Settings','settings','Application']].map(([name,target,kind])=>({name,target,kind,id:target}));
let demoClips=[{id:'sample-text',type:'text',text:'A little less switching. A little more doing.',label:'A little less switching. A little more doing.',copied:new Date().toISOString(),pinned:true},{id:'sample-link',type:'link',text:'https://github.com',label:'https://github.com',copied:new Date(Date.now()-120000).toISOString(),pinned:false}];
const demoWalls=['Dune','Alpine','Afterglow','Shore','Midnight'].map((name,i)=>({id:name,name,path:name,image:null,art:i}));
const demoConnections={wifi:{available:true,enabled:true,label:'Wi-Fi on',detail:'Demo network'},bluetooth:{available:true,enabled:true,label:'Bluetooth on',detail:'Demo Bluetooth adapter'},audio:{available:true,enabled:true,label:'Audio on',detail:'Demo speakers'}};
export async function call(method,args={}){
  if(native)return new Promise((resolve,reject)=>{const id=uid();const timeout=setTimeout(()=>{pending.delete(id);reject(new Error('This action took too long. Try again.'));},60000);pending.set(id,{resolve,reject,timeout});window.chrome.webview.postMessage({id,method,args});});
  demo ||= demoState();
  switch(method){
    case 'bootstrap':return {state:structuredClone(demo),apps:demoApps,clips:demoClips,monitors:[{id:0,name:'Display 1 · primary'}],hotkey:true};
    case 'darchState':return {status:'unlinked'};
    case 'darchConnect':case 'darchFrame':case 'darchOpen':case 'darchDisconnect':throw new Error('Connect Darch from the Windows app after the Darch API is deployed.');
    case 'save':demo[args.key]=structuredClone(args.value);localStorage.setItem(previewStorageKey,JSON.stringify(demo));return true;
    case 'hotkeyRecord':return true;
    case 'hotkeySet':{const shortcut=normalizeHotkey(args.shortcut);const next=structuredClone(demo);next.settings.hotkey=shortcut;next.hotkeyVersion=1;localStorage.setItem(previewStorageKey,JSON.stringify(next));demo=next;return {shortcut,label:shortcutLabel(shortcut),registered:true};}
    case 'expand':for(const f of listeners.get('expanded')||[])f(args.value?{value:true,view:args.full?'full':args.view||'full'}:false);return true;
    case 'resize':case 'dialogState':return true;
    case 'controls':return {volume:64,muted:false,brightness:78,output:'Speakers · demo',battery:84,charging:false};
    case 'connections':return structuredClone(demoConnections);
    case 'connectionSet':{const c=demoConnections[args.kind];if(!c)throw new Error('Unknown connection');c.enabled=args.enabled;c.label=args.kind==='audio'?(c.enabled?'Audio on':'Muted'):(args.kind==='wifi'?'Wi-Fi':'Bluetooth')+(c.enabled?' on':' off');return structuredClone(demoConnections);}
    case 'appIcons':return {};
    case 'media':return {available:false};
    case 'stats':return {cpu:12,gpu:8,ram:42,cpuName:'Demo processor',gpuName:'Demo graphics',totalRam:16*1024**3,usedRam:6.7*1024**3,freeRam:9.3*1024**3,diskTotal:512*1024**3,diskFree:284*1024**3,download:120000,upload:45000,uptime:14520,processes:[{name:'Code',memory:1.2*1024**3,cpu:4},{name:'Chrome',memory:820*1024**2,cpu:2},{name:'Spotify',memory:280*1024**2,cpu:.4},{name:'Yuta',memory:120*1024**2,cpu:.1}]};
    case 'apps':return demoApps;
    case 'clipboard':return demoClips;
    case 'clipPin':demoClips=demoClips.map(x=>x.id===args.id?{...x,pinned:!x.pinned}:x);return demoClips;
    case 'clipRemove':demoClips=demoClips.filter(x=>!args.ids.includes(x.id));return demoClips;
    case 'copyText':case 'clipCopy':case 'clipPaste':throw new Error('Copy and paste are available in the Windows app.');
    case 'wallpapers':case 'wallpaperFolder':return demoWalls;
    case 'wallpaperApply':return true;
    case 'routine':return {opened:demo.routines.find(x=>x.id===args.id)?.targets.length||0,errors:[]};
    case 'pick':throw new Error('File and folder pickers are available in the Windows app.');
    case 'weather':throw new Error('Live weather is available in the Windows app after you set your city.');
    case 'speed':throw new Error('The live speed test is available in the Windows app.');
    case 'launch':case 'settings':case 'power':case 'mediaAction':case 'volume':case 'brightness':case 'mute':case 'quit':throw new Error('This is an interface preview. This action works in the Windows app.');
    default:throw new Error('Unknown preview action.');
  }
}
