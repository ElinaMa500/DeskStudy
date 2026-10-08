# Builds the landing page in Chinese and English: docs/index.html and docs/en/index.html.
#   python tools/build_site.py            -> writes into docs/
#   python tools/build_site.py <folder>   -> writes into another folder (for a preview; copy docs/images there too)
# The "looks" section (working notebooks in five layouts) lives in site_notebooks.py.
# For a new release, change DOWNLOAD and the version in final_small (both languages).
import os, sys, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from site_notebooks import NB_CSS, NB_JS, nb_text

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), 'docs')
DOWNLOAD = "https://github.com/ElinaMa500/DeskStudy/releases/download/v1.6.1/DeskStudy-1.6.1-Windows.zip"
REPO = "https://github.com/ElinaMa500/DeskStudy"

CSS = r"""
:root{--bg:#fffdf8;--ink:#1a1a1a;--sub:#6b6760;--mute:#9a958c;--line:#ece7dc;--card:#fff;
--accent:#e8643c;--accent-soft:#ffd7c6;--green:#2f6f53;--mint:#d9f2e6;--yellow:#fff3b0;--lilac:#dfe5ff;--peach:#ffe4d6;
--sans:"Segoe UI Variable Display","Segoe UI","PingFang SC","Microsoft YaHei UI","Microsoft YaHei",system-ui,sans-serif}
*{box-sizing:border-box}
html{scroll-behavior:smooth}
body{margin:0;background:var(--bg);color:var(--ink);font-family:var(--sans);-webkit-font-smoothing:antialiased;overflow-x:hidden}
a{color:inherit}
img{display:block;max-width:100%;height:auto}
.wrap{max-width:1200px;margin:0 auto;padding:0 32px}
.bgs{position:absolute;left:0;right:0;top:-72px;bottom:-240px;overflow:hidden;pointer-events:none;z-index:0}
.blob{position:absolute;border-radius:50%;filter:blur(70px);opacity:.5;pointer-events:none;z-index:0}

/* nav */
.nav{position:sticky;top:0;z-index:50;transition:background .25s,box-shadow .25s}
.nav.scrolled{background:rgba(255,253,248,.85);backdrop-filter:blur(14px);box-shadow:0 1px 0 var(--line)}
.nav .wrap{display:flex;align-items:center;justify-content:space-between;height:72px}
.brand{display:flex;gap:10px;align-items:center;font-weight:800;font-size:17px;text-decoration:none}
.brand i{width:22px;height:22px;border-radius:7px;background:var(--accent);transition:transform .4s cubic-bezier(.3,1.6,.5,1)}
.brand:hover i{transform:rotate(-12deg) scale(1.1)}
.nav nav{display:flex;gap:26px;align-items:center;font-size:15px;color:var(--sub)}
.nav nav a{text-decoration:none;transition:color .2s}
.nav nav a:hover{color:var(--ink)}
.nav .lang{border:1.5px solid var(--line);border-radius:99px;padding:5px 12px;white-space:nowrap}
.nav .get{background:var(--ink);color:#fff!important;border-radius:99px;padding:8px 16px;font-weight:700}

/* buttons */
.btn{display:inline-flex;align-items:center;gap:10px;background:var(--ink);color:#fff;font-weight:750;font-size:17px;padding:17px 30px;border-radius:14px;text-decoration:none;
 box-shadow:0 6px 0 var(--accent);transition:transform .15s,box-shadow .15s}
.btn:hover{transform:translateY(-2px);box-shadow:0 8px 0 var(--accent)}
.btn:active{transform:translateY(4px);box-shadow:0 2px 0 var(--accent)}
.btn svg{width:20px;height:20px}

/* hero */
.hero{position:relative;min-height:min(calc(100vh - 72px),820px);display:flex;align-items:center;padding:20px 0 60px}
.hero .wrap{position:relative;z-index:1;display:grid;grid-template-columns:1fr 1.25fr;gap:24px;align-items:center;width:100%}
h1{font-size:clamp(40px,4.9vw,68px);font-weight:900;letter-spacing:-.035em;line-height:1.12;margin:0}
em.mark{font-style:normal;color:var(--accent);position:relative;z-index:0;white-space:nowrap}
em.mark::after{content:"";position:absolute;left:-2px;right:-2px;bottom:.08em;height:.2em;background:var(--accent-soft);z-index:-1;border-radius:4px;transform-origin:left;animation:swipe .8s .5s cubic-bezier(.6,0,.2,1) both}
@keyframes swipe{from{transform:scaleX(0)}}
.hero p.lead{font-size:21px;color:var(--sub);line-height:1.6;margin:22px 0 34px}
.facts{margin-top:28px;display:flex;flex-wrap:wrap;gap:8px 18px;color:var(--mute);font-size:14px}
.facts span+span::before{content:"·";margin-right:18px}
.art{position:relative;aspect-ratio:820/760;width:100%}
.w{position:absolute;touch-action:none;user-select:none;-webkit-user-select:none;transition:box-shadow .2s, transform .45s cubic-bezier(.3,1.4,.5,1)}
.w img{width:100%;border-radius:12px;box-shadow:0 30px 60px rgba(80,60,30,.22),0 0 0 1px rgba(0,0,0,.05);pointer-events:none;transition:box-shadow .2s}
.w.drag{transition:none;cursor:grabbing}
.w.drag img{box-shadow:0 50px 90px rgba(80,60,30,.32),0 0 0 1px rgba(0,0,0,.05)}
@media (pointer:fine){.w{cursor:grab}}
.sticker{position:absolute;font-size:15px;font-weight:750;padding:9px 15px;border-radius:12px;box-shadow:0 8px 20px rgba(0,0,0,.12);white-space:nowrap;pointer-events:none}
.hint{position:absolute;left:38%;bottom:-7%;font-size:15px;color:var(--mute);display:flex;gap:8px;align-items:center;transition:opacity .4s}
.hint svg{width:34px;height:34px;color:var(--accent)}
.hint.gone{opacity:0}
.enter{animation:drop .9s cubic-bezier(.2,1.2,.4,1) both}
@keyframes drop{from{opacity:0;translate:0 40px}}

/* sections */
section{position:relative;padding:120px 0}
.tag{display:inline-block;font-size:14px;font-weight:800;padding:6px 13px;border-radius:10px;transform:rotate(-3deg);margin-bottom:18px}
h2{font-size:clamp(36px,4.6vw,58px);font-weight:900;letter-spacing:-.035em;line-height:1.08;margin:0}
.sec-p{font-size:19px;color:var(--sub);line-height:1.6;margin:16px 0 0;max-width:560px}
.split{display:grid;grid-template-columns:.9fr 1.1fr;gap:64px;align-items:center}
.split.flip{grid-template-columns:1.1fr .9fr}
.split.flip > :first-child{order:2}
.panel{background:var(--card);border-radius:22px;box-shadow:0 30px 70px rgba(80,60,30,.13),0 0 0 1px rgba(0,0,0,.04);padding:22px;position:relative}

/* DDL demo */
.week{display:grid;grid-template-columns:repeat(7,1fr);gap:6px;margin-bottom:16px}
.day{border-radius:12px;padding:8px 0 9px;text-align:center;font-size:12.5px;color:var(--mute);background:#faf8f3;position:relative;cursor:default;transition:background .2s,transform .2s}
.day b{display:block;font-size:17px;color:var(--ink);font-weight:700;margin-top:2px}
.day.today{background:var(--mint)}
.day .flag{position:absolute;top:-7px;right:-4px;background:var(--accent);color:#fff;font-size:11px;font-weight:800;border-radius:99px;padding:2px 6px;transform:scale(0);transition:transform .35s cubic-bezier(.3,1.8,.5,1)}
.day.has{cursor:pointer}
.day.has .flag{transform:scale(1)}
.day.has:hover{transform:translateY(-2px);background:var(--peach)}
.list{position:relative}
.item{display:flex;align-items:center;gap:13px;padding:13px 12px;border-radius:12px;background:#fff;border-bottom:1px solid var(--line);transition:background .4s}
.item .box{width:22px;height:22px;border-radius:7px;border:2px solid #cfc8bb;flex:none;display:grid;place-items:center;cursor:pointer;transition:all .2s;background:#fff}
.item .box:hover{border-color:var(--green)}
.item .box svg{width:14px;height:14px;color:#fff;opacity:0;transform:scale(.4);transition:all .25s cubic-bezier(.3,1.8,.5,1)}
.item.done .box{background:var(--green);border-color:var(--green)}
.item.done .box svg{opacity:1;transform:none}
.item .t{flex:1;min-width:0}
.item .t div{font-size:16px;font-weight:600;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.item .t small{display:block;font-size:13px;color:var(--mute);margin-top:3px}
.item .due{font-size:12.5px;font-weight:800;padding:4px 9px;border-radius:99px;background:#f3efe6;color:var(--sub);white-space:nowrap}
.item .due.hot{background:var(--accent-soft);color:#b8431f}
.item.done .t div{color:var(--mute);text-decoration:line-through}
.item.done .due{opacity:.4}
.item.hl{background:var(--yellow)}
.add{display:flex;gap:8px;margin-top:16px;flex-wrap:wrap}
.add input{flex:1 1 200px;min-width:0;font:inherit;font-size:15px;border:1.5px solid var(--line);border-radius:12px;padding:11px 14px;background:#fffdf8;outline:none;transition:border-color .2s}
.add input:focus{border-color:var(--accent)}
.chips{display:flex;gap:6px;flex-wrap:wrap}
.chip{font:inherit;font-size:13.5px;font-weight:650;border:1.5px solid var(--line);background:#fff;border-radius:99px;padding:8px 12px;cursor:pointer;color:var(--sub);transition:all .15s}
.chip:hover{border-color:#cfc8bb}
.chip[aria-pressed=true]{background:var(--ink);color:#fff;border-color:var(--ink)}
.go{font:inherit;font-weight:800;font-size:14px;border:0;border-radius:12px;background:var(--accent);color:#fff;padding:0 18px;cursor:pointer;min-height:42px;transition:transform .15s}
.go:active{transform:scale(.95)}
.try{position:absolute;top:-16px;right:22px;background:var(--yellow);transform:rotate(4deg)}

/* Outlook demo */
.cal{display:grid;grid-template-columns:44px repeat(5,1fr);position:relative}
.cal .hd{font-size:13px;color:var(--mute);text-align:center;padding-bottom:8px}
.cal .hd b{color:var(--ink);font-size:15px;margin-left:3px}
.allday{grid-column:2/7;height:26px;position:relative;margin-bottom:6px}
.grid-area{grid-column:1/7;display:grid;grid-template-columns:44px repeat(5,1fr);position:relative;height:300px;border-top:1px solid var(--line)}
.hours{position:relative}
.hours span{position:absolute;right:8px;font-size:11px;color:var(--mute);transform:translateY(-50%)}
.col{position:relative;border-left:1px solid var(--line)}
.ev{position:absolute;left:3px;right:3px;border-radius:7px;padding:4px 6px;font-size:12px;line-height:1.3;overflow:hidden;border-left:3px solid;transition:background .3s,border-color .3s,opacity .4s,transform .4s cubic-bezier(.3,1.5,.5,1)}
.ev b{display:block;font-weight:700}
.ev.own{background:#f4ece1;border-color:#c99a6b;color:#5b4430}
.ev.ext{cursor:pointer;color:#1f2a44;opacity:0;transform:scale(.85);pointer-events:none}
.on .ev.ext{opacity:1;transform:none;pointer-events:auto}
.ev.ext:hover{filter:brightness(.96)}
.ev.bar{top:0;bottom:0;display:flex;align-items:center}
.switch{display:flex;align-items:center;gap:12px;font-weight:750;font-size:16px;cursor:pointer;margin-bottom:18px;user-select:none}
.switch i{width:50px;height:30px;border-radius:99px;background:#ddd6c9;position:relative;transition:background .25s;flex:none}
.switch i::after{content:"";position:absolute;left:3px;top:3px;width:24px;height:24px;border-radius:50%;background:#fff;box-shadow:0 2px 6px rgba(0,0,0,.2);transition:transform .3s cubic-bezier(.3,1.6,.5,1)}
.on .switch i{background:var(--green)}
.on .switch i::after{transform:translateX(20px)}
.switch small{font-weight:500;color:var(--mute);font-size:13.5px}
.tip{font-size:13.5px;color:var(--mute);margin-top:12px;min-height:20px;transition:opacity .3s;opacity:0}
.on .tip{opacity:1}

/* looks */
.looks{text-align:center}
.looks .sec-p{margin-left:auto;margin-right:auto}
.ltabs{display:flex;justify-content:center;flex-wrap:wrap;gap:10px;margin:36px 0 30px}
.ltab{font:inherit;font-weight:750;font-size:15px;border:0;border-radius:12px;padding:11px 18px;cursor:pointer;background:#fff;box-shadow:0 0 0 1.5px var(--line);transition:transform .25s cubic-bezier(.3,1.6,.5,1),background .2s}
.ltab:hover{transform:rotate(-2deg) translateY(-2px)}
.ltab[aria-selected=true]{background:var(--ink);color:#fff;box-shadow:0 5px 0 var(--accent);transform:rotate(-2deg)}
.stage{max-width:900px;margin:0 auto;position:relative}
.stage img{border-radius:16px;box-shadow:0 30px 70px rgba(80,60,30,.18);transition:opacity .25s,transform .35s cubic-bezier(.3,1.3,.5,1)}
.stage img.out{opacity:0;transform:translateY(10px) scale(.985)}
.stage p{color:var(--sub);font-size:16px;margin:20px 0 0;min-height:24px}

/* bento */
.bento{display:grid;grid-template-columns:repeat(3,1fr);gap:16px;margin-top:44px}
.tile{border-radius:20px;padding:26px;min-height:170px;position:relative;overflow:hidden;transition:transform .35s cubic-bezier(.3,1.5,.5,1)}
.tile:hover{transform:translateY(-4px) rotate(-.6deg)}
.tile:nth-child(even):hover{transform:translateY(-4px) rotate(.6deg)}
.tile svg{width:34px;height:34px;margin-bottom:18px}
.tile h3{font-size:21px;margin:0 0 6px;letter-spacing:-.01em}
.tile p{margin:0;color:var(--sub);font-size:15px;line-height:1.55}

/* trust + final */
.trust{display:flex;flex-wrap:wrap;justify-content:center;gap:12px;margin-top:8px}
.trust span{background:#fff;border-radius:99px;padding:11px 18px;font-weight:700;font-size:15px;box-shadow:0 0 0 1.5px var(--line);display:flex;align-items:center;gap:8px}
.trust span i{width:8px;height:8px;border-radius:50%;background:var(--green)}
.final{text-align:center;padding:30px 0 110px}
.final h2{margin-bottom:34px}
.final .small{color:var(--mute);font-size:14px;margin-top:30px;line-height:1.8}
footer{border-top:1px solid var(--line);padding:30px 0 40px;color:var(--mute);font-size:14px;text-align:center}
footer a{margin:0 8px}

.reveal{opacity:0;transform:translateY(28px);transition:opacity .7s,transform .7s cubic-bezier(.2,.8,.2,1)}
.reveal.in{opacity:1;transform:none}

@media (max-width:900px){
 .wrap{padding:0 20px}
 .nav nav a:not(.lang):not(.get){display:none}
 .hero{min-height:0;padding:30px 0 60px}
 .hero .wrap,.split,.split.flip{grid-template-columns:1fr}
 .split.flip > :first-child{order:0}
 .split{gap:36px}
 .art{margin-top:30px}
 .hint{display:none}
 section{padding:80px 0}
 .bento{grid-template-columns:1fr 1fr}
 .sticker{font-size:12px;padding:6px 10px}
}
@media (max-width:560px){.bento{grid-template-columns:1fr}.panel{padding:16px}.cal .hd{font-size:11px}.cal .hd b{font-size:13px;display:block;margin:0}.ev{font-size:10.5px;padding:3px 4px}.ev span{display:none}}
@media (prefers-reduced-motion:reduce){*,*::before,*::after{animation:none!important;transition:none!important}.reveal{opacity:1;transform:none}}
"""

ICON = {
 'down': '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"><path d="M12 4v11"/><path d="m7 11 5 5 5-5"/><path d="M5 20h14"/></svg>',
 'check': '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3.4" stroke-linecap="round" stroke-linejoin="round"><path d="m5 12 5 5 9-10"/></svg>',
 'arrow': '<svg viewBox="0 0 40 40" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M6 32c10-2 20-10 24-24"/><path d="m22 10 8-2 2 8"/></svg>',
 'sun': '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"><rect x="3" y="5" width="18" height="16" rx="3"/><path d="M3 10h18M8 3v4M16 3v4"/><path d="M7 14h10" stroke-width="3"/></svg>',
 'pen': '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M4 20h4L19 9l-4-4L4 16z"/><path d="m13 7 4 4"/></svg>',
 'bar': '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"><rect x="3" y="16" width="18" height="5" rx="2"/><rect x="10" y="17.5" width="4" height="2" rx="1" fill="currentColor"/><rect x="5" y="3" width="14" height="10" rx="2"/></svg>',
 'bell': '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M6 16V11a6 6 0 0 1 12 0v5l2 2H4z"/><path d="M10 21h4"/></svg>',
 'week': '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"><rect x="3" y="5" width="18" height="16" rx="3"/><path d="M3 10h18"/><text x="12" y="18.5" text-anchor="middle" font-size="7.5" font-weight="800" fill="currentColor" stroke="none" font-family="Segoe UI">6</text></svg>',
 'lang': '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><path d="M4 5h9M8.5 3v2M6 5c0 4 3 7 6 8M11 5c0 4-3 8-7 9"/><path d="m13 21 4-9 4 9M14.5 18h5"/></svg>',
}

JS = r"""
(function(){
var T = window.TXT, reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;
var nav = document.querySelector('.nav');
addEventListener('scroll', function(){ nav.classList.toggle('scrolled', scrollY > 8); }, {passive:true});

/* reveal on scroll */
var io = 'IntersectionObserver' in window ? new IntersectionObserver(function(es){ es.forEach(function(e){ if (e.isIntersecting){ e.target.classList.add('in'); io.unobserve(e.target); } }); }, {threshold:.12}) : null;
document.querySelectorAll('.reveal').forEach(function(el){ io ? io.observe(el) : el.classList.add('in'); });

/* hero: drag the widgets around like on a real desktop */
var z = 10, hint = document.querySelector('.hint');
document.querySelectorAll('.w').forEach(function(w){
  var sx, sy, ox = 0, oy = 0, rot = w.dataset.rot || 0;
  w.style.transform = 'translate(0px,0px) rotate(' + rot + 'deg)';
  w.addEventListener('pointerdown', function(e){
    if (e.pointerType === 'touch') return;
    e.preventDefault(); w.setPointerCapture(e.pointerId); w.classList.add('drag'); w.style.zIndex = ++z;
    sx = e.clientX - ox; sy = e.clientY - oy;
    w.style.transform = 'translate(' + ox + 'px,' + oy + 'px) rotate(0deg) scale(1.03)';
    if (hint) hint.classList.add('gone');
  });
  w.addEventListener('pointermove', function(e){
    if (!w.classList.contains('drag')) return;
    ox = e.clientX - sx; oy = e.clientY - sy;
    w.style.transform = 'translate(' + ox + 'px,' + oy + 'px) rotate(0deg) scale(1.03)';
  });
  function drop(){ if (!w.classList.contains('drag')) return; w.classList.remove('drag'); w.style.transform = 'translate(' + ox + 'px,' + oy + 'px) rotate(' + rot * .4 + 'deg)'; }
  w.addEventListener('pointerup', drop); w.addEventListener('pointercancel', drop);
});

/* DDL demo: sorts itself, flags the week, ticks sink to the bottom */
var today = new Date(); today.setHours(0,0,0,0);
var items = T.ddl.map(function(d, i){ return { id: i, text: d[0], days: d[1], done: false }; }), nextId = items.length, pick = 1;
var list = document.getElementById('ddl-list'), week = document.getElementById('ddl-week');
function dateOf(n){ var d = new Date(today); d.setDate(d.getDate() + n); return d; }
function dueText(n){ return n === 0 ? T.dueToday : n === 1 ? T.dueTomorrow : T.daysLeft.replace('{0}', n); }
function dateText(n){ var d = dateOf(n); return (d.getMonth() + 1) + '/' + d.getDate() + ' ' + T.wd[d.getDay()]; }
for (var i = 0; i < 7; i++) {
  var d = dateOf(i), el = document.createElement('div');
  el.className = 'day' + (i === 0 ? ' today' : ''); el.dataset.n = i;
  el.innerHTML = T.wd[d.getDay()] + '<b>' + d.getDate() + '</b><span class="flag"></span>';
  el.addEventListener('click', function(){
    var n = +this.dataset.n;
    items.forEach(function(it){ if (it.days === n && !it.done) { var node = list.querySelector('[data-id="' + it.id + '"]'); node.classList.add('hl'); setTimeout(function(){ node.classList.remove('hl'); }, 1200); } });
  });
  week.appendChild(el);
}
function node(it){
  var n = document.createElement('div'); n.className = 'item'; n.dataset.id = it.id;
  n.innerHTML = '<span class="box" role="checkbox" tabindex="0" aria-checked="false">' + window.CHECK + '</span><div class="t"><div></div><small></small></div><span class="due"></span>';
  n.querySelector('.t div').textContent = it.text;
  function toggle(){ it.done = !it.done; render(); }
  n.querySelector('.box').addEventListener('click', toggle);
  n.querySelector('.box').addEventListener('keydown', function(e){ if (e.key === ' ' || e.key === 'Enter') { e.preventDefault(); toggle(); } });
  return n;
}
function render(fresh){
  var before = {};
  list.querySelectorAll('.item').forEach(function(n){ before[n.dataset.id] = n.getBoundingClientRect().top; });
  items.sort(function(a, b){ return (a.done - b.done) || (a.days - b.days) || (a.id - b.id); });
  items.forEach(function(it){
    var n = list.querySelector('[data-id="' + it.id + '"]') || node(it);
    n.classList.toggle('done', it.done);
    n.querySelector('.box').setAttribute('aria-checked', it.done);
    n.querySelector('small').textContent = dateText(it.days);
    var due = n.querySelector('.due'); due.textContent = dueText(it.days); due.classList.toggle('hot', it.days <= 1);
    list.appendChild(n);
  });
  if (!reduce) list.querySelectorAll('.item').forEach(function(n){
    var was = before[n.dataset.id], now = n.getBoundingClientRect().top;
    if (was === undefined) { if (fresh) n.animate([{opacity:0, transform:'translateY(-12px) scale(.97)', background:'#fff3b0'}, {opacity:1, transform:'none', background:'#fff3b0', offset:.4}, {background:'#fff'}], {duration:1100, easing:'cubic-bezier(.2,.8,.2,1)'}); }
    else if (was !== now) n.animate([{transform:'translateY(' + (was - now) + 'px)'}, {transform:'none'}], {duration:450, easing:'cubic-bezier(.2,.8,.2,1)'});
  });
  week.querySelectorAll('.day').forEach(function(el){
    var c = items.filter(function(it){ return !it.done && it.days === +el.dataset.n; }).length;
    el.classList.toggle('has', c > 0); el.querySelector('.flag').textContent = '⚑ ' + c;
  });
}
render();
var input = document.getElementById('ddl-input');
document.querySelectorAll('#ddl-chips .chip').forEach(function(c){
  c.addEventListener('click', function(){ pick = +c.dataset.days; document.querySelectorAll('#ddl-chips .chip').forEach(function(o){ o.setAttribute('aria-pressed', o === c); }); });
});
function add(){
  var text = input.value.trim() || T.newDdl;
  items.push({ id: nextId++, text: text, days: pick, done: false });
  if (items.length > 7) { var old = items.filter(function(it){ return it.done; })[0] || items[items.length - 2]; items.splice(items.indexOf(old), 1); var gone = list.querySelector('[data-id="' + old.id + '"]'); if (gone) gone.remove(); }
  input.value = ''; render(true);
}
document.getElementById('ddl-add').addEventListener('click', add);
input.addEventListener('keydown', function(e){ if (e.key === 'Enter') add(); });

/* Outlook demo */
var box = document.getElementById('ol'), palette = ['#cfe0ff|#5b8def', '#ffd9e6|#e0709a', '#d6f2e4|#3fae7f', '#ffe8c4|#e09a2c', '#e6dcff|#8a6be0'];
var colorOf = {};
T.ext.forEach(function(e, i){ if (!(e[0] in colorOf)) colorOf[e[0]] = Object.keys(colorOf).length % palette.length; });
var cols = box.querySelectorAll('.col'), allday = box.querySelector('.allday');
function px(h){ return (h - 8) / 10 * 300; }
function place(e, ext){
  var el = document.createElement('div'); el.className = 'ev ' + (ext ? 'ext' : 'own');
  el.innerHTML = '<b></b><span></span>'; el.querySelector('b').textContent = e[0];
  if (e[1] < 0) { el.classList.add('bar'); el.style.left = (e[2] * 20) + '%'; el.style.right = (100 - e[3] * 20) + '%'; el.querySelector('span').remove(); allday.appendChild(el); }
  else { el.style.top = px(e[2]) + 'px'; el.style.height = (px(e[3]) - px(e[2]) - 2) + 'px'; el.querySelector('span').textContent = e[4] || ''; cols[e[1]].appendChild(el); }
  if (ext) { el.dataset.name = e[0]; el.style.transitionDelay = (Math.random() * .25).toFixed(2) + 's'; el.addEventListener('click', function(){ colorOf[e[0]] = (colorOf[e[0]] + 1) % palette.length; paint(); }); }
}
function paint(){ box.querySelectorAll('.ev.ext').forEach(function(el){ var c = palette[colorOf[el.dataset.name]].split('|'); el.style.background = c[0]; el.style.borderColor = c[1]; }); }
T.own.forEach(function(e){ place(e, false); }); T.ext.forEach(function(e){ place(e, true); }); paint();
var sw = document.getElementById('ol-switch');
function flip(){ var on = !box.classList.contains('on'); box.classList.toggle('on', on); sw.setAttribute('aria-checked', on); }
sw.addEventListener('click', flip); sw.addEventListener('keydown', function(e){ if (e.key === ' ' || e.key === 'Enter') { e.preventDefault(); flip(); } });
if (io) { var auto = new IntersectionObserver(function(es){ if (es[0].isIntersecting) { setTimeout(function(){ if (!box.classList.contains('on') && !box.dataset.touched) flip(); }, 700); auto.disconnect(); } }, {threshold:.6}); auto.observe(box); }
sw.addEventListener('pointerdown', function(){ box.dataset.touched = 1; });

})();
"""

def page(t, img):
    looks = ''.join(f'<button class="ltab" role="tab" aria-selected="{str(i == 0).lower()}" data-lay="{k}" data-desc="{d}">{n}</button>' for i, (k, n, d, a) in enumerate(t['looks']))
    l0 = t['looks'][0]
    tiles = ''.join(f'<div class="tile reveal" style="background:{bg};transition-delay:{i * 60}ms"><span style="color:{fg}">{ICON[ic]}</span><h3>{h}</h3><p>{p}</p></div>' for i, (ic, bg, fg, h, p) in enumerate(t['tiles']))
    chips = ''.join(f'<button class="chip" data-days="{d}" aria-pressed="{str(d == 1).lower()}">{n}</button>' for d, n in t['chips'])
    hours = ''.join(f'<span style="top:{(h - 8) * 30}px">{h:02d}:00</span>' for h in range(9, 18))
    data = json.dumps(t['js'], ensure_ascii=False)
    return f"""<!doctype html>
<html lang="{t['lang']}">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{t['title']}</title>
<meta name="description" content="{t['desc']}">
<meta property="og:type" content="website">
<meta property="og:title" content="{t['title']}">
<meta property="og:description" content="{t['desc']}">
<meta property="og:url" content="{t['url']}">
<meta property="og:image" content="{t['ogimg']}">
<meta name="twitter:card" content="summary_large_image">
<meta name="theme-color" content="#fffdf8">
<link rel="alternate" hreflang="zh-CN" href="https://elinama500.github.io/DeskStudy/">
<link rel="alternate" hreflang="en" href="https://elinama500.github.io/DeskStudy/en/">
<style>{CSS}{NB_CSS}</style>
</head>
<body>
<header class="nav"><div class="wrap">
  <a class="brand" href="#top"><i></i>{t['brand']}</a>
  <nav>
    <a href="#ddl">{t['nav'][0]}</a><a href="#looks">{t['nav'][1]}</a><a href="#more">{t['nav'][2]}</a>
    <a class="lang" href="{t['other_href']}" hreflang="{t['other_lang']}">{t['other']}</a>
    <a class="get" href="{DOWNLOAD}">{t['get']}</a>
  </nav>
</div></header>

<main id="top">
<div class="hero">
  <div class="bgs">
  <div class="blob" style="width:560px;height:560px;background:#ffc9a8;right:6%;top:4%"></div>
  <div class="blob" style="width:440px;height:440px;background:#b9e3cf;right:38%;top:52%"></div>
  <div class="blob" style="width:400px;height:400px;background:#cfd8ff;right:-6%;top:56%"></div>
  </div>
  <div class="wrap">
    <div class="enter">
      <h1>{t['h1']}</h1>
      <p class="lead">{t['lead']}</p>
      <a class="btn" href="{DOWNLOAD}">{ICON['down']}{t['download']}</a>
      <div class="facts">{''.join(f'<span>{f}</span>' for f in t['facts'])}</div>
    </div>
    <div class="art enter" style="animation-delay:.15s" aria-label="{t['art_alt']}">
      <div class="w" data-rot="2" style="left:17%;top:2%;width:68%"><img src="{img}hero-cal.png" width="842" height="977" alt="{t['alt_cal']}" draggable="false">
        <span class="sticker" style="right:-4%;top:-3%;background:var(--mint);transform:rotate(5deg)">{t['st_outlook']}</span></div>
      <div class="w" data-rot="-3" style="left:0;top:30%;width:41%"><img src="{img}hero-ddl.png" width="416" height="484" alt="{t['alt_ddl']}" draggable="false">
        <span class="sticker" style="left:-4%;top:-14%;background:var(--yellow);transform:rotate(-4deg)">{t['st_sort']}</span></div>
      <div class="w" data-rot="4" style="left:63%;top:54%;width:36%"><img src="{img}hero-todo.png" width="416" height="484" alt="{t['alt_todo']}" draggable="false"></div>
      <div class="hint">{t['hint']}{ICON['arrow']}</div>
    </div>
  </div>
</div>

<section id="ddl">
  <div class="wrap split">
    <div class="reveal">
      <span class="tag" style="background:var(--yellow)">{t['ddl_tag']}</span>
      <h2>{t['ddl_h']}</h2>
      <p class="sec-p">{t['ddl_p']}</p>
    </div>
    <div class="panel reveal">
      <span class="sticker try">{t['try_']}</span>
      <div class="week" id="ddl-week"></div>
      <div class="list" id="ddl-list"></div>
      <div class="add">
        <input id="ddl-input" maxlength="40" placeholder="{t['placeholder']}" aria-label="{t['placeholder']}">
        <div class="chips" id="ddl-chips">{chips}</div>
        <button class="go" id="ddl-add">{t['add']}</button>
      </div>
    </div>
  </div>
</section>

<section id="outlook" style="background:linear-gradient(#fffdf8,#f6f8ff 30%,#f6f8ff 70%,#fffdf8)">
  <div class="wrap split flip">
    <div class="reveal">
      <span class="tag" style="background:var(--lilac)">Outlook</span>
      <h2>{t['ol_h']}</h2>
      <p class="sec-p">{t['ol_p']}</p>
    </div>
    <div class="panel reveal" id="ol">
      <div class="switch" id="ol-switch" role="switch" tabindex="0" aria-checked="false"><i></i><span>{t['ol_switch']} <small>{t['ol_switch_s']}</small></span></div>
      <div class="cal">
        <div></div>{''.join(f'<div class="hd">{w}<b>{d}</b></div>' for w, d in t['ol_days'])}
        <div style="font-size:11px;color:var(--mute);text-align:right;padding-right:8px;line-height:26px">{t['allday']}</div><div class="allday"></div>
        <div class="grid-area"><div class="hours">{hours}</div><div class="col"></div><div class="col"></div><div class="col"></div><div class="col"></div><div class="col"></div></div>
      </div>
      <div class="tip">{t['ol_tip']}</div>
    </div>
  </div>
</section>

<section id="looks" class="looks">
  <div class="wrap">
    <div class="reveal">
      <span class="tag" style="background:var(--peach)">{t['look_tag']}</span>
      <h2>{t['look_h']}</h2>
    </div>
    <div class="ltabs reveal" role="tablist">{looks}</div>
    <div class="stage reveal">
      <p class="nbtry">{t['nbtry']}</p>
      <div class="nbpair lay-{l0[0]}" id="nbpair" aria-label="{t['nbaria']}"></div>
      <p id="look-cap" aria-live="polite">{l0[2]}</p>
    </div>
  </div>
</section>

<section id="more" style="padding-top:60px">
  <div class="wrap">
    <div class="reveal"><span class="tag" style="background:var(--mint)">{t['more_tag']}</span><h2>{t['more_h']}</h2></div>
    <div class="bento">{tiles}</div>
  </div>
</section>

<div class="final">
  <div class="wrap">
    <div class="trust reveal">{''.join(f'<span><i></i>{x}</span>' for x in t['trust'])}</div>
    <h2 class="reveal" style="margin-top:70px">{t['final_h']}</h2>
    <a class="btn reveal" href="{DOWNLOAD}">{ICON['down']}{t['download']}</a>
    <p class="small">{t['final_small']}</p>
  </div>
</div>
</main>

<footer><div class="wrap">{t['footer']}</div></footer>
<script>window.TXT = {data}; window.NBTXT = {json.dumps(nb_text(t['lang'][:2]), ensure_ascii=False)}; window.CHECK = {json.dumps(ICON['check'])};</script>
<script>{JS}</script>
<script>{NB_JS}</script>
</body>
</html>
"""

ZH = dict(
 lang='zh-CN', brand='桌面课笺', other='English', other_lang='en', other_href='en/',
 title='桌面课笺 · 抬头就能看到的课表和 DDL', url='https://elinama500.github.io/DeskStudy/', ogimg='https://elinama500.github.io/DeskStudy/images/site/zh/persona-research.png',
 desc='常驻 Windows 桌面的课表、待办和 DDL 组件。DDL 自动排序并标在日历上，可同步 Outlook 日历。开源免费，无需账号。',
 nav=['功能', '外观', '更多'], get='下载',
 h1='抬头就能看到的<br>课表和 <em class="mark">DDL</em>。', lead='三个小组件常驻桌面，<br>DDL 自动排好、到点提醒。',
 download='免费下载 Windows 版', facts=['开源免费', '无需账号', '630 KB 解压即用'],
 art_alt='桌面课笺的三个组件', alt_cal='周课表组件', alt_ddl='DDL 便签组件', alt_todo='待办便签组件',
 st_outlook='同步学校 Outlook 日历', st_sort='⚑ 自动按截止时间排', hint='拖拖看',
 ddl_tag='⚑ DDL', ddl_h='DDL 自己<br>排好队', ddl_p='最急的永远在最上面，做完的沉到底；有 DDL 的日子，日历上会插一面小旗。',
 try_='试试看 ↓', placeholder='加一个 DDL，回车…', add='添加',
 chips=[(0, '今天'), (1, '明天'), (3, '3 天后'), (6, '下周')],
 ol_h='学校日历，<br>一键同步进来', ol_p='粘贴 Outlook 发布的日历链接，讲座、答疑直接出现在课表里。只读，不会改动你的 Outlook。',
 ol_switch='同步 Outlook', ol_switch_s='只读', ol_tip='点一下 Outlook 日程，换个颜色 →',
 ol_days=[('周一', ''), ('周二', ''), ('周三', ''), ('周四', ''), ('周五', '')], allday='全天',
 look_tag='外观', look_h='五种便签外观，<br>点一下就换', nbtry='下面的便签可以直接用：勾选、翻页、双击改字、添加任务。', nbaria='可以操作的 DDL 与 Todo 便签',
 looks=[('clean', '清爽卡片', '白底细线，最简洁。', '清爽卡片外观'),
        ('journal', '手账纸页', '米色纸页加页签，像一本笔记本。', '手账纸页外观'),
        ('card', '轻量卡片', '默认外观，每个任务一张小卡片。', '轻量卡片外观'),
        ('paper', '纸页本', '暖色纸张，左边点页码直接翻页。', '纸页本外观'),
        ('original', '原始外观', '按钮最全，编辑、移动、删除都在手边。', '原始外观')],
 more_tag='还有', more_h='小事也替你想好了',
 tiles=[('sun', '#fff3b0', '#b88a00', '全天日程', '考试周、假期，跨几天都行。'),
        ('pen', '#ffe4d6', '#e8643c', '双击就改', '改完点别处，自动保存。'),
        ('bar', '#d9f2e6', '#2f6f53', '一键唤出', '单击任务栏按钮，组件浮到最前。'),
        ('bell', '#dfe5ff', '#4b63d6', '准时提醒', '提前量逐项设置，错过会补发。'),
        ('week', '#f3e8ff', '#8a5bd6', '教学周', '日历上直接显示第几周。'),
        ('lang', '#e6f4f7', '#2a8aa0', '中文 / English', '界面一键切换。')],
 trust=['开源 · MIT', '无需账号', '默认不联网', '数据只在本机', '768 项自动化测试'],
 final_h='这学期，<br>换个方式记 DDL', final_small=f'v1.6.1 · Windows 10 / 11 · 约 630 KB · 解压即用<br>首次运行若被 Windows 拦截：「更多信息」→「仍要运行」',
 footer=f'<a href="{REPO}">GitHub</a>·<a href="{REPO}/blob/main/USER_GUIDE.md">使用指南</a>·<a href="{REPO}/releases">所有版本</a>·<a href="en/" hreflang="en">English</a>',
 js=dict(
  dueToday='今天截止', dueTomorrow='明天截止', daysLeft='剩余 {0} 天', newDdl='新的 DDL', wd=['周日', '周一', '周二', '周三', '周四', '周五', '周六'],
  ddl=[['会议论文截稿', 3], ['返修意见回复', 1], ['物理实验报告', 5], ['奖学金申请材料', 9]],
  own=[['高等数学', 0, 8, 9.75, '教学楼 A201'], ['线性代数', 1, 10, 11.75, 'A305'], ['高等数学', 2, 8, 9.75, '教学楼 A201'], ['线性代数', 3, 10, 11.75, 'A305'], ['物理实验', 4, 14, 16.5, '实验楼']],
  ext=[['学术讲座', 1, 14, 15.5, '报告厅'], ['导师答疑', 0, 15.5, 16.5, ''], ['导师答疑', 3, 15.5, 16.5, ''], ['学院例会', 2, 12.5, 13.5, ''], ['运动会', -1, 3, 5]]),
)

EN = dict(
 lang='en', brand='DeskStudy', other='中文', other_lang='zh-CN', other_href='../',
 title='DeskStudy · Your week and your deadlines, always in sight', url='https://elinama500.github.io/DeskStudy/en/', ogimg='https://elinama500.github.io/DeskStudy/images/site/en/persona-research.png',
 desc='Timetable, to-do and deadline widgets that stay on your Windows desktop. Deadlines sort themselves and show on the calendar; Outlook calendars sync in. Free, open source, no account.',
 nav=['Features', 'Looks', 'More'], get='Download',
 h1='Your week and<br>your <em class="mark">deadlines</em>,<br>always in sight.', lead='Three small widgets that live on your desktop.<br>Deadlines sort themselves and remind you in time.',
 download='Download for Windows', facts=['Free & open source', 'No account', '630 KB, no installer'],
 art_alt='The three DeskStudy widgets', alt_cal='Week calendar widget', alt_ddl='Deadlines notebook widget', alt_todo='To-do notebook widget',
 st_outlook='Syncs your Outlook calendar', st_sort='⚑ Sorted by due time', hint='Drag me',
 ddl_tag='⚑ Deadlines', ddl_h='Deadlines that<br>line themselves up', ddl_p='The most urgent stays on top, done ones sink. Days with a deadline get a little flag on the calendar.',
 try_='Try it ↓', placeholder='Add a deadline, press Enter…', add='Add',
 chips=[(0, 'Today'), (1, 'Tomorrow'), (3, 'In 3 days'), (6, 'Next week')],
 ol_h='Your school calendar,<br>synced in', ol_p='Paste the link of a published Outlook calendar and seminars and office hours appear next to your classes. Read-only: your Outlook is never changed.',
 ol_switch='Sync Outlook', ol_switch_s='read-only', ol_tip='Click an Outlook event to change its color →',
 ol_days=[('Mon', ''), ('Tue', ''), ('Wed', ''), ('Thu', ''), ('Fri', '')], allday='All day',
 look_tag='Looks', look_h='Five looks,<br>one click away', nbtry='These notebooks work: tick, turn pages, double-click to edit, add tasks.', nbaria='Working Deadlines and To-do notebooks',
 looks=[('clean', 'Clean cards', 'White with hairlines. The simplest.', 'Clean cards layout'),
        ('journal', 'Journal', 'Cream paper with page tabs, like a notebook.', 'Journal layout'),
        ('card', 'Light cards', 'The default: each task is a small card.', 'Light cards layout'),
        ('paper', 'Paper', 'Warm paper; click a page number to turn to it.', 'Paper layout'),
        ('original', 'Original', 'Every button in reach: edit, move, delete.', 'Original layout')],
 more_tag='And', more_h='The small things,<br>thought through',
 tiles=[('sun', '#fff3b0', '#b88a00', 'All-day events', 'Exam weeks and breaks, across several days.'),
        ('pen', '#ffe4d6', '#e8643c', 'Double-click to edit', 'Click anywhere else and it is saved.'),
        ('bar', '#d9f2e6', '#2f6f53', 'One click away', 'The taskbar button brings the widgets forward.'),
        ('bell', '#dfe5ff', '#4b63d6', 'Reminders on time', 'Lead time per task; missed ones arrive later.'),
        ('week', '#f3e8ff', '#8a5bd6', 'Teaching week', 'The calendar shows which week of term it is.'),
        ('lang', '#e6f4f7', '#2a8aa0', 'English / 中文', 'Switch the interface any time.')],
 trust=['Open source · MIT', 'No account', 'Offline by default', 'Data stays on your PC', '768 automated checks'],
 final_h='This term, stop keeping<br>deadlines in your head', final_small='v1.6.1 · Windows 10 / 11 · about 630 KB · unzip and run<br>If Windows stops it the first time: More info → Run anyway',
 footer=f'<a href="{REPO}">GitHub</a>·<a href="{REPO}/blob/main/USER_GUIDE.en.md">User guide</a>·<a href="{REPO}/releases">All versions</a>·<a href="../" hreflang="zh-CN">中文</a>',
 js=dict(
  dueToday='Due today', dueTomorrow='Due tomorrow', daysLeft='{0} days left', newDdl='New deadline', wd=['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'],
  ddl=[['Conference paper', 3], ['Response to reviewers', 1], ['Physics lab report', 5], ['Scholarship application', 9]],
  own=[['Calculus', 0, 8, 9.75, 'Room A201'], ['Linear Algebra', 1, 10, 11.75, 'A305'], ['Calculus', 2, 8, 9.75, 'Room A201'], ['Linear Algebra', 3, 10, 11.75, 'A305'], ['Physics lab', 4, 14, 16.5, 'Lab block']],
  ext=[['Seminar', 1, 14, 15.5, 'Hall'], ['Office hours', 0, 15.5, 16.5, ''], ['Office hours', 3, 15.5, 16.5, ''], ['Staff meeting', 2, 12.5, 13.5, ''], ['Sports day', -1, 3, 5]]),
)

os.makedirs(os.path.join(OUT, 'en'), exist_ok=True)
open(os.path.join(OUT, 'index.html'), 'w', encoding='utf-8', newline='\n').write(page(ZH, 'images/site/zh/'))
open(os.path.join(OUT, 'en', 'index.html'), 'w', encoding='utf-8', newline='\n').write(page(EN, '../images/site/en/'))
print('ok')
