// Event-driven material only: no interval, timeout or animation-frame loop.
(() => {
  const root=document.documentElement,main=document.querySelector('main');
  const selector='.sidebar,.toolbar-group,dialog,.ui-toast,[role="menu"],.segmented,.segmented .selected,.nav-lens,.icon-button';
  const backdrop=document.createElement('div');backdrop.className='lg-backdrop';backdrop.setAttribute('aria-hidden','true');
  const layers=[document.createElement('div'),document.createElement('div')];backdrop.append(...layers);document.body.prepend(backdrop);
  const cache=new Map(),observed=new Set();let surfaces=[],layer=0,sourceKey=null,pendingSource=null,paused=false,queued=false,paletteReads=0;
  const blocked=()=>root.dataset.opaque==='true'||root.dataset.reducedMotion==='true'||matchMedia('(forced-colors: active),(prefers-reduced-transparency: reduce),(prefers-reduced-motion: reduce)').matches;
  const visible=e=>{const r=e.getBoundingClientRect();return r.width>0&&r.height>0&&r.bottom>0&&r.right>0&&r.top<innerHeight&&r.left<innerWidth&&getComputedStyle(e).visibility==='visible';};
  const mapCache=new Map(),filters=new Map(),paletteCache=new Map();let nextFilter=0,mapBuilds=0,refractionScale=12,refractionDisabled='.toolbar-group';
  const filterTemplate=document.querySelector('#lg-refract'),filterDefs=filterTemplate.parentNode;
  const rawSize=el=>({w:el.offsetWidth,h:el.offsetHeight});
  const small=el=>el.matches('.toolbar-group,.segmented,.segmented .selected,.nav-lens,.ui-toast,.icon-button');
  const priority=el=>el.matches('.ui-toast')?100:el.matches('[role=menu]')?95:el.matches('dialog')?90:el.matches('.toolbar-group')?85:el.matches('.sidebar')?80:el.matches('.selected')?75:el.matches('.nav-lens')?70:el.matches('.segmented')?65:60;
  function eligible(el){const {w,h}=rawSize(el);return small(el)&&w>0&&h>0&&w<=320&&h<=96;}
  function displacement(w,h,r){
    const key=w+'x'+h+':'+r;if(mapCache.has(key))return mapCache.get(key);
    const pad=24,canvas=document.createElement('canvas');canvas.width=w+pad*2;canvas.height=h+pad*2;
    const ctx=canvas.getContext('2d'),image=ctx.createImageData(canvas.width,canvas.height),band=Math.min(16,h*.28);
    const sdf=(x,y)=>{const qx=Math.abs(x-w/2)-(w/2-r),qy=Math.abs(y-h/2)-(h/2-r);return Math.hypot(Math.max(qx,0),Math.max(qy,0))+Math.min(Math.max(qx,qy),0)-r;};
    for(let y=0;y<canvas.height;y++)for(let x=0;x<canvas.width;x++){
      const px=x-pad+.5,py=y-pad+.5,depth=-sdf(px,py),inside=depth>0&&depth<band;
      const weight=inside?Math.sin(Math.PI*depth/band)**2:0,gx=sdf(px+1,py)-sdf(px-1,py),gy=sdf(px,py+1)-sdf(px,py-1),norm=Math.hypot(gx,gy)||1,i=(y*canvas.width+x)*4;
      image.data[i]=128-gx/norm*90*weight;image.data[i+1]=128-gy/norm*90*weight;image.data[i+2]=128;image.data[i+3]=255;
    }
    ctx.putImageData(image,0,0);const value={uri:canvas.toDataURL('image/png'),pad,width:canvas.width,height:canvas.height,key};mapCache.set(key,value);mapBuilds++;
    if(mapCache.size>64)mapCache.delete(mapCache.keys().next().value);return value;
  }
  function syncRefraction(el){
    if(document.hidden||paused)return;
    const allow=el.hasAttribute('data-lg-active')&&!blocked()&&eligible(el)&&!(refractionDisabled&&el.matches(refractionDisabled));
    el.toggleAttribute('data-lg-refract',allow);if(!allow)return;
    const {w,h}=rawSize(el),radius=Math.min(parseFloat(getComputedStyle(el).borderTopLeftRadius)||12,w/2,h/2),map=displacement(w,h,radius);
    let state=filters.get(el);if(!state){const filter=filterTemplate.cloneNode(true);filter.id='lg-refract-'+(++nextFilter);filterDefs.append(filter);state={filter,key:null};filters.set(el,state);el.style.setProperty('--glass-refraction','url(#'+filter.id+')');}
    if(state.key!==map.key){state.key=map.key;const filter=state.filter,img=filter.querySelector('feImage');for(const node of [filter,img]){node.setAttribute('x',-map.pad);node.setAttribute('y',-map.pad);node.setAttribute('width',map.width);node.setAttribute('height',map.height);}img.setAttribute('href',map.uri);}
    state.filter.querySelector('feDisplacementMap').setAttribute('scale',String(refractionScale));
  }
  const resize=new ResizeObserver(entries=>{if(document.hidden||paused)return;for(const entry of entries)syncRefraction(entry.target);});
  function release(){if(document.hidden||paused)return;document.querySelectorAll('[data-lg-pressed]').forEach(el=>el.removeAttribute('data-lg-pressed'));}
  function allocate(){
    if(document.hidden||paused)return;
    const modal=document.querySelector('dialog[open]');let budget=5;
    for(const el of surfaces){
      const nested=el.matches('.icon-button')&&el.closest('.toolbar-group,.segmented');const wideSegment=el.matches('.segmented')&&!eligible(el);
      const active=!blocked()&&!nested&&!wideSegment&&visible(el)&&(!modal||el===modal||modal.contains(el))&&budget>0;
      el.toggleAttribute('data-lg-active',active);syncRefraction(el);if(active)budget--;
    }
  }
  const intersection=new IntersectionObserver(allocate);
  let previousSegment=null,segmentAnimation=null;
  function refresh(){
    if(document.hidden||paused)return;
    refractionScale=Number(getComputedStyle(root).getPropertyValue('--glass-refraction-scale'))||12;
    surfaces=[...document.querySelectorAll(selector)].filter(el=>!el.matches('.icon-button')||!el.closest('.toolbar-group,.segmented')).sort((a,b)=>priority(b)-priority(a));
    for(const el of [...observed])if(!el.isConnected){intersection.unobserve(el);resize.unobserve(el);observed.delete(el);const state=filters.get(el);if(state){state.filter.remove();filters.delete(el);}}
    for(const el of surfaces){el.setAttribute('data-lg','');if(!observed.has(el)){observed.add(el);intersection.observe(el);resize.observe(el);}}
    syncCovers();syncHero();allocate();syncPalette();
    if(previousSegment){const next=document.querySelector('.segmented .selected'),old=previousSegment;previousSegment=null;if(next&&!blocked()&&next.dataset.filter!==old.key){const r=next.getBoundingClientRect();segmentAnimation?.cancel();segmentAnimation=next.animate([{transform:`translateX(${old.left-r.left}px) scaleX(${old.width/r.width})`},{transform:'translateX(0) scaleX(1)'}],{duration:220,easing:'cubic-bezier(.2,.8,.2,1)'});segmentAnimation.finished.then(()=>{segmentAnimation?.cancel();segmentAnimation=null;}).catch(()=>{});}}
  }
  function schedule(){if(queued||document.hidden||paused)return;queued=true;queueMicrotask(()=>{queued=false;refresh();});}
  function syncCovers(){
    for(const host of main.querySelectorAll('.cover .icon-cover,.cover .placeholder-cover')){
      const img=host.querySelector('.game-icon'),name=host.closest('.game-card')?.getAttribute('aria-label')||'',key=img?.currentSrc||name;
      if(host.dataset.coverKey===key||img&&(!img.complete||!img.naturalWidth))continue;
      let hash=0;for(const c of name)hash=(hash*31+c.charCodeAt(0))>>>0;let rgb=[80+hash%110,80+(hash>>>8)%110,80+(hash>>>16)%110];
      if(img){try{let p=cache.get(key);if(!p){p=colors(img);cache.set(key,p);}rgb=p?.[0]||rgb;}catch{}host.style.setProperty('--cover-icon-url','url('+JSON.stringify(key)+')');host.style.setProperty('--icon-native-width',img.naturalWidth+'px');const tile=document.createElement('span');tile.className='cover-icon-tile';img.before(tile);tile.append(img);}
      host.style.setProperty('--cover-color',rgb.join(','));host.dataset.coverKey=key;
    }
  }
  function syncHero(){
    const hero=main.querySelector('.hero'),img=hero?.querySelector(':scope > img.game-art,.icon-cover img.game-icon');
    if(!img?.complete||!img.naturalWidth)return;
    const src=img.currentSrc||img.src,key=src+'|'+hero.clientWidth;if(hero.dataset.artKey===key)return;
    hero.dataset.artKey=key;hero.style.setProperty('--hero-art-url','url('+JSON.stringify(src)+')');hero.style.setProperty('--hero-native-width',img.naturalWidth+'px');
    try{const rgb=colors(img)?.[0];if(rgb)hero.style.setProperty('--hero-color',rgb.join(','));}catch{}
    hero.querySelector('.hero-cover-tile')?.remove();hero.querySelector('.hero-clear-art')?.remove();const icon=img.classList.contains('game-icon'),poster=img.classList.contains('steam-poster')&&img.naturalHeight>img.naturalWidth,compact=!icon&&img.naturalWidth<hero.clientWidth*1.2;
    hero.dataset.artLayout=icon?'icon':poster?'poster':compact?'compact':'full';
    const top=hero.querySelector('.hero-platform').getBoundingClientRect().top-hero.getBoundingClientRect().top;hero.style.setProperty('--hero-shade-stop',Math.max(55,Math.min(94,100-(top-12)/hero.clientHeight*100))+'%');
    if(icon&&!img.closest('.hero-icon-tile')){const tile=document.createElement('div');tile.className='hero-icon-tile';img.before(tile);tile.append(img);}
    if(compact||poster){const sharp=img.cloneNode();sharp.className='hero-clear-art';sharp.alt='';sharp.setAttribute('aria-hidden','true');if(img.naturalWidth<60){const tile=document.createElement('div');tile.className='hero-icon-tile hero-cover-tile';tile.append(sharp);hero.append(tile);}else hero.append(sharp);}
  }
  function bounded(rgb){
    const dark=root.dataset.theme==='dark';
    const c=rgb.map(v=>v/255),max=Math.max(...c),min=Math.min(...c),delta=max-min;let l=(max+min)/2,s=delta?delta/(1-Math.abs(2*l-1)):0,h=0;
    if(delta)h=60*(max===c[0]?((c[1]-c[2])/delta)%6:max===c[1]?(c[2]-c[0])/delta+2:(c[0]-c[1])/delta+4);
    if(h<0)h+=360;if(delta<.02)h=210;l=dark?Math.max(.18,Math.min(.32,l)):Math.max(.78,Math.min(.90,l));s=dark?Math.max(.25,Math.min(.55,s)):Math.min(.40,s);
    const a=s*Math.min(l,1-l),f=n=>{const k=(n+h/30)%12;return Math.round(255*(l-a*Math.max(-1,Math.min(k-3,9-k,1))));};return [f(0),f(8),f(4)];
  }
  function colors(image){
    const key=image.currentSrc||image.src;if(paletteCache.has(key))return paletteCache.get(key);
    const canvas=document.createElement('canvas');canvas.width=canvas.height=16;
    const ctx=canvas.getContext('2d',{willReadFrequently:true});ctx.drawImage(image,0,0,16,16);const pixels=ctx.getImageData(0,0,16,16).data,bins=new Map();paletteReads++;
    for(let i=0;i<pixels.length;i+=4){const c=[pixels[i],pixels[i+1],pixels[i+2]],max=Math.max(...c),min=Math.min(...c);if(pixels[i+3]<128||max<38||min>226)continue;const key=c.map(v=>Math.floor(v/32)).join(',');let bin=bins.get(key);if(!bin){bin={sum:[0,0,0],n:0,score:0};bins.set(key,bin);}bin.n++;c.forEach((v,j)=>bin.sum[j]+=v);bin.score+=1+(max-min)/128;}
    const ranked=[...bins.values()].sort((a,b)=>b.score-a.score).map(b=>b.sum.map(v=>Math.round(v/b.n)));
    const second=ranked.find(c=>c.reduce((v,x,i)=>v+Math.abs(x-ranked[0][i]),0)>90)||ranked[0],third=ranked.find(c=>c.reduce((v,x,i)=>v+Math.abs(x-ranked[0][i]),0)>70&&c.reduce((v,x,i)=>v+Math.abs(x-second[i]),0)>70)||ranked[2]||second;const value=ranked.length?[ranked[0],second,third]:null;paletteCache.set(key,value);if(paletteCache.size>128){const old=[...paletteCache.keys()].find(k=>k!==sourceKey);if(old)paletteCache.delete(old);}return value;
  }
  function showPalette(palette){
    if(document.hidden||paused||blocked())return;
    const next=layers[layer^1];
    for(const [i,key]of ['a','b','c'].entries()){if(palette)next.style.setProperty('--palette-'+key,bounded(palette[i]).join(','));else next.style.removeProperty('--palette-'+key);}
    next.classList.add('is-visible');layers[layer].classList.remove('is-visible');layer^=1;
  }
  function syncPalette(){
    if(document.hidden||paused||blocked())return;
    const playing=document.querySelector('.tracker.is-playing strong')?.textContent.replace(/^Сейчас играет ·\s*/,'');
    const game=games.find(g=>g.name===playing)||games[0];
    const image=[...document.querySelectorAll('img.game-art,img.game-icon')].find(img=>img.closest('[data-game]')?.dataset.game===game?.id)||document.querySelector('.hero img');
    let src=game?.customCover||game?.exeIcon||image?.currentSrc||image?.src||'';
    if(!src&&game?.legacy?.icon?.data){const data=game.legacy.icon.data;src='data:image/'+(data.startsWith('/9j/')?'jpeg':'png')+';base64,'+data;}
    if(src===sourceKey)return;
    if(cache.has(src)){sourceKey=src;showPalette(cache.get(src));return;}
    if(pendingSource===src)return;
    if(!src){sourceKey=src;cache.set(src,null);showPalette(null);return;}
    const extract=img=>{pendingSource=null;if(document.hidden||paused)return;if(cache.has(src)){sourceKey=src;showPalette(cache.get(src));return;}try{const palette=colors(img);cache.set(src,palette);sourceKey=src;showPalette(palette);if(cache.size>16)cache.delete(cache.keys().next().value);}catch{sourceKey=src;cache.set(src,null);showPalette(null);}};
    if(image?.complete&&image.naturalWidth&&(image.currentSrc||image.src)===src)extract(image);
    else if(src.startsWith('data:image/')){pendingSource=src;const img=new Image();img.onload=()=>extract(img);img.onerror=()=>{pendingSource=null;cache.set(src,null);sourceKey=src;showPalette(null);};img.src=src;}
  }
  document.addEventListener('pointermove',event=>{
    if(document.hidden||paused||blocked()||event.pointerType==='touch')return;
    const nav=event.target.closest('.nav-item.active'),el=nav?document.querySelector('.nav-lens[data-lg-active]'):event.target.closest('.toolbar-group[data-lg-active]');if(!el)return;
    const r=el.getBoundingClientRect();el.style.setProperty('--spec-x',((event.clientX-r.left)/r.width*100).toFixed(1)+'%');el.style.setProperty('--spec-y',((event.clientY-r.top)/r.height*100).toFixed(1)+'%');
  },{passive:true});
  document.addEventListener('pointerdown',event=>{if(document.hidden||paused||blocked())return;const nav=event.target.closest('.nav-item.active'),el=nav?document.querySelector('.nav-lens[data-lg-active]'):event.target.closest('[data-lg-active]');if(el&&!el.matches('.sidebar,dialog'))el.setAttribute('data-lg-pressed','');},{passive:true});
  document.addEventListener('pointerup',release,{passive:true});document.addEventListener('pointercancel',release,{passive:true});window.addEventListener('blur',release);
  document.addEventListener('click',event=>{if(document.hidden||paused||blocked()||!event.target.closest('[data-filter]'))return;const selected=document.querySelector('.segmented .selected');if(selected){const r=selected.getBoundingClientRect();previousSegment={left:r.left,width:r.width,key:selected.dataset.filter};}},{capture:true,passive:true});
  const mutations=new MutationObserver(schedule);mutations.observe(main,{childList:true,subtree:true});
  mutations.observe(document.querySelector('#detail-content'),{childList:true,subtree:true});
  mutations.observe(document.body,{attributes:true,subtree:true,attributeFilter:['open']});
  mutations.observe(document.querySelector('#toast'),{attributes:true,attributeFilter:['class'],childList:true});
  mutations.observe(document.querySelector('.tracker'),{attributes:true,attributeFilter:['class']});
  GaidenUI.on('rendered',schedule);GaidenUI.on('detailOpened',schedule);
  document.addEventListener('load',event=>{if(event.target.matches?.('img.game-art,img.game-icon'))schedule();},true);
  document.addEventListener('gaiden-appearance',()=>{if(document.hidden||paused)return;if(blocked())segmentAnimation?.cancel();sourceKey='__refresh__';schedule();});
  document.addEventListener('visibilitychange',()=>{if(document.hidden){root.dataset.lgHidden='true';segmentAnimation?.cancel();}else{delete root.dataset.lgHidden;schedule();}});
  window.addEventListener('resize',schedule,{passive:true});
  window.GaidenGlass={refresh,syncPalette,dominant(image){try{return colors(image)?.[0]||null;}catch{return null;}},pause(value){paused=value;if(!value){release();refresh();}},setRefractionDisabled(selector=''){refractionDisabled=selector;refresh();},diagnostics:()=>({surfaces:surfaces.filter(e=>e.hasAttribute('data-lg-active')).length,paletteReads,animations:document.getAnimations().filter(a=>a.playState==='running').length,timers:0,hidden:document.hidden,blocked:blocked(),hasSource:!!sourceKey,refracted:surfaces.filter(e=>e.hasAttribute('data-lg-refract')).length,mapBuilds,mapCache:mapCache.size,refractionDisabled,palette:['a','b','c'].map(k=>layers[layer].style.getPropertyValue('--palette-'+k))})};
  refresh();
})();
