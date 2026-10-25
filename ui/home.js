import {escapeHtml as esc} from './core.js';

export function homeShortcuts(apps,state){
 const all=[...apps,...state.files];
 if(state.settings.pinsCustomized)return state.favorites.map(id=>all.find(a=>a.id===id)).filter(Boolean).slice(0,12);
 const preferred=[/google chrome/i,/microsoft edge/i,/spotify/i,/file explorer/i,/visual studio code/i,/windows terminal/i];
 const candidates=[...state.favorites.map(id=>all.find(a=>a.id===id)),...preferred.map(pattern=>all.find(a=>pattern.test(a.name))),...all];
 const seen=new Set();return candidates.filter(a=>{if(!a||seen.has(a.target))return false;seen.add(a.target);return true;}).slice(0,6);
}
export function appArtwork(app,icons){
 const image=icons[app.id];
 return image&&/^data:image\/png;base64,/.test(image)?`<img src="${esc(image)}" alt="" decoding="async">`:`<span class="app-initial" aria-hidden="true">${esc(app.name.slice(0,1).toUpperCase())}</span>`;
}
export function powerToolbar(ib){
 return `<div class="top-power" role="group" aria-label="Power actions">${[['lock','lock-simple','Lock'],['sleep','moon','Sleep'],['restart','arrows-clockwise','Restart'],['shutdown','power','Shut down']].map(([action,ico,label])=>ib('power',label,ico,`data-command="${action}"`)).join('')}</div>`;
}
export function homeDashboard({apps,clips,state,player,icon,ib,button,connections={},connectionBusy=new Set(),icons={}}){
 const shortcuts=homeShortcuts(apps,state);
 const recent=[...clips].sort((a,b)=>Number(b.pinned)-Number(a.pinned)||new Date(b.copied)-new Date(a.copied));
 const devices=[['wifi','network-wifi','wifi-high','Wi-Fi'],['bluetooth','bluetooth','bluetooth','Bluetooth'],['audio','sound','headphones','Audio']];
 return `<div class="home-layout">
   <div class="connection-tools" role="group" aria-label="Connections">${devices.map(([key,page,ico,label])=>{const c=connections[key],busy=connectionBusy.has(key),on=c?.enabled;return `<div class="connection ${key}" data-key="connection-${key}"><div class="connection-split ${on?'is-on':''}" aria-busy="${busy}"><button data-action="connectionToggle" data-kind="${key}" aria-label="${key==='audio'?(on?'Mute':'Unmute'):(on?'Turn off':'Turn on')} ${label}" aria-pressed="${Boolean(on)}" ${busy||!c?.available?'disabled':''}>${icon(key==='audio'&&!on?'speaker-slash':ico)}</button>${ib('systemSettings',`${label} settings`,'caret-right',`data-page="${page}"`)}</div><span title="${esc(c?.detail||label)}">${esc(busy?'Updating…':c?.label||label)}</span></div>`;}).join('')}</div>
  <section class="home-clipboard" aria-labelledby="home-clips-title">
   <div class="home-section-heading"><h2 id="home-clips-title">Clipboard <small>${recent.length}</small></h2>${button('capture',state.settings.clipboardEnabled?'Capturing':'Paused',state.settings.clipboardEnabled?'shield-check':'pause','capture-status',`aria-label="${state.settings.clipboardEnabled?'Pause':'Enable'} clipboard capture"`)}</div>
   <div class="home-clips" tabindex="0" role="region" aria-label="Clipboard history">${recent.length?recent.map(clip=>{
    const label=clip.type==='image'?`${clip.width||''} × ${clip.height||''}`:(clip.text||clip.label||'');
    const thumbnail=clip.type==='image'&&/^data:image\/(png|jpeg|jpg|webp|bmp);base64,/i.test(clip.image||'');
    const stamp=new Date(clip.copied).toLocaleTimeString(undefined,{hour:'numeric',minute:'2-digit'});
    return `<div class="home-clip ${clip.type==='image'?'is-image':''}" data-key="home-clip-${esc(clip.id)}"><button type="button" data-action="homePaste" data-id="${esc(clip.id)}" class="home-clip-preview" aria-label="Paste ${esc(clip.label||'image')}" title="${esc((clip.text||clip.label||'').slice(0,2000))}">${thumbnail?`<img class="clip-thumbnail" src="${esc(clip.image)}" alt="Copied image" loading="lazy" decoding="async">`:`<span class="clip-type">${icon(clip.type==='link'?'link':clip.type==='image'?'image':'text-t')}</span>`}<span class="clip-content"><strong>${esc(label)}</strong><small>${clip.pinned?'Pinned · ':''}${clip.type==='image'?'Image · ':clip.type==='link'?'Link · ':''}${esc(stamp)}</small></span></button><div class="clip-tools">${ib('homeDelete','Delete '+esc(clip.label||'image'),'trash',`data-id="${esc(clip.id)}"`)}${ib('homePaste','Paste '+esc(clip.label||'image'),'clipboard-text',`data-id="${esc(clip.id)}"`)}</div></div>`;
   }).join(''):`<div class="home-empty">${icon('clipboard-text')}<span>${state.settings.clipboardEnabled?'Your next copy will appear here.':'Turn on capture to keep your copies here.'}</span></div>`}</div>
  </section>

  <aside class="home-side">${player}<section class="home-apps" aria-labelledby="home-apps-title"><div class="home-section-heading"><h2 id="home-apps-title">Pinned apps</h2>${ib('editPins','Edit pinned apps','note-pencil')}</div><div class="home-app-grid">${shortcuts.map(app=>`<button type="button" class="home-app" aria-label="Open ${esc(app.name)}" data-action="launch" data-target="${esc(app.target)}" data-key="home-app-${esc(app.id)}" title="${esc(app.name)}">${appArtwork(app,icons)}</button>`).join('')||button('editPins','Pin an app','plus','subtle')}</div></section></aside>
 </div>`;
}
