/* Original @zakisheriff/liquid-glass components, isolated from Questlio's native bridge. */
import React, {useEffect, useState} from 'react';
import {createRoot} from 'react-dom/client';
import {flushSync} from 'react-dom';
import {GlassButton, GlassSwitch, GlassSegmented, GlassCard, setGlassQuality} from '@zakisheriff/liquid-glass';
import '@zakisheriff/liquid-glass/styles.css';

const entries=new Map();
let paused=false,queued=false;
let sidebarRoot=null;
const sidebar=document.querySelector('.sidebar');
const sidebarMaterial=document.createElement('div');sidebarMaterial.className='qg-sidebar-material';sidebarMaterial.setAttribute('aria-hidden','true');sidebar.prepend(sidebarMaterial);
const blocked=()=>paused||document.hidden||document.documentElement.dataset.opaque==='true'||document.documentElement.dataset.reducedMotion==='true'||matchMedia('(prefers-reduced-transparency: reduce),(prefers-reduced-motion: reduce),(forced-colors: active)').matches;
const translate=text=>window.QuestlioLocale?.translate(text)||text;
const viewport=new IntersectionObserver(records=>{for(const record of records){const entry=[...entries.values()].find(e=>e.row===record.target);const inView=record.isIntersecting&&record.intersectionRect.height>0&&record.boundingClientRect.height>0;if(entry&&entry.inView!==inView){entry.inView=inView;show(entry);}}});
function Icon({html}){return <span className="qg-icon" aria-hidden="true" dangerouslySetInnerHTML={{__html:html}}/>;}
function stop(event){event.stopPropagation();}
function Control({entry}){
  const source=entry.source;
  const [,update]=useState(0);
  useEffect(()=>{entry.update=()=>update(n=>n+1);return ()=>{entry.update=null;};},[entry]);
  if(entry.kind==='switch'){
    const input=entry.input;
    return <GlassSwitch id={entry.id} checked={input.checked} disabled={input.disabled}
      aria-labelledby={source.getAttribute('aria-labelledby')||undefined}
      aria-label={source.getAttribute('aria-label')||undefined}
      aria-describedby={source.getAttribute('aria-describedby')||undefined}
      onChange={value=>{input.checked=value;input.dispatchEvent(new Event('change',{bubbles:true}));update(n=>n+1);}}/>;
  }
  if(entry.kind==='segmented'){
    const buttons=[...source.querySelectorAll('button')];
    return <div onClick={stop}><GlassSegmented options={buttons.map(b=>translate(b.textContent.trim()))}
      value={Math.max(0,buttons.findIndex(b=>b.classList.contains('selected')))}
      aria-label={translate(source.getAttribute('aria-label')||'Filter')}
      onChange={index=>{buttons[index]?.click();schedule();}}/>
    </div>;
  }
  const icon=source.querySelector('svg');
  const attrs={};for(const attr of source.attributes)if(attr.name.startsWith('aria-')||attr.name.startsWith('data-'))attrs[attr.name]=attr.value;
  delete attrs['data-qg-source'];for(const name of Object.keys(attrs))if(name.startsWith('data-lg'))delete attrs[name];
  if(entry.id==='theme-toggle')attrs['aria-pressed']=String(prefs.dark);
  const iconOnly=entry.source.classList.contains('icon-button');
  const label=translate(source.textContent.trim());
  return <GlassButton {...attrs} id={entry.id||undefined} className="qg-button" type="button" size="md"
    variant={source.classList.contains('primary')?'filled':'glass'}
    disabled={source.disabled} title={translate(source.title)}
    icon={icon?<Icon html={icon.outerHTML}/>:undefined}
    onClick={event=>{event.stopPropagation();source.disabled=event.currentTarget.disabled;source.click();schedule();}}>
    {iconOnly?undefined:label}
  </GlassButton>;
}
function show(entry){
  const {source,host}=entry;
  // Both representations share the native input, including when motion is disabled.
  if(entry.input){source.setAttribute('aria-checked',String(entry.input.checked));source.disabled=entry.input.disabled;}
  if(blocked()||entry.inView===false){
    if(entry.root){entry.root.unmount();entry.root=null;}
    host.hidden=true;source.hidden=false;if(entry.id)source.id=entry.id;
    return;
  }
  source.hidden=true;source.removeAttribute('id');host.hidden=false;
  entry.root??=createRoot(host);
  flushSync(()=>entry.root.render(<Control entry={entry}/>));
}
function showSidebar(){
  if(blocked()){
    sidebarRoot?.unmount();sidebarRoot=null;sidebarMaterial.hidden=true;sidebar.classList.remove('qg-sidebar-active');return;
  }
  sidebarMaterial.hidden=false;sidebar.classList.add('qg-sidebar-active');
  if(!sidebarRoot){sidebarRoot=createRoot(sidebarMaterial);flushSync(()=>sidebarRoot.render(<GlassCard padding="0" radius="var(--glass-radius-sidebar)" className="qg-sidebar-card"/>));}
}
function add(source,kind,input){
  if(entries.has(source)||source.closest('[data-qg-root],[data-qg-source]'))return;
  const host=document.createElement('span');host.dataset.qgRoot=kind;host.className='qg-island';
  const id=source.id;source.before(host);source.dataset.qgSource=kind;
  const entry={source,host,id,kind,input};entries.set(source,entry);
  if(kind==='switch'){
    entry.row=input.closest('.setting-row');
    if(entry.row){const rect=entry.row.getBoundingClientRect();entry.inView=rect.bottom>0&&rect.top<innerHeight;viewport.observe(entry.row);}
  }
  if(input){input.dataset.qgSwitch='true';input.addEventListener('change',()=>{source.setAttribute('aria-checked',String(input.checked));entry.update?.();});}
  const observer=new MutationObserver(()=>entry.update?.());
  observer.observe(source,{attributes:true,attributeFilter:['disabled','aria-pressed','aria-busy','aria-checked','title'],childList:true,subtree:true,characterData:true});
  entry.observer=observer;show(entry);
}
function mount(){
  showSidebar();
  for(const [source,entry] of entries){
    if(!source.isConnected){entry.root?.unmount();entry.observer.disconnect();if(entry.row)viewport.unobserve(entry.row);entries.delete(source);}
  }
  document.querySelectorAll('.toolbar-group > button,.hero-bottom > button,.empty-actions > button,.heading > .glass-button').forEach(b=>add(b,'button'));
  document.querySelectorAll('input.toggle').forEach(input=>{
    if(input.dataset.qgSwitch)return;
    const button=input.nextElementSibling;
    if(button?.matches('.ui-switch'))add(button,'switch',input);
  });
  document.querySelectorAll('.segmented').forEach(group=>{
    if(group.querySelector('[data-filter]')||(group.matches('.ui-theme-segments')&&group.previousElementSibling?.matches('[data-theme-mode]')))add(group,'segmented');
  });
}
function schedule(){if(queued)return;queued=true;queueMicrotask(()=>{queued=false;mount();});}
function appearance(){queueMicrotask(()=>{for(const entry of entries.values())if(entry.source.isConnected)show(entry);schedule();});}
GaidenUI.on('rendered',schedule);
GaidenUI.on('detailOpened',schedule);
document.addEventListener('gaiden-appearance',appearance);
document.addEventListener('visibilitychange',appearance);
document.addEventListener('gaiden-pause',()=>{paused=true;appearance();});
document.addEventListener('gaiden-resume',()=>{paused=false;appearance();});
const media=matchMedia('(prefers-reduced-motion: reduce),(prefers-reduced-transparency: reduce),(forced-colors: active)');media.addEventListener('change',appearance);
// Library's own adaptive governor disables refraction while scrolling and on weak GPUs.
setGlassQuality('auto');
window.QuestlioReactGlass={mount,schedule,diagnostics:()=>({version:'0.3.16',reactRoots:[...entries.values()].filter(e=>!!e.root).length,blocked:blocked(),components:document.querySelectorAll('[data-qg-root] [data-liquid-glass]').length})};
mount();
