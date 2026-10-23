// Match the native policy in HoverIntent.cs. The idle preview is unscaled,
// so pointer coordinates are already in display-independent CSS pixels.
export class HoverIntent {
  reset(){this.enteredAt=null;}
  constructor(){this.reset();}
  observe(x,y,held,now){
    if(held){this.reset();return false;}
    if(this.enteredAt===null){this.enteredAt=this.settledAt=now;this.x=x;this.y=y;return false;}
    if((x-this.x)**2+(y-this.y)**2>9){this.x=x;this.y=y;this.settledAt=now;}
    return now-this.enteredAt>=450&&now-this.settledAt>=250;
  }
}
