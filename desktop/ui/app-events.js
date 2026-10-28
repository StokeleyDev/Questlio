document.addEventListener('click',e=>{const nav=e.target.closest('[data-view]');if(nav){navigate(nav.dataset.view);return;}const g=e.target.closest('[data-game]');if(g){openDetail(g.dataset.game);return;}const add=e.target.closest('[data-action="add"]');if(add){lastFocused=document.activeElement;$('#add-dialog').showModal();return;}if(e.target.closest('.close-dialog'))e.target.closest('dialog').close();const f=e.target.closest('[data-filter]');if(f){filter=f.dataset.filter;render();document.querySelector(`[data-filter="${filter}"]`).focus();}const save=e.target.closest('#save-game');if(save){const game=games.find(g=>g.id===save.dataset.id);game.status=$('#game-status').value;game.notes=$('#game-notes').value;write('gaiden.games.v1',games);render();lastFocused=[...document.querySelectorAll('[data-game]')].find(b=>b.dataset.game===game.id)||$('#main');$('#detail-dialog').close();if(storageAvailable&&!window.gaidenBootstrap)notify('Изменения сохранены');}});
document.addEventListener('input',e=>{if(e.target.id==='library-search'){search=e.target.value;$('#library-grid').innerHTML=libraryCards();}});
document.addEventListener('change',e=>{if(e.target.dataset.pref){prefs[e.target.dataset.pref]=e.target.checked;write('gaiden.prefs.v1',prefs);applyPrefs();}});
$('#theme-toggle').addEventListener('click',()=>{GaidenTheme.select(prefs.dark?'light':'dark');if(view==='settings')render();});
$('#add-form').elements.name.addEventListener('input',e=>e.target.setCustomValidity(''));
document.querySelectorAll('dialog').forEach(d=>{d.addEventListener('click',e=>{if(e.target===d){const r=d.getBoundingClientRect();if(e.clientX<r.left||e.clientX>r.right||e.clientY<r.top||e.clientY>r.bottom)d.close();}});d.addEventListener('close',()=>{if(lastFocused?.isConnected)lastFocused.focus();else $('#main').focus({preventScroll:true});});});
window.addEventListener('hashchange',()=>{const hash=location.hash.slice(1);if(['home','library','stats','sessions','settings','computer'].includes(hash)){view=hash;render();}});
if(['home','library','stats','sessions','settings','computer'].includes(location.hash.slice(1)))view=location.hash.slice(1);

document.addEventListener('error',event=>{if(event.target.matches?.('img.game-art'))event.target.remove();},true);
$('#global-search').addEventListener('click',()=>{filter='all';navigate('library');requestAnimationFrame(()=>$('#library-search')?.focus({preventScroll:true}));});
let edgeFrame=null;
window.addEventListener('scroll',()=>{if(edgeFrame||document.documentElement.dataset.uiPaused)return;edgeFrame=requestAnimationFrame(()=>{document.body.classList.toggle('is-scrolled',scrollY>24);edgeFrame=null;});},{passive:true});
document.addEventListener('gaiden-pause',()=>{cancelAnimationFrame(edgeFrame);edgeFrame=null;});
document.addEventListener('gaiden-resume',()=>{cancelAnimationFrame(edgeFrame);edgeFrame=null;document.body.classList.toggle('is-scrolled',scrollY>24);});






document.addEventListener('change',e=>{if(e.target.matches('[data-theme-mode]')){GaidenTheme.select(e.target.value);}});
