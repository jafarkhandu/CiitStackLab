(function () {
    function initDrawer() {
        const sidebar=document.getElementById('ciit-learning-global-sidebar');
        const toggle=document.querySelector('[data-learn-mobile-menu-toggle]');
        const backdrop=document.querySelector('[data-learn-drawer-backdrop]');
        if(!sidebar||!toggle||!backdrop)return;
        sidebar.setAttribute('tabindex','-1');
        const focusable=()=>Array.from(sidebar.querySelectorAll('a[href],button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex="-1"])'))
            .filter(item=>!item.hasAttribute('hidden')&&item.getClientRects().length>0);
        const close=restore=>{
            document.body.classList.remove('learn-drawer-open');backdrop.hidden=true;
            toggle.setAttribute('aria-expanded','false');toggle.setAttribute('aria-label','Open navigation menu');toggle.title='Open navigation menu';
            const label=toggle.querySelector('.visually-hidden');if(label)label.textContent='Open navigation menu';
            if(restore!==false)toggle.focus();
        };
        const open=()=>{
            backdrop.hidden=false;document.body.classList.add('learn-drawer-open');
            toggle.setAttribute('aria-expanded','true');toggle.setAttribute('aria-label','Close navigation menu');toggle.title='Close navigation menu';
            const label=toggle.querySelector('.visually-hidden');if(label)label.textContent='Close navigation menu';
            (focusable()[0]||sidebar).focus();
        };
        toggle.addEventListener('click',()=>toggle.getAttribute('aria-expanded')==='true'?close(true):open());
        backdrop.addEventListener('click',()=>close(true));
        sidebar.addEventListener('click',event=>{if(event.target.closest('a[href]'))close(false);});
        document.addEventListener('keydown',event=>{
            if(!document.body.classList.contains('learn-drawer-open'))return;
            if(event.key==='Escape'){event.preventDefault();close(true);return;}
            if(event.key!=='Tab')return;
            const items=focusable();if(!items.length){event.preventDefault();sidebar.focus();return;}
            const first=items[0],last=items[items.length-1];
            if(event.shiftKey&&(document.activeElement===first||!sidebar.contains(document.activeElement))){event.preventDefault();last.focus();}
            else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first.focus();}
        });
        window.addEventListener('resize',()=>{if(window.matchMedia('(min-width: 768px)').matches&&document.body.classList.contains('learn-drawer-open'))close(false);});
    }
    function initDisclosure(){
        const panel=document.querySelector('[data-learn-path-disclosure]');if(!panel)return;
        const desktop=window.matchMedia('(min-width: 1024px)'),sync=()=>{panel.open=desktop.matches;};
        sync();if(desktop.addEventListener)desktop.addEventListener('change',sync);else desktop.addListener(sync);
    }
    function initSubtopics(){
        const notes=document.querySelector('.ciit-learning-notes-content'),nav=document.querySelector('[data-learning-subtopics]');
        if(!notes||!nav)return;
        const headings=Array.from(notes.querySelectorAll('h2'));if(!headings.length){nav.hidden=true;return;}
        const rows=headings.map((heading,index)=>{
            if(!heading.id){const base='learn-section-'+(index+1);let id=base,suffix=2;while(document.getElementById(id))id=base+'-'+suffix++;heading.id=id;}
            const link=document.createElement('a');link.href='#'+heading.id;
            const badge=document.createElement('span');badge.className='learn-subtopic-number';badge.textContent=String(index+1);
            const text=document.createElement('span');text.textContent=heading.textContent.trim().replace(/\s+/g,' ');
            link.append(badge,text);nav.appendChild(link);return{heading,link};
        });
        nav.hidden=false;
        const activate=selected=>rows.forEach(row=>{
            const active=row===selected;row.link.classList.toggle('active',active);
            if(active)row.link.setAttribute('aria-current','location');else row.link.removeAttribute('aria-current');
        });
        if('IntersectionObserver'in window){
            const observer=new IntersectionObserver(items=>{
                const visible=items.filter(x=>x.isIntersecting).sort((a,b)=>a.boundingClientRect.top-b.boundingClientRect.top)[0];
                if(!visible)return;const selected=rows.find(row=>row.heading===visible.target);if(selected)activate(selected);
            },{root:null,rootMargin:'-14% 0px -70% 0px',threshold:0});
            headings.forEach(h=>observer.observe(h));
        }else activate(rows[0]);
    }
    function initCopyButtons(){
        document.querySelectorAll('.ciit-learning-notes-content pre').forEach(pre=>{
            if(pre.querySelector('.learn-code-copy'))return;
            const button=document.createElement('button');button.type='button';button.className='learn-code-copy';button.textContent='Copy';button.setAttribute('aria-label','Copy code example');
            button.addEventListener('click',async()=>{
                const clone=pre.cloneNode(true);clone.querySelector('.learn-code-copy')?.remove();
                const text=(clone.querySelector('code')||clone).textContent.trimEnd();
                const fallback=()=>{
                    const area=document.createElement('textarea');area.value=text;area.setAttribute('readonly','');
                    area.style.position='fixed';area.style.opacity='0';document.body.appendChild(area);area.select();
                    let copied=false;try{copied=document.execCommand('copy');}finally{area.remove();}return copied;
                };
                let copied=false;
                if(navigator.clipboard&&window.isSecureContext){try{await navigator.clipboard.writeText(text);copied=true;}catch(error){copied=fallback();}}
                else copied=fallback();
                button.textContent=copied?'Copied':'Copy failed';
                if(copied)window.setTimeout(()=>{if(button.isConnected)button.textContent='Copy';},1400);
            });
            pre.appendChild(button);
        });
    }
    function wrapTables(){
        document.querySelectorAll('.ciit-learning-notes-content table').forEach(table=>{
            if(table.parentElement?.classList.contains('learn-table-scroll'))return;
            const wrapper=document.createElement('div');wrapper.className='learn-table-scroll';
            table.parentNode.insertBefore(wrapper,table);wrapper.appendChild(table);
        });
    }
    function init(){initDrawer();initDisclosure();initSubtopics();initCopyButtons();wrapTables();}
    if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',init,{once:true});else init();
})();
