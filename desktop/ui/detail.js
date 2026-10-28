function createExeSection(g,id){
     const section=document.createElement('details');section.className='form-section exe-binding detail-section exe-group';section.style.marginTop='24px';
     const summary=document.createElement('summary');summary.textContent='Файлы игры';section.append(summary);const header=document.createElement('div');header.className='exe-heading';
     const title=document.createElement('h3');title.textContent='Файлы игры';
     const button=document.createElement('button');button.type='button';button.className='exe-add';button.innerHTML='<svg aria-hidden="true"><use href="icons.svg#plus"/></svg> Добавить EXE';
     button.onclick=()=>{window.chooseGameExe({action:'bind',id});};
     header.append(title,button);section.append(header);
     const description=document.createElement('p');description.className='exe-description';description.textContent='По этим файлам Questlio определяет запуск игры.';section.append(description);
     const list=document.createElement('div');list.className='exe-list';
     const exes=g.executables||g.legacy?.executableName?.split(',').map(x=>x.trim()).filter(Boolean)||[];
     for(const exe of exes){const row=document.createElement('div');row.className='exe-file';
       const glyph=document.createElement('span');glyph.className='exe-file-icon';glyph.innerHTML='<svg viewBox="0 0 24 24" aria-hidden="true"><rect x="3" y="4" width="18" height="16" rx="4"/><path d="M3 9h18M7 6.5h.01M10 6.5h.01m-2 7 3 2-3 2m5 0h3"/></svg>';
       const info=document.createElement('div');info.className='exe-file-info';const text=document.createElement('strong');text.textContent=exe.replace(/\.exe$/i,'')+'.exe';const hint=document.createElement('small');hint.textContent='Исполняемый файл';info.append(text,hint);
       const remove=document.createElement('button');remove.type='button';remove.className='exe-remove';remove.innerHTML='<svg aria-hidden="true"><use href="icons.svg#minus"/></svg>';remove.title='Убрать файл';remove.setAttribute('aria-label','Убрать '+text.textContent);remove.onclick=()=>window.chrome.webview.postMessage({action:'removeExe',id,exe});row.append(glyph,info,remove);list.append(row);
     }
     if(!exes.length){const empty=document.createElement('p');empty.className='exe-empty';empty.textContent='Добавьте EXE, чтобы включить автотрекинг игры.';list.append(empty);}
     section.append(list);
     return section;
 }
GaidenUI.detail.build=g=>{
 const id=g.id,host=document.querySelector('#detail-content');
 const info=`<section class="detail-section" aria-label="Статус и заметки"><label for="game-status">Статус прохождения</label><select id="game-status">${Object.entries(statusNames).filter(([key])=>key!=='forever'||g.status==='forever').map(([key,label])=>`<option value="${key}" ${g.status===key?'selected':''}>${label}</option>`).join('')}</select><div class="detail-notes-heading"><label for="game-notes">Заметки</label><span id="detail-saved" role="status">Сохранено</span></div><textarea id="game-notes" maxlength="3000" placeholder="Что запомнилось в игре?">${escapeHTML(g.notes||'')}</textarea></section>`;
 host.innerHTML=`<div class="detail-art">${media(g,'detail','eager')}<button type="button" class="detail-more icon-button" aria-label="Действия с игрой"><svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="6" cy="12" r="1.4" fill="currentColor"/><circle cx="12" cy="12" r="1.4" fill="currentColor"/><circle cx="18" cy="12" r="1.4" fill="currentColor"/></svg></button><button class="close-dialog icon-button" aria-label="Закрыть">×</button></div><div class="detail-body"><div class="detail-title-wrap"><h2 id="detail-title" class="detail-title" aria-label="${escapeHTML(g.name)}"><input id="edit-game-name" aria-label="Название игры" maxlength="80" required></h2><button type="button" class="detail-edit-marker icon-button" aria-label="Изменить название игры" title="Изменить название игры"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg></button></div><span class="eyebrow">${escapeHTML(g.platform)} · ${escapeHTML(g.genre||'Библиотека')}</span><div class="detail-launch"></div><div class="detail-stat"><div class="detail-stat-time"><small>Время в игре</small><div class="detail-value"><strong>${fmt(g.minutes*60)}</strong><small class="detail-source"> · в Questlio</small></div>${g.steam?`<small class="detail-steam-time">${fmt(g.steam.minutes*60)} · в Steam</small>`:''}</div><div><small>Сессий</small><strong>${g.sessions||0}</strong></div><div><small>Последний запуск</small><strong>${GaidenDetail.lastPlayed(g)}</strong></div></div>${info}</div>`;
 host.querySelector('#edit-game-name').value=g.name;
 const body=host.querySelector('.detail-body');if(g.platform==='PC')body.append(createExeSection(g,id));
 body.append(...GaidenUI.detail.featureSections(g));
 GaidenDetail.bind(g,host);
};
