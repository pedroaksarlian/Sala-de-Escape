// scaloni.js
(function(){
    let rows = 8;
    let cols = 16;
    let path = []; // path[col] = row
    let userIndex = 0;
    let errors = 0;
    let showing = false;
    const mazeGrid = () => document.getElementById('mazeGrid');
    const resultEl = () => document.getElementById('mazeResult');

    function makePath(r, c) {
        const p = new Array(c);
        let cur = Math.floor(Math.random() * r);
        p[0] = cur;
        for (let col = 1; col < c; col++) {
            // Solo movimientos adyacentes para mantener el camino contiguo [-1, 0, 1]
            const moves = [-1, 0, 1];
            const choice = moves[Math.floor(Math.random()*moves.length)];
            let next = cur + choice;
            if (next < 0) next = 0;
            if (next >= r) next = r - 1;
            cur = next;
            p[col] = cur;
        }
        return p;
    }

    function buildGrid() {
        const grid = mazeGrid();
        grid.innerHTML = '';
        grid.style.gridTemplateColumns = `repeat(${cols}, 1fr)`;
        for (let row=0; row<rows; row++){
            for (let col=0; col<cols; col++){
                const cell = document.createElement('div');
                cell.className = 'maze-cell';
                cell.setAttribute('data-row', row);
                cell.setAttribute('data-col', col);
                cell.addEventListener('click', () => onCellClick(row, col, cell));
                grid.appendChild(cell);
            }
        }
    }

    function showPath(duration=2000){
        showing = true;
        clearHighlights();
        path.forEach((r, col) => {
            const sel = mazeGrid().querySelector(`.maze-cell[data-row='${r}'][data-col='${col}']`);
            if (sel) sel.classList.add('maze-path');
        });
        setTimeout(()=>{ hidePath(); showing=false; }, duration);
    }

    function hidePath(){
        clearHighlights();
    }

    function clearHighlights(){
        mazeGrid().querySelectorAll('.maze-cell').forEach(el=>{
            el.classList.remove('maze-path','maze-correct','maze-wrong','maze-active');
        });
    }

    function resetProgress(){
        userIndex = 0;
        clearHighlights();
    }

    function onCellClick(row,col,cellEl){
        if (showing) return; // ignore clicks while showing
        // expected col must equal userIndex
        if (col !== userIndex) {
            // wrong col
            cellEl.classList.add('maze-wrong');
            errors++;
            resetProgress();
            if (errors >= 5) {
                errors = 0; showPath(2500);
            }
            return;
        }

        // check row
        const expectedRow = path[userIndex];
        if (row === expectedRow) {
            cellEl.classList.add('maze-correct');
            userIndex++;
            if (userIndex === cols) {
                // success: completed path
                resultEl().textContent = 'Correcto: seguiste el camino completo. Avanzando...';
                // POST to server to save progress and get redirect
                fetch('/Home/ScaloniComplete', { method: 'POST', headers:{'Content-Type':'application/json'}, body:'{}'})
                    .then(r=>r.json())
                    .then(j=>{ if (j && j.redirect) setTimeout(()=> window.location.href = j.redirect,900); })
                    .catch(()=>{});
            }
        } else {
            cellEl.classList.add('maze-wrong');
            errors++;
            resetProgress();
            if (errors >= 5) {
                errors = 0; showPath(2500);
            }
        }
    }

    document.addEventListener('DOMContentLoaded', ()=>{
        const newBtn = document.getElementById('newPathBtn');
        const revealBtn = document.getElementById('revealPathBtn');

        function gen(){
            path = makePath(rows, cols);
            buildGrid();
            resetProgress();
            // show once
            setTimeout(()=> showPath(2500), 300);
        }

        newBtn.addEventListener('click', (e)=>{ e.preventDefault(); gen(); });
        revealBtn.addEventListener('click', (e)=>{ e.preventDefault(); showPath(2200); });

        // initial gen
        path = makePath(rows, cols);
        buildGrid();
        setTimeout(()=> showPath(2500), 300);
    });

})();
