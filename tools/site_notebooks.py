# The "looks" section of the landing page: two working notebooks drawn in HTML, restyled for each of
# the five layouts. Used by build_site.py.

NB_CSS = r"""
.nbpair{display:grid;grid-template-columns:1fr 1fr;align-items:start;gap:6px;max-width:900px;margin:0 auto;padding:14px;border-radius:18px;background:#d6deda;box-shadow:0 30px 70px rgba(80,60,30,.18);text-align:left;
 font-family:"Segoe UI","Microsoft YaHei UI","Microsoft YaHei",sans-serif;color:#25303f;transition:background .3s}
.nb{--c:#9b5a3c;display:flex;flex-direction:column;height:500px;border-radius:10px;overflow:hidden;background:#fff;transition:height .35s cubic-bezier(.3,1.2,.5,1),background .3s;position:relative}
.nb.todo{--c:#4f7a62}
.nb.folded{height:46px}
.nb button{font:inherit;color:inherit;background:none;border:0;padding:0;cursor:pointer}
.nb .hd{display:flex;align-items:center;gap:14px;height:46px;flex:none;padding:0 14px 0 18px;font-size:14px;border-bottom:1px solid #eceae4}
.nb .hd .nm{flex:1;display:flex;align-items:center;gap:9px;color:#6e7885}
.nb .hd .nm i{width:8px;height:8px;border-radius:50%;background:var(--c)}
.nb .hd .pin.on{color:var(--c);font-weight:700}
.nb .hd .fold{transition:transform .3s;display:inline-block;width:18px;text-align:center}
.nb.folded .hd .fold{transform:rotate(180deg)}
.nb .bd{flex:1;display:flex;min-height:0}
.nb .tabs{display:none}
.nb .main{flex:1;display:flex;flex-direction:column;min-width:0;padding:0 18px}
.nb .ttl{display:flex;align-items:flex-start;padding:16px 4px 8px}
.nb .ttl h4{margin:0;font-size:22px;font-weight:400;flex:1;outline:none}
.nb .ttl small{display:block;font-size:13px;color:#7b8590;margin-top:4px}
.nb .more{color:#9aa3ad;letter-spacing:2px}
.nb .meta{display:none}
.nb .list{flex:1;overflow:auto;min-height:0;position:relative}
.nb .it{display:flex;gap:12px;align-items:flex-start;padding:11px 4px;border-bottom:1px solid #eceae4;transition:background .6s}
.nb .it.flash{background:#fff3b0}
.nb .bx{width:19px;height:19px;flex:none;border:1.5px solid #8e969e;border-radius:4px;margin-top:2px;display:grid;place-items:center;transition:all .15s}
.nb .bx svg{width:12px;height:12px;color:#fff;opacity:0;transform:scale(.4);transition:all .25s cubic-bezier(.3,1.8,.5,1)}
.nb .it.done .bx{background:#4f6f5d;border-color:#4f6f5d}
.nb .it.done .bx svg{opacity:1;transform:none}
.nb .tx{flex:1;min-width:0;font-size:15px}
.nb .tx b{display:block;font-weight:400;outline:none;border-radius:4px;cursor:text}
.nb .tx b[contenteditable=true]{background:#fff9e3;box-shadow:0 0 0 2px #f3d98a}
.nb .it.done .tx b{color:#98a0a8;text-decoration:line-through}
.nb .due{display:block;font-size:13px;color:#7b8590;margin-top:3px}
.nb .due.hot{color:#9b4a26;font-weight:700}
.nb .due2{display:none}
.nb .sht{display:none}
.lay-card .nb .sht,.lay-paper .nb .sht,.lay-original .nb .sht{display:inline}
.lay-card .nb .lbl,.lay-paper .nb .lbl,.lay-original .nb .lbl{display:none}
.nb .due2.nodue{display:none!important}
.lay-original .nb .due2.nodue{display:block!important}
.nb .acts{display:none}
.nb .dots{color:#b4bac0;font-size:13px;letter-spacing:1px}
.nb .add{display:flex;align-items:center;gap:10px;padding:12px 4px;color:#6e7885;font-size:15px}
.nb .add input{flex:1;font:inherit;border:0;background:none;outline:none;color:#25303f;min-width:0}
.nb .add input::placeholder{color:#6e7885}
.nb .list{order:2}
.nb .note{order:3;padding:8px 14px 10px;font-size:14px;color:#6e7885}
.nb .note textarea{display:none;width:100%;margin-top:8px;font:inherit;font-size:13px;border:1px solid #e3e1da;border-radius:6px;padding:7px;resize:none;height:56px;background:#fffdf8}
.nb .note.open textarea{display:block}
.nb .note .car{display:inline-block;transition:transform .2s;font-size:10px;margin-right:6px}
.nb .note.open .car{transform:rotate(90deg)}
.nb .ft{display:flex;align-items:center;gap:18px;height:50px;flex:none;padding:0 18px;border-top:1px solid #eceae4;font-size:14px;color:#55606c}
.nb .ft .pg{flex:1}
.nb .ft button:disabled{opacity:.3;cursor:default}
.nb .oldnav,.nb .oldmeta,.nb .oldtasks,.nb .hint{display:none}
.nb .list.turn{animation:turn .3s cubic-bezier(.2,.8,.2,1)}
@keyframes turn{from{opacity:0;transform:translateX(var(--dx,14px))}}

/* 手账纸页 */
.lay-journal .nb{background:#faf7ee}
.lay-journal .nb .tabs{display:flex;flex-direction:column;gap:6px;width:46px;flex:none;padding:12px 4px;background:#efece2}
.nb .tabs button{height:34px;border-radius:6px;font-size:14px;color:#6e7885}
.lay-journal .nb .tabs button.on{background:#fffdf7;color:#25303f;box-shadow:0 1px 2px rgba(0,0,0,.06)}

/* 轻量卡片 */
.lay-card .nb,.lay-paper .nb,.lay-original .nb{border-top:4px solid var(--c)}
.lay-card .nb .hd,.lay-paper .nb .hd,.lay-original .nb .hd{color:#25303f}
.lay-card .nb .hd .nm,.lay-paper .nb .hd .nm,.lay-original .nb .hd .nm{color:#25303f;font-size:16px}
.lay-card .nb .hd .nm i,.lay-paper .nb .hd .nm i,.lay-original .nb .hd .nm i{display:none}
.nb .hd .hide{display:none}
.lay-card .nb .hd .hide,.lay-paper .nb .hd .hide,.lay-original .nb .hd .hide{display:inline}
.lay-card .nb .hd .pin::before,.lay-paper .nb .hd .pin::before,.lay-original .nb .hd .pin::before{content:"";display:inline-block;width:12px;height:12px;border:1.5px solid #6e7885;border-radius:3px;margin-right:6px;vertical-align:-2px}
.lay-card .nb .hd .pin.on::before,.lay-paper .nb .hd .pin.on::before,.lay-original .nb .hd .pin.on::before{background:var(--c);border-color:var(--c)}
.lay-card .nb .bd{background:#f3f5f3}
.lay-card .nb .ttl h4,.lay-paper .nb .ttl h4{font-size:17px;font-weight:700}
.lay-card .nb .ttl small,.lay-paper .nb .ttl small{display:none}
.lay-card .nb .meta,.lay-paper .nb .meta{display:flex;justify-content:space-between;padding:0 4px 8px;font-size:14px;color:#55606c}
.lay-card .nb .meta span+span,.lay-paper .nb .meta span+span{color:#8e969e;font-size:13px}
.lay-card .nb .it{background:#fff;border:1px solid #e3e6e3;border-radius:8px;margin-bottom:9px;padding:12px}
.lay-card .nb .due,.lay-paper .nb .due,.lay-original .nb .due{color:#8e969e;font-weight:400}
.lay-card .nb .due.hot,.lay-paper .nb .due.hot,.lay-original .nb .due.hot{color:#c07a22;font-weight:400}
.lay-card .nb .due2,.lay-paper .nb .due2,.lay-original .nb .due2{display:block}
.lay-card .nb .due1,.lay-paper .nb .due1,.lay-original .nb .due1{display:none}
.lay-card .nb .note,.lay-paper .nb .note{order:1;background:#fff;margin:0 0 10px;padding:7px 12px}
.lay-card .nb .main,.lay-paper .nb .main{padding:0 14px}

/* 纸页本 */
.lay-paper .nb{background:#fbf7ea}
.lay-paper .nb .bd{background:#fbf7ea}
.lay-paper .nb .tabs{display:flex;flex-direction:column;gap:6px;width:56px;flex:none;padding:14px 8px;border-right:1px solid #e6dcc4}
.lay-paper .nb .tabs button.on{background:var(--c);color:#fff}
.lay-paper .nb .note{background:#fffbf0}

/* 原始外观 */
.lay-original .nb{height:560px}
.lay-original .nb .note .sht{display:inline}
.lay-original .nb .note .lbl,.lay-card .nb .note .sht,.lay-paper .nb .note .sht{display:none}
.lay-card .nb .note .lbl,.lay-paper .nb .note .lbl{display:inline}
.lay-original .nb .bd{background:#f6f7fb}
.lay-original .nb .hd .pin,.lay-original .nb .hd .gear,.lay-original .nb .hd .fold,.lay-original .nb .hd .hide{border:1px solid #dfe2ea;border-radius:4px;padding:5px 10px;background:#fff}
.lay-original .nb .ttl,.lay-original .nb .meta,.lay-original .nb .ft,.lay-original .nb .add{display:none}
.lay-original .nb .main{padding:10px 14px 6px}
.lay-original .nb .oldnav{display:flex;gap:6px;margin-bottom:8px}
.lay-original .nb .oldnav button,.lay-original .nb .oldnav select,.lay-original .nb .oldmeta button,.lay-original .nb .oldtasks button,.lay-original .nb .acts button{border:1px solid #dfe2ea;background:#fff;border-radius:3px;padding:5px 10px;font-size:13px}
.lay-original .nb .oldnav select{flex:1;font:inherit;font-size:13px;padding:4px 6px}
.lay-original .nb .oldtitle{display:block;border:1px solid #25303f;padding:4px 6px;font-weight:700;font-size:15px;margin-bottom:6px;outline:none}
.lay-original .nb .oldmeta{display:flex;justify-content:space-between;align-items:center;font-size:13px;color:#55606c;margin-bottom:6px}
.lay-original .nb .note{padding:0 0 6px;order:0}
.lay-original .nb .note textarea{display:block;height:48px;border-color:#c8ccd6;border-radius:0;background:#fff}
.lay-original .nb .oldtasks{display:flex;justify-content:space-between;align-items:center;font-size:14px;margin:4px 0 8px}
.lay-original .nb .it{background:#fff;border:1px solid #dfe2ea;border-radius:2px;margin-bottom:8px;padding:10px;flex-wrap:wrap}
.lay-original .nb .acts{display:flex;gap:6px;width:100%;padding-left:31px;margin-top:4px}
.lay-original .nb .hint{display:block;padding:8px 14px;font-size:12.5px;color:#55606c;border-top:1px solid #e3e6ee;flex:none}
.lay-original .nb .dots{display:none}
.lay-original .nb .oldadd{display:none;gap:6px;margin-bottom:8px}
.lay-original .nb .oldadd.open{display:flex}
.lay-original .nb .oldadd input{flex:1;font:inherit;font-size:14px;border:1px solid #c8ccd6;padding:5px 7px;min-width:0}
.nb .oldtitle,.nb .oldadd{display:none}
.nb .oldtitle{display:none}
.lay-original .nb .oldtitle{display:block}

.nbtry{display:inline-block;margin:0 0 14px;font-size:14px;color:var(--mute)}
@media (max-width:760px){.nbpair{grid-template-columns:1fr;padding:8px}.nb{height:470px}}
"""

NB_JS = r"""
/* looks: two working notebooks, restyled per layout */
(function(){
var T = window.NBTXT, pair = document.getElementById('nbpair'), cap = document.getElementById('look-cap');
var CHECK = window.CHECK, today = new Date(); today.setHours(0,0,0,0);
var nextId = 1;
var books = T.books.map(function(b){ return { id: b.id, label: b.label, short: b.short, sorted: b.sorted, page: 0, folded: false, pin: false, note: false,
  pages: b.pages.map(function(p){ return { title: p.title, note: '', items: p.items.map(function(i){ return { id: nextId++, t: i[0], d: i[1], time: i[2], done: !!i[3] }; }) }; }) }; });
function day(n){ var d = new Date(today); d.setDate(d.getDate() + n); return d; }
function two(n){ return (n < 10 ? '0' : '') + n; }
function md(d){ return (d.getMonth() + 1) + '/' + d.getDate(); }
function ymd(d){ return d.getFullYear() + '/' + two(d.getMonth() + 1) + '/' + two(d.getDate()); }
function due(it){
  if (it.d === null || it.d === undefined) return null;
  var d = day(it.d), at = it.time ? md(d) + ' ' + it.time : md(d) + ' ' + T.wd[d.getDay()];
  var rel = it.d === 0 ? T.today : it.d === 1 ? T.tomorrow : T.left.replace('{0}', it.d);
  var lead = it.time ? T.lead : T.leadDay;
  var one = it.d <= 1 && it.time ? rel + ' ' + it.time + ' · ' + md(d) + ' · ' + lead : it.d > 7 ? at + ' · ' + lead : rel + ' · ' + at + ' · ' + lead;
  var first = (it.d <= 1 ? (it.d === 0 ? T.dueToday : T.dueTomorrow) : rel) + ' · ' + ymd(d) + (it.time ? ' ' + it.time : '');
  return { one: one, two: [first, lead], hot: it.d <= 1 };
}
function order(b, items){
  if (!b.sorted) return items.slice();
  return items.slice().sort(function(a, c){
    if (a.done !== c.done) return a.done ? 1 : -1;
    var x = a.d === null || a.d === undefined ? 1e9 : a.d, y = c.d === null || c.d === undefined ? 1e9 : c.d;
    return x - y || a.id - c.id;
  });
}
function esc(s){ var e = document.createElement('i'); e.textContent = s; return e.innerHTML; }
function el(html){ var t = document.createElement('div'); t.innerHTML = html.trim(); return t.firstChild; }

function build(b){
  var node = document.createElement('div'); node.className = 'nb ' + b.id + (b.folded ? ' folded' : ''); node.dataset.book = b.id;
  var p = b.pages[b.page], items = order(b, p.items), done = p.items.filter(function(i){ return i.done; }).length, n = b.pages.length;
  var sub = T.date.replace('{m}', today.getMonth() + 1).replace('{d}', today.getDate()) + ' · ' + (b.sorted ? T.toDo.replace('{0}', p.items.length - done) : T.doneOf.replace('{0}', done).replace('{1}', p.items.length));
  var tabs = b.pages.map(function(_, i){ return '<button class="' + (i === b.page ? 'on' : '') + '" data-go="' + i + '">' + two(i + 1) + '</button>'; }).join('');
  var options = b.pages.map(function(pg, i){ return '<option value="' + i + '"' + (i === b.page ? ' selected' : '') + '>' + two(i + 1) + '  ' + esc(pg.title) + '</option>'; }).join('');
  node.innerHTML =
    '<div class="hd"><span class="nm"><i></i><span class="lbl">' + esc(b.label) + '</span><span class="sht">' + esc(b.short) + '</span></span>' +
      '<button class="pin' + (b.pin ? ' on' : '') + '">' + T.pin + '</button><button class="gear" aria-label="⚙">⚙</button>' +
      '<button class="fold" aria-label="fold">⌄</button><button class="hide">' + T.hide + '</button></div>' +
    '<div class="bd"><div class="tabs">' + tabs + '</div><div class="main">' +
      '<div class="oldnav"><button data-step="-1">‹</button><select>' + options + '</select><button data-step="1">›</button><button class="newp">+ ' + T.newPageShort + '</button></div>' +
      '<b class="oldtitle" contenteditable="false">' + esc(p.title) + '</b>' +
      '<div class="oldmeta"><span>' + T.created.replace('{0}', ymd(today).replace(/\//g, '.')) + '</span><button>' + T.rename + '</button></div>' +
      '<div class="ttl"><div style="flex:1"><h4>' + esc(p.title) + '</h4><small>' + sub + '</small></div><span class="more">···</span></div>' +
      '<div class="meta"><span>' + T.doneOf2.replace('{0}', done).replace('{1}', p.items.length) + '</span><span>' + T.saved + '</span></div>' +
      '<div class="note' + (b.note ? ' open' : '') + '"><button class="nt"><span class="car">▸</span><span class="lbl">' + T.notes + '</span><span class="sht">' + T.notesOld + '</span></button><textarea placeholder="' + T.notePh + '">' + esc(p.note) + '</textarea></div>' +
      '<div class="oldtasks"><span>' + T.tasks + '  ' + T.doneOf2.replace('{0}', done).replace('{1}', p.items.length) + '</span><button class="oldaddbtn">+ ' + T.addTask + '</button></div>' +
      '<div class="oldadd"><input placeholder="' + T.addPh + '"><button class="oldok">' + T.ok + '</button></div>' +
      '<div class="list">' + items.map(function(it){
        var du = due(it), line1 = it.done ? T.done : du ? du.one : '', l2 = it.done ? [T.done] : du ? du.two : [T.noDue];
        return '<div class="it' + (it.done ? ' done' : '') + '" data-id="' + it.id + '"><button class="bx" role="checkbox" aria-checked="' + it.done + '">' + CHECK + '</button>' +
          '<div class="tx"><b title="' + T.dbl + '">' + esc(it.t) + '</b>' +
          (line1 ? '<span class="due due1' + (du && du.hot && !it.done ? ' hot' : '') + '">' + line1 + '</span>' : '') +
          '<span class="due due2' + (du && du.hot && !it.done ? ' hot' : '') + (!du && !it.done ? ' nodue' : '') + '">' + l2.join('<br>') + '</span></div><span class="dots">···</span>' +
          '<div class="acts"><button class="ed">' + T.edit + '</button><button class="del">' + T.remove + '</button></div></div>';
      }).join('') + '<label class="add">+ <input placeholder="' + T.addPh + '"><span style="font-size:12px">↵</span></label></div>' +
    '</div></div>' +
    '<div class="ft"><button data-step="-1"' + (b.page === 0 ? ' disabled' : '') + '>‹</button><span class="pg">' + T.page.replace('{0}', b.page + 1).replace('{1}', n) + '</span>' +
      '<button data-step="1"' + (b.page === n - 1 ? ' disabled' : '') + '>›</button><button class="newp">+ ' + T.newPage + '</button></div>' +
    '<div class="hint">' + T.hint + '</div>';
  return node;
}

function draw(b, opts){
  opts = opts || {};
  var old = pair.querySelector('[data-book="' + b.id + '"]'), before = {}, scroll = 0;
  if (old) { old.querySelectorAll('.it').forEach(function(n){ before[n.dataset.id] = n.getBoundingClientRect().top; }); scroll = old.querySelector('.list').scrollTop; }
  var node = build(b);
  if (old) pair.replaceChild(node, old); else pair.appendChild(node);
  var list = node.querySelector('.list'); if (!opts.turn) list.scrollTop = scroll;
  if (opts.turn) { list.style.setProperty('--dx', opts.turn > 0 ? '16px' : '-16px'); list.classList.add('turn'); }
  else if (!matchMedia('(prefers-reduced-motion: reduce)').matches) node.querySelectorAll('.it').forEach(function(n){
    var was = before[n.dataset.id], now = n.getBoundingClientRect().top;
    if (was !== undefined && was !== now) n.animate([{ transform: 'translateY(' + (was - now) + 'px)' }, { transform: 'none' }], { duration: 420, easing: 'cubic-bezier(.2,.8,.2,1)' });
  });
  if (opts.flash) { var f = node.querySelector('.it[data-id="' + opts.flash + '"]'); if (f) { f.classList.add('flash'); f.scrollIntoView({ block: 'nearest' }); setTimeout(function(){ f.classList.remove('flash'); }, 900); } }
  if (opts.focusAdd) { var inp = node.querySelector(opts.focusAdd); if (inp) inp.focus(); }
  wire(b, node);
}
function item(b, id){ return b.pages[b.page].items.filter(function(i){ return i.id === +id; })[0]; }
function go(b, page){ if (page < 0 || page >= b.pages.length || page === b.page) return; var dir = page > b.page ? 1 : -1; b.page = page; draw(b, { turn: dir }); }
function add(b, input, sel){
  var t = input.value.trim(); if (!t) return;
  var it = { id: nextId++, t: t, d: null, time: null, done: false }; b.pages[b.page].items.push(it);
  draw(b, { flash: it.id, focusAdd: sel });
}
function edit(b, node, id){
  var label = node.querySelector('.it[data-id="' + id + '"] .tx b'), it = item(b, id); if (!label || !it) return;
  label.contentEditable = 'true'; label.focus();
  var r = document.createRange(); r.selectNodeContents(label); var s = getSelection(); s.removeAllRanges(); s.addRange(r);
  var finished = false;
  function finish(save){ if (finished) return; finished = true; var v = label.textContent.trim(); if (save && v) it.t = v; draw(b); }
  label.addEventListener('keydown', function(e){ if (e.key === 'Enter') { e.preventDefault(); finish(true); } else if (e.key === 'Escape') finish(false); });
  label.addEventListener('blur', function(){ finish(true); });
}
function wire(b, node){
  node.querySelector('.fold').onclick = function(){ b.folded = !b.folded; node.classList.toggle('folded', b.folded); };
  node.querySelector('.hd').ondblclick = function(e){ if (e.target.closest('button')) return; b.folded = !b.folded; node.classList.toggle('folded', b.folded); };
  node.querySelector('.pin').onclick = function(){ b.pin = !b.pin; this.classList.toggle('on', b.pin); };
  node.querySelectorAll('[data-step]').forEach(function(x){ x.onclick = function(){ go(b, b.page + +x.dataset.step); }; });
  node.querySelectorAll('[data-go]').forEach(function(x){ x.onclick = function(){ go(b, +x.dataset.go); }; });
  node.querySelector('select').onchange = function(){ go(b, +this.value); };
  node.querySelectorAll('.newp').forEach(function(x){ x.onclick = function(){ if (b.pages.length >= 5) return; b.pages.push({ title: T.newTitle, note: '', items: [] }); go(b, b.pages.length - 1); }; });
  node.querySelector('.nt').onclick = function(){ b.note = !b.note; node.querySelector('.note').classList.toggle('open', b.note); if (b.note) node.querySelector('.note textarea').focus(); };
  node.querySelector('.note textarea').oninput = function(){ b.pages[b.page].note = this.value; };
  node.querySelectorAll('.it').forEach(function(row){
    var id = row.dataset.id;
    row.querySelector('.bx').onclick = function(){ var it = item(b, id); it.done = !it.done; draw(b); };
    row.querySelector('.tx b').ondblclick = function(){ edit(b, node, id); };
    row.querySelector('.ed').onclick = function(){ edit(b, node, id); };
    row.querySelector('.del').onclick = function(){ var its = b.pages[b.page].items; its.splice(its.indexOf(item(b, id)), 1); draw(b); };
  });
  var inp = node.querySelector('.add input'); inp.onkeydown = function(e){ if (e.key === 'Enter') add(b, inp, '.add input'); };
  node.querySelector('.oldaddbtn').onclick = function(){ var box = node.querySelector('.oldadd'); box.classList.toggle('open'); if (box.classList.contains('open')) box.querySelector('input').focus(); };
  var oi = node.querySelector('.oldadd input');
  oi.onkeydown = function(e){ if (e.key === 'Enter') { add(b, oi, '.oldadd input'); var box = pair.querySelector('[data-book="' + b.id + '"] .oldadd'); if (box) { box.classList.add('open'); box.querySelector('input').focus(); } } };
  node.querySelector('.oldok').onclick = function(){ add(b, oi, null); };
}
books.forEach(function(b){ draw(b); });

var tabs = document.querySelectorAll('.ltab');
tabs.forEach(function(t){
  t.addEventListener('click', function(){
    tabs.forEach(function(o){ o.setAttribute('aria-selected', o === t); });
    pair.className = 'nbpair lay-' + t.dataset.lay; cap.textContent = t.dataset.desc;
    pair.animate([{ opacity: .4, transform: 'scale(.985)' }, { opacity: 1, transform: 'none' }], { duration: 260, easing: 'ease-out' });
  });
});
// A link ending in #look-paper (or another layout) opens with that look.
var m = /^#look-(\w+)$/.exec(location.hash); if (m) { tabs.forEach(function(t){ if (t.dataset.lay === m[1]) t.click(); }); document.querySelectorAll('#looks .reveal').forEach(function(r){ r.classList.add('in'); }); document.getElementById('nbpair').scrollIntoView({ block: 'center' }); }
})();
"""

def nb_text(lang):
    if lang == 'zh':
        return dict(
            books=[
                dict(id='ddl', label='DDL · 截止本', short='DDL', sorted=True, pages=[
                    dict(title='课程作业', items=[['物理实验报告', 3, '23:59'], ['高数习题 3.2', 1, '12:00'], ['英语作文', 4, None], ['小组展示 PPT', 9, '09:00']]),
                    dict(title='上周', items=[['线代作业 2', -3, '23:59', True], ['文献综述初稿', -1, '18:00', True]])]),
                dict(id='todo', label='Todo · 待办本', short='Todo', sorted=False, pages=[
                    dict(title='本周要做', items=[['复习线性代数第 3 章', None, None], ['预约图书馆研讨室', None, None, True], ['小组讨论分工', None, None], ['整理实验数据', 4, '17:00']]),
                    dict(title='以后再说', items=[['换一个新书包', None, None], ['整理电脑桌面', None, None]])])],
            wd=['周日', '周一', '周二', '周三', '周四', '周五', '周六'],
            today='今天', tomorrow='明天', left='剩余 {0} 天', lead='提前 15 分钟', leadDay='当天 09:00 提醒',
            dueToday='今天截止', dueTomorrow='明天截止', done='已完成', noDue='未设置截止时间',
            date='{m} 月 {d} 日', toDo='{0} 项待完成', doneOf='{0}/{1} 完成', doneOf2='{0} / {1} 已完成', saved='已保存',
            pin='置顶', hide='隐藏', notes='页内备注', notesOld='文字记录', notePh='随手记点什么…', addPh='添加一项任务…', page='第 {0} / {1} 页',
            newPage='新一页', newPageShort='新页', newTitle='新的一页', created='创建于 {0}', rename='重命名便签', tasks='任务',
            addTask='添加任务', ok='添加', edit='编辑', remove='删除', hint='隐藏、翻页、归档后仍会提醒', dbl='双击修改')
    return dict(
        books=[
            dict(id='ddl', label='Deadlines', short='Deadlines', sorted=True, pages=[
                dict(title='Coursework', items=[['Physics lab report', 3, '23:59'], ['Calculus problem set 3.2', 1, '12:00'], ['English essay', 4, None], ['Group presentation slides', 9, '09:00']]),
                dict(title='Last week', items=[['Linear algebra set 2', -3, '23:59', True], ['Literature review draft', -1, '18:00', True]])]),
            dict(id='todo', label='To-do', short='To-do', sorted=False, pages=[
                dict(title='This week', items=[['Review Linear Algebra ch. 3', None, None], ['Book a library study room', None, None, True], ['Split up the group project', None, None], ['Tidy up the lab data', 4, '17:00']]),
                dict(title='Someday', items=[['Get a new backpack', None, None], ['Clean up the desktop', None, None]])])],
        wd=['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'],
        today='Today', tomorrow='Tomorrow', left='{0} days left', lead='15 min before', leadDay='reminder 09:00 that day',
        dueToday='Due today', dueTomorrow='Due tomorrow', done='Done', noDue='No deadline',
        date='{m}/{d}', toDo='{0} to do', doneOf='{0}/{1} done', doneOf2='{0} / {1} done', saved='Saved',
        pin='Pin', hide='Hide', notes='Page notes', notesOld='Notes', notePh='Jot something down…', addPh='Add a task…', page='Page {0} of {1}',
        newPage='New page', newPageShort='New page', newTitle='New page', created='Created {0}', rename='Rename', tasks='Tasks',
        addTask='Add task', ok='Add', edit='Edit', remove='Delete', hint='Reminders continue when hidden, paged or archived', dbl='Double-click to edit')
