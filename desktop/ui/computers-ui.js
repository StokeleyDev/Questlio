if(window.gaidenBootstrap){
const modelHardware=GaidenUI.moduleState('hardware',{hardware:null,busy:false});
 
 const fields=[['windows','Windows'],['architecture','Архитектура'],['processor','Процессор'],['graphics','Видеокарта'],['ram','Оперативная память (RAM)'],['motherboard','Материнская плата']];
 GaidenUI.screens.computer=()=>heading('Мой компьютер')+`<section id="hardware-info" class="panel settings-panel hardware-panel"><div class="section-heading"><h2>Характеристики</h2><button class="text-button" id="refresh-hardware" ${modelHardware.busy?'disabled':''}>${modelHardware.busy?'Определение…':'Обновить'}</button></div><p class="muted hardware-intro">Характеристики определяются автоматически в Windows.</p><div class="inset-group hardware-list">${fields.map(([key,label])=>{const value=modelHardware.hardware?.[key];return `<div class="hardware-row"><span class="hardware-label">${label}</span><span class="hardware-value${value?'':' is-empty'}">${escapeHTML(value||(modelHardware.busy?'Определяется…':'Не удалось определить'))}</span></div>`;}).join('')}</div></section>`;
 function refresh(){if(modelHardware.busy)return;modelHardware.busy=true;if(view==='computer')render();window.chrome.webview.postMessage({action:'hardware'});}
 window.chrome.webview.addEventListener('message',e=>{if(!e.data.hardware)return;modelHardware.hardware=e.data.hardware;modelHardware.busy=false;if(view==='computer')render();});
 document.addEventListener('click',e=>{if(e.target.closest('#refresh-hardware'))refresh();});refresh();
}
