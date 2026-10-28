// One authoritative UI state. Compatibility accessors preserve the existing bridge/check API.
const GaidenUI=(()=>{
 const snapshot=window.gaidenBootstrap||{games:[],sessions:[],daily:[],emulators:[]};
 let preferences;try{preferences=JSON.parse(localStorage.getItem('gaiden.prefs.v1'))||{};}catch{preferences={};}
 const state={modules:{},ready:false,snapshot,games:[],prefs:{dark:false,reduced:false,opaque:false,...preferences},view:'home',filter:'all',search:'',storageAvailable:true,detailClosing:false,renderedView:null};
 const events=new Map();
 function path(element,root){if(element===root)return '';if(element.id)return '#'+CSS.escape(element.id);const parts=[];for(let node=element;node&&node!==root;node=node.parentElement){const index=[...node.parentElement.children].indexOf(node)+1;parts.unshift(node.tagName.toLowerCase()+':nth-child('+index+')');}return parts.join('>');}
 function remember(root){
  const active=document.activeElement;
  return {x:scrollX,y:scrollY,focus:root.contains(active)?path(active,root):null,fields:[...root.querySelectorAll('input:not([data-pref]),textarea,select:not([data-theme-mode])')].map(el=>({key:path(el,root),value:el.value,checked:el.checked,start:el.selectionStart,end:el.selectionEnd})),details:[...root.querySelectorAll('details')].map(el=>({key:path(el,root),open:el.open})),scrolls:[...root.querySelectorAll('*')].filter(el=>el.scrollTop||el.scrollLeft).map(el=>({key:path(el,root),top:el.scrollTop,left:el.scrollLeft}))};
 }
 function restore(root,saved){
  for(const field of saved.fields){const el=root.querySelector(field.key);if(!el)continue;el.value=field.value;el.checked=field.checked;if((field.start!==null&&['text','search','url','tel','password'].includes(el.type))||el.tagName==='TEXTAREA')try{el.setSelectionRange(field.start,field.end);}catch{}}
  for(const detail of saved.details){const el=root.querySelector(detail.key);if(el)el.open=detail.open;}
  for(const item of saved.scrolls){const el=root.querySelector(item.key);if(el){el.scrollTop=item.top;el.scrollLeft=item.left;}}
  if(saved.focus)root.querySelector(saved.focus)?.focus({preventScroll:true});window.scrollTo(saved.x,saved.y);
 }
 function paint(root,html){
  const sameView=state.renderedView===state.view;
  if(sameView&&state.renderHtml===html)return;
  const saved=sameView?remember(root):null;root.innerHTML=html;state.renderHtml=html;state.renderedView=state.view;GaidenUI.components.kit?.mount(root);if(saved)restore(root,saved);
 }
 function commitSnapshot(value){
  if(!Array.isArray(value?.games)||!Array.isArray(value?.sessions))throw new Error('Некорректный снимок библиотеки');
  GaidenUI.emit('beforeSnapshot',value);
  const previous=state.snapshot;state.snapshot=value;
  const latest=g=>Math.max(0,...[g.lastPlayedAt,g.legacy?.lastPlayedAt,g.steam?.lastPlayedAt].map(x=>Date.parse(x)).filter(Number.isFinite));
  state.games=[...value.games].filter(g=>!GaidenUI.components.kit||GaidenKit.deletions.visible(g.id)).sort((a,b)=>latest(b)-latest(a));
  const version=window.gaidenBootstrap?.version||'';document.querySelector('.sidebar-footer').textContent='Questlio Desktop '+version;document.querySelectorAll('[data-app-version]').forEach(node=>node.textContent='Questlio '+version);
  const relevant=state.view==='settings'?['emulators']:state.view==='library'?['games']:['games','sessions','daily'];
  if(!state.renderedView||relevant.some(key=>JSON.stringify(previous[key])!==JSON.stringify(value[key])))render();
  GaidenUI.emit('snapshotApplied',value,previous);
 }
 const api={state,moduleState(name,initial){return state.modules[name]??=initial;},screens:{},components:{},settings:{},layouts:{},detail:{},paint,commitSnapshot,on(name,fn){if(!events.has(name))events.set(name,[]);events.get(name).push(fn);},emit(name,...args){for(const fn of events.get(name)||[])fn(...args);}};
 for(const key of ['games','prefs','view','filter','search','storageAvailable'])Object.defineProperty(window,key,{get:()=>state[key],set:value=>{state[key]=value;},configurable:false});
 for(const [key,field]of [['gaidenSessions','sessions'],['gaidenEmulators','emulators']])Object.defineProperty(window,key,{get:()=>state.snapshot[field]||[],configurable:false});
 return api;
})();
