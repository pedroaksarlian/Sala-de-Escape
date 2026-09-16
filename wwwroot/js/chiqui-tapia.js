// chiqui-tapia.js
(function () {
    const COLORS = [
        { name: 'amarillo', hex: '#f1c40f' },
        { name: 'rojo', hex: '#e74c3c' },
        { name: 'azul', hex: '#3498db' },
        { name: 'verde', hex: '#2ecc71' },
        { name: 'violeta', hex: '#9b59b6' }
    ];

    const LEVELS = [3, 4, 5]; // order: easy, medium, hard
    let unlockedIndex = 0; // 0 -> only easy unlocked
    let level = LEVELS[unlockedIndex];

    let secret = []; // permutation of 0..level-1
    let current = []; // -1 means empty, otherwise color index
    let selectedPalette = null;

    function randPermutation(n) {
        const arr = Array.from({ length: n }, (_, i) => i);
        for (let i = arr.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [arr[i], arr[j]] = [arr[j], arr[i]];
        }
        return arr;
    }

    function initForLevel(n) {
        level = n;
        secret = randPermutation(level);
        current = Array.from({ length: level }, () => -1);
        selectedPalette = null;
        updateLevelButtons();
        render();
        clearResult();
    }

    function updateLevelButtons() {
        const buttons = document.querySelectorAll('.level-btn');
        buttons.forEach(btn => {
            const val = Number(btn.getAttribute('data-level'));
            const idx = LEVELS.indexOf(val);
            if (idx === -1) return;
            if (idx <= unlockedIndex) {
                btn.classList.remove('locked');
                btn.disabled = false;
            } else {
                btn.classList.add('locked');
                btn.disabled = true;
            }
            // active state
            if (val === level) btn.classList.add('active'); else btn.classList.remove('active');
        });
    }

    function render() {
        renderPalette();
        renderSlots();
    }

    function renderPalette() {
        const pal = document.getElementById('palette');
        if (!pal) return;
        pal.innerHTML = '';
        for (let i = 0; i < level; i++) {
            const item = document.createElement('button');
            item.type = 'button';
            item.className = 'palette-item';
            item.style.background = COLORS[i].hex;
            item.title = COLORS[i].name;
            if (isColorPlaced(i)) {
                item.classList.add('placed');
                item.disabled = true;
            }
            if (selectedPalette === i) item.classList.add('selected');
            item.addEventListener('click', () => {
                if (isColorPlaced(i)) return;
                selectedPalette = selectedPalette === i ? null : i;
                renderPalette();
            });
            pal.appendChild(item);
        }
    }

    function renderSlots() {
        const slots = document.getElementById('slots');
        if (!slots) return;
        slots.innerHTML = '';
        for (let i = 0; i < level; i++) {
            const s = document.createElement('div');
            s.className = 'slot';
            s.setAttribute('data-index', i);
            s.tabIndex = 0;
            if (current[i] !== -1) {
                s.style.background = COLORS[current[i]].hex;
                s.setAttribute('data-color', COLORS[current[i]].name);
            } else {
                s.style.background = 'transparent';
                s.removeAttribute('data-color');
            }
            s.addEventListener('click', () => onSlotClick(i));
            slots.appendChild(s);
        }
    }

    function isColorPlaced(colorIdx) {
        return current.includes(colorIdx);
    }

    function onSlotClick(slotIdx) {
        if (selectedPalette !== null) {
            const color = selectedPalette;
            const previousSlot = current.findIndex((v) => v === color);
            if (previousSlot !== -1) current[previousSlot] = -1;
            current[slotIdx] = color;
            selectedPalette = null;
            render();
            return;
        }

        if (current[slotIdx] !== -1) {
            current[slotIdx] = -1;
            render();
        }
    }

    function clearResult() {
        const r = document.getElementById('result');
        if (!r) return;
        r.textContent = '';
        r.classList.remove('text-success', 'text-danger', 'text-muted');
    }

    function verify() {
        const r = document.getElementById('result');
        if (!r) return;
        if (current.some(v => v === -1)) {
            r.textContent = 'Completá todas las posiciones antes de verificar.';
            r.classList.remove('text-success');
            r.classList.add('text-danger');
            return;
        }

        let correct = 0;
        for (let i = 0; i < level; i++) if (current[i] === secret[i]) correct++;
        r.textContent = `El chiqui dice: ${correct} luces en la posición correcta.`;
        if (correct === level) {
            r.classList.remove('text-danger');
            r.classList.add('text-success');
            const pattern = secret.map(i => COLORS[i].name).join(', ');
            r.textContent += ` ¡Secuencia correcta! Patrón: ${pattern}`;

            // If not final level, unlock next and advance locally; else notify server and redirect
            const levelIdx = LEVELS.indexOf(level);
            if (levelIdx < LEVELS.length - 1) {
                // unlock next
                unlockedIndex = Math.max(unlockedIndex, levelIdx + 1);
                setTimeout(() => {
                    // notify user and auto-advance to next level
                    const nextLevel = LEVELS[levelIdx + 1];
                    r.textContent = `Nivel ${level} completado. Se desbloqueó nivel ${nextLevel}. Avanzando...`;
                    initForLevel(nextLevel);
                }, 800);
            } else {
                // final: complete room on server
                const tiempoActual = window.getTiempoRestanteActual ? window.getTiempoRestanteActual() : 0;
                fetch('/Home/TapiaComplete', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8' },
                    body: 'tiempoSegundos=' + encodeURIComponent(tiempoActual)
                })
                    .then(resp => resp.json())
                    .then(json => {
                        if (json && json.redirect) {
                            setTimeout(() => { window.location.href = json.redirect; }, 900);
                        }
                    })
                    .catch(() => {});
            }
        } else {
            r.classList.remove('text-success');
            r.classList.add('text-danger');
        }
    }

    document.addEventListener('DOMContentLoaded', () => {
        const levelButtons = document.querySelectorAll('.level-btn');
        levelButtons.forEach(btn => {
            btn.addEventListener('click', () => {
                const n = Number(btn.getAttribute('data-level')) || LEVELS[LEVELS.length - 1];
                const idx = LEVELS.indexOf(n);
                if (idx === -1) return;
                if (idx > unlockedIndex) return; // locked
                initForLevel(n);
            });
        });

        const verifyBtn = document.getElementById('verifyBtn');
        if (verifyBtn) verifyBtn.addEventListener('click', verify);
        const resetBtn = document.getElementById('resetBtn');
        if (resetBtn) resetBtn.addEventListener('click', () => initForLevel(level));

        // start with only easy unlocked
        unlockedIndex = 0;
        initForLevel(LEVELS[unlockedIndex]);
    });

})();
