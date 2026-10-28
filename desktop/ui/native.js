// Native SQLite is authoritative for games; preferences remain in the WebView profile.
if (window.gaidenBootstrap && window.chrome?.webview) {

 window.chrome.webview.addEventListener('message',e=>{
   if(typeof e.data.windowState==='string'){window.GaidenMotion?.pause('minimized');if(e.data.windowState==='normal')window.GaidenMotion?.resume('minimized');return;}
   if(e.data.coverReady||e.data.coverLoading||e.data.coverMissing||typeof e.data.coverPreference==='boolean'){window.GaidenCovers?.message(e.data);return;}
   if(e.data.revealed||e.data.launched||e.data.launchError||e.data.detailSaved){window.GaidenDetail?.message(e.data);return;}
   if(e.data.featuresUpdated||e.data.featureNotice)return;
   if(e.data.processList||e.data.processError)return;
   if(e.data.steamPreview||e.data.steamError||e.data.steamImported)return;
   if(e.data.coverEdited){window.applyCoverSnapshot(e.data);return;}
   if(e.data.inlineEdited){window.applyExeSnapshot(e.data);return;}

   if(e.data.backups||e.data.backupWarning||e.data.emulatorFile)return;
   if(e.data.hardware)return;
   if(e.data.emulatorSaved){window.applyNativeSnapshot(e.data.snapshot);notify('Эмулятор настроен');return;}
   if(e.data.edited){const dialog=document.querySelector('#detail-dialog');if(dialog.open&&(!e.data.deleted||(dialog.dataset.gameId||dialog.querySelector('#save-game')?.dataset.id)===e.data.id))dialog.close();window.applyNativeSnapshot(e.data.snapshot);if(!e.data.deleted)openDetail(e.data.id);if(e.data.deleted)GaidenKit.deletions.ack(e.data.id);else notify('Изменения сохранены');return;}
   if(e.data.exeSelected){document.querySelector('#selected-exe').textContent=e.data.exeSelected;document.querySelector('#selected-exe').dataset.selected='true';const input=document.querySelector('#add-form [name="name"]');if(!input.value.trim())input.value=e.data.suggestedName;window.GaidenAdd?.validate();return;}
   if(e.data.created){window.applyNativeSnapshot(e.data.snapshot);document.querySelector('#add-form').reset();document.querySelector('#selected-exe').textContent='Файл не выбран';delete document.querySelector('#selected-exe').dataset.selected;document.querySelector('#add-dialog').close();navigate('library');notify('Игра добавлена');return;}
   if(e.data.saved) notify('Изменения сохранены в базе');
   else { storageAvailable=false; notify('Ошибка сохранения: '+e.data.error); }
 });
 const addForm=document.querySelector('#add-form');
 const exeButton=addForm.querySelector('.form-section button');
 exeButton.onclick=()=>window.chooseGameExe({action:'pickNewExe'});
 const exeLabel=document.querySelector('#selected-exe');
 addForm.addEventListener('submit',e=>{e.preventDefault();e.stopImmediatePropagation();if(addForm.elements.platform.value==='PC'&&!exeLabel.dataset.selected){notify('Выберите EXE игры');return;}const name=addForm.elements.name.value.trim();if(!name)return;window.chrome.webview.postMessage({action:'createGame',name,platform:addForm.elements.platform.value});},true);
 document.addEventListener('click',e=>{if(e.target.closest('#native-import'))window.chrome.webview.postMessage({action:'import'});});
 window.applyCoverSnapshot=data=>{window.applyNativeSnapshot(data.snapshot);const g=games.find(g=>g.id===data.id);const artBox=document.querySelector('#detail-dialog[open] .detail-art');if(g&&artBox)window.GaidenDetail.refreshArt(g,artBox);notify('Обложка обновлена');};
 window.applyExeSnapshot=data=>{window.applyNativeSnapshot(data.snapshot);const g=games.find(g=>g.id===data.id);const old=document.querySelector('#detail-dialog[open] .exe-binding');if(g&&old){const next=createExeSection(g,g.id);next.open=old.open;old.replaceWith(next);window.GaidenDetail?.refreshLaunch(g);next.querySelector('.exe-add')?.focus();}notify('Файлы игры обновлены');};
 document.addEventListener('click',e=>{if(e.target.closest('#save-game')){const field=document.querySelector('#edit-game-name');const name=field?.value.trim();if(!name){e.stopImmediatePropagation();notify('Введите название игры');return;}const g=games.find(g=>g.id===e.target.closest('#save-game').dataset.id);if(g)g.name=name;}},true);
 window.showTrackerState=state=>{
   const block=document.querySelector('.tracker');
   const object=state&&typeof state==='object';
   const name=object?state.name:null;
   const active=object?state.active!==false:state!=='Трекер остановлен';
   const error=!object&&state&&state!=='Трекер остановлен'&&state!=='Ожидание игры'?state:null;
   block.classList.toggle('is-playing',!!name);
   block.classList.toggle('is-active',!!name||active);
   const label=block.querySelector('.tracker-label'),separator=block.querySelector('.tracker-sep');
   if(label){label.textContent=name?'Сейчас играет':'';label.hidden=!name;}
   if(separator)separator.textContent=name?' · ':'';
   block.querySelector('strong').textContent=name||error||(active?'Отслеживание активно':'Трекер приостановлен');
   block.title=name||'';
 };
 window.applyNativeSnapshot=value=>GaidenUI.commitSnapshot(value);
}
