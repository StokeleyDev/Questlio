if(window.gaidenBootstrap){
const modelBackups=GaidenUI.moduleState('backups',{backups:[],warning:''});
 GaidenUI.settings.backups=()=>{
  const actions='<div class="backup-actions"><button class="primary" data-backup="backupCreate">Создать копию</button><button class="glass-button" data-backup="backupRestore">Восстановить…</button><button class="icon-button" type="button" data-backup-menu aria-label="Другие действия с копиями">…</button><button id="native-import" type="button" hidden>Импорт библиотеки</button><button type="button" data-backup="backupFolder" hidden>Открыть папку</button><button type="button" data-backup="backupList" hidden>Обновить</button></div>';
  const list=modelBackups.backups.length
   ?'<div class="inset-group backup-list">'+modelBackups.backups.slice(0,20).map(b=>`<div class="backup-row"><div class="backup-copy"><strong>${escapeHTML(b.kind)}</strong><small>${escapeHTML(GaidenUI.formatDate(b.date))}</small></div><span class="backup-size">${Math.max(1,Math.round(b.bytes/1024))}<small> КБ</small></span></div>`).join('')+'</div>'
   :GaidenKit.empty('Копий пока нет','Создайте первую копию, чтобы сохранить библиотеку и историю.','Создать копию','backups').replace('class="ui-empty"','class="ui-empty backup-empty"').replace('class="primary"','class="text-button"').replace('data-ui-empty="backups"','data-backup-empty="backupCreate"');
  return `<section class="panel settings-panel" id="backup-panel"><h2>Резервные копии</h2><p class="muted">Пять последних автоматических копий. Ручные и страховочные копии хранятся отдельно.</p>${actions}${modelBackups.warning?`<p role="alert">${escapeHTML(modelBackups.warning)}</p>`:''}${list}<p class="muted backup-note">Копия содержит библиотеку, историю и настройки базы. Настройки оформления сохраняются отдельно. При восстановлении текущая база сохраняется отдельно.</p></section>`;
 };

 
 document.addEventListener('click',e=>{const menu=e.target.closest('[data-backup-menu]');if(menu){GaidenKit.menu(menu,[{label:'Открыть папку',run:()=>window.chrome.webview.postMessage({action:'backupFolder'})},{label:'Импорт библиотеки',run:()=>document.querySelector('#native-import').click()},{label:'Обновить',run:()=>window.chrome.webview.postMessage({action:'backupList'})}]);return;}const empty=e.target.closest('[data-backup-empty]');if(empty){window.chrome.webview.postMessage({action:empty.dataset.backupEmpty});return;}const button=e.target.closest('[data-backup]');if(button)window.chrome.webview.postMessage({action:button.dataset.backup});});
 window.chrome.webview.addEventListener('message',e=>{if(e.data.backups){modelBackups.backups=e.data.backups;modelBackups.warning='';if(view==='settings')render();if(e.data.backupMessage)notify(e.data.backupMessage);}if(e.data.backupWarning){modelBackups.warning=e.data.backupWarning;if(view==='settings')render();notify(modelBackups.warning);}});
 window.chrome.webview.postMessage({action:'backupList'});
}
