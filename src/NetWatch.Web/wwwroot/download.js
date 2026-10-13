window.netwatchDownload = (fileName, base64, contentType = 'application/octet-stream') => {
    const binary = atob(base64);
    const bytes = new Uint8Array(binary.length);
    for (let index = 0; index < binary.length; index++) bytes[index] = binary.charCodeAt(index);
    const url = URL.createObjectURL(new Blob([bytes], { type: contentType }));
    const link = document.createElement('a');
    link.download = fileName;
    link.href = url;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
};

window.netwatchPdfPreview = (() => {
    const windows = new Map();

    const loadingDocument = `<!doctype html><html lang="es"><head><meta charset="utf-8"><title>Preparando reporte | NetWatch</title><style>body{margin:0;min-height:100vh;display:grid;place-items:center;background:#f3f6fa;font-family:system-ui,sans-serif;color:#172234}.card{width:min(420px,calc(100vw - 40px));padding:34px;border-radius:20px;background:white;box-shadow:0 24px 70px rgba(14,35,59,.18);text-align:center}.spinner{width:38px;height:38px;margin:0 auto 18px;border:4px solid #dce7f6;border-top-color:#2563eb;border-radius:50%;animation:spin .8s linear infinite}h1{font-size:1.2rem;margin:0 0 8px}p{font-size:.9rem;color:#66768a;margin:0}@keyframes spin{to{transform:rotate(360deg)}}</style></head><body><div class="card"><div class="spinner"></div><h1>Preparando vista previa</h1><p>NetWatch está organizando el reporte en formato vertical.</p></div></body></html>`;

    return {
        open: () => {
            const id = `netwatch-pdf-${Date.now()}-${Math.random().toString(16).slice(2)}`;
            const preview = window.open('', '_blank');
            if (!preview) return null;
            preview.document.open();
            preview.document.write(loadingDocument);
            preview.document.close();
            windows.set(id, preview);
            return id;
        },
        show: (id, fileName, base64) => {
            const preview = windows.get(id);
            if (!preview || preview.closed) throw new Error('La ventana de vista previa fue cerrada.');
            const binary = atob(base64);
            const bytes = new Uint8Array(binary.length);
            for (let index = 0; index < binary.length; index++) bytes[index] = binary.charCodeAt(index);
            const url = URL.createObjectURL(new Blob([bytes], { type: 'application/pdf' }));
            preview.document.title = fileName;
            preview.location.replace(url);
            preview.focus();
            windows.delete(id);
            window.setTimeout(() => URL.revokeObjectURL(url), 15 * 60 * 1000);
        },
        close: (id) => {
            const preview = windows.get(id);
            if (preview && !preview.closed) preview.close();
            windows.delete(id);
        }
    };
})();

window.netwatchGuide = (() => {
    const waitForTarget = async (selector) => {
        for (let attempt = 0; attempt < 25; attempt++) {
            const target = document.querySelector(selector);
            if (target && target.getBoundingClientRect().width > 0) return target;
            await new Promise(resolve => window.setTimeout(resolve, 80));
        }
        return null;
    };

    const clamp = (value, minimum, maximum) => Math.max(minimum, Math.min(value, maximum));

    return {
        show: async (selector) => {
            const target = await waitForTarget(selector);
            const spotlight = document.getElementById('netwatch-guide-spotlight');
            const card = document.getElementById('netwatch-guide-card');
            if (!target || !spotlight || !card) return false;

            card.style.visibility = 'hidden';
            target.scrollIntoView({ block: 'center', inline: 'nearest', behavior: 'smooth' });
            await new Promise(resolve => window.setTimeout(resolve, 280));

            const rect = target.getBoundingClientRect();
            const padding = 8;
            const top = clamp(rect.top - padding, 8, window.innerHeight - 24);
            const left = clamp(rect.left - padding, 8, window.innerWidth - 24);
            const right = clamp(rect.right + padding, 24, window.innerWidth - 8);
            const bottom = clamp(rect.bottom + padding, 24, window.innerHeight - 8);
            spotlight.style.top = `${top}px`;
            spotlight.style.left = `${left}px`;
            spotlight.style.width = `${Math.max(16, right - left)}px`;
            spotlight.style.height = `${Math.max(16, bottom - top)}px`;

            const cardRect = card.getBoundingClientRect();
            const margin = 16;
            const gap = 20;
            let cardLeft;
            let cardTop;
            if (right + gap + cardRect.width <= window.innerWidth - margin) {
                cardLeft = right + gap;
                cardTop = clamp(top, margin, window.innerHeight - cardRect.height - margin);
            } else if (left - gap - cardRect.width >= margin) {
                cardLeft = left - gap - cardRect.width;
                cardTop = clamp(top, margin, window.innerHeight - cardRect.height - margin);
            } else if (bottom + gap + cardRect.height <= window.innerHeight - margin) {
                cardLeft = clamp((window.innerWidth - cardRect.width) / 2, margin, window.innerWidth - cardRect.width - margin);
                cardTop = bottom + gap;
            } else {
                cardLeft = clamp((window.innerWidth - cardRect.width) / 2, margin, window.innerWidth - cardRect.width - margin);
                cardTop = Math.max(margin, top - gap - cardRect.height);
            }
            card.style.left = `${cardLeft}px`;
            card.style.top = `${cardTop}px`;
            card.style.visibility = 'visible';
            return true;
        },
        clear: () => {
            const card = document.getElementById('netwatch-guide-card');
            if (card) card.style.visibility = 'hidden';
        }
    };
})();
