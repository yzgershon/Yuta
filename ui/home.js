import {escapeHtml as esc,filterClips} from './core.js';

export function homeShortcuts(apps,state){
 const all=[...apps,...state.files,...state.snippets.map(s=>({id:s.id,name:s.name,target:'snippet:'+s.id,kind:'Snippet'}))];
 const preferred=[/google chrome/i,/microsoft edge/i,/spotify/i,/file explorer/i,/visual studio code/i,/windows terminal/i];
 const candidates=[...state.favorites.map(id=>all.find(a=>a.id===id)),...state.recent.map(target=>all.find(a=>a.target===target)),...preferred.map(pattern=>all.find(a=>pattern.test(a.name))),...all];
 const seen=new Set();return candidates.filter(a=>{if(!a||seen.has(a.target))return false;seen.add(a.target);return true;}).slice(0,6);
}

function appIcon(app){
 if(app.kind==='Folder'||/explorer/i.test(app.name))return 'folder';
 if(app.kind==='Snippet')return 'text-t';
 if(app.kind==='Website'||/chrome|edge|firefox|browser/i.test(app.name))return 'globe';
 if(app.kind==='File')return 'file';
 if(/spotify|music/i.test(app.name))return 'music-notes';
 if(/code|terminal/i.test(app.name))return 'code';
 if(/settings/i.test(app.name))return 'gear-six';
 return 'squares-four';
}

export function homeDashboard({apps,clips,state,player,icon,ib,button,powerPending,clipQuery=''}){
 const shortcuts=homeShortcuts(apps,state);
 const recent=filterClips(clips,clipQuery,'all').sort((a,b)=>Number(b.pinned)-Number(a.pinned)||new Date(b.copied)-new Date(a.copied)).slice(0,3);
 const connections=[['network-wifi','wifi-high','Wi-Fi settings'],['bluetooth','bluetooth','Bluetooth settings'],['sound','headphones','Audio settings']];
 return `<div class="home-layout">
  <section class="home-clipboard" aria-labelledby="home-clips-title">
   <div class="home-section-heading"><h2 id="home-clips-title">Clipboard${!state.settings.clipboardEnabled?'<small>Paused</small>':''}</h2>${button('navigate','History','arrow-up-right','text-button','data-tab="Clipboard"')}</div>
   <label class="home-search" for="home-clip-search">${icon('magnifying-glass')}<input id="home-clip-search" type="search" aria-label="Search clipboard" placeholder="Search copied text, links, and images…" value="${esc(clipQuery)}" autocomplete="off" spellcheck="false"></label>
   <div class="home-clips">${recent.length?recent.map(clip=>`<div class="home-clip" data-key="home-clip-${esc(clip.id)}"><button type="button" data-action="homeClip" data-id="${esc(clip.id)}" class="home-clip-preview" title="${esc(clip.label)}">${icon(clip.pinned?'push-pin':clip.type==='image'?'image':clip.type==='link'?'link':'text-t')}<span><strong>${esc(clip.label||'Copied image')}</strong><small>${clip.pinned?'Pinned':clip.type==='image'?'Image':clip.type==='link'?'Link':'Text'}</small></span></button>${ib('homeClipCopy',esc('Copy '+(clip.label||'image')),'copy',`data-id="${esc(clip.id)}"`)}</div>`).join(''):`<div class="home-empty">${icon('clipboard-text')}<span>${clipQuery.trim()?'No matching clipboard items.':state.settings.clipboardEnabled?'Copy something to keep it here.':'Keep text, links, and images here.'}</span>${state.settings.clipboardEnabled||clipQuery.trim()?'':button('capture','Enable history','plus','subtle')}</div>`}</div>
   <div class="home-quick-row"><div class="connection-tools" role="group" aria-label="Connection settings">${connections.map(([page,ico,name])=>ib('systemSettings',name,ico,`data-page="${page}"`)).join('')}</div>${ib('navigate','Open quick note','note-pencil','data-tab="Notes"')}</div>
   <div class="home-power" role="group" aria-label="Power actions">${[['lock','lock-simple','Lock'],['sleep','moon','Sleep'],['restart','arrows-clockwise','Restart'],['shutdown','power','Shut down']].map(([action,ico,label])=>button('power',label,ico,action==='shutdown'?'home-shutdown':'',`data-command="${action}"${powerPending?' disabled':''}`)).join('')}</div>
  </section>
  <aside class="home-side">
   ${player}
   <section class="home-apps" aria-labelledby="home-apps-title"><div class="home-section-heading"><h2 id="home-apps-title">Pinned apps</h2>${button('navigate','All apps','arrow-up-right','text-button','data-tab="Apps"')}</div>
    <div class="home-app-grid">${shortcuts.length?shortcuts.map(app=>`<button type="button" class="home-app" aria-label="Open ${esc(app.name)}" data-action="launch" data-target="${esc(app.target)}" data-key="home-app-${esc(app.id)}" title="${esc(app.name)}"><span class="home-app-icon">${icon(appIcon(app))}</span><span>${esc(app.name)}</span>${state.favorites.includes(app.id)?icon('star','home-favorite'):''}</button>`).join(''):`<div class="home-empty"><span>No app shortcuts yet</span>${button('navigate','Add a shortcut','plus','subtle','data-tab="Apps"')}</div>`}</div>
   </section>
  </aside>
 </div>`;
}
