if(window.gaidenBootstrap){
const modelSteam=GaidenUI.moduleState('steam',{preview:null,token:'',limit:10,busy:false,selection:new Map()});
 const dialog=document.createElement('dialog');dialog.id='steam-dialog';dialog.setAttribute('aria-labelledby','steam-title');document.body.append(dialog);
 
 const post=data=>window.chrome.webview.postMessage(data);
 function count(){const b=dialog.querySelector('[data-steam-import]');if(b){b.disabled=modelSteam.busy||modelSteam.selection.size===0;b.textContent='Импортировать · '+modelSteam.selection.size;}}
 function rows(){
   if(!modelSteam.preview)return;
   const list=dialog.querySelector('#steam-results');list.replaceChildren();
   const available=modelSteam.preview.games.length;list.classList.toggle('has-rows',available>0);
   dialog.querySelector('.steam-selection').hidden=!available;
   for(const g of modelSteam.preview.games.slice(0,modelSteam.limit)){
     const row=document.createElement('div');row.className='steam-row';
     const checkbox=document.createElement('input');checkbox.type='checkbox';checkbox.checked=modelSteam.selection.has(g.appId);checkbox.setAttribute('aria-label','Импортировать '+g.name);checkbox.onchange=()=>{if(checkbox.checked)modelSteam.selection.set(g.appId,row.querySelector('select').value);else modelSteam.selection.delete(g.appId);count();};
     const info=document.createElement('div');info.className='steam-game-info';const title=document.createElement('strong');title.textContent=g.name;const time=document.createElement('span');time.className='muted';time.textContent='Steam · '+fmtCompact(g.minutes*60);info.append(title,time);
     const select=document.createElement('select');select.setAttribute('aria-label','Сопоставить '+g.name);
     const linked=games.find(x=>x.steam?.account===modelSteam.preview.steamId&&x.steam?.appId===g.appId);
     const option=(value,text)=>{const o=document.createElement('option');o.value=value;o.textContent=text;select.append(o);};
     if(linked){option(linked.id,'Обновить: '+linked.name);select.disabled=true;}else{option('','Добавить новую');for(const existing of games.filter(x=>x.platform==='PC'&&!x.steam))option(existing.id,'Связать: '+existing.name);}
     select.value=modelSteam.selection.get(g.appId)??linked?.id??'';
     select.onchange=()=>{if(checkbox.checked)modelSteam.selection.set(g.appId,select.value);};row.append(checkbox,info,select);list.append(row);
   }
   if(!available){const empty=document.createElement('div');empty.className='ui-empty compact';empty.setAttribute('role','status');empty.innerHTML='<h2>В библиотеке Steam нет игр</h2><p class="muted">Проверьте профиль и видимость списка игр, затем попробуйте снова.</p><button class="text-button" type="button" data-steam-change>Другой профиль</button>';list.append(empty);}
   const more=dialog.querySelector('[data-steam-more]');more.hidden=modelSteam.limit>=modelSteam.preview.games.length;more.textContent='Показать ещё 10';
   dialog.querySelector('#steam-count').textContent='Показано '+Math.min(modelSteam.limit,modelSteam.preview.games.length)+' из '+modelSteam.preview.games.length;count();GaidenKit.mount(list);
 }
 function open(){
   modelSteam.preview=null;modelSteam.token='';modelSteam.limit=10;modelSteam.busy=false;modelSteam.selection.clear();
   dialog.innerHTML=`<div class="steam-dialog-body"><div class="dialog-heading"><span class="preview-badge">Тестовая функция</span><div class="steam-header-actions"><button type="button" class="icon-button" data-steam-menu aria-label="Другие действия Steam">…</button><button class="icon-button" type="button" data-steam-close aria-label="Закрыть">×</button></div></div><h2 id="steam-title">Импорт из Steam</h2><p class="muted">Выберите игры перед импортом. Часы Steam сохраняются отдельно от времени Questlio, без создания старых сессий.</p><form id="steam-form"><label for="steam-profile">Профиль Steam</label><input id="steam-profile" required maxlength="200" placeholder="SteamID64 или ссылка на профиль" autocomplete="off"><label for="steam-key">API-ключ Steam</label><input id="steam-key" type="password" required maxlength="32" autocomplete="off" spellcheck="false" placeholder="32 символа"><p class="muted">Ключ используется для запроса и не сохраняется. Пароль Steam не нужен.</p><button type="button" class="text-button" data-steam-key>Открыть страницу API-ключа</button><div class="sheet-actions"><button class="secondary" type="button" data-steam-close>Отмена</button><button class="primary" type="submit">Найти игры</button></div></form><p id="steam-status" role="status" class="muted" aria-live="polite"></p><button type="button" class="text-button" data-steam-change hidden>Другой профиль или обновить список</button><div id="steam-preview" hidden><div class="steam-selection"><button class="text-button" type="button" data-steam-all>Выбрать все</button><button class="text-button" type="button" data-steam-none>Снять выбор</button></div><div id="steam-results"></div><div class="session-pagination"><button class="glass-button" type="button" data-steam-more>Показать ещё 10</button></div><div class="steam-sheet-actions"><small id="steam-count"></small><button class="primary" type="button" data-steam-import>Импортировать</button></div></div></div>`;
   dialog.querySelector('form').onsubmit=e=>{e.preventDefault();if(modelSteam.busy)return;modelSteam.busy=true;modelSteam.preview=null;modelSteam.selection.clear();dialog.querySelector('#steam-preview').hidden=true;dialog.querySelector('#steam-status').textContent='Загружаем библиотеку Steam…';dialog.querySelector('[type=submit]').disabled=true;post({action:'steamPreview',profile:dialog.querySelector('#steam-profile').value,key:dialog.querySelector('#steam-key').value});dialog.querySelector('#steam-key').value='';};
   dialog.showModal();
 }
 dialog.addEventListener('close',()=>{if(dialog.open)return;post({action:'steamCancel'});modelSteam.preview=null;modelSteam.token='';modelSteam.selection.clear();dialog.replaceChildren();});
 document.addEventListener('click',e=>{
   if(e.target.closest('[data-steam-open]')){open();return;}
   if(e.target.closest('[data-steam-menu]')){const anchor=e.target.closest('[data-steam-menu]');GaidenKit.menu(anchor,[{label:'Другой профиль или обновить список',disabled:!modelSteam.preview,run:()=>dialog.querySelector('[data-steam-change]').click()},{label:'Открыть страницу API-ключа',run:()=>post({action:'steamKeyPage'})}]);return;}
   if(e.target.closest('[data-steam-close]')){dialog.close();return;}
   if(e.target.closest('[data-steam-key]')){post({action:'steamKeyPage'});return;}
   if(e.target.closest('[data-steam-change]')){post({action:'steamCancel'});modelSteam.preview=null;modelSteam.token='';modelSteam.selection.clear();dialog.querySelector('form').hidden=false;dialog.querySelector('#steam-preview').hidden=true;dialog.querySelector('[data-steam-change]').hidden=true;dialog.querySelector('#steam-status').textContent='';dialog.querySelector('#steam-key').focus();return;}
   if(e.target.closest('[data-steam-more]')){modelSteam.limit+=10;rows();return;}
   if(e.target.closest('[data-steam-none]')){modelSteam.selection.clear();rows();return;}
   if(e.target.closest('[data-steam-all]')){for(const g of modelSteam.preview.games){const linked=games.find(x=>x.steam?.account===modelSteam.preview.steamId&&x.steam?.appId===g.appId);modelSteam.selection.set(g.appId,linked?.id||'');}rows();return;}
   if(e.target.closest('[data-steam-import]')&&!modelSteam.busy&&modelSteam.selection.size){modelSteam.busy=true;count();dialog.querySelector('#steam-status').textContent='Сохраняем выбранные игры…';post({action:'steamImport',token:modelSteam.token,selection:[...modelSteam.selection].map(([appId,gameId])=>({appId,gameId}))});}
 });
 window.applySteamMessage=data=>{
   if(!dialog.open)return;
   if(data.steamError){modelSteam.busy=false;dialog.querySelector('#steam-status').textContent=data.steamError;dialog.querySelector('[type=submit]').disabled=false;count();}
   if(data.steamPreview){modelSteam.busy=false;modelSteam.preview=data.steamPreview;modelSteam.preview.games.sort((a,b)=>b.minutes-a.minutes||a.name.localeCompare(b.name));modelSteam.token=data.token;dialog.querySelector('form').hidden=true;dialog.querySelector('[data-steam-change]').hidden=true;modelSteam.limit=10;modelSteam.selection.clear();for(const g of modelSteam.preview.games){const linked=games.find(x=>x.steam?.account===modelSteam.preview.steamId&&x.steam?.appId===g.appId);modelSteam.selection.set(g.appId,linked?.id||'');}dialog.querySelector('#steam-preview').hidden=false;dialog.querySelector('[type=submit]').disabled=false;dialog.querySelector('#steam-status').textContent=modelSteam.preview.games.length?'Игры найдены. Проверьте выбор и сопоставление.':'В доступной библиотеке нет игр.';rows();}
   if(data.steamImported){window.applyNativeSnapshot(data.snapshot);dialog.close();notify('Импортировано из Steam: '+data.steamImported);}
 };
 window.chrome.webview.addEventListener('message',e=>window.applySteamMessage(e.data));
}
