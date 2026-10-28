if(window.gaidenBootstrap){
const modelSettings=GaidenUI.moduleState('settings',{openGroups:new Map()});
 
 GaidenUI.layouts.settings=html=>{
   const template=document.createElement('template');template.innerHTML=html;
   const group=document.createElement('div');group.className='settings-groups';
   const descriptions={'Внешний вид':'Тема, движение и прозрачность','Эмуляторы':'Программы для запуска ROM','Резервные копии':'Сохранение и восстановление библиотеки'};
   template.content.querySelectorAll('section.settings-panel').forEach((section,index)=>{
     const title=section.querySelector('h2');const name=title?.textContent||'Настройки';title?.remove();
     const details=document.createElement('details');details.className='settings-group';details.dataset.group=name;details.open=modelSettings.openGroups.get(name)??index===0;
     const summary=document.createElement('summary');const text=document.createElement('span');const strong=document.createElement('strong');strong.textContent=name;const small=document.createElement('small');small.textContent=descriptions[name]||'';text.append(strong,small);const chevron=document.createElement('span');chevron.className='settings-chevron';chevron.textContent='›';chevron.setAttribute('aria-hidden','true');summary.append(text,chevron);
     const body=document.createElement('div');body.className='settings-group-body';while(section.firstChild)body.append(section.firstChild);
     details.append(summary,body);section.append(details);group.append(section);
   });
   template.content.append(group);const result=document.createElement('div');result.append(template.content);return GaidenUI.components.settingsExtras(result.innerHTML);
 };
 document.addEventListener('toggle',e=>{if(e.target.matches?.('details.settings-group'))modelSettings.openGroups.set(e.target.dataset.group,e.target.open);},true);

}

