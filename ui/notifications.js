import {escapeHtml as esc} from './core.js';
import {notchPath} from './notch-path.js';

export function notificationSettings(access={state:'package-required'},enabled=false){
 const status=access.state||'permission-required';
 const description=status==='package-required'?'Windows notifications need the signed MSIX edition of Yuta. The portable edition can show a sample preview.':status==='denied'?'Notification access is off in Windows. You can allow it in Windows privacy settings.':status==='unavailable'?'Windows notification access is temporarily unavailable. Try again later.':enabled?'Brief previews only. No inbox or saved message history. Hidden mode stays quiet.':'Show new Windows notifications briefly in the notch. Windows will ask for access.';
 const setup=status==='package-required'?'<span class="notification-requirement">MSIX required</span>':status==='denied'?'<button type="button" class="subtle" data-action="notificationPrivacy">Windows settings</button>':!enabled?'<button type="button" class="subtle" data-action="notificationsEnable">Enable previews</button>':'';
 const toggle=enabled?'<button type="button" role="switch" aria-label="Notification previews" aria-checked="true" class="switch" data-action="notificationsDisable"><i></i></button>':'';
 return `<div class="notification-setting"><span><strong>Notification previews</strong><small>${esc(description)}</small></span><div class="notification-actions"><button type="button" class="subtle" data-action="notificationPreview">Try a preview</button>${setup}${toggle}</div></div>`;
}

export function showNotificationSample(){
 document.querySelector('.notification-sample')?.remove();
 const preview=document.createElement('button');preview.type='button';preview.className='notification-sample';preview.setAttribute('aria-label','Dismiss sample notification');
 preview.style.clipPath=`path('${notchPath(Math.min(320,window.innerWidth),64)}')`;
 preview.innerHTML='<span class="notification-sample-icon" aria-hidden="true">Y</span><span><small>Yuta · Preview</small><strong>A little update, right here.</strong><span>Brief previews. No inbox. Back to your flow.</span></span><span aria-hidden="true">×</span>';
 preview.addEventListener('click',()=>preview.remove());document.body.append(preview);setTimeout(()=>preview.remove(),5500);
}
