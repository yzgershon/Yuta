const modifiers=['Ctrl','Alt','Shift','Win'];
const keys=['Space','Tab','Enter','Backspace','Delete','Insert','Home','End','PageUp','PageDown','ArrowLeft','ArrowUp','ArrowRight','ArrowDown'];
export const defaultHotkey='Alt+Space';
export function normalizeHotkey(value){
 const parts=String(value||'').split('+').map(x=>x.trim()).filter(Boolean),raw=parts.pop()||'';
 const names=parts.map(x=>modifiers.find(name=>name.toLowerCase()===x.toLowerCase()));
 if(names.some(x=>!x)||new Set(names).size!==names.length)throw new Error('Use each modifier once: Ctrl, Alt, Shift, or Win.');
 if(!names.some(x=>['Ctrl','Alt','Win'].includes(x)))throw new Error('Include Ctrl, Alt, or Win so normal typing stays available.');
 let key=keys.find(x=>x.toLowerCase()===raw.toLowerCase());
 if(/^[a-z0-9]$/i.test(raw))key=raw.toUpperCase();
 if(/^f([1-9]|1\d|2[0-4])$/i.test(raw))key=raw.toUpperCase();
 if(!key)throw new Error('Choose a letter, number, function key, Space, or a navigation key.');
 return [...modifiers.filter(x=>names.includes(x)),key].join('+');
}
export function shortcutLabel(value){return (value||defaultHotkey).replaceAll('+',' + ');}
export function shortcutFromEvent(event){
 if(['Control','Alt','Shift','Meta','AltGraph'].includes(event.key))return null;
 if(event.getModifierState?.('AltGraph'))throw new Error('Use Ctrl, Alt, or Win instead of AltGr.');
 const names=[event.ctrlKey&&'Ctrl',event.altKey&&'Alt',event.shiftKey&&'Shift',event.metaKey&&'Win'].filter(Boolean);
 let key=event.code==='Space'?'Space':event.key;
 if(/^Key[A-Z]$/.test(event.code))key=event.code.slice(3);
 if(/^Digit\d$/.test(event.code))key=event.code.slice(5);
 return normalizeHotkey([...names,key].join('+'));
}
export function migrateHotkey(state){
 state.settings ||= {};
 let shortcut;try{shortcut=normalizeHotkey(state.settings.hotkey);}catch{shortcut=defaultHotkey;}
 if((state.hotkeyVersion||0)<1&&shortcut==='Ctrl+Alt+Space')shortcut=defaultHotkey;
 state.settings.hotkey=shortcut;state.hotkeyVersion=1;
 return state;
}
