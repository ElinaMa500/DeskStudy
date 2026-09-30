const fs = require('fs');
const path = require('path');
const size = 48;
const stride = size * 4;
const maskStride = Math.ceil(size / 32) * 4;
const dib = Buffer.alloc(40 + stride * size + maskStride * size);
dib.writeUInt32LE(40, 0); dib.writeInt32LE(size, 4); dib.writeInt32LE(size * 2, 8); dib.writeUInt16LE(1, 12); dib.writeUInt16LE(32, 14);
function rounded(x,y,left,top,right,bottom,r) {
  if (x<left||x>=right||y<top||y>=bottom) return false;
  const cx=Math.max(left+r,Math.min(x,right-r-1)), cy=Math.max(top+r,Math.min(y,bottom-r-1));
  return (x-cx)**2+(y-cy)**2<=r*r;
}
for(let y=0;y<size;y++)for(let x=0;x<size;x++) {
  let color=[0,0,0,0];
  if(rounded(x,y,1,1,47,47,11))color=[78,103,204,255];
  if(rounded(x,y,10,12,38,39,3))color=[255,255,255,255];
  if(x>=10&&x<38&&y>=18&&y<21)color=[221,227,247,255];
  if(((x>=16&&x<19)||(x>=29&&x<32))&&y>=8&&y<16)color=[255,255,255,255];
  if(((x>=16&&x<21)||(x>=26&&x<31))&&((y>=25&&y<28)||(y>=32&&y<35)))color=[78,103,204,255];
  const offset=40+(size-1-y)*stride+x*4;
  dib[offset]=color[2];dib[offset+1]=color[1];dib[offset+2]=color[0];dib[offset+3]=color[3];
}
const header=Buffer.alloc(22);header.writeUInt16LE(1,2);header.writeUInt16LE(1,4);header[6]=size;header[7]=size;header.writeUInt16LE(1,10);header.writeUInt16LE(32,12);header.writeUInt32LE(dib.length,14);header.writeUInt32LE(22,18);
fs.writeFileSync(path.join(__dirname,'../src/app.ico'),Buffer.concat([header,dib]));
