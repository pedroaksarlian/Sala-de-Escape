document.addEventListener('DOMContentLoaded', function () {
  const hotspots = Array.from(document.querySelectorAll('.hotspot'));
  const STORAGE_KEY = 'acunaHotspots-v2';
  const LEGACY_STORAGE_KEY = 'acunaHotspots';

  if (!hotspots.length) return;

  // remove any leftover checklist UI if present
  const checklist = document.querySelector('.check-list');
  if (checklist) checklist.remove();

  function hasManualInlinePositions() {
    return hotspots.some(h => (h.style.left && h.style.top) || h.style.left || h.style.top);
  }

  function saveCurrentPositions() {
    const map = {};
    hotspots.forEach(h => {
      const item = h.getAttribute('data-item');
      map[item] = {
        left: h.style.left || getComputedStyle(h).left,
        top: h.style.top || getComputedStyle(h).top,
        radius: h.getAttribute('data-radius-percent')
      };
    });
    localStorage.setItem(STORAGE_KEY, JSON.stringify(map));
  }

  function resetToDefaults() {
    const defaultMap = {
      gorra: { left: '31%', top: '12%', radius: '4' },
      remera: { left: '55%', top: '15%', radius: '4' },
      mochila: { left: '69%', top: '76%', radius: '6' }
    };

    hotspots.forEach(h => {
      const item = h.getAttribute('data-item');
      if (defaultMap[item]) {
        h.style.left = defaultMap[item].left;
        h.style.top = defaultMap[item].top;
        h.setAttribute('data-radius-percent', defaultMap[item].radius);
      }
    });

    saveCurrentPositions();
  }

  // Load saved positions from localStorage (if any), but do NOT overwrite inline values manually set in the markup.
  function loadSavedPositions() {
    try {
      localStorage.removeItem(LEGACY_STORAGE_KEY);

      if (hasManualInlinePositions()) {
        saveCurrentPositions();
        return;
      }

      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) {
        resetToDefaults();
        return;
      }

      const map = JSON.parse(raw);
      let hasValidData = false;

      hotspots.forEach(h => {
        const item = h.getAttribute('data-item');
        if (map[item] && map[item].left && map[item].top) {
          h.style.left = map[item].left;
          h.style.top = map[item].top;
          if (map[item].radius) h.setAttribute('data-radius-percent', map[item].radius);
          hasValidData = true;
        }
      });

      if (!hasValidData) {
        resetToDefaults();
      }
    } catch (e) {
      console.warn('Could not load saved hotspots', e);
      resetToDefaults();
    }
  }

  function updatePositionsDisplay() {
    const area = document.getElementById('next-button-area');
    if (!area) return;
    const parts = hotspots.map(h => {
      const item = h.getAttribute('data-item');
      const left = h.style.left || getComputedStyle(h).left || '0%';
      const top = h.style.top || getComputedStyle(h).top || '0%';
      const radius = h.getAttribute('data-radius-percent') || '';
      return `${item}: ${left} / ${top}${radius ? (' (r=' + radius + '%)') : ''}`;
    });
    area.textContent = parts.join('   |   ');
  }

  loadSavedPositions();
  updatePositionsDisplay();

  // mark found state
  function markFound(h) {
    if (!h || h.classList.contains('found')) return false;
    h.classList.add('found');
    const live = document.getElementById('find-live');
    const item = h.getAttribute('data-item');
    if (live && item) live.textContent = `${item} encontrado`;
    return true;
  }

  // Check if all found -> auto-submit
  function checkAllFound() {
    const found = hotspots.filter(h => h.classList.contains('found')).map(h => h.getAttribute('data-item'));
    if (found.length >= 3) {
      const form = document.createElement('form');
      form.method = 'POST';
      // post to dedicated endpoint that marks Acuna complete and redirects to Demichelis
      form.action = '/Home/AdvanceFromAcuna';
      found.forEach(item => {
        const input = document.createElement('input');
        input.type = 'hidden';
        input.name = 'objetos';
        input.value = item;
        form.appendChild(input);
      });
      document.body.appendChild(form);
      form.submit();
    }
  }

  // hotspot click handlers
  hotspots.forEach(h => {
    h.addEventListener('click', function (e) {
      e.stopPropagation();
      const changed = markFound(h);
      if (changed) checkAllFound();
    });
  });

  // image click: find nearest hotspot within radius
  const image = document.getElementById('acuna-image');
  if (image) {
    image.addEventListener('click', function (ev) {
      const rect = image.getBoundingClientRect();
      const clickX = ev.clientX - rect.left;
      const clickY = ev.clientY - rect.top;
      const imgW = rect.width;
      const imgH = rect.height;

      // If user Shift+clicks, set nearest hotspot position and save
      if (ev.shiftKey) {
        // find nearest hotspot center
        let nearest = null;
        let bestDist = Infinity;
        hotspots.forEach(h => {
          let left = h.style.left || '';
          let top = h.style.top || '';
          let cx = left.endsWith('%') ? (parseFloat(left) / 100) * imgW : parseFloat(getComputedStyle(h).left) || 0;
          let cy = top.endsWith('%') ? (parseFloat(top) / 100) * imgH : parseFloat(getComputedStyle(h).top) || 0;
          const dx = clickX - cx;
          const dy = clickY - cy;
          const dist = Math.sqrt(dx * dx + dy * dy);
          if (dist < bestDist) { bestDist = dist; nearest = h; }
        });
        if (nearest) {
          const leftPct = Math.max(0, Math.min(100, (clickX / imgW) * 100));
          const topPct = Math.max(0, Math.min(100, (clickY / imgH) * 100));
          nearest.style.left = leftPct.toFixed(2) + '%';
          nearest.style.top = topPct.toFixed(2) + '%';
          saveCurrentPositions();
          updatePositionsDisplay();
          alert('Posición actualizada y guardada localmente para: ' + nearest.getAttribute('data-item'));
        }
        return;
      }

      let foundAny = false;

      hotspots.forEach(h => {
        if (h.classList.contains('found')) return;
        let left = h.style.left || '';
        let top = h.style.top || '';
        let cx, cy;
        if (left.endsWith('%')) cx = (parseFloat(left) / 100) * imgW; else cx = parseFloat(getComputedStyle(h).left) || 0;
        if (top.endsWith('%')) cy = (parseFloat(top) / 100) * imgH; else cy = parseFloat(getComputedStyle(h).top) || 0;
        const dx = clickX - cx;
        const dy = clickY - cy;
        const dist = Math.sqrt(dx * dx + dy * dy);
        const radiusPercent = parseFloat(h.getAttribute('data-radius-percent')) || 4;
        const radiusPx = (radiusPercent / 100) * imgW;
        if (dist <= radiusPx) {
          const changed = markFound(h);
          if (changed) foundAny = true;
        }
      });

      if (foundAny) checkAllFound();
    });
  }
});
