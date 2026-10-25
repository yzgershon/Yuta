// Browser-side protocol fixture. It never calls Windows APIs or accesses user data.
export async function useNativeBridge(page,{delays={},wallpaperCount=30,hotkey=true,startSection='Today',initialState={}}={}){
 await page.addInitScript(({delays,wallpaperCount,hotkey,initialState})=>{
  const state={todos:[],events:[],routines:[],snippets:[],favorites:[],recent:[],files:[],notes:'',settings:{hotkey:'Alt+Space',shape:'notch',monitor:0,clipboardEnabled:false,city:'',fahrenheit:true,openOnHover:true,accent:'custom',customAccent:'#9b80ff'},timer:{remaining:1500,duration:1500,running:false}};
  Object.assign(state,initialState);
  const apps=Array.from({length:200},(_,i)=>({id:`app-${i}`,name:i===0?'Visual Studio Code':`Application ${i}`,target:`C:\\Apps\\app-${i}.exe`,kind:'Application'}));
  const walls=Array.from({length:wallpaperCount},(_,i)=>({id:`wall-${i}`,name:`Wallpaper ${i+1}`,path:`C:\\Wallpapers\\wall-${i}.png`,image:`data:image/svg+xml,${encodeURIComponent(`<svg xmlns="http://www.w3.org/2000/svg" width="400" height="240"><rect width="400" height="240" fill="hsl(${i*31} 35% 45%)"/><circle cx="240" cy="110" r="60" fill="#cbd3af"/></svg>`)}`}));
  const handlers=[];
  const emit=(event,data)=>handlers.forEach(fn=>fn({data:{event,data}}));
  const resultFor=({method,args})=>{
   switch(method){
    case 'bootstrap':return {state,apps,clips:window.__nativeFixture.clips,monitors:[{id:0,name:'Test display'}],hotkey};
    case 'controls':return {volume:64,muted:false,brightness:78,output:'Fixture speakers',battery:84,charging:false};
    case 'connections':return window.__nativeFixture.connections;
    case 'connectionSet':{const f=window.__nativeFixture;if(f.connectionFailure)throw new Error('Windows could not change this connection.');const c=f.connections[args.kind];c.enabled=args.enabled;c.label=args.enabled?'On':'Off';return f.connections;}
    case 'appIcons':return Object.fromEntries(args.ids.map(id=>[id,window.__nativeFixture.media.artwork]));
    case 'media':return {...window.__nativeFixture.media,title:window.__nativeFixture.track||window.__nativeFixture.media.title};
    case 'mediaAction':{const f=window.__nativeFixture;if(f.mediaFailure)throw new Error('Music app rejected the action.');const update=()=>{if(args.action==='pause')f.media.playing=false;if(args.action==='play')f.media.playing=true;if(args.action==='next'){f.media.title='Next track';f.media.album='Next album';f.media.position=0;}if(args.action==='seek')f.media.position=args.position;f.media.updatedAt=Date.now();f.media.revision++;emit('media',{...f.media});};setTimeout(update,f.delays.mediaAction||0);return true;}
    case 'stats':return {cpu:12,gpu:8,ram:42,cpuName:'Snapdragon(R) X Elite - X1E80100 - Qualcomm(R) Oryon(TM) CPU',gpuName:'Qualcomm(R) Adreno(TM) X1-85 GPU',totalRam:16*1024**3,usedRam:6.7*1024**3,freeRam:9.3*1024**3,diskTotal:512*1024**3,diskFree:284*1024**3,download:120000,upload:45000,uptime:14520,processes:Array.from({length:4},(_,i)=>({name:`Fixture app ${i+1}`,memory:(4-i)*128*1024**2,cpu:4-i}))};
    case 'apps':return apps;
    case 'clipboard':return window.__nativeFixture.clips;
    case 'clipRemove':{const f=window.__nativeFixture;f.clips=f.clips.filter(c=>!args.ids.includes(c.id));return f.clips;}
    case 'clipCopy':case 'clipPaste':case 'launch':case 'copyText':case 'power':return true;
    case 'wallpapers':return walls;
    case 'save':state[args.key]=structuredClone(args.value);return true;
    case 'expand':emit('expanded',args.value?{value:true,view:args.full?'full':args.view||'full'}:false);return true;
    case 'presentationReady':emit('presentationShown',args.epoch);return true;
    case 'resize':return true;
    case 'settings':return true;
    case 'dialogState':window.__nativeFixture.dialogOpen=args.open;return true;
    case 'hotkeyRecord':window.__nativeFixture.hotkeyRecording=args.active;return true;
    case 'hotkeySet':if(window.__nativeFixture.hotkeyConflict)throw new Error('This shortcut is already in use. Your previous shortcut is unchanged.');state.settings.hotkey=args.shortcut;return {shortcut:args.shortcut,label:args.shortcut,registered:true};
    default:throw new Error(`Unexpected fixture method: ${method}`);
   }
  };
  window.__nativeFixture={calls:[],emit,apps,walls,delays,state,clips:[],media:{available:true,title:'Fixture track',artist:'Fixture artist',album:'Fixture album',source:'Spotify.exe',sourceName:'Spotify',artwork:'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==',playing:true,position:42,duration:180,updatedAt:Date.now(),playbackRate:1,canPlay:true,canPause:true,canToggle:true,canNext:true,canPrevious:true,canSeek:true,revision:1}};
  window.chrome ||= {};
  window.__nativeFixture.connections={wifi:{available:true,enabled:true,label:'Wi-Fi on'},bluetooth:{available:true,enabled:true,label:'Bluetooth on'},audio:{available:true,enabled:true,label:'Audio on'}};
  window.chrome.webview={addEventListener(type,handler){if(type==='message')handlers.push(handler);},postMessage(message){
   window.__nativeFixture.calls.push(structuredClone(message));
   let response;try{response={id:message.id,result:structuredClone(resultFor(message))};}catch(error){response={id:message.id,error:error.message};}
   setTimeout(()=>handlers.forEach(fn=>fn({data:response})),delays[message.method]||0);
  }};
 },{delays,wallpaperCount,hotkey,initialState});
 await page.goto('/');
 await page.waitForFunction(()=>window.__nativeFixture?.calls.some(x=>x.method==='bootstrap')&&document.querySelector('#yuta .tabs'));
 await page.evaluate(()=>window.__nativeFixture.emit('expanded',true));
 await page.getByRole('tabpanel',{name:'Home',exact:true}).waitFor();
 if(startSection!=='Home')await page.evaluate(section=>window.__nativeFixture.emit('navigate',section),startSection);
 await page.getByRole('tabpanel',{name:startSection,exact:true}).waitFor();
}
