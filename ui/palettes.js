// Original studies plus the violet selected in the approved workbench draft.
export const defaultCustomAccent='#9b80ff';
export const normalizeCustomAccent=color=>/^#[0-9a-f]{6}$/i.test(color||'')?color.toLowerCase():defaultCustomAccent;
export const palettes=Object.freeze([
  Object.freeze({id:'lavender',name:'Lavender',color:'#c2b6ec',ink:'#2a2243'}),
  Object.freeze({id:'ice',name:'Ice blue',color:'#acd4f5',ink:'#172a3b'}),
  Object.freeze({id:'rose',name:'Rose',color:'#e4b2c2',ink:'#402330'}),
  Object.freeze({id:'amber',name:'Amber',color:'#dfc18e',ink:'#382b18'}),
  Object.freeze({id:'porcelain',name:'Porcelain',color:'#d8d9d3',ink:'#292d2b'}),
  Object.freeze({id:'custom',name:'Violet',color:defaultCustomAccent,ink:'#2a2243'})
]);

export function resolvePalette(id,custom){const palette=palettes.find(palette=>palette.id===id)||palettes[0];return palette.id==='custom'?{...palette,color:normalizeCustomAccent(custom)}:palette;}

export function applyPalette(id,target=document.documentElement,custom){
  const palette=resolvePalette(id,custom);
  target.style.setProperty('--accent',palette.color);
  target.style.setProperty('--accent-ink',palette.ink);
  target.dataset.accent=palette.id;
  return palette;
}
