// Appearance only. No business state or WebView message changes.
(() => {
  const root=document.documentElement;
  const queries={dark:matchMedia('(prefers-color-scheme: dark)'),motion:matchMedia('(prefers-reduced-motion: reduce)'),transparency:matchMedia('(prefers-reduced-transparency: reduce)'),contrast:matchMedia('(forced-colors: active)')};
  const valid=mode=>['auto','light','dark'].includes(mode);
  let stored={};try{stored=JSON.parse(localStorage.getItem('gaiden.prefs.v1')||'{}');}catch{}
  if(window.gaidenNativeAppearance?.hasTheme){stored.theme=window.gaidenNativeAppearance.mode;stored.dark=window.gaidenNativeAppearance.dark;}
  const modeOf=p=>valid(p.theme)?p.theme:Object.hasOwn(stored,'dark')?(p.dark?'dark':'light'):'auto';
  let applied=false;
  let lastMode=modeOf(stored),lastDark=lastMode==='auto'?queries.dark.matches:lastMode==='dark';
  function paint(p,mode){
    const dark=mode==='auto'?queries.dark.matches:mode==='dark';
    root.dataset.theme=dark?'dark':'light';
    root.dataset.opaque=String(!!p.opaque||queries.transparency.matches);
    root.dataset.reducedMotion=String(!!p.reduced||queries.motion.matches||queries.contrast.matches);
    lastMode=mode;lastDark=dark;return dark;
  }
  paint(stored,lastMode);
  window.GaidenTheme={
    apply(p){
      let mode=modeOf(p);
      // Retain compatibility with existing callers that set prefs.dark explicitly.
      if(applied&&mode===lastMode&&!!p.dark!==lastDark)mode=p.dark?'dark':'light';
      p.theme=mode;p.dark=paint(p,mode);applied=true;
      document.dispatchEvent(new Event('gaiden-appearance'));
      return p.dark;
    },
    select(mode){
      if(!valid(mode))return;
      prefs.theme=mode;prefs.dark=mode==='auto'?queries.dark.matches:mode==='dark';
      lastMode=mode;lastDark=prefs.dark;applyPrefs();write('gaiden.prefs.v1',prefs);
    }
  };
  Object.values(queries).forEach(q=>q.addEventListener('change',()=>{
    if(typeof GaidenUI==='undefined')return;
    prefs.dark=lastMode==='auto'?queries.dark.matches:lastMode==='dark';lastDark=prefs.dark;
    applyPrefs();
  }));
})();
