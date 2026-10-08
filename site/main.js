(function(){
  // Reduced motion: pause the hero video.
  var rm = window.matchMedia && matchMedia('(prefers-reduced-motion: reduce)').matches;
  if (rm) document.querySelectorAll('video').forEach(function(v){ v.removeAttribute('autoplay'); v.pause(); });

  // The jump box: substring or initials, like Sholto's own search.
  var targets = [
    ['Drop the vocal. Keep the drums.','#stems','stems'],
    ['Search that fits your mix','#search','Space'],
    ['Build tonight’s set while you search','#tracklist','Ctrl L'],
    ['See the whole song, phrase by phrase','#sections','DROP 16'],
    ['Scratch, backspin, brake','#platter','jog'],
    ['Beats that snap together','#snap','sync bpm'],
    ['Your controller, drawn on screen','#controller','FLX4'],
    ['Make it yours','#themes','themes'],
    ['One command','#install','install'],
    ['Before you install','#faq','faq']
  ];
  var input = document.getElementById('jump'), hits = document.getElementById('hits'), sel = 0, shown = [];
  function norm(s){ return s.toLowerCase().normalize('NFD').replace(/[^a-z0-9 ]/g,''); }
  function initials(s){ return norm(s).split(/\s+/).filter(Boolean).map(function(w){return w[0];}).join(''); }
  function render(){
    var q = norm(input.value).trim();
    if (!q){ hits.hidden = true; return; }
    shown = targets.filter(function(t){
      var hay = norm(t[0]+' '+t[2]);
      return q.split(/\s+/).every(function(w){ return hay.indexOf(w) >= 0 || initials(t[0]).indexOf(w) === 0; });
    });
    sel = 0;
    hits.innerHTML = shown.length ? shown.map(function(t,i){
      return '<li role="option" aria-selected="'+(i===sel)+'"><a href="'+t[1]+'"><span class="fit"></span>'+t[0]+'<small>'+t[2]+'</small></a></li>';
    }).join('') : '<li style="padding:8px 10px;color:var(--muted);font-size:14px">Nothing matches. Try “stems” or “install”.</li>';
    hits.hidden = false;
  }
  function mark(){ Array.prototype.forEach.call(hits.children,function(li,i){ li.setAttribute('aria-selected', String(i===sel)); }); }
  input.addEventListener('input', render);
  input.addEventListener('keydown', function(e){
    if (e.key==='ArrowDown' && shown.length){ sel=(sel+1)%shown.length; mark(); e.preventDefault(); }
    else if (e.key==='ArrowUp' && shown.length){ sel=(sel-1+shown.length)%shown.length; mark(); e.preventDefault(); }
    else if (e.key==='Enter'){ var t = shown[sel] || (input.value.trim()==='' ? targets[8] : null); if (t){ location.hash = t[1].slice(1); hits.hidden=true; input.blur(); } e.preventDefault(); }
    else if (e.key==='Escape'){ input.value=''; hits.hidden=true; input.blur(); }
  });
  input.addEventListener('blur', function(){ setTimeout(function(){ hits.hidden = true; }, 150); });
  document.addEventListener('keydown', function(e){
    var tag = (e.target.tagName||'').toLowerCase();
    if (e.code==='Space' && tag!=='input' && tag!=='textarea' && tag!=='button' && tag!=='summary' && tag!=='a'){ e.preventDefault(); input.focus(); }
  });


  // Below-the-fold videos: fetch and play only when scrolled near, never for reduced motion.
  var lazy=document.querySelectorAll('video[data-lazyplay]');
  if (lazy.length && 'IntersectionObserver' in window && !matchMedia('(prefers-reduced-motion: reduce)').matches){
    var vio=new IntersectionObserver(function(es){ es.forEach(function(e){
      if (e.isIntersecting){ var p=e.target.play(); if (p&&p.catch) p.catch(function(){}); }
      else e.target.pause();
    }); },{rootMargin:'200px 0px'});
    lazy.forEach(function(v){ vio.observe(v); });
  }

  // Theme picker: swap the one big image.
  var timg=document.getElementById('themeimg');
  document.querySelectorAll('.tchip').forEach(function(b){
    b.addEventListener('click',function(){
      document.querySelectorAll('.tchip').forEach(function(o){o.setAttribute('aria-selected','false');});
      b.setAttribute('aria-selected','true'); timg.src=b.dataset.src; timg.alt='Two decks playing in the '+b.textContent+' theme';
    });
  });

  // Copy the install command.
  var btn = document.getElementById('copybtn'), pre = document.getElementById('cmd');
  var text = 'curl -fsSL https://raw.githubusercontent.com/freedomfirst26/Sholto/main/get-sholto.sh | bash';
  function selectIt(){ var r=document.createRange(); r.selectNodeContents(pre); var s=getSelection(); s.removeAllRanges(); s.addRange(r); }
  btn.addEventListener('click', function(){
    function ok(){ btn.classList.add('done'); btn.firstChild.textContent='Copied'; setTimeout(function(){ btn.classList.remove('done'); btn.firstChild.textContent='Copy'; },1800); }
    try { navigator.clipboard.writeText(text).then(ok, function(){ selectIt(); btn.firstChild.textContent='Selected'; }); }
    catch(_) { selectIt(); btn.firstChild.textContent='Selected'; }
  });

  // Latest release tag: plain text stays if the request fails.
  fetch('https://api.github.com/repos/freedomfirst26/Sholto/releases/latest',{headers:{Accept:'application/vnd.github+json'}})
    .then(function(r){ return r.ok ? r.json() : Promise.reject(); })
    .then(function(d){
      if (!d || !/^[\w.+-]{1,32}$/.test(d.tag_name||'')) return;
      var tag = d.tag_name.replace(/^sholto-/,'');
      document.querySelectorAll('[data-version]').forEach(function(e){ e.textContent = tag; });
      document.querySelectorAll('[data-version-link]').forEach(function(e){ e.textContent = 'Release ' + tag; });
    })
    .catch(function(){});
})();
