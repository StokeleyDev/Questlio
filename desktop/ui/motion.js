// Browser equivalents of a shared glass selection and a source-based zoom.
// Application state and persistence remain owned by app.js.
(() => {
  const media = matchMedia('(prefers-reduced-motion: reduce)');
  const reduced = () => document.documentElement.dataset.reducedMotion==='true'||document.documentElement.dataset.opaque==='true'||matchMedia('(prefers-reduced-transparency: reduce),(forced-colors: active)').matches;
  const sidebar = document.querySelector('.sidebar');
  const lens = document.createElement('div');
  lens.className = 'nav-lens';
  lens.setAttribute('aria-hidden', 'true');
  sidebar.prepend(lens);
  let lastLens = null;
  let lensAnimation = null;
  let screenAnimation = null;
  let lensFrame = null;

  function positionLens(animate = false) {
    const active = [...sidebar.querySelectorAll('.nav-item.active')]
      .find(button => button.getClientRects().length && button.getBoundingClientRect().width);
    if (!active) { lens.hidden = true; lastLens = null; return; }
    lens.hidden = false;
    const parent = sidebar.getBoundingClientRect();
    const rect = active.getBoundingClientRect();
    const next = { x: rect.left-parent.left-sidebar.clientLeft,
      y: rect.top-parent.top-sidebar.clientTop, width: rect.width, height: rect.height };
    const current = lens.getBoundingClientRect();
    lensAnimation?.cancel();
    lens.style.width = next.width+'px';
    lens.style.height = next.height+'px';
    lens.style.transform = `translate(${next.x}px,${next.y}px)`;
    if (animate && lastLens && !reduced() && current.width &&
      (Math.abs(lastLens.x-next.x)>1 || Math.abs(lastLens.y-next.y)>1)) {
      const fromX = current.left-parent.left-sidebar.clientLeft;
      const fromY = current.top-parent.top-sidebar.clientTop;
      const dx = next.x-fromX, dy = next.y-fromY;
      // A single surface travels between controls, with a small elastic settle.
      lensAnimation = lens.animate([
        {transform:`translate(${fromX}px,${fromY}px) scale(${current.width/next.width},${current.height/next.height})`},
        {transform:`translate(${next.x+dx*.018}px,${next.y+dy*.018}px) scale(1.018,.98)`,offset:.78},
        {transform:`translate(${next.x}px,${next.y}px) scale(1,1)`}
      ], {duration:220,easing:'cubic-bezier(.2,.75,.2,1)'});
    }
    lastLens = next;
  }
  GaidenUI.on('rendered',()=>positionLens(true));
  GaidenUI.on('navigated',(next,previous)=>{
    const changed=next!==previous;screenAnimation?.cancel();
    if(changed&&!reduced()&&!document.documentElement.dataset.uiPaused)screenAnimation=document.querySelector('main').animate([{opacity:.3,transform:'translateY(8px)'},{opacity:1,transform:'translateY(0)'}],{duration:180,easing:'cubic-bezier(.2,.7,.2,1)'});
  });
  let observer=null;
  function observeSidebar(){
    if(observer)return;observer=new ResizeObserver(() => {
      cancelAnimationFrame(lensFrame);
      lensFrame=requestAnimationFrame(()=>positionLens(false));
    });observer.observe(sidebar);
  }
  observeSidebar();

  function stopMotion(){if(reduced()){lensAnimation?.cancel();screenAnimation?.cancel();}}
  document.addEventListener('change',event=>{if(event.target.dataset.pref==='reduced')stopMotion();});
  media.addEventListener('change',stopMotion);
  document.addEventListener('gaiden-appearance',stopMotion);
  document.addEventListener('gaiden-pause',()=>{observer?.disconnect();observer=null;lensAnimation?.cancel();screenAnimation?.cancel();cancelAnimationFrame(lensFrame);});
  document.addEventListener('gaiden-resume',()=>{observeSidebar();positionLens(false);});
  positionLens(false);
})();
