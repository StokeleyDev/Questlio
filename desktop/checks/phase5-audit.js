window.phase5Audit=(()=>{
  const visible=e=>{const r=e.getBoundingClientRect(),s=getComputedStyle(e);return r.width>0&&r.height>0&&s.visibility!=='hidden'&&s.display!=='none';};
  const intersect=(a,b)=>{const x=Math.max(0,Math.min(a.right,b.right)-Math.max(a.left,b.left)),y=Math.max(0,Math.min(a.bottom,b.bottom)-Math.max(a.top,b.top));return x*y;};
  function audit(key){
    const base=phaseOneAudit.collect(key),dialog=document.querySelector('dialog[open]'),main=document.querySelector('#main');
    const scope=dialog||main,headings=[...scope.querySelectorAll('h1,h2')],heading=headings.find(h=>h.textContent.trim()||h.getAttribute('aria-label')||h.querySelector('input')?.value)||headings[0],topbar=document.querySelector('.topbar')?.getBoundingClientRect();
    const hr=heading?.getBoundingClientRect();
    const headingVisible=!!(heading&&visible(heading)&&hr.width>0&&hr.height>0&&hr.top>=-1&&hr.bottom<=innerHeight+1&&hr.left>=-1&&hr.right<=innerWidth+1&&(dialog||!topbar||hr.top>=topbar.bottom-1||document.documentElement.scrollTop>0));
    const hScroll=document.documentElement.scrollWidth>innerWidth+1||document.body.scrollWidth>innerWidth+1;
    const clipped=[...scope.querySelectorAll('.panel,.hero,.game-card,.metric,.settings-groups,.session-day,.ui-empty')].filter(visible).filter(e=>{const r=e.getBoundingClientRect();return r.right>innerWidth+1||r.left<-1;}).map(e=>String(e.className).slice(0,40));
    let overlaps=[];
    for(const grid of document.querySelectorAll('.cover-grid,.overview-metrics,.stats-grid,.session-days,.add-entry-grid,.settings-groups')){
      const kids=[...grid.children].filter(visible);
      for(let i=0;i<kids.length;i++)for(let j=i+1;j<kids.length;j++){const area=intersect(kids[i].getBoundingClientRect(),kids[j].getBoundingClientRect());if(area>4)overlaps.push({i,j,area:Math.round(area)});}
    }
    return {...base,headingTag:heading?.tagName,headingText:(heading?.textContent||'').trim().slice(0,60),headingVisible,hScroll,clipped,overlaps:overlaps.slice(0,6),dialog:!!dialog};
  }
  function color(value){const probe=document.createElement('span');probe.style.color=value;probe.style.display='none';document.body.append(probe);const c=getComputedStyle(probe).color;probe.remove();const m=c.match(/[\d.]+/g)||[0,0,0,1];return m.map(Number);}
  const over=(a,b)=>{const t=a.length>3?a[3]:1;return a.slice(0,3).map((v,i)=>v*t+b[i]*(1-t));};
  const lum=c=>c.map(x=>{x/=255;return x<=.04045?x/12.92:((x+.055)/1.055)**2.4;}).reduce((s,v,i)=>s+v*[.2126,.7152,.0722][i],0);
  const ratio=(a,b)=>{const x=lum(a),y=lum(b);return +((Math.max(x,y)+.05)/(Math.min(x,y)+.05)).toFixed(3);};
  function tokens(){
    const names={text:'--text',muted:'--muted',background:'--background',surface:'--surface',soft:'--surface-soft',accent:'--accent',onAccent:'--on-accent',danger:'--danger',success:'--success',warning:'--warning',focus:'--focus',control:'--control-line'};
    const values={};for(const [key,name] of Object.entries(names))values[key]=color('var('+name+')');
    const white=[255,255,255],pairs=[];
    const add=(name,fg,bg,min)=>{const back=over(values[bg],white),front=over(values[fg],back);pairs.push({name,ratio:ratio(front,back),min});};
    for(const fg of ['text','muted','danger','success','warning'])for(const bg of ['background','surface','soft'])add(fg+'/'+bg,fg,bg,4.5);
    for(const bg of ['background','surface'])add('accent/'+bg,'accent',bg,4.5);
    add('accent/soft (indicator)','accent','soft',3);
    add('onAccent/accent','onAccent','accent',4.5);
    add('focus/background','focus','background',3);add('focus/surface','focus','surface',3);add('control/surface','control','surface',3);
    return {theme:document.documentElement.dataset.theme,opaque:document.documentElement.dataset.opaque,pairs,failures:pairs.filter(p=>p.ratio+0.001<p.min)};
  }
  return {audit,tokens};
})();
