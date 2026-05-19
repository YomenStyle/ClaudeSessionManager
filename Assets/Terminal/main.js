var term = null;
var fitAddon = null;
var resizeDebounceMs = 150;

function post(o) {
    window.chrome.webview.postMessage(JSON.stringify(o));
}

window.chrome.webview.addEventListener('message', function(e) {
    var m = JSON.parse(e.data);
    switch (m.type) {
        case 'config':
            initTerm(m);
            break;
        case 'data':
            if (term) term.write(m.payload);
            if (m.payload && m.payload.indexOf('\x1b]7;') !== -1) {
                post({type:'debug', msg:'osc7-in-data', sample: m.payload.substring(0, 80)});
            }
            break;
        case 'exit':
            if (term) term.writeln('\r\n[exit ' + m.code + ']');
            break;
        case 'error':
            if (term) term.writeln('\r\n[error] ' + m.message);
            break;
    }
});

function initTerm(cfg) {
    term = new Terminal({
        allowProposedApi: true,
        screenReaderMode: false,
        scrollback: cfg.scrollback,
        cursorBlink: cfg.cursorBlink,
        fontFamily: cfg.fontFamily,
        fontSize: cfg.fontSize,
        theme: cfg.theme === 'dark' ? { background: '#0C0C0C', foreground: '#CCCCCC' } : undefined
    });
    fitAddon = new FitAddon.FitAddon();
    term.loadAddon(fitAddon);
    term.open(document.getElementById('terminal'));
    fitAddon.fit();
    term.unicode.register({
        version: '11',
        wcwidth(codepoint) {
            if (codepoint >= 0x1100 && codepoint <= 0x115F) return 2;
            if (codepoint >= 0x2E80 && codepoint <= 0x303E) return 2;
            if (codepoint >= 0x3041 && codepoint <= 0x33FF) return 2;
            if (codepoint >= 0x3400 && codepoint <= 0x4DBF) return 2;
            if (codepoint >= 0x4E00 && codepoint <= 0x9FFF) return 2;
            if (codepoint >= 0xA000 && codepoint <= 0xA4CF) return 2;
            if (codepoint >= 0xAC00 && codepoint <= 0xD7A3) return 2;
            if (codepoint >= 0xF900 && codepoint <= 0xFAFF) return 2;
            if (codepoint >= 0xFE30 && codepoint <= 0xFE4F) return 2;
            if (codepoint >= 0xFF00 && codepoint <= 0xFF60) return 2;
            if (codepoint >= 0xFFE0 && codepoint <= 0xFFE6) return 2;
            if (codepoint >= 0x20000 && codepoint <= 0x2FFFD) return 2;
            if (codepoint >= 0x30000 && codepoint <= 0x3FFFD) return 2;
            return 1;
        }
    });
    term.unicode.activeVersion = '11';
    term.attachCustomKeyEventHandler(function(e) {
        if (e.type !== 'keydown') return true;
        if (e.ctrlKey && !e.altKey && (e.key === 'v' || e.key === 'V')) {
            if (e.preventDefault) e.preventDefault();
            if (e.stopPropagation) e.stopPropagation();
            post({ type: 'paste-request' });
            return false;
        }
        return true;
    });
    resizeDebounceMs = cfg.resizeDebounceMs || 150;
    term.onData(function(d) { post({ type: 'input', data: d }); });
    try {
        if (typeof term.onFocus === 'function') {
            term.onFocus(function() { post({ type: 'focus' }); });
        } else if (term.textarea) {
            term.textarea.addEventListener('focus', function() { post({ type: 'focus' }); });
        }
    } catch (e) { /* xterm focus API 미지원 무시 */ }
    var __termEl = document.getElementById('terminal');
    if (__termEl) {
        __termEl.addEventListener('mousedown', function() { post({ type: 'focus' }); });
    }
    if (term.parser && term.parser.registerOscHandler) {
        term.parser.registerOscHandler(7, function(payload) {
            try {
                post({type:'debug', msg:'osc7-cb', payload: payload});
                var m = payload.match(/^file:\/\/[^/]*\/(.*)$/);
                if (m) {
                    var raw = decodeURIComponent(m[1]);
                    // Windows: 'C:/Users/...' -> 'C:\Users\...'
                    var path = raw.replace(/\//g, '\\');
                    if (path) post({ type: 'cwd', path: path });
                }
            } catch (e) {}
            return true;
        });
    }
    var rt = null;
    new ResizeObserver(function() {
        if (rt) clearTimeout(rt);
        rt = setTimeout(function() {
            fitAddon.fit();
            post({ type: 'resize', cols: term.cols, rows: term.rows });
        }, resizeDebounceMs);
    }).observe(document.getElementById('terminal'));
}

post({ type: 'ready' });
