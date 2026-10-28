// Motion and idle control. Only transform/opacity animate; everything stops when the window is hidden.
const GaidenMotion=(()=>{
 const root=document.documentElement,reasons=new Set(),pausedAnimations=new Set(),listeners={pause:[],resume:[]};
 const reduced=()=>root.dataset.reducedMotion==='true'||root.dataset.opaque==='true'||matchMedia('(prefers-reduced-motion: reduce),(prefers-reduced-transparency: reduce),(forced-colors: active)').matches;
 const paused=()=>reasons.size>0;
 function freeze(){
  for(const animation of document.getAnimations()){
   if(animation.playState!=='running')continue;
   if(animation.effect?.getTiming?.().iterations!==Infinity&&animation instanceof CSSTransition)continue;
   animation.pause();pausedAnimations.add(animation);
  }
 }
 function thaw(){
  for(const animation of [...pausedAnimations]){if(animation.playState==='paused')animation.play();pausedAnimations.delete(animation);}
 }
 function update(reason,active){
  const was=paused();if(active)reasons.add(reason);else reasons.delete(reason);
  const now=paused();if(was===now)return;
  if(now)root.dataset.uiPaused='true';else delete root.dataset.uiPaused;
  document.dispatchEvent(new CustomEvent(now?'gaiden-pause':'gaiden-resume'));
  if(now){freeze();for(const fn of listeners.pause)fn();GaidenGlass?.pause(true);}
  else{GaidenGlass?.pause(false);for(const fn of listeners.resume)fn();thaw();}
 }
 function on(name,fn){listeners[name].push(fn);return()=>{const i=listeners[name].indexOf(fn);if(i>=0)listeners[name].splice(i,1);};}
 // View Transitions carry the poster from the clicked card into the detail sheet; CSS is the fallback.
 function transition(run,kind='screen'){
  if(reduced()||paused()||typeof document.startViewTransition!=='function'){run();return null;}
  try{return document.startViewTransition({update:run,types:[kind]});}catch{run();return null;}
 }
 function armPoster(name,element){
  if(!element||reduced())return false;
  document.querySelectorAll('*').forEach(node=>{if(node.style.viewTransitionName===name)node.style.viewTransitionName='';});
  element.style.viewTransitionName=name;return true;
 }
 document.addEventListener('visibilitychange',()=>update('hidden',document.hidden));
 window.chrome?.webview?.addEventListener('message',event=>{if(typeof event.data?.windowState==='string')update('minimized',event.data.windowState!=='normal');});
 window.addEventListener('pagehide',()=>update('hidden',true));window.addEventListener('pageshow',()=>update('hidden',document.hidden));
 return {reduced,transition,armPoster,pause:reason=>update(reason,true),resume:reason=>update(reason,false),on,diagnostics:()=>({paused:paused(),reasons:[...reasons],frozen:pausedAnimations.size,running:document.getAnimations().filter(a=>a.playState==='running').length,viewTransitions:typeof document.startViewTransition==='function',reduced:reduced()})};
})();
window.GaidenMotion=GaidenMotion;
