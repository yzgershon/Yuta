import {escapeHtml as esc} from './core.js';
export const darchOrigin='https://darch-sepia.vercel.app';
export const darchTabs=[['today','Today'],['assignments','Assignments'],['calendar','Calendar'],['focus','Focus'],['timelog','Time log'],['grades','Grades'],['games','Brain games'],['sat','SAT practice'],['settings','Settings'],['admin','Admin']].map(([id,label])=>({id,label}));
export function visibleDarchTabs(settings={},available=darchTabs){
 const hidden=new Set(settings.darchHiddenTabs||[]),order=[...new Set([...(settings.darchTabOrder||[]),...available.map(t=>t.id)])];
 const result=order.map(id=>available.find(t=>t.id===id)).filter(t=>t&&!hidden.has(t.id));
 return result.length?result:available.slice(0,1);
}
export function createDarch({call,render,getSettings,saveSettings,icon,toast}){
 let status={status:'unlinked'},section='today',frameUrl='',loading=false,frameError='',ready=false,epoch=0,readyTimeout,visibleNow=false,modalOpen=false;
 const linked=()=>['connected','offline'].includes(status.status);
 const available=()=>status.snapshot?.tabs||darchTabs.filter(t=>!['sat','admin'].includes(t.id));
 const visible=()=>visibleDarchTabs(getSettings(),available());
 const send=()=>{const frame=document.getElementById('darch-frame');if(ready&&frame)frame.contentWindow?.postMessage({source:'yuta',type:'section',section},darchOrigin);};
 function accept(value){if(!value)return;status=value;if(!linked()){frameUrl='';ready=false;epoch++;clearTimeout(readyTimeout);document.getElementById('darch-frame')?.remove();if(modalOpen){modalOpen=false;call('dialogState',{open:false}).catch(()=>{});}}render();}
 function setVisible(value){if(visibleNow===value)return;visibleNow=value;if(modalOpen)call('dialogState',{open:value}).catch(()=>{});}
 async function open(force=false){
  if(loading)return;
  if(!linked()){accept(await call('darchState'));if(!linked())return;}
  if(frameUrl&&!force){send();return;}
  loading=true;frameError='';const revision=++epoch;render();
  try{const result=await call('darchFrame');if(revision!==epoch)return;const url=new URL(result.url);if(url.origin!==darchOrigin||url.pathname!=='/yuta')throw new Error('Invalid Darch workspace address.');frameUrl=url.href;ready=false;clearTimeout(readyTimeout);readyTimeout=setTimeout(()=>{if(!ready&&revision===epoch){frameError='Darch did not finish loading. Reload to try again.';render();}},20000);}
  catch(error){frameError=error.message;}
  finally{loading=false;render();}
 }
 function view(){
  const items=visible();if(!items.some(t=>t.id===section))section=items[0]?.id||'today';
  const stateLabel=loading?'Connecting':status.status==='offline'?'Offline':linked()?'Connected':'Not connected';
  const content=frameUrl?`<iframe id="darch-frame" title="Darch workspace" src="${esc(frameUrl)}" sandbox="allow-scripts allow-same-origin allow-forms allow-downloads" referrerpolicy="no-referrer"></iframe>${frameError?`<div class="darch-frame-error" role="alert">${esc(frameError)}<button data-action="darchReload">Try again</button></div>`:''}`:
   `<div class="darch-welcome"><div class="darch-monogram" aria-hidden="true">D</div><h2>${loading?'Opening Darch…':linked()?'Your Darch workspace':'Your school day, in Yuta.'}</h2><p>${esc(frameError||status.message||(linked()?'Your assignments, focus sessions, and all your Darch tools.':'Connect your Darch account to use every section here. Your changes stay synced.'))}</p>${loading?'':`<button class="darch-primary" data-action="${linked()?'darchReload':'darchConnect'}" ${status.status==='connecting'?'disabled':''}>${status.status==='connecting'?'Waiting for Chrome…':linked()?'Open workspace':'Connect Darch'}${icon('arrow-up-right')}</button>`}<small>${linked()?'':'Sign in and approve in Chrome. No API key needed.'}</small></div>`;
  return `<div class="darch-shell"><aside class="darch-rail"><div class="darch-brand"><strong>Darch<span></span></strong><button class="icon-button" data-action="darchOpen" title="Open Darch in Chrome" aria-label="Open Darch in Chrome">${icon('arrow-up-right')}</button></div><nav aria-label="Darch sections">${items.map(t=>`<button type="button" data-action="darchSection" data-id="${t.id}" ${t.id==='settings'?'aria-label="Darch settings"':''} ${t.id===section?'aria-current="page"':''}>${esc(t.label)}</button>`).join('')}</nav><div class="darch-status" data-status="${esc(status.status)}"><span></span>${stateLabel}<button class="icon-button" data-action="darchReload" aria-label="Reload Darch" title="Reload Darch">${icon('arrows-clockwise')}</button></div></aside><div class="darch-stage" aria-busy="${loading}">${content}</div></div>`;
 }
 function settings(){
  const all=visibleDarchTabs({...getSettings(),darchHiddenTabs:[]},darchTabs),hidden=new Set(getSettings().darchHiddenTabs||[]);
  return `<section class="darch-settings"><div class="darch-settings-heading"><span><strong>Darch</strong><small>${linked()?'Connected to '+esc(status.snapshot?.account?.displayName||'your account'):'Connect your school workspace'}</small></span><button class="subtle" data-action="${linked()?'darchDisconnect':'darchConnect'}">${linked()?'Disconnect':'Connect'}</button></div><p>Choose the sections shown in Yuta. SAT and Admin appear when your Darch account has access.</p><div class="darch-tab-settings">${all.map((t,i)=>`<div><button class="darch-visibility" type="button" role="switch" aria-checked="${!hidden.has(t.id)}" aria-label="Show ${t.label} in Darch" data-action="darchHide" data-id="${t.id}">${icon(hidden.has(t.id)?'plus':'check')}<span>${t.label}</span></button><button class="icon-button" data-action="darchMove" data-id="${t.id}" data-direction="-1" aria-label="Move ${t.label} up" ${i===0?'disabled':''}>${icon('caret-up')}</button><button class="icon-button darch-down" data-action="darchMove" data-id="${t.id}" data-direction="1" aria-label="Move ${t.label} down" ${i===all.length-1?'disabled':''}>${icon('caret-up')}</button></div>`).join('')}</div></section>`;
 }
 async function action(name,button){
  if(!name.startsWith('darch'))return false;
  if(name==='darchSection'){section=button.dataset.id;render();if(frameUrl)send();else if(linked())await open();}
  if(name==='darchOpen')await call('darchOpen',{section});
  if(name==='darchConnect'){accept(await call('darchConnect'));}
  if(name==='darchDisconnect'){accept(await call('darchDisconnect'));toast('Darch disconnected.');}
  if(name==='darchReload')await open(true);
  if(name==='darchHide'){
   const hidden=new Set(getSettings().darchHiddenTabs||[]),id=button.dataset.id;hidden.has(id)?hidden.delete(id):hidden.add(id);
   if(!available().some(t=>!hidden.has(t.id))){toast('Keep at least one available Darch section visible.');return true;}
   await saveSettings({darchHiddenTabs:[...hidden]});render();send();
  }
  if(name==='darchMove'){
   const order=visibleDarchTabs({...getSettings(),darchHiddenTabs:[]},darchTabs).map(t=>t.id),i=order.indexOf(button.dataset.id),j=i+Number(button.dataset.direction);
   if(j>=0&&j<order.length){[order[i],order[j]]=[order[j],order[i]];await saveSettings({darchTabOrder:order});render();}
  }
  return true;
 }
 window.addEventListener('message',event=>{
  const frame=document.getElementById('darch-frame');if(!frame||event.source!==frame.contentWindow||event.origin!==darchOrigin||event.data?.source!=='darch-yuta')return;
  if(event.data.type==='ready'){ready=true;clearTimeout(readyTimeout);send();}
  if(event.data.type==='error'){frameError='Darch could not open. Reload the workspace to try again.';render();}
  if(event.data.type==='section'&&available().some(t=>t.id===event.data.section)){section=event.data.section;render();}
  if(event.data.type==='open')call('darchOpen',{section}).catch(e=>toast(e.message));
  if(event.data.type==='dialog'){modalOpen=event.data.open===true;call('dialogState',{open:visibleNow&&modalOpen}).catch(()=>{});}
 });
 return {view,settings,action,accept,open,setVisible};
}
