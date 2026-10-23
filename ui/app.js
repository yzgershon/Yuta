import {call,native,on} from './bridge.js';
import {uid,escapeHtml as esc,bytes,timerRemaining,timeText,dateKey,weekDays,parseTargets,filterClips,weatherLabel} from './core.js';
import {palettes,resolvePalette,applyPalette} from './palettes.js';
import {defaultHotkey,shortcutFromEvent,shortcutLabel} from './hotkeys.js';
import {mediaCard,mediaPosition,mediaTime} from './media.js';
import {HoverIntent} from './hover-intent.js';
import {createIdlePreview} from './idle-preview.js';
import {homeDashboard} from './home.js';

const root=document.querySelector('#yuta'), dialog=document.querySelector('#dialog');
const tabs=['Home','Today','Stats','Wallpaper'];
const tabIcons=['house','calendar-blank','chart-line','image'];
let state,apps=[],clips=[],walls=[],monitors=[],expanded=!native,tab='Home',media=null,stats=null,weather=null,speed=null;
let query='',homeClipQuery='',clipFilter='all',appFilter='all',selectedClip=null,selectedClips=new Set(),wallIndex=0,sort='memory',selectedDate=dateKey(new Date()),speedBusy=false,hotkey=true;
let wallMode='all';
let panelTimer,toastTimer,noteTimer,hoverTimer,weatherKey='',statsInFlight=false;
let activePanel,previewPill,notesSaved='',wallRevision=0,wallpapersLoaded=false,lastHeight=0,wallRequest=0;
let updateIdlePreview;
let leaveTimer,pointerInside=false,pointerHeld=false,pointerX=0,pointerY=0,hoverRequiresExit=false;
const hoverIntent=new HoverIntent();
let accentRevision=0,powerPending=false,clipsRevision=0;
let hotkeyPhase='idle',hotkeyMessage='',hotkeyError=false,hotkeyRevision=0;
let mediaPending=null,mediaConfirmed=null,mediaIntent=0,mediaQueue=Promise.resolve(),mediaScrubbing=false,mediaScrubTrack=null;
const panelHeights={Home:452,Today:450,Stats:470,Clipboard:580,Apps:570,Wallpaper:440,Routines:460,Notes:480,Settings:640};
const openingAnimations=[];
const panels=new Map(),loads=new Map();
window.__yutaErrors=[];
window.addEventListener('error',e=>window.__yutaErrors.push(e.message));
window.addEventListener('unhandledrejection',e=>window.__yutaErrors.push(String(e.reason)));
const icon=(name,cls='')=>`<span class="icon ${cls}" style="--icon:url('./assets/icons/${name}.svg')" aria-hidden="true"></span>`;
const button=(action,label,ico,cls='',data='')=>`<button type="button" class="${cls}" data-action="${action}" ${data}>${ico?icon(ico):''}<span>${label}</span></button>`;
const ib=(action,label,ico,data='')=>`<button type="button" class="icon-button" data-action="${action}" aria-label="${label}" title="${label}" ${data}>${icon(ico)}</button>`;
const empty=(ico,title,detail='',action='')=>`<div class="empty">${icon(ico)}<strong>${title}</strong>${detail?`<p>${detail}</p>`:''}${action}</div>`;
const header=(title,right='')=>`<div class="section-heading"><h2>${title}</h2>${right}</div>`;
const subtitle=(text)=>`<p class="muted">${text}</p>`;
const safeError=e=>toast(e?.message||'Something went wrong. Please try again.',true);
function toast(text,error=false){const t=document.querySelector('#toast');t.textContent=text;t.className=error?'visible error':'visible';clearTimeout(toastTimer);toastTimer=setTimeout(()=>t.className='',4500);}
async function save(key){const value=state[key];await call('save',{key,value});if(key==='notes')notesSaved=value;}
async function flushNotes(){clearTimeout(noteTimer);if(state.notes!==notesSaved)await save('notes');}
function timer(){state.timer ||= {duration:1500,remaining:1500,running:false};return state.timer;}
function setExpanded(value){stopHoverIntent();if(expanded===value)return;if(!value&&hotkeyPhase!=='idle')cancelShortcutRecording().catch(safeError);clearTimeout(leaveTimer);if(!value)hoverRequiresExit=pointerInside;expanded=value;openingAnimations.splice(0).forEach(animation=>animation.cancel());if(!value&&state)flushNotes().catch(safeError);render();startPanelUpdates();if(value){resize(true);animateOpen();loadPanel().catch(safeError);}}
function resize(force=false){const height=panelHeights[tab]||450;if(expanded&&(force||height!==lastHeight)){lastHeight=height;call('resize',{width:900,height,reducedMotion:matchMedia('(prefers-reduced-motion: reduce)').matches}).catch(safeError);}}
function animateOpen(){if(matchMedia('(prefers-reduced-motion: reduce)').matches)return;root.querySelectorAll('.topbar,.panel').forEach(element=>openingAnimations.push(element.animate([{opacity:.35,transform:'translateY(-6px) scale(.992)'},{opacity:1,transform:'none'}],{duration:280,easing:'cubic-bezier(.16,1,.3,1)'})));}
const parentTab=name=>['Apps','Clipboard'].includes(name)?'Home':name;
async function navigate(next){if(next==='Controls'||next==='Power')next='Home';if(![...tabs,'Apps','Clipboard','Routines','Notes','Settings'].includes(next)||tab===next)return;if(hotkeyPhase!=='idle')cancelShortcutRecording().catch(safeError);if(tab==='Notes')flushNotes().catch(safeError);tab=next;query='';render();resize();startPanelUpdates();loadPanel().catch(safeError);}
function shell(content){return `<header class="topbar"><nav class="tabs" role="tablist" aria-label="Sections">${tabs.map((name,i)=>`<button type="button" id="tab-${name}" role="tab" aria-selected="${name===parentTab(tab)}" aria-controls="panel" data-action="navigate" data-tab="${name}">${icon(tabIcons[i])}<span>${name}</span></button>`).join('')}</nav><div class="top-actions">${ib('navigate','Routines','lightning','data-tab="Routines"')}${ib('navigate','Quick note','note-pencil','data-tab="Notes"')}${ib('navigate','Settings','gear-six','data-tab="Settings"')}${ib('collapse','Collapse menu','caret-up')}</div></header><section class="panel panel-${tab.toLowerCase()}" id="panel" role="tabpanel" aria-label="${tab}">${content}</section>`;}
function render(){
  if(!state)return;
  // The native window clips the already laid-out menu into a pill. Keeping its
  // backing view mounted avoids a new layout and image decode on every open.
  root.className=`yuta ${expanded||native?'expanded':'collapsed'} ${state.settings.shape==='notch'?'notch':''}`;
  root.dataset.expanded=String(expanded);
  root.style.setProperty('--panel-height',`${panelHeights[tab]||400}px`);
  scalePreview();
  const views={Home:homeView,Today:today,Stats:statsView,Clipboard:()=>homeDetail('Clipboard',clipboardView()),Apps:()=>homeDetail('Apps',appsView()),Wallpaper:wallpaperView,Routines:routinesView,Notes:notesView,Settings:settingsView};
  if(!activePanel){
    root.innerHTML=shell('');activePanel=root.querySelector('.panel');
    panels.set(tab,{element:activePanel,html:null});
    if(!native){
      previewPill=document.createElement('button');previewPill.className='pill preview-pill';previewPill.dataset.action='expand';previewPill.setAttribute('aria-label','Open Yuta');
      previewPill.innerHTML=`<svg class="pill-surface pill-surface-rounded" viewBox="0 0 128 22" aria-hidden="true" focusable="false"><rect x=".25" y=".25" width="127.5" height="21.5" rx="10.75"/></svg><svg class="pill-surface pill-surface-notch" viewBox="0 0 136 22" aria-hidden="true" focusable="false"><path d="M0 0H136C128.667 0 126.222 4.278 126.222 9.778C126.222 16.528 120.75 22 114 22H22C15.25 22 9.778 16.528 9.778 9.778C9.778 4.278 7.333 0 0 0Z"/><path class="pill-edge" d="M136 0C128.667 0 126.222 4.278 126.222 9.778C126.222 16.528 120.75 22 114 22H22C15.25 22 9.778 16.528 9.778 9.778C9.778 4.278 7.333 0 0 0"/></svg><span class="pill-content"><span id="pill-time" aria-hidden="true"></span><span id="pill-label" aria-hidden="true"></span></span>`;
      root.append(previewPill);
      updateIdlePreview=createIdlePreview(root,previewPill,()=>setExpanded(false));
    }
  }
  if(previewPill){previewPill.hidden=expanded;root.querySelectorAll('.topbar,.panel').forEach(el=>el.hidden=!expanded);previewPill.classList.toggle('running',timer().running);}
  const entry=panels.get(tab)||{element:document.createElement('section'),html:null};
  if(entry.element!==activePanel){
    activePanel.replaceWith(entry.element);activePanel=entry.element;
    activePanel.id='panel';activePanel.className=`panel panel-${tab.toLowerCase()}`;
    activePanel.setAttribute('role','tabpanel');activePanel.setAttribute('aria-label',tab);activePanel.hidden=!native&&!expanded;
    panels.set(tab,entry);
  }
  root.querySelectorAll('[role="tab"]').forEach(el=>el.setAttribute('aria-selected',String(el.dataset.tab===parentTab(tab))));
  root.querySelectorAll('.top-actions [data-tab]').forEach(el=>{if(el.dataset.tab===tab)el.setAttribute('aria-current','page');else el.removeAttribute('aria-current');});
  if(tab==='Wallpaper'){
    const revision=`${wallRevision}:${wallMode}:${(state.wallpaperRecent||[]).join('|')}`;
    if(entry.revision!==revision){reconcile(entry.element,wallpaperView());entry.revision=revision;}
    updateWallpaperSelection();
  }else{
    const html=views[tab]();
    if(entry.html!==html){reconcile(entry.element,html);entry.html=html;}
  }
  const shortcutHint=document.querySelector('[data-preview-hotkey]');if(shortcutHint)shortcutHint.textContent=shortcutLabel(state.settings.hotkey);
  updateClock();
  updateIdlePreview?.({media});
}
// Patch existing elements, preserving focus, selection, scroll positions and
// decoded images. Keys keep list items attached to their data during filtering.
function nodeKey(node){if(node.nodeType!==1)return null;const d=node.dataset;return node.id||d.key||(d.action&&(d.id||d.date||d.tab||d.filter||d.target)?`${d.action}:${d.id||d.date||d.tab||d.filter||d.target}`:null);}
function reconcile(container,html){const template=document.createElement('template');template.innerHTML=html;patchChildren(container,template.content);}
function patchChildren(current,next){
  const keyed=new Map([...current.childNodes].map(node=>[nodeKey(node),node]).filter(([key])=>key));
  let cursor=current.firstChild;
  for(const desired of [...next.childNodes]){
    const key=nodeKey(desired);let node=key?keyed.get(key):cursor;
    if(node&&(node.nodeType!==desired.nodeType||node.nodeName!==desired.nodeName||(!key&&nodeKey(node))))node=null;
    if(!node){node=desired.cloneNode(true);current.insertBefore(node,cursor);}
    else{if(node!==cursor)current.insertBefore(node,cursor);patchNode(node,desired);}
    cursor=node.nextSibling;
  }
  while(cursor){const nextNode=cursor.nextSibling;cursor.remove();cursor=nextNode;}
}
function patchNode(current,next){
  if(current.nodeType!==1){if(current.nodeValue!==next.nodeValue)current.nodeValue=next.nodeValue;return;}
  const previousValue=current.getAttribute('value');
  for(const attr of [...current.attributes])if(!next.hasAttribute(attr.name))current.removeAttribute(attr.name);
  for(const attr of [...next.attributes])if(current.getAttribute(attr.name)!==attr.value)current.setAttribute(attr.name,attr.value);
  const focused=current===document.activeElement;
  if(current.tagName==='TEXTAREA'){if(!focused&&current.value!==next.value)current.value=next.value;return;}
  patchChildren(current,next);
  // Uncontrolled drafts (such as the next to-do) have no value attribute.
  // Leave them intact when another part of their cached panel changes.
  if(!focused&&current.value!==next.value&&(current.tagName==='SELECT'||current.tagName==='INPUT'&&previousValue!==next.getAttribute('value')))current.value=next.value;
}
function scalePreview(){if(!native)root.style.zoom=expanded?Math.min(1,(innerWidth-32)/900):1;}
window.addEventListener('resize',scalePreview);
function today(){
  const days=weekDays(new Date(selectedDate+'T12:00:00')),events=state.events.filter(x=>x.date===selectedDate).sort((a,b)=>a.time.localeCompare(b.time));
  return `<div class="today-layout"><aside class="day-glance"><div class="clock" id="clock"></div><div class="clock-date">${new Date().toLocaleDateString(undefined,{weekday:'short',month:'long',day:'numeric'})}</div><div class="weather">${icon(weather&&weather.code>3?'cloud':'sun')}<div><strong>${weather?`${Math.round(weather.temperature)}<small>${weather.unit}</small>`:'Weather'}</strong><span>${weather?esc(weatherLabel(weather.code)):'Your forecast'}</span></div></div>${button('weatherLocation',weather?esc(weather.city):'Set your city','arrow-up-right','text-button weather-location')}</aside><div class="day-middle"><div class="calendar">${header('Week',`<div class="week-actions">${ib('weekPrevious','Previous week','caret-left')}${ib('weekNext','Next week','caret-right')}${ib('eventNew','Add event','plus')}</div>`)}<div class="week">${days.map(d=>`<button class="day ${dateKey(d)===selectedDate?'selected':''} ${dateKey(d)===dateKey(new Date())?'is-today':''}" data-action="date" data-date="${dateKey(d)}"><span>${d.toLocaleDateString(undefined,{weekday:'narrow'})}</span><strong>${d.getDate()}</strong><i class="${state.events.some(x=>x.date===dateKey(d))?'has-event':''}"></i></button>`).join('')}</div><div class="agenda">${events.length?events.map(e=>`<button class="event" data-action="eventEdit" data-id="${e.id}"><span class="event-line"></span><span><strong>${esc(e.title)}</strong><small>${esc(e.time||'All day')}</small></span>${icon('arrow-up-right')}</button>`).join(''):`<div class="agenda-empty"><span class="event-line"></span><div><strong>No events</strong><small>Local calendar</small></div></div>`}</div></div><div class="timer-card"><div class="timer-ring" id="timer-ring"><span id="timer-value">${timeText(timerRemaining(timer()))}</span><small>FOCUS</small></div><div class="timer-tools"><div class="timer-title"><h3>Timer</h3>${ib('timerCustom','Custom timer duration','timer')}</div><div class="presets">${[1,5,10,25,45].map(m=>`<button data-action="timerPreset" data-minutes="${m}" class="${timer().duration===m*60?'selected':''}">${m}m</button>`).join('')}</div><div class="timer-buttons">${button('timerToggle',timer().running?'Pause':'Start',timer().running?'pause':'play','primary peach')}${ib('timerReset','Reset timer','arrows-clockwise')}</div></div></div></div><div class="tasks"><div class="section-heading"><h2>Today</h2><span class="counter">${state.todos.filter(x=>x.done).length}/${state.todos.length}</span></div><div class="task-list">${state.todos.length?state.todos.map(t=>`<div data-key="task-${t.id}" class="task ${t.done?'done':''}"><button data-action="todoToggle" data-id="${t.id}" aria-label="${t.done?'Mark incomplete':'Complete'} ${esc(t.text)}">${icon(t.done?'check-circle':'circle')}<span>${esc(t.text)}</span></button>${ib('todoRemove','Remove task','x',`data-id="${t.id}"`)}</div>`).join(''):empty('check-circle','No tasks','Add a task below.')}</div><form id="todo-form" class="inline-form"><label class="sr-only" for="todo-input">New to-do</label><input id="todo-input" name="text" placeholder="Add a to-do…" maxlength="180" required autocomplete="off"><button type="submit" aria-label="Add task">${icon('arrow-right')}</button></form></div></div>`;
}
function homeView(){
 const focused=document.activeElement;
 const player=mediaCard(mediaScrubbing&&focused?.id==='media-progress'?{...media,position:Number(focused.value),updatedAt:Date.now()}:media,mediaPending,icon,ib);
 return homeDashboard({apps,clips,state,player,icon,ib,button,powerPending,clipQuery:homeClipQuery});
}
function homeDetail(name,content){return `<div class="home-detail-heading">${button('navigate','Home','caret-left','text-button','data-tab="Home"')}<span>/</span><h2>${name}</h2></div>${content}`;}
function acceptMedia(value){
 if(!value||typeof value.available!=='boolean')return;
 if(mediaConfirmed&&Number(value.revision)<Number(mediaConfirmed.revision))return;
 const sameTrack=mediaPending&&value.source===mediaPending.source&&value.title===mediaPending.title;
 if(sameTrack&&mediaPending.accepted&&((mediaPending.playing!=null&&value.playing===mediaPending.playing)||(mediaPending.position!=null&&Math.abs(mediaPosition(value)-mediaPending.position)<3)))mediaPending=null;
 mediaConfirmed=value;
 media=mediaPending?.playing!=null&&value.source===mediaPending.source&&value.title===mediaPending.title?{...value,playing:mediaPending.playing,position:mediaPosition(media),updatedAt:Date.now()}:value;
 if(mediaPending?.position!=null&&sameTrack)media={...value,position:mediaPending.position,updatedAt:Date.now()};
 if(tab==='Home'&&expanded)render();
 updateIdlePreview?.({media});
}
function updateMediaProgress(){
 if(!expanded||tab!=='Home'||mediaScrubbing)return;
 const progress=document.querySelector('#media-progress');if(!progress||!media?.duration)return;
 const value=mediaPosition(media);progress.value=value;progress.style.setProperty('--media-progress',`${value/media.duration*100}%`);
 progress.setAttribute('aria-valuetext',`${mediaTime(value)} of ${mediaTime(media.duration)}`);
 const elapsed=document.querySelector('#media-elapsed');if(elapsed)elapsed.textContent=mediaTime(value);
}
async function controlMedia(action,position,track=media){
 if(!media?.available)return;
 if(action==='seek'&&(track?.source!==media.source||track?.title!==media.title))throw new Error('The track changed. Try seeking again.');
 const id=++mediaIntent,playing=action==='toggle'?!media.playing:null,source=media.source,trackTitle=media.title;
 const command=action==='toggle'?(playing?'play':'pause'):action;
 mediaPending={id,playing,position:action==='seek'?position:null,source:media.source,title:media.title,accepted:false};
 if(playing!=null)media={...media,position:mediaPosition(media),updatedAt:Date.now(),playing};
 if(action==='seek')media={...media,position,updatedAt:Date.now()};
 render();
 const operation=mediaQueue.catch(()=>{}).then(async()=>{
  if(action==='seek'&&(mediaConfirmed?.source!==source||mediaConfirmed?.title!==trackTitle))throw new Error('The track changed. Try seeking again.');
  const accepted=await call('mediaAction',{action:command,source,trackTitle,...(position!=null?{position}:{})});
  if(accepted===false)throw new Error('Your music app could not complete that action.');
  if(mediaPending?.id===id)mediaPending.accepted=true;
  const snapshot=await call('media');
  if(mediaPending?.id===id){
   if(playing==null&&action!=='seek')mediaPending=null;
   acceptMedia(snapshot);
   if(mediaPending?.id===id)setTimeout(async()=>{if(mediaPending?.id!==id)return;try{const latest=await call('media');if(mediaPending?.id===id){mediaPending=null;acceptMedia(latest);}}catch(error){if(mediaPending?.id===id){mediaPending=null;media=mediaConfirmed;render();safeError(error);}}},1800);
  }
 });
 mediaQueue=operation;
 try{await operation;}catch(error){if(mediaPending?.id===id){mediaPending=null;media=mediaConfirmed;if(tab==='Home'&&expanded)render();}throw error;}
}
function gauge(value,label,detail,color){return `<div class="gauge-group"><div class="gauge" style="--progress:${value??0};--ring-color:${color}" role="meter" aria-label="${label}" ${value!=null?`aria-valuenow="${Math.round(value)}" aria-valuemin="0" aria-valuemax="100"`:''}><strong>${value==null?'—':Math.round(value)+'<small>%</small>'}</strong></div><h3>${label}</h3><p title="${esc(detail)}">${esc(detail)}</p></div>`;}
function statsView(){const s=stats;return `<div class="stats-layout"><div><div class="gauges">${gauge(s?.cpu,'CPU',s?.cpuName||'Reading processor…','var(--sage)')}${gauge(s?.gpu,'GPU',s?.gpuName||'Reading graphics…','var(--accent-tint)')}${gauge(s?.ram,'Memory',s?`${bytes(s.usedRam)} / ${bytes(s.totalRam)}`:'Reading memory…','var(--accent-dim)')}</div><div class="stat-bottom"><div>${icon('hard-drives')}<span>Storage available</span><strong>${s?bytes(s.diskFree):'—'}</strong></div><div>${icon('arrows-clockwise')}<span>Network now</span><strong>${s?bytes(s.download)+'/s ↓':'—'}</strong><small>${s?bytes(s.upload)+'/s ↑':''}</small></div><div>${icon('clock')}<span>Up for</span><strong>${s?`${Math.floor(s.uptime/3600)}h ${Math.floor(s.uptime%3600/60)}m`:'—'}</strong></div></div></div><div class="stats-side">${header('Top apps',`<button class="text-button" data-action="sort">${sort==='memory'?'RAM':'CPU'} ${icon('caret-down')}</button>`)}<div class="process-list">${s?[...s.processes].sort((a,b)=>b[sort]-a[sort]).slice(0,4).map((p,i)=>`<div class="process"><span class="app-avatar color-${i}">${icon('squares-four')}</span><div><div><strong>${esc(p.name)}</strong><span>${sort==='memory'?bytes(p.memory):p.cpu+'%'}</span></div><div class="process-track"><i style="width:${sort==='memory'?Math.max(3,p.memory/s.totalRam*100):Math.max(3,p.cpu)}%"></i></div></div></div>`).join(''):'<p class="muted">Reading running apps…</p>'}</div><div class="speed-test">${header('Internet speed')}<div class="speed-values"><span><strong>${speed?.download??'—'}</strong> Mbps ↓</span><span><strong>${speed?.upload??'—'}</strong> Mbps ↑</span></div>${button('speed',speedBusy?'Measuring…':'Run speed test','lightning','primary',speedBusy?'disabled':'')}<small>${speed?'Estimate · Cloudflare endpoint':'Uses about 11 MB · Cloudflare'}</small></div></div></div>${s&&s.gpu==null?'<p class="footnote">GPU readings are unavailable from this graphics driver.</p>':''}`;}
function clipboardView(){const list=filterClips(clips,query,clipFilter);const item=clips.find(x=>x.id===selectedClip)||list[0];if(item&&!selectedClip)selectedClip=item.id;return `<div class="toolbar"><label class="search">${icon('magnifying-glass')}<span class="sr-only">Search clipboard</span><input id="search" value="${esc(query)}" placeholder="Search ${clips.length} items…" autocomplete="off"></label><div class="segmented">${['all','text','image','link'].map(x=>`<button data-action="clipFilter" data-filter="${x}" class="${clipFilter===x?'selected':''}">${x==='image'?'Images':x==='link'?'Links':x[0].toUpperCase()+x.slice(1)}</button>`).join('')}</div></div><div class="capture-line"><span>${icon(state.settings.clipboardEnabled?'shield-check':'pause')}${state.settings.clipboardEnabled?'History is on · encrypted on this device':'Clipboard history is paused'}</span><button class="text-button" data-action="capture">${state.settings.clipboardEnabled?'Pause':'Enable history'}</button></div><div class="clipboard-layout"><div class="clip-list" role="listbox" aria-label="Clipboard entries" aria-multiselectable="true">${list.length?list.map(c=>`<button class="clip-row ${item?.id===c.id?'active':''} ${selectedClips.has(c.id)?'multi-selected':''}" data-action="clipSelect" data-id="${c.id}" role="option" aria-selected="${selectedClips.has(c.id)||item?.id===c.id}">${c.type==='image'?`<img src="${c.image}" alt="Copied image">`:icon(c.type==='link'?'link':'text-t')}<span><strong>${esc(c.label)}</strong><small>${new Date(c.copied).toLocaleTimeString(undefined,{hour:'numeric',minute:'2-digit'})}${c.pinned?' · pinned':''}</small></span>${c.pinned?icon('push-pin'):''}</button>`).join(''):empty('clipboard-text',query?'No matching items.':'No clipboard items','Enable history, then copy text, links, or images.')}</div><div class="clip-detail">${item?`<div class="clip-preview">${item.type==='image'?`<img src="${item.image}" alt="Clipboard preview">`:`<pre>${esc(item.text)}</pre>`}</div><dl><dt>Type</dt><dd>${item.type}</dd>${item.type==='image'?`<dt>Size</dt><dd>${item.width} × ${item.height} px</dd>`:''}<dt>Copied</dt><dd>${new Date(item.copied).toLocaleString()}</dd></dl>`:empty('copy','Select an item','Your selected item will appear here.')}</div></div><div class="clip-actions">${button('clipPaste','Paste','arrow-right','primary',!item?'disabled':'')}${button('clipCopy','Copy','copy','subtle',!item?'disabled':'')}${button('clipPin',item?.pinned?'Unpin':'Pin','push-pin','subtle',!item?'disabled':'')}${button('clipRemove',selectedClips.size>1?`Remove ${selectedClips.size}`:'Remove','trash','subtle',!item?'disabled':'')}<span>Ctrl / Shift to select several</span></div>`;}
function appsView(){
 const all=[...apps,...state.files,...state.snippets.map(s=>({id:s.id,name:s.name,target:'snippet:'+s.id,kind:'Snippet',searchText:s.text}))];let list=all.filter(a=>(a.name+' '+a.target+' '+(a.searchText||'')).toLowerCase().includes(query.toLowerCase()));
 if(appFilter==='favorites')list=list.filter(x=>state.favorites.includes(x.id));if(appFilter==='recent')list=state.recent.map(t=>all.find(x=>x.target===t)).filter(Boolean).filter(x=>x.name.toLowerCase().includes(query.toLowerCase()));
 return `<div class="toolbar"><label class="search">${icon('magnifying-glass')}<span class="sr-only">Search apps, files and snippets</span><input id="search" value="${esc(query)}" placeholder="Search apps, files and snippets…" autocomplete="off"></label>${button('launcherNew','Add shortcut','plus','subtle')}</div><div class="filter-row">${['all','favorites','recent','snippets'].map(x=>`<button data-action="appFilter" data-filter="${x}" class="${appFilter===x?'selected':''}">${x[0].toUpperCase()+x.slice(1)}</button>`).join('')}<span>${appFilter==='snippets'?state.snippets.length:list.length} items</span></div><div class="launcher-list">${appFilter==='snippets'?`${state.snippets.filter(x=>(x.name+x.text).toLowerCase().includes(query.toLowerCase())).map(s=>`<div class="launcher-row" data-key="snippet-${s.id}"><button data-action="snippetCopy" data-id="${s.id}"><span class="app-avatar color-2">${icon('text-t')}</span><span><strong>${esc(s.name)}</strong><small>${esc(s.text.slice(0,100))}</small></span><span class="item-kind">Copy</span></button>${ib('snippetEdit','Edit snippet','note-pencil',`data-id="${s.id}"`)}</div>`).join('')}${button('snippetNew','New reusable text','plus','subtle')}`:list.length?list.slice(0,160).map(a=>`<div class="launcher-row" data-key="app-${esc(a.id)}"><button data-action="launch" data-target="${esc(a.target)}"><span class="app-avatar">${icon(a.kind==='Folder'?'folder':a.kind==='Website'?'globe':a.kind==='File'?'file':'squares-four')}</span><span><strong>${esc(a.name)}</strong></span><span class="item-kind">${a.kind}</span></button>${ib('favorite',state.favorites.includes(a.id)?'Remove favorite':'Add favorite','star',`data-id="${esc(a.id)}" data-selected="${state.favorites.includes(a.id)}"`)}${state.files.some(f=>f.id===a.id)?ib('launcherRemove','Remove shortcut','x',`data-id="${esc(a.id)}"`):''}</div>`).join(''):empty('magnifying-glass','No results','Try another search or add a shortcut.')}</div>`;
}
const currentWalls=()=>wallMode==='recent'?(state.wallpaperRecent||[]).map(id=>walls.find(w=>w.id===id)).filter(Boolean):walls;
function wallpaperView(){const shown=currentWalls();return `<div class="wallpaper-toolbar"><div><h2>Wallpapers</h2><span>${shown.length} pictures \u00b7 ${native?'your collection':'demo collection'}</span></div><div class="wallpaper-options"><div class="segmented">${['all','recent'].map(mode=>`<button data-action="wallFilter" data-filter="${mode}" class="${wallMode===mode?'selected':''}">${mode==='all'?'Collection':'Recent'}</button>`).join('')}</div>${button('wallpaperFolder','My folder','folder','subtle')}</div></div><div class="wallpaper-carousel">${shown.length?shown.map((w,i)=>`<button aria-label="Select ${esc(w.name)}" aria-pressed="false" data-action="wallpaperSelect" data-key="wall-${esc(w.id)}" data-index="${i}" class="wallpaper art-${w.art??i%5}">${w.image?`<img src="${w.image}" alt="${esc(w.name)}" decoding="async">`:''}<span>${icon('check')}</span></button>`).join(''):empty('image',wallMode==='recent'?'No recent wallpapers':'No wallpapers',wallMode==='recent'?'Wallpapers you apply will appear here.':'Choose a folder of JPG, PNG, or BMP images.')}</div><div class="wallpaper-bottom">${ib('wallPrevious','Previous wallpaper','caret-left')}<div><strong></strong><small></small></div>${ib('wallNext','Next wallpaper','caret-right')}<span></span>${button('wallpaperApply','Set wallpaper','check','primary')}</div>`;}
function updateWallpaperSelection(){
 const panel=panels.get('Wallpaper')?.element;if(!panel)return;
 const shown=currentWalls();wallIndex=Math.max(0,Math.min(wallIndex,shown.length-1));const item=shown[wallIndex];
 panel.querySelectorAll('.wallpaper').forEach((card,i)=>{
  let distance=i-wallIndex;if(shown.length>2){if(distance>shown.length/2)distance-=shown.length;else if(distance<-shown.length/2)distance+=shown.length;}
  const absolute=Math.abs(distance),direction=Math.sign(distance),near=absolute<=2;
  card.classList.toggle('selected',distance===0);card.classList.toggle('nearby',near);card.classList.toggle('offstage',!near);
  card.setAttribute('aria-pressed',String(distance===0));card.setAttribute('aria-hidden',String(!near));card.tabIndex=near?0:-1;
  card.style.setProperty('--offset',distance===0?'0px':`calc(var(--wall-near) * ${direction} + var(--wall-step) * ${distance-direction})`);
  card.style.setProperty('--turn',`${direction*-64}deg`);card.style.setProperty('--scale',distance===0?'1':'.83');card.style.zIndex=String(Math.max(0,4-absolute));
 });
 panel.querySelector('.wallpaper-bottom strong').textContent=item?.name||'Choose a picture';
 panel.querySelector('.wallpaper-bottom small').textContent=state.activeWallpaper===item?.id?'Current wallpaper':'Select a picture, then apply';
 panel.querySelector('[data-action="wallpaperApply"]').disabled=!item;
 panel.querySelectorAll('[data-action="wallPrevious"],[data-action="wallNext"]').forEach(button=>button.disabled=shown.length<2);
}


function routinesView(){return `${header('Routines',button('routineNew','New routine','plus','primary'))}<div class="routine-list">${state.routines.length?state.routines.map((r,i)=>`<article class="routine-card" data-key="routine-${r.id}"><span class="routine-icon color-${i%4}">${icon('lightning')}</span><div><h3>${esc(r.name)}</h3><p>${r.targets.length} destinations</p></div>${ib('routineEdit','Edit routine','gear-six',`data-id="${r.id}"`)}${button('routineRun','Run','play','primary',`data-id="${r.id}"`)}</article>`).join(''):empty('lightning','No routines','Create a routine to open several destinations.',button('routineNew','Create your first routine','plus','primary'))}</div><div class="routine-ideas"><span>Templates</span>${['Start coding','Schoolwork','Evening'].map(n=>button('routineNew',n,null,'subtle',`data-name="${n}"`)).join('')}</div>`;}
function notesView(){return `${header('Quick note',`<span id="note-status" class="muted">Saved on this device</span>`)}<label class="sr-only" for="notes">Quick note</label><textarea id="notes" class="note-editor" placeholder="Write a note…" maxlength="100000">${esc(state.notes)}</textarea><div class="note-footer">${button('noteCopy','Copy note','copy','subtle')}<span class="muted">Your note saves as you type.</span></div>`;}
function shortcutRecorder(){return `<div><span><strong>Keyboard shortcut</strong><small>Click to record a new combination</small></span><div class="hotkey-recorder"><button type="button" class="hotkey-button ${hotkeyPhase!=='idle'?'recording':''}" data-action="hotkey-record" aria-label="Change keyboard shortcut" aria-describedby="hotkey-status" aria-pressed="${hotkeyPhase!=='idle'}" ${['arming','saving'].includes(hotkeyPhase)?'disabled':''}>${esc(hotkeyPhase==='arming'?'Getting ready\u2026':hotkeyPhase==='saving'?'Saving\u2026':hotkeyPhase==='recording'?'Press shortcut\u2026':shortcutLabel(state.settings.hotkey))}</button>${hotkeyPhase!=='idle'?`<button type="button" class="subtle" data-action="hotkey-cancel" ${hotkeyPhase==='saving'?'disabled':''}>Cancel</button>`:''}<small id="hotkey-status" class="hotkey-status ${hotkeyError?'error':''}" role="status">${esc(hotkeyMessage||'')}</small></div></div>`;}
async function beginShortcutRecording(){
 if(hotkeyPhase!=='idle')return;
 const revision=++hotkeyRevision;hotkeyPhase='arming';hotkeyError=false;hotkeyMessage='';clearTimeout(leaveTimer);render();
 try{await call('hotkeyRecord',{active:true});if(revision!==hotkeyRevision)return;hotkeyPhase='recording';hotkeyMessage='Press a combination with Ctrl, Alt, or Win. Esc cancels.';render();document.querySelector('[data-action="hotkey-record"]')?.focus();}
 catch(error){if(revision===hotkeyRevision){hotkeyPhase='idle';hotkeyError=true;hotkeyMessage=error.message;render();}}
}
async function cancelShortcutRecording(){
 if(hotkeyPhase==='idle'||hotkeyPhase==='saving')return;
 ++hotkeyRevision;hotkeyPhase='idle';hotkeyError=false;hotkeyMessage='Shortcut unchanged.';render();
 try{await call('hotkeyRecord',{active:false});}finally{schedulePreviewRetraction();}
}
async function captureShortcut(shortcut){
 if(hotkeyPhase!=='recording')return;
 const revision=hotkeyRevision;hotkeyPhase='saving';hotkeyError=false;hotkeyMessage='';render();
 try{const result=await call('hotkeySet',{shortcut});state.settings.hotkey=result.shortcut;hotkey=result.registered;if(revision!==hotkeyRevision){render();return;}hotkeyPhase='idle';hotkeyMessage=native?'Shortcut updated.':'Saved for this preview. Global shortcuts work in the Windows app.';await call('hotkeyRecord',{active:false});render();schedulePreviewRetraction();}
 catch(error){if(revision===hotkeyRevision){hotkeyPhase='recording';hotkeyError=true;hotkeyMessage=error.message;render();document.querySelector('[data-action="hotkey-record"]')?.focus();}}
}
function recordShortcutKey(event){
 event.preventDefault();event.stopPropagation();
 if(hotkeyPhase==='saving')return;
 if(event.key==='Escape'){cancelShortcutRecording().catch(safeError);return;}
 if(event.repeat||hotkeyPhase!=='recording')return;
 try{const shortcut=shortcutFromEvent(event);if(shortcut)captureShortcut(shortcut).catch(safeError);}
 catch(error){hotkeyMessage=error.message;hotkeyError=true;render();}
}
function settingsView(){const s=state.settings;return `${header('Settings','<div class="wordmark" aria-label="Yuta">Yuta<span class="brand-dot"></span></div>')}<div class="settings-list">${shortcutRecorder()}<div class="accent-setting"><span><strong>Accent</strong><small>Menu and notch color</small></span><div class="accent-choices" role="group" aria-label="Accent color">${palettes.map(p=>resolvePalette(p.id,s.customAccent)).map(p=>`<button type="button" data-action="accent" data-accent="${p.id}" aria-pressed="${s.accent===p.id}" style="--swatch:${p.color};--swatch-ink:${p.ink}"><span class="accent-swatch" aria-hidden="true">${icon('check')}</span><span>${p.name}</span></button>`).join('')}</div></div><div><span><strong>Shape</strong><small>Floating pill or attached notch</small></span><div class="segmented"><button data-action="shape" data-shape="pill" class="${s.shape==='pill'?'selected':''}">Pill</button><button data-action="shape" data-shape="notch" class="${s.shape==='notch'?'selected':''}">Notch</button></div></div><div><label for="monitor"><strong>Display</strong><small>Display for the notch</small></label><select id="monitor">${monitors.map(m=>`<option value="${m.id}" ${m.id===s.monitor?'selected':''}>${m.name}</option>`).join('')}</select></div><div><span><strong>Deliberate hover</strong><small>Hover for 0.45 seconds, click, or use your shortcut.</small></span><button role="switch" aria-checked="${s.openOnHover}" class="switch" aria-label="Open with deliberate hover" data-action="hoverToggle"><i></i></button></div><div><span><strong>Smart flattening</strong><small>Make room when the idle notch covers a tab or button.</small></span><button role="switch" aria-checked="${s.smartFlatten!==false}" class="switch" aria-label="Flatten over covered controls" data-action="flattenToggle"><i></i></button></div><div><span><strong>Clipboard history</strong><small>Stores up to 200 items, encrypted on this PC</small></span><button role="switch" aria-checked="${s.clipboardEnabled}" class="switch" data-action="capture"><i></i></button></div><div><span><strong>Weather</strong><small>${esc(s.city||'Choose your city')}</small></span>${button('weatherLocation','Edit city','arrow-up-right','subtle')}</div><div><span><strong>Temperature</strong></span><div class="segmented"><button data-action="unit" data-fahrenheit="true" class="${s.fahrenheit?'selected':''}">°F</button><button data-action="unit" data-fahrenheit="false" class="${!s.fahrenheit?'selected':''}">°C</button></div></div></div><div class="settings-bottom"><p>Yuta stays in your system tray.<br>To run at sign-in, add a shortcut to your Windows Startup folder.${hotkey?'':'<br>The shortcut is busy. Use the notch or system tray.'}</p>${button('quit','Quit Yuta','power','subtle')}</div>`;}

function updateClock(){const el=document.querySelector('#clock');if(el){const parts=new Date().toLocaleTimeString('en-US',{hour:'2-digit',minute:'2-digit',hour12:true}).split(' ');el.innerHTML=`${parts[0]}<span>${parts[1]}</span>`;}const remaining=timerRemaining(timer());const time=document.querySelector('#timer-value');if(time)time.textContent=timeText(remaining);const ring=document.querySelector('#timer-ring');if(ring)ring.style.setProperty('--progress',`${timer().duration?remaining/timer().duration*100:0}`);if(previewPill){previewPill.classList.toggle('running',timer().running);const clock=new Date().toLocaleTimeString('en-US',{hour:'numeric',minute:'2-digit',hour12:true}).split(' ');previewPill.querySelector('#pill-time').textContent=timer().running?timeText(remaining):clock[0];previewPill.querySelector('#pill-label').textContent=timer().running?'FOCUS':clock[1];previewPill.title=timer().running?`Focus: ${timeText(remaining)} remaining — Open Yuta`:`Open Yuta · ${shortcutLabel(state.settings.hotkey)}`;}if(!native&&timer().running&&remaining===0){state.timer.running=false;state.timer.remaining=0;save('timer').catch(safeError);toast('Focus complete. Time for a breather.');render();}}
function startPanelUpdates(){clearInterval(panelTimer);if(!expanded)return;if(tab==='Stats')panelTimer=setInterval(()=>loadStats().catch(safeError),3000);if(tab==='Home')panelTimer=setInterval(updateMediaProgress,250);}
async function loadStats(){if(statsInFlight||!expanded||tab!=='Stats')return;statsInFlight=true;try{const next=await call('stats');if(next)stats=next;if(tab==='Stats'&&expanded)render();}finally{statsInFlight=false;}}
async function refreshMedia(){acceptMedia(await call('media'));}
async function loadWeather(){if(!state.settings.city)return;const key=state.settings.city+state.settings.fahrenheit;if(weatherKey===key&&weather)return;const next=await cachedLoad('weather:'+key,()=>call('weather',{city:state.settings.city,fahrenheit:state.settings.fahrenheit}));if(key!==state.settings.city+state.settings.fahrenheit)return;weather=next;weatherKey=key;if(tab==='Today'&&expanded)render();}
async function cachedLoad(key,fetch,ttl=Infinity){
 const previous=loads.get(key);if(previous?.promise)return previous.promise;if(previous&&Date.now()<previous.expires)return previous.value;
 const entry={promise:null};loads.set(key,entry);entry.promise=fetch();
 try{const value=await entry.promise;loads.set(key,{value,expires:Date.now()+ttl});return value;}catch(error){loads.delete(key);throw error;}
}
async function loadClipboard(){if(!loads.has('clipboard')){const revision=clipsRevision;const value=await cachedLoad('clipboard',()=>call('clipboard'));if(revision===clipsRevision)clips=value;}else if(loads.get('clipboard').promise)await loads.get('clipboard').promise;}
async function loadPanel(){
 const requested=tab;
 switch(requested){
 case 'Home':await Promise.all([refreshMedia(),loadClipboard()]);break;
 case 'Stats':await loadStats();return;
 case 'Clipboard':await loadClipboard();break;
 case 'Wallpaper':if(!wallpapersLoaded){const version=wallRequest;const next=await cachedLoad('wallpapers',()=>call('wallpapers'));if(version===wallRequest&&!wallpapersLoaded){walls=next;wallpapersLoaded=true;wallRevision++;wallIndex=Math.max(0,walls.findIndex(x=>x.id===state.activeWallpaper));}}break;
 case 'Today':await loadWeather();return;
 default:return;
 }
 if(tab===requested&&expanded)render();
}

function showDialog(title,body,submit,onSubmit,{remove}={}){
 dialog.innerHTML=`<form id="modal-form"><div class="dialog-heading"><h2>${title}</h2>${ib('dialogClose','Close dialog','x')}</div>${body}<p class="form-error" id="form-error" role="alert"></p><div class="dialog-actions">${remove?button('dialogRemove','Remove','trash','subtle danger'):''}<span></span>${button('dialogClose','Cancel',null,'subtle')}<button class="primary" type="submit">${submit}</button></div></form>`;
 clearTimeout(leaveTimer);dialog.showModal();call('dialogState',{open:true}).catch(safeError);dialog.querySelector('input,textarea,select')?.focus();
 dialog.querySelector('form').onsubmit=async e=>{e.preventDefault();const btn=e.submitter;btn.disabled=true;try{await onSubmit(new FormData(e.target));dialog.close();render();}catch(err){dialog.querySelector('#form-error').textContent=err.message;}finally{btn.disabled=false;}};
 dialog.querySelector('[data-action="dialogRemove"]')?.addEventListener('click',async()=>{try{await remove();dialog.close();render();}catch(e){safeError(e);}});
}
const field=(name,label,value='',type='text',extra='')=>`<label class="field">${label}<input name="${name}" type="${type}" value="${esc(value)}" ${extra}></label>`;
function routineEditor(id,name){const r=state.routines.find(x=>x.id===id);showDialog(r?'Edit routine':'A routine of your own',`${field('name','Name',r?.name||name||'','text','required maxlength="60"')}<label class="field">Websites, apps, or folders<textarea name="targets" rows="5" required placeholder="https://github.com&#10;C:\\Dev">${esc(r?.targets.join('\n')||'')}</textarea></label><p class="form-hint">One destination per line, up to 12. Use full Windows paths for apps and folders.</p>`,r?'Save routine':'Create routine',async f=>{const item={id:r?.id||uid(),name:String(f.get('name')).trim(),targets:parseTargets(String(f.get('targets')))};if(!item.name)throw new Error('Give your routine a name.');if(r)state.routines=state.routines.map(x=>x.id===r.id?item:x);else state.routines.push(item);await save('routines');},{remove:r?async()=>{state.routines=state.routines.filter(x=>x.id!==r.id);await save('routines');}:undefined});}
function eventEditor(id){const e=state.events.find(x=>x.id===id);showDialog(e?'Edit event':'Make a little plan',`${field('title','Event',e?.title||'','text','required maxlength="160"')}${field('date','Date',e?.date||selectedDate,'date','required')}${field('time','Time',e?.time||'09:00','time','required')}`,'Save event',async f=>{const item={id:e?.id||uid(),title:String(f.get('title')).trim(),date:String(f.get('date')),time:String(f.get('time'))};if(!item.title)throw new Error('Enter an event title.');state.events=e?state.events.map(x=>x.id===e.id?item:x):[...state.events,item];await save('events');},{remove:e?async()=>{state.events=state.events.filter(x=>x.id!==e.id);await save('events');}:undefined});}
function snippetEditor(id){const s=state.snippets.find(x=>x.id===id);showDialog(s?'Edit reusable text':'Keep the words you reuse',`${field('name','Name',s?.name||'','text','required maxlength="80"')}<label class="field">Text<textarea name="text" rows="6" required maxlength="100000">${esc(s?.text||'')}</textarea></label>`,'Save snippet',async f=>{const item={id:s?.id||uid(),name:String(f.get('name')).trim(),text:String(f.get('text'))};state.snippets=s?state.snippets.map(x=>x.id===s.id?item:x):[...state.snippets,item];await save('snippets');},{remove:s?async()=>{state.snippets=state.snippets.filter(x=>x.id!==s.id);await save('snippets');}:undefined});}

async function action(a,b,event){const id=b.dataset?.id;
 switch(a){
 case 'expand':return call('expand',{value:true});case 'collapse':return call('expand',{value:false});
 case 'navigate':return navigate(b.dataset.tab);
 case 'todoToggle':state.todos=state.todos.map(x=>x.id===id?{...x,done:!x.done}:x);await save('todos');break;
 case 'todoRemove':state.todos=state.todos.filter(x=>x.id!==id);await save('todos');break;
 case 'date':selectedDate=b.dataset.date;break;
 case 'weekPrevious':case 'weekNext':{const date=new Date(selectedDate+'T12:00:00');date.setDate(date.getDate()+(a==='weekNext'?7:-7));selectedDate=dateKey(date);break;}
 case 'eventNew':eventEditor();return;case 'eventEdit':eventEditor(id);return;
 case 'timerPreset':state.timer={duration:Number(b.dataset.minutes)*60,remaining:Number(b.dataset.minutes)*60,running:false};await save('timer');break;
 case 'timerToggle':{const t=timer(),remaining=timerRemaining(t)||t.duration;if(t.running){t.remaining=remaining;t.running=false;}else{t.endsAt=Date.now()+remaining*1000;t.running=true;}await save('timer');break;}
 case 'timerReset':state.timer={duration:timer().duration,remaining:timer().duration,running:false};await save('timer');break;
 case 'timerCustom':showDialog('Make time for it.',field('minutes','Minutes',Math.round(timer().duration/60),'number','min="1" max="1440" required'),'Set timer',async f=>{const minutes=Number(f.get('minutes'));if(!Number.isInteger(minutes)||minutes<1||minutes>1440)throw new Error('Choose between 1 and 1,440 minutes.');state.timer={duration:minutes*60,remaining:minutes*60,running:false};await save('timer');});return;
 case 'weatherLocation':showDialog('Your weather, nearby.',field('city','City or postal code',state.settings.city,'text','required minlength="2" maxlength="120"')+'<p class="form-hint">Your search goes to Open-Meteo. No location permission is needed.</p>','Save city',async f=>{state.settings.city=String(f.get('city')).trim();await save('settings');weatherKey='';loadWeather().catch(safeError);});return;
 case 'systemSettings':await call('settings',{page:b.dataset.page});return;
 case 'media':return controlMedia(b.dataset.command);
 case 'sort':sort=sort==='memory'?'cpu':'memory';break;
 case 'speed':speedBusy=true;render();try{speed=await call('speed');}finally{speedBusy=false;render();}return;
 case 'capture':state.settings.clipboardEnabled=!state.settings.clipboardEnabled;await save('settings');toast(state.settings.clipboardEnabled?'Clipboard history enabled. New copies will appear here.':'Clipboard history paused.');break;
 case 'clipFilter':clipFilter=b.dataset.filter;selectedClip=null;selectedClips.clear();break;
 case 'clipSelect':if(event.ctrlKey||event.metaKey){if(selectedClips.has(id))selectedClips.delete(id);else selectedClips.add(id);}else if(event.shiftKey&&selectedClip){const list=filterClips(clips,query,clipFilter),from=list.findIndex(x=>x.id===selectedClip),to=list.findIndex(x=>x.id===id);for(const c of list.slice(Math.min(from,to),Math.max(from,to)+1))selectedClips.add(c.id);}else{selectedClips.clear();selectedClips.add(id);}selectedClip=id;break;
 case 'clipCopy':case 'clipPaste':if(selectedClip){await call(a,{id:selectedClip});toast(a==='clipCopy'?'Copied.':'Pasted into your previous app.');}return;
 case 'clipPin':if(selectedClip)clips=await call('clipPin',{id:selectedClip});break;
 case 'clipRemove':clips=await call('clipRemove',{ids:selectedClips.size?[...selectedClips]:[selectedClip]});selectedClips.clear();selectedClip=null;break;
 case 'appFilter':appFilter=b.dataset.filter;break;
 case 'launch':if(b.dataset.target.startsWith('snippet:')){const s=state.snippets.find(x=>x.id===b.dataset.target.slice(8));if(s){await call('copyText',{text:s.text});toast('Snippet copied.');}return;}await call('launch',{target:b.dataset.target});state.recent=[b.dataset.target,...state.recent.filter(x=>x!==b.dataset.target)].slice(0,12);await save('recent');toast('Opened.');return;
 case 'favorite':state.favorites=state.favorites.includes(id)?state.favorites.filter(x=>x!==id):[...state.favorites,id];await save('favorites');break;
 case 'launcherRemove':state.files=state.files.filter(x=>x.id!==id);await save('files');break;
 case 'launcherNew':showDialog('Keep it one click away.',`${field('name','Name','','text','required maxlength="80"')}${field('target','Website or full Windows path','','text','required')}<div class="picker-buttons">${button('pickFile','Choose file','file','subtle')}${button('pickFolder','Choose folder','folder','subtle')}</div>`,'Add shortcut',async f=>{const target=String(f.get('target')).trim();parseTargets(target);state.files.push({id:uid(),name:String(f.get('name')).trim(),target,kind:target.startsWith('http')?'Website':/\.[a-z0-9]+$/i.test(target)?'File':'Folder'});await save('files');});return;
 case 'pickFile':case 'pickFolder':{const path=await call('pick',{kind:a==='pickFolder'?'folder':'file'});if(path)dialog.querySelector('[name="target"]').value=path;return;}
 case 'snippetNew':snippetEditor();return;case 'snippetEdit':snippetEditor(id);return;
 case 'snippetCopy':await call('copyText',{text:state.snippets.find(x=>x.id===id).text});toast('Snippet copied.');return;
 case 'wallFilter':if(wallMode===b.dataset.filter)return;wallMode=b.dataset.filter;wallIndex=0;break;
 case 'wallpaperFolder':wallRequest++;walls=await call('wallpaperFolder');wallpapersLoaded=true;wallRevision++;wallMode='all';wallIndex=0;break;
 case 'wallpaperSelect':wallIndex=Number(b.dataset.index);updateWallpaperSelection();return;
 case 'wallPrevious':if(currentWalls().length)wallIndex=(wallIndex+currentWalls().length-1)%currentWalls().length;updateWallpaperSelection();return;
 case 'wallNext':if(currentWalls().length)wallIndex=(wallIndex+1)%currentWalls().length;updateWallpaperSelection();return;
 case 'wallpaperApply':{const item=currentWalls()[wallIndex];if(item){await call('wallpaperApply',{id:item.id});state.activeWallpaper=item.id;state.wallpaperRecent=[item.id,...(state.wallpaperRecent||[]).filter(id=>id!==item.id)].slice(0,12);if(wallMode==='recent')wallIndex=0;toast(native?'Wallpaper updated.':'Preview selection updated. Your desktop is unchanged.');}break;}
 case 'homeClip':selectedClip=id;selectedClips=new Set([id]);clipFilter='all';return navigate('Clipboard');
 case 'homeClipCopy':await call('clipCopy',{id});toast('Copied.');return;
 case 'power':if(powerPending)return;powerPending=true;render();try{await call('power',{action:b.dataset.command});}finally{powerPending=false;render();}return;
 case 'routineNew':routineEditor(null,b.dataset.name);return;case 'routineEdit':routineEditor(id);return;
 case 'routineRun':b.disabled=true;try{const result=await call('routine',{id});toast(`${native?'Opened':'Previewed'} ${result.opened} destinations.${result.errors.length?' '+result.errors.join(' '):''}`,result.errors.length>0);}finally{b.disabled=false;}return;
 case 'noteCopy':if(state.notes){await call('copyText',{text:state.notes});toast('Note copied.');}return;
 case 'hotkey-record':await beginShortcutRecording();return;
 case 'hotkey-cancel':await cancelShortcutRecording();return;
 case 'shape':state.settings.shape=b.dataset.shape;await save('settings');break;
 case 'accent':{const previous=state.settings.accent,revision=++accentRevision;state.settings.accent=resolvePalette(b.dataset.accent).id;applyPalette(state.settings.accent,document.documentElement,state.settings.customAccent);render();try{await save('settings');}catch(error){if(revision===accentRevision){state.settings.accent=previous;applyPalette(previous,document.documentElement,state.settings.customAccent);render();}throw error;}return;}
 case 'flattenToggle':state.settings.smartFlatten=state.settings.smartFlatten===false;await save('settings');break;
 case 'hoverToggle':state.settings.openOnHover=!state.settings.openOnHover;await save('settings');break;
 case 'unit':state.settings.fahrenheit=b.dataset.fahrenheit==='true';await save('settings');weatherKey='';loadWeather().catch(safeError);break;
 case 'quit':await call('quit');return;
 case 'dialogClose':dialog.close();return;
 default:return;
 }render();
}
root.addEventListener('contextmenu',e=>{if(native){e.preventDefault();call('visibilityMenu').catch(safeError);}});
document.addEventListener('click',e=>{const b=e.target.closest('[data-action]');if(b&&!b.disabled)action(b.dataset.action,b,e).catch(safeError);});
document.addEventListener('submit',async e=>{if(e.target.id!=='todo-form')return;e.preventDefault();const input=e.target.elements.text,text=input.value.trim();if(!text)return;state.todos.push({id:uid(),text,done:false});input.value='';try{await save('todos');render();document.querySelector('#todo-input')?.focus();}catch(err){safeError(err);}});
document.addEventListener('input',e=>{
 if(e.target.id==='home-clip-search'){homeClipQuery=e.target.value;render();}
 if(e.target.id==='search'){query=e.target.value;selectedClip=null;render();}
 if(e.target.id==='media-progress'){if(!mediaScrubbing)mediaScrubTrack={source:media?.source,title:media?.title};mediaScrubbing=true;const value=Number(e.target.value);e.target.style.setProperty('--media-progress',`${value/(media?.duration||1)*100}%`);document.querySelector('#media-elapsed').textContent=mediaTime(value);}
 if(e.target.id==='notes'){state.notes=e.target.value;clearTimeout(noteTimer);document.querySelector('#note-status').textContent='Saving…';noteTimer=setTimeout(async()=>{try{await flushNotes();const status=document.querySelector('#note-status');if(status&&state.notes===notesSaved)status.textContent='Saved on this device';}catch(err){safeError(err);}},400);}
});
document.addEventListener('change',async e=>{try{if(e.target.id==='media-progress'){mediaScrubbing=false;const track=mediaScrubTrack;mediaScrubTrack=null;await controlMedia('seek',Number(e.target.value),track||media);return;}if(e.target.id==='monitor'){state.settings.monitor=Number(e.target.value);await save('settings');}}catch(err){safeError(err);}});
function schedulePreviewRetraction(){clearTimeout(leaveTimer);if(native||!expanded||pointerInside||pointerHeld||dialog.open||hotkeyPhase!=='idle')return;leaveTimer=setTimeout(()=>{if(expanded&&!pointerInside&&!pointerHeld&&!dialog.open&&hotkeyPhase==='idle')call('expand',{value:false}).catch(safeError);},180);}
function stopHoverIntent(){clearInterval(hoverTimer);hoverIntent.reset();}
root.addEventListener('pointermove',e=>{pointerX=e.clientX;pointerY=e.clientY;});
root.addEventListener('pointerenter',e=>{
 pointerInside=true;pointerX=e.clientX;pointerY=e.clientY;clearTimeout(leaveTimer);stopHoverIntent();
 if(!native&&!expanded&&!hoverRequiresExit&&state?.settings.openOnHover){hoverIntent.observe(pointerX,pointerY,pointerHeld,performance.now());hoverTimer=setInterval(()=>{
  if(expanded||hoverRequiresExit||!pointerInside||!state?.settings.openOnHover){stopHoverIntent();return;}
  if(hoverIntent.observe(pointerX,pointerY,pointerHeld,performance.now())){stopHoverIntent();call('expand',{value:true}).catch(safeError);}
 },30);}
});
window.addEventListener('blur',stopHoverIntent);
root.addEventListener('pointerleave',()=>{pointerInside=false;hoverRequiresExit=false;stopHoverIntent();schedulePreviewRetraction();});
document.addEventListener('pointerdown',e=>{if(e.target.id==='media-progress'){mediaScrubbing=true;mediaScrubTrack={source:media?.source,title:media?.title};}pointerHeld=true;clearTimeout(leaveTimer);});
document.addEventListener('pointerup',()=>{mediaScrubbing=false;pointerHeld=false;schedulePreviewRetraction();});
document.addEventListener('pointercancel',()=>{mediaScrubbing=false;pointerHeld=false;schedulePreviewRetraction();});
dialog.addEventListener('close',()=>{call('dialogState',{open:false}).catch(safeError);schedulePreviewRetraction();});
document.addEventListener('keydown',e=>{
 if(hotkeyPhase!=='idle'){recordShortcutKey(e);return;}
 if(dialog.open)return;
 const typing=['INPUT','TEXTAREA','SELECT'].includes(e.target.tagName);
 if(e.key==='Escape'){call('expand',{value:false}).catch(safeError);return;}
 if(e.ctrlKey&&e.key.toLowerCase()==='k'){e.preventDefault();if(!expanded)call('expand',{value:true});navigate('Apps').then(()=>document.querySelector('#search')?.focus()).catch(safeError);}
 if(e.target.getAttribute('role')==='tab'&&['ArrowRight','ArrowLeft','Home','End'].includes(e.key)){e.preventDefault();const i=tabs.indexOf(parentTab(tab)),next=e.key==='Home'?0:e.key==='End'?tabs.length-1:(i+(e.key==='ArrowRight'?1:tabs.length-1))%tabs.length;navigate(tabs[next]).then(()=>document.querySelector('#tab-'+tabs[next])?.focus()).catch(safeError);}
 if(tab==='Clipboard'&&!typing){if(e.key==='Delete'){e.preventDefault();action('clipRemove',{},e).catch(safeError);}if(e.ctrlKey&&e.key.toLowerCase()==='c'){e.preventDefault();action('clipCopy',{},e).catch(safeError);}if(e.ctrlKey&&e.key.toLowerCase()==='p'){e.preventDefault();action('clipPin',{},e).catch(safeError);}if(e.target.closest('.clip-list')&&['ArrowDown','ArrowUp'].includes(e.key)){e.preventDefault();const list=filterClips(clips,query,clipFilter),index=list.findIndex(x=>x.id===selectedClip),next=list[Math.max(0,Math.min(list.length-1,index+(e.key==='ArrowDown'?1:-1)))];if(next)action('clipSelect',{dataset:{id:next.id}},e).then(()=>document.querySelector(`.clip-row[data-id="${next.id}"]`)?.focus()).catch(safeError);}if(e.key==='Enter'&&e.target.closest('.clip-list')){e.preventDefault();action('clipPaste',{},e).catch(safeError);}}
});
on('media',acceptMedia);window.addEventListener('blur',()=>{if(hotkeyPhase==='recording'||hotkeyPhase==='arming')cancelShortcutRecording().catch(safeError);});
on('hotkeyCaptured',shortcut=>captureShortcut(shortcut).catch(safeError));
on('hotkeyCancel',()=>cancelShortcutRecording().catch(safeError));
on('hotkeyError',message=>{if(hotkeyPhase==='recording'){hotkeyError=true;hotkeyMessage=message;render();}});
on('expanded',setExpanded);on('navigate',next=>navigate(next).catch(safeError));on('apps',value=>{apps=value;if((tab==='Apps'||tab==='Home')&&expanded)render();});on('clipboard',value=>{clipsRevision++;clips=value;if((tab==='Clipboard'||tab==='Home')&&expanded)render();});on('timerDone',()=>{state.timer.running=false;state.timer.remaining=0;render();toast('Your focus timer is complete.');});
if(!native){document.body.classList.add('preview');document.querySelector('#preview-caption').hidden=false;document.querySelector('#preview-footer').hidden=false;}else document.body.classList.add('native');
try{const data=await call('bootstrap');state=data.state;state.settings.hotkey ||= defaultHotkey;state.settings.accent=applyPalette(state.settings.accent,document.documentElement,state.settings.customAccent).id;notesSaved=state.notes;apps=data.apps;clips=data.clips;monitors=data.monitors;hotkey=data.hotkey;render();if(expanded){startPanelUpdates();loadPanel().catch(safeError);}setInterval(()=>{if(expanded||!native)updateClock();},1000);}catch(e){root.innerHTML=empty('arrows-clockwise','Yuta couldn’t load.',esc(e.message));safeError(e);}
