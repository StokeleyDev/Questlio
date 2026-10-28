if(window.gaidenBootstrap){
const modelProcesses=GaidenUI.moduleState('processes',{target:null,entries:[],limit:30,loading:false});
 const picker=document.createElement('dialog');picker.id='process-dialog';picker.setAttribute('aria-labelledby','process-title');document.body.append(picker);
 
 const post=data=>window.chrome.webview.postMessage(data);
 function draw(){
   const list=picker.querySelector('#process-results');if(!list)return;
   const query=(picker.querySelector('#process-search')?.value||'').trim().toLocaleLowerCase();
   const filtered=modelProcesses.entries.filter(x=>(x.name+' '+x.title+' '+x.pid).toLocaleLowerCase().includes(query));
   list.replaceChildren();list.classList.toggle('has-rows',filtered.length>0);
   for(const item of filtered.slice(0,modelProcesses.limit)){
     const row=document.createElement('button');row.type='button';row.className='process-row';row.disabled=!item.available;row.dataset.processToken=item.token;
     const info=document.createElement('span');info.className='process-info';const name=document.createElement('strong');name.textContent=item.name;const hint=document.createElement('small');hint.textContent=(item.title?item.title+' · ':'')+'PID '+item.pid+(item.available?'':' · EXE недоступен');info.append(name,hint);
     const memory=document.createElement('span');memory.className='process-memory';const amount=document.createElement('strong');amount.textContent=new Intl.NumberFormat((QuestlioLocale.language==='en'?'en-US':'ru-RU'),{maximumFractionDigits:0}).format(item.memory/1024/1024);const unit=document.createElement('small');unit.textContent=' МБ';memory.append(amount,unit);row.append(info,memory);list.append(row);
   }
   picker.querySelector('#process-count').textContent='Показано '+Math.min(modelProcesses.limit,filtered.length)+' из '+filtered.length;
   picker.querySelector('[data-process-more]').hidden=modelProcesses.limit>=filtered.length;
   if(!filtered.length){const empty=document.createElement('div');empty.className='ui-empty compact';empty.setAttribute('role','status');empty.innerHTML=`<h2>${query?'Процессы не найдены':'Список пока пуст'}</h2><p class="muted">${query?'Измените запрос или сбросьте поиск.':'Обновите список или выберите файл вручную.'}</p><button class="text-button" type="button" ${query?'data-process-reset':'data-process-refresh'}>${query?'Сбросить поиск':'Обновить список'}</button>`;list.append(empty);}
 }
 function load(){if(modelProcesses.loading)return;modelProcesses.loading=true;picker.querySelector('#process-status').textContent='Загружаем процессы…';picker.querySelector('[data-process-refresh]').disabled=true;post({action:'processList'});}
 window.chooseGameExe=action=>{
   modelProcesses.target=action;modelProcesses.entries=[];modelProcesses.limit=30;modelProcesses.loading=false;
   picker.innerHTML=`<div class="process-dialog-body"><div class="dialog-heading"><h2 id="process-title">Выбрать EXE</h2><button type="button" class="icon-button" data-process-close aria-label="Закрыть">×</button></div><div class="process-sources"><button type="button" class="primary" data-process-load>Из работающих процессов</button><button type="button" class="glass-button" data-process-file>Выбрать файл…</button></div><div id="process-panel" hidden><p class="muted">По занятой оперативной памяти — от большего к меньшему. Недоступный EXE можно выбрать вручную.</p><div class="process-tools"><input type="search" id="process-search" aria-label="Найти процесс" placeholder="Имя процесса или PID"><button type="button" class="text-button" data-process-refresh>Обновить</button></div><p id="process-status" class="muted" role="status"></p><div id="process-results"></div><div class="session-pagination"><span id="process-count" class="muted"></span><button type="button" class="glass-button" data-process-more>Показать ещё</button></div></div></div>`;
   picker.querySelector('#process-search').oninput=()=>{modelProcesses.limit=30;draw();};
   if(!picker.open)picker.showModal();
 };
 picker.addEventListener('close',()=>{if(picker.open)return;modelProcesses.target=null;modelProcesses.entries=[];});
 picker.addEventListener('click',e=>{
   if(e.target.closest('[data-process-close]')){picker.close();return;}
   if(e.target.closest('[data-process-file]')){const action=modelProcesses.target;picker.close();if(action)post(action);return;}
   if(e.target.closest('[data-process-load]')){picker.querySelector('#process-panel').hidden=false;load();return;}
   if(e.target.closest('[data-process-refresh]')){load();return;}
   if(e.target.closest('[data-process-reset]')){const search=picker.querySelector('#process-search');search.value='';modelProcesses.limit=30;draw();search.focus();return;}
   if(e.target.closest('[data-process-more]')){modelProcesses.limit+=30;draw();return;}
   const row=e.target.closest('[data-process-token]');if(row&&modelProcesses.target&&!modelProcesses.loading){modelProcesses.loading=true;picker.querySelector('#process-status').textContent='Привязываем EXE…';post({action:'processSelect',token:row.dataset.processToken,gameId:modelProcesses.target.id||null});}
 });
 window.applyProcessMessage=data=>{
   if(!picker.open)return;
   if(data.processList){modelProcesses.loading=false;modelProcesses.entries=[...data.processList].sort((a,b)=>b.memory-a.memory||a.name.localeCompare(b.name));modelProcesses.limit=30;picker.querySelector('#process-status').textContent='Выберите программу из списка.';picker.querySelector('[data-process-refresh]').disabled=false;draw();}
   if(data.processError){modelProcesses.loading=false;picker.querySelector('#process-status').textContent=data.processError;picker.querySelector('[data-process-refresh]').disabled=false;}
   if(data.exeSelected||data.inlineEdited)picker.close();
 };
 window.chrome.webview.addEventListener('message',e=>window.applyProcessMessage(e.data));
 window.openAddFromTray=()=>{
   const dialog=document.querySelector('#add-dialog');
   if(!dialog.open){document.querySelector('#add-form').reset();const file=document.querySelector('#selected-exe');file.textContent='Файл не выбран';delete file.dataset.selected;dialog.showModal();}
   window.chooseGameExe({action:'pickNewExe'});
 };
}
