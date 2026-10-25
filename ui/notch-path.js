// Same shoulder geometry as NotchGeometry.cs, in logical pixels.
export function notchPath(width,height,x=0){
 const s=Math.min(22,height*.57,width/4),depth=Math.min(34,height*.52);
 const r=Math.min(26,height-depth,(width-2*s)/2),k=r*.55228475;
 const right=x+width-s,left=x+s,cy=height-r;
 return `M${x} 0H${x+width}C${x+width-s*.62} 0 ${right} ${depth*.38} ${right} ${depth}V${cy}C${right} ${cy+k} ${right-r+k} ${height} ${right-r} ${height}H${left+r}C${left+r-k} ${height} ${left} ${cy+k} ${left} ${cy}V${depth}C${left} ${depth*.38} ${x+s*.62} 0 ${x} 0Z`;
}
