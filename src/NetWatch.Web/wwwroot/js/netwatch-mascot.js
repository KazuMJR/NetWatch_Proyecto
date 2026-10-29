(() => {
    "use strict";
    const moods = ["idle", "curious", "groom", "nap", "walk", "run", "alert", "celebrate", "scratch", "play", "sit", "stretch", "drag"];
    const profiles = {
        login: { actions: ["sit", "groom", "nap", "stretch"], selectors: [".login-card", ".login-feature"] },
        dashboard: { actions: ["curious", "groom", "sit", "stretch", "nap"], selectors: [".metric-card", ".panel"] },
        devices: { actions: ["curious", "groom", "sit", "nap"], selectors: [".table", ".page-actions"] },
        "device-detail": { actions: ["curious", "sit", "groom", "nap"], selectors: [".detail-hero", ".panel"] },
        "device-form": { actions: ["sit", "groom", "stretch", "nap"], selectors: [".panel", ".form-control"] },
        events: { actions: ["curious", "sit", "stretch", "nap"], selectors: [".table", ".panel"] },
        reports: { actions: ["sit", "groom", "play", "nap"], selectors: ["[data-guide='report-export']", ".panel"] },
        users: { actions: ["sit", "curious", "groom", "nap"], selectors: [".table", ".page-actions"] },
        default: { actions: ["sit", "groom", "stretch", "nap"], selectors: ["main"] }
    };
    const labels = {
        calm: "Tranquilo: descansa y cambia de postura aquí.",
        explore: "Explorando despacio, con descansos entre paseos.",
        sit: "Sentado aquí. Puedes seguir trabajando.",
        nap: "Durmiendo. Zzz…",
        groom: "Aseándose con calma.",
        play: "Jugando con su ovillo.",
        stretch: "Estirándose después de una siesta."
    };
    const clamp = (n, min, max) => Math.min(max, Math.max(min, n));
    const ACTIVITY_MS = 60000;
    const CHASE_MS = 18000;
    const SWAT_MS = 2300;
    const CARRY_MS = 4200;
    let active = null;
    const corners = ["bottom-right", "bottom-left", "top-right", "top-left", "free"];
    function dockPreference() {
        try { const saved = localStorage.getItem("netwatch-nero-dock"); return corners.includes(saved) ? saved : "bottom-right"; }
        catch { return "bottom-right"; }
    }
    function positionDock(state) {
        if (state.dockCorner === "free") return;
        const b = bounds(state);
        state.x = state.dockCorner.endsWith("right") ? Math.max(b.minX, b.maxX - 16) : b.minX + 16;
        state.y = state.dockCorner.startsWith("bottom") ? Math.max(b.minY, b.maxY - 16) : b.minY + 16;
        state.x = clamp(state.x, b.minX, b.maxX); state.y = clamp(state.y, b.minY, b.maxY);
        stop(state);
    }

    function bounds(state) {
        // Only the viewport edges constrain dragging; the sidebar and header are reachable.
        const viewport = window.visualViewport;
        const minX = viewport?.offsetLeft || 0;
        const minY = viewport?.offsetTop || 0;
        const width = state.character.offsetWidth || 72;
        const height = state.character.offsetHeight || 72;
        const right = minX + (viewport?.width || innerWidth);
        const bottom = minY + (viewport?.height || innerHeight);
        return { minX, minY, right, bottom, width, height,
            maxX: Math.max(minX, right - width), maxY: Math.max(minY, bottom - height) };
    }

    function clampPosition(state) {
        const b = bounds(state);
        if (!state.entering) {
            state.x = clamp(state.x, b.minX, b.maxX);
            state.y = clamp(state.y, b.minY, b.maxY);
        }
        state.targetX = clamp(state.targetX, b.minX, b.maxX);
        state.targetY = clamp(state.targetY, b.minY, b.maxY);
    }

    function setMood(state, mood) {
        if (!moods.includes(mood)) return;
        const cat = state.character.querySelector(".pixel-cat");
        if (cat && !cat.classList.contains("mood-" + mood)) {
            moods.forEach(name => cat.classList.remove("mood-" + name));
            cat.classList.add("mood-" + mood);
        }
        state.mood = mood;
        state.root.dataset.mascotMood = mood;
        cat?.classList.toggle("is-annoyed", state.chasing);
        cat?.classList.toggle("is-batting", state.batting);
        cat?.classList.toggle("motion-enabled", !state.reducedMotion);
        cat?.classList.toggle("motion-disabled", state.reducedMotion);
        state.root.classList.toggle("is-chasing", state.chasing);
    }

    function syncControls(state) {
        const motion = state.root.querySelector(".mascot-motion");
        if (motion) motion.checked = !state.reducedMotion;
        const notice = state.root.querySelector(".mascot-motion-notice");
        if (notice) notice.hidden = !state.reducedMotion;
        const label = state.dockCorner !== "free" ? "Modo discreto: Nero permanece en esta esquina."
            : state.reducedMotion ? "Animaciones pausadas. Activa «Animar a Nero» para jugar."
            : state.batting ? "¡Paf, paf! Nero juega con el cursor. Mueve el ratón o pulsa Esc."
            : state.carrying ? "¡Te atrapé! Mueve el ratón o pulsa Esc para soltarlo."
            : state.chasing ? "¡Nero persigue el cursor!"
            : state.perch && !state.moving ? "Descansando sobre el borde. Se quedará aquí un minuto."
            : labels[state.mode === "stay" ? state.selectedMood : state.mode];
        state.root.dataset.mascotMode = state.mode;
        for (const element of state.root.querySelectorAll(".mascot-status, .mascot-control-status")) {
            if (element.textContent !== label) element.textContent = label;
        }
        for (const button of state.root.querySelectorAll("[data-mascot-command]")) {
            const selected = state.mode === "stay" ? state.selectedMood : state.mode;
            button.setAttribute("aria-pressed", String(button.dataset.mascotCommand === selected));
        }
    }

    function layout(state) {
        const b = bounds(state);
        const toggleX = state.x + b.width + 1 > b.right - 28 ? state.x - 25 : state.x + b.width - 4;
        state.root.style.setProperty("--menu-x", Math.round(clamp(toggleX, b.minX + 2, b.right - 30)) + "px");
        state.root.style.setProperty("--menu-y", Math.round(clamp(state.y + 38, b.minY + 2, b.bottom - 30)) + "px");
        const panel = state.root.querySelector(".mascot-controls") || state.root.querySelector(".mascot-message");
        if (panel) {
            const w = panel.offsetWidth || 280;
            const h = panel.offsetHeight || 180;
            const left = state.x - w - 12;
            const right = state.x + b.width + 12;
            let x = left >= b.minX + 8 ? left : right;
            let y = state.y - h + b.height;
            if (left < b.minX + 8 && right + w > b.right - 8) {
                x = state.x + b.width / 2 - w / 2;
                y = state.y - h - 10;
                if (y < b.minY + 8) y = state.y + b.height + 10;
            }
            x = clamp(x, b.minX + 8, Math.max(b.minX + 8, b.right - w - 8));
            y = clamp(y, b.minY + 8, Math.max(b.minY + 8, b.bottom - h - 8));
            state.root.style.setProperty("--bubble-x", Math.round(x) + "px");
            state.root.style.setProperty("--bubble-y", Math.round(y) + "px");
            const headX = state.x + b.width / 2, headY = state.y + 16;
            const edgeX = clamp(headX, x + 10, x + w - 10), edgeY = clamp(headY, y + 10, y + h - 10);
            for (const [name, fraction, radius] of [["near", .32, 4], ["far", .68, 7]]) {
                state.root.style.setProperty(`--thought-${name}-x`, Math.round(headX + (edgeX - headX) * fraction - radius) + "px");
                state.root.style.setProperty(`--thought-${name}-y`, Math.round(headY + (edgeY - headY) * fraction - radius) + "px");
            }
        }
        syncControls(state);
    }

    function applyPosition(state) {
        positionDock(state);
        state.root.style.setProperty("--mascot-x", Math.round(state.x) + "px");
        state.root.style.setProperty("--mascot-y", Math.round(state.y) + "px");
        state.root.style.setProperty("--mascot-facing", String(state.facing));
        const b = bounds(state);
        state.root.style.setProperty("--cursor-x", Math.round(clamp(state.x + (state.facing > 0 ? 51 : 5), b.minX, b.right - 22)) + "px");
        state.root.style.setProperty("--cursor-y", Math.round(clamp(state.y + (state.batting ? 48 : 39), b.minY, b.bottom - 26)) + "px");
        state.root.style.setProperty("--emote-x", Math.round(clamp(state.x - 7, b.minX + 4, b.right - 110)) + "px");
        state.root.style.setProperty("--emote-y", Math.round(clamp(state.y - 26, b.minY + 4, b.bottom - 30)) + "px");
        state.root.style.setProperty("--surface-x", Math.round(state.x + 8) + "px");
        state.root.style.setProperty("--surface-y", Math.round(state.y + 68) + "px");
        state.root.classList.toggle("is-entering", state.entering);
        state.root.classList.toggle("is-perched", !!state.perch && !state.moving && !state.dragging);
        layout(state);
    }

    function save(state) {
        try {
            sessionStorage.setItem("netwatch-nero-position", JSON.stringify({ x: state.x, y: state.y }));
            sessionStorage.setItem("netwatch-nero-behavior", JSON.stringify({ mode: state.mode, mood: state.selectedMood, animationChoice: state.animationChoice }));
        } catch { /* The companion also works with browser storage disabled. */ }
    }

    function stop(state) {
        state.targetX = state.x;
        state.targetY = state.y;
        state.speed = 0;
        state.moving = false;
    }

    function idle(state, now) {
        const profile = profiles[state.profile] || profiles.default;
        const mood = state.mode === "stay" ? state.selectedMood : profile.actions[state.actionIndex++ % profile.actions.length];
        setMood(state, mood);
        state.nextAction = now + ACTIVITY_MS;
    }

    function chooseWalk(state) {
        const b = bounds(state);
        state.perch = null;
        const candidates = [];
        const selectors = (profiles[state.profile] || profiles.default).selectors;
        for (const selector of selectors) {
            for (const element of document.querySelectorAll(selector)) {
                const r = element.getBoundingClientRect();
                if (r.width < 80 || r.height < 24 || r.right < b.minX || r.left > b.right) continue;
                for (const edge of ["top", "bottom"]) {
                    const y = r[edge] - 68;
                    if (y < b.minY || y > b.maxY) continue;
                    const x = clamp(state.x, Math.max(b.minX, r.left + 8), Math.min(b.maxX, r.right - b.width - 8));
                    candidates.push({ element, edge, x, y, offset:x - r.left,
                        distance:Math.hypot(x - state.x, y - state.y) });
                }
            }
        }
        candidates.sort((a, c) => a.distance - c.distance);
        const surface = candidates.find(c => c.distance > 35) || candidates[0];
        let x = surface?.x ?? state.x + (Math.random() - .5) * 320;
        let y = surface?.y ?? state.y + (Math.random() - .5) * 200;
        const distance = Math.hypot(x - state.x, y - state.y);
        const factor = Math.min(1, 320 / Math.max(1, distance));
        state.pendingPerch = factor === 1 ? surface || null : null;
        state.targetX = clamp(state.x + (x - state.x) * factor, b.minX, b.maxX);
        state.targetY = clamp(state.y + (y - state.y) * factor, b.minY, b.maxY);
        state.moving = Math.hypot(state.targetX - state.x, state.targetY - state.y) > 3;
        if (!state.moving && state.pendingPerch) landOnSurface(state, performance.now());
    }

    function landOnSurface(state, now) {
        state.perch = state.pendingPerch;
        state.pendingPerch = null;
        setMood(state, state.profile === "reports" ? "play" : state.profile === "login" ? "groom" : "scratch");
        state.nextAction = now + ACTIVITY_MS;
    }

    function followSurface(state) {
        if (!state.perch || state.moving || state.dragging) return;
        const { element, edge, offset } = state.perch;
        if (!element.isConnected) { state.perch = null; return; }
        const r = element.getBoundingClientRect(), b = bounds(state);
        const y = r[edge] - 68;
        if (y < b.minY || y > b.maxY || r.width < 80) { state.perch = null; return; }
        state.x = clamp(r.left + offset, b.minX, b.maxX);
        state.y = y;
        stop(state);
    }

    function releaseCursor(state) {
        if (state.cursorTimer) clearTimeout(state.cursorTimer);
        state.cursorTimer = null;
        state.carrying = false;
        state.batting = false;
        document.documentElement.classList.remove("nero-cursor-caught");
        state.root.classList.remove("is-carrying");
        state.root.classList.remove("is-batting");
        state.character.querySelector(".pixel-cat")?.classList.remove("is-batting");
    }

    function catchCursor(state, now) {
        if (state.reducedMotion || state.carrying) return;
        state.chasing = false;
        state.carrying = true;
        state.batting = true;
        state.perch = null;
        state.pendingPerch = null;
        state.catchX = state.pointerX; state.catchY = state.pointerY;
        state.swatUntil = now + SWAT_MS;
        state.carryUntil = now + SWAT_MS + CARRY_MS;
        document.documentElement.classList.add("nero-cursor-caught");
        state.root.classList.add("is-carrying");
        state.root.classList.add("is-batting");
        stop(state);
        const b = bounds(state);
        const direction = state.x < (b.minX + b.maxX) / 2 ? 1 : -1;
        state.targetX = clamp(state.x + direction * 100, b.minX, b.maxX);
        state.targetY = clamp(state.y + 24, b.minY, b.maxY);
        setMood(state, "play");
        syncControls(state);
        // A wall-clock cleanup also restores the pointer if animation frames are suspended.
        state.cursorTimer = setTimeout(() => {
            if (!state.destroyed) finishChase(state, performance.now());
            else releaseCursor(state);
        }, SWAT_MS + CARRY_MS);
    }

    function finishChase(state, now) {
        releaseCursor(state);
        state.chasing = false;
        state.entering = false;
        clampPosition(state);
        state.clicks = [];
        state.chaseCooldown = now + 5000;
        stop(state);
        setMood(state, state.mode === "stay" ? state.selectedMood : "sit");
        state.nextAction = now + ACTIVITY_MS;
        syncControls(state);
    }

    function tick(now) {
        const state = active;
        if (!state || state.destroyed) return;
        const delta = clamp((now - state.lastFrame) / 1000, 0, .05);
        state.lastFrame = now;
        if (now > state.overlayCheckAt) {
            state.obscured = !!document.querySelector(".guide-dialog, dialog[open], .modal.show");
            state.root.classList.toggle("is-obscured", state.obscured);
            followSurface(state);
            state.overlayCheckAt = now + 300;
        }
        const menuOpen = state.root.dataset.menuOpen === "true";
        if ((state.carrying && (now >= state.carryUntil || menuOpen || state.obscured || document.hidden)) ||
            (state.chasing && (now >= state.chaseUntil || document.hidden || state.obscured))) finishChase(state, now);
        const paused = document.hidden || state.obscured || state.dragging || menuOpen ||
            (!state.entering && !state.chasing && !state.carrying && (state.hovering || state.keyboardFocus));
        if (!paused) {
            if (state.batting && now >= state.swatUntil) {
                state.batting = false;
                state.root.classList.remove("is-batting");
                state.moving = true;
                setMood(state, "walk");
            }
            if (state.chasing && !state.entering) {
                const b = bounds(state);
                const dx = state.pointerX - (state.x + b.width / 2);
                const dy = state.pointerY - (state.y + b.height / 2);
                const distance = Math.hypot(dx, dy);
                if (distance <= 18 && now >= state.chaseWarmup && !state.reducedMotion) catchCursor(state, now);
                else {
                    state.targetX = state.x + dx;
                    state.targetY = state.y + dy;
                    state.moving = distance > 5 && !state.reducedMotion && now >= state.chaseWarmup;
                    if (!state.moving) setMood(state, "alert");
                }
            } else if (!state.entering && !state.carrying && !state.moving && now >= state.nextAction) {
                if (state.mode === "explore" && !state.reducedMotion && state.takeWalk) {
                    chooseWalk(state);
                    state.takeWalk = false;
                    if (!state.moving && !state.perch) idle(state, now);
                } else {
                    idle(state, now);
                    state.takeWalk = true;
                }
            }
            if (state.moving) {
                clampPosition(state);
                const dx = state.targetX - state.x, dy = state.targetY - state.y;
                const distance = Math.hypot(dx, dy);
                if (distance > 2) {
                    const maxSpeed = state.chasing ? 108 : state.entering ? 58 : state.carrying ? 55 : 42;
                    const desiredSpeed = Math.min(maxSpeed, Math.max(12, distance * 2));
                    state.speed += clamp(desiredSpeed - state.speed, -160 * delta, 100 * delta);
                    const step = Math.min(distance, state.speed * delta);
                    state.x += dx / distance * step;
                    state.y += dy / distance * step;
                    if (Math.abs(dx) > 3) state.facing = dx < 0 ? -1 : 1;
                    setMood(state, state.entering && state.root.dataset.mascotEntry === "top" ? "drag" : state.chasing ? "run" : "walk");
                } else {
                    stop(state);
                    if (state.entering) {
                        state.entering = false;
                        setMood(state, state.selectedMood);
                        state.nextAction = now + ACTIVITY_MS;
                    } else if (state.carrying) setMood(state, "play");
                    else if (!state.chasing) {
                        if (state.pendingPerch) landOnSurface(state, now);
                        else idle(state, now);
                    }
                }
            }
        }
        clampPosition(state);
        applyPosition(state);
        if (now - state.lastSaved > 2000) { save(state); state.lastSaved = now; }
        state.animationFrame = requestAnimationFrame(tick);
    }

    function pet(state, event) {
        const now = performance.now();
        if (now < state.suppressClickUntil) {
            event.preventDefault();
            event.stopImmediatePropagation();
            return;
        }
        state.clicks = state.clicks.filter(time => now - time < 3500);
        state.clicks.push(now);
        if (state.dockCorner === "free" && state.clicks.length >= 4 && now >= state.chaseCooldown && !state.chasing && !state.carrying) {
            state.entering = false;
            state.perch = null;
            state.pendingPerch = null;
            state.chasing = true;
            state.chaseWarmup = now + 600;
            state.chaseUntil = now + CHASE_MS;
            state.hovering = false;
            state.keyboardFocus = false;
            stop(state);
            setMood(state, "alert");
        } else if (!state.chasing) {
            stop(state);
            setMood(state, "celebrate");
            state.nextAction = now + 2000;
        }
        syncControls(state);
    }

    function beginDrag(state, event) {
        if (state.dockCorner !== "free" || event.button !== 0 || state.dragging) return;
        if (state.carrying) finishChase(state, performance.now());
        state.entering = false;
        state.perch = null;
        state.pendingPerch = null;
        state.dragging = true;
        state.dragMoved = false;
        state.dragPointerId = event.pointerId;
        state.pressX = event.clientX; state.pressY = event.clientY;
        state.offsetX = event.clientX - state.x; state.offsetY = event.clientY - state.y;
        state.beforeDragMood = state.mood;
        state.character.setPointerCapture?.(event.pointerId);
        state.character.classList.add("is-dragging");
        state.root.classList.add("is-dragging");
        stop(state);
        setMood(state, "drag");
        event.preventDefault();
    }

    function movePointer(state, event) {
        state.pointerX = event.clientX; state.pointerY = event.clientY;
        if (state.carrying && Math.hypot(event.clientX - state.catchX, event.clientY - state.catchY) > 8) finishChase(state, performance.now());
        if (!state.dragging || event.pointerId !== state.dragPointerId) return;
        if (Math.hypot(event.clientX - state.pressX, event.clientY - state.pressY) > 4) state.dragMoved = true;
        if (state.dragMoved) {
            state.x = event.clientX - state.offsetX; state.y = event.clientY - state.offsetY;
            stop(state);
            clampPosition(state);
            applyPosition(state);
        }
        event.preventDefault();
    }

    function endDrag(state, event) {
        if (!state.dragging || event.pointerId !== state.dragPointerId) return;
        state.dragging = false;
        if (state.character.hasPointerCapture?.(state.dragPointerId)) state.character.releasePointerCapture(state.dragPointerId);
        state.dragPointerId = null;
        state.character.classList.remove("is-dragging");
        state.root.classList.remove("is-dragging");
        const cancelled = event.type !== "pointerup";
        if (state.dragMoved || cancelled) {
            state.suppressClickUntil = performance.now() + 500;
            state.clicks = [];
            state.chasing = false;
            state.mode = "stay";
            state.selectedMood = "sit";
            setMood(state, "sit");
        } else setMood(state, state.beforeDragMood);
        stop(state);
        state.nextAction = performance.now() + ACTIVITY_MS;
        syncControls(state);
        save(state);
    }

    function command(root, action) {
        const state = active;
        if (!state || state.root !== root || !Object.hasOwn(labels, action)) return;
        if (state.dockCorner !== "free" && action === "explore") return;
        releaseCursor(state);
        state.chasing = false;
        state.entering = false;
        state.pendingPerch = null;
        state.clicks = [];
        stop(state);
        if (action === "calm" || action === "explore") {
            state.mode = action;
            state.selectedMood = "sit";
            state.takeWalk = true;
        } else {
            state.mode = "stay";
            state.selectedMood = action;
        }
        setMood(state, state.selectedMood);
        state.nextAction = performance.now() + (action === "explore" ? 1500 : ACTIVITY_MS);
        syncControls(state);
        save(state);
    }

    function reactToSystem(state, event) {
        if (state.carrying) finishChase(state, performance.now());
        if (state.dockCorner !== "free" || state.root.contains(event.target) || state.mode === "stay" || state.dragging || state.chasing) return;
        const element = event.target.closest?.("button, a");
        if (!element || performance.now() < state.reactionUntil) return;
        const text = (element.textContent || element.getAttribute("aria-label") || "").toLowerCase();
        if (!/guardar|export|pdf|excel|generar|comprobar/.test(text)) return;
        stop(state);
        setMood(state, "curious");
        state.nextAction = performance.now() + ACTIVITY_MS;
        state.reactionUntil = state.nextAction;
    }

    function restore(state) {
        try {
            const behavior = JSON.parse(sessionStorage.getItem("netwatch-nero-behavior") || "null");
            if (["calm", "explore", "stay"].includes(behavior?.mode)) state.mode = behavior.mode;
            if (["sit", "nap", "groom", "play", "stretch"].includes(behavior?.mood)) state.selectedMood = behavior.mood;
            if (typeof behavior?.animationChoice === "boolean") { state.animationChoice = behavior.animationChoice; state.reducedMotion = !behavior.animationChoice; }
        } catch { }
        try {
            const preference = localStorage.getItem("netwatch-nero-animated");
            if (preference === "true" || preference === "false") {
                state.animationChoice = preference === "true";
                state.reducedMotion = !state.animationChoice;
            }
        } catch { /* Session settings still work if local storage is unavailable. */ }
        state.dockCorner = dockPreference();
        state.root.classList.toggle("is-docked", state.dockCorner !== "free");
        if (state.dockCorner === "free") startEntrance(state);
        else { state.mode = "calm"; positionDock(state); }
    }

    function startEntrance(state) {
        const b = bounds(state);
        let previous = "";
        try { previous = sessionStorage.getItem("netwatch-nero-entry") || ""; } catch { }
        const sides = ["left", "right", "top", "bottom"].filter(side => side !== previous);
        const side = sides[Math.floor(Math.random() * sides.length)];
        try { sessionStorage.setItem("netwatch-nero-entry", side); } catch { }
        state.root.dataset.mascotEntry = side;
        const x = b.minX + (b.maxX - b.minX) * (.15 + Math.random() * .7);
        const y = b.minY + (b.maxY - b.minY) * (.15 + Math.random() * .7);
        state.x = side === "left" ? b.minX - b.width : side === "right" ? b.right : x;
        state.y = side === "top" ? b.minY - b.height : side === "bottom" ? b.bottom : y;
        state.targetX = clamp(side === "left" ? b.minX + 32 : side === "right" ? b.maxX - 32 : x, b.minX, b.maxX);
        state.targetY = clamp(side === "top" ? b.minY + 20 : side === "bottom" ? b.maxY - 20 : y, b.minY, b.maxY);
        state.entering = !state.reducedMotion;
        state.moving = state.entering;
        if (!state.entering) { state.x = state.targetX; state.y = state.targetY; }
        state.nextAction = performance.now() + ACTIVITY_MS;
    }

    function attach(root, profile) {
        if (!root) return;
        if (active?.root === root) {
            if (active.profile !== profile) {
                finishChase(active, performance.now());
                active.perch = null; active.pendingPerch = null;
            }
            active.profile = profile || "default";
            setMood(active, active.mood);
            layout(active);
            return;
        }
        if (active) detach(active.root);
        const character = root.querySelector(".mascot-character");
        if (!character) return;
        const state = {
            root, character, profile, mode:"calm", dockCorner:"bottom-right", selectedMood:"sit", x:0, y:0, targetX:0, targetY:0,
            speed:0, facing:1, mood:"", moving:false, dragging:false, hovering:false, keyboardFocus:false,
            pointerX:innerWidth / 2, pointerY:innerHeight / 2, clicks:[], chasing:false, chaseUntil:0, chaseCooldown:0,
            nextAction:performance.now() + ACTIVITY_MS, lastFrame:performance.now(), lastSaved:0,
            entering:false, perch:null, pendingPerch:null, carrying:false, batting:false, swatUntil:0, carryUntil:0, cursorTimer:null,
            actionIndex:1, walkIndex:0, takeWalk:true, reactionUntil:0, suppressClickUntil:0, overlayCheckAt:0,
            listeners:[], animationChoice:true
        };
        // Nero is explicitly requested as an animated companion. Keep its pause
        // control independent of OS motion settings; never change those settings.
        state.reducedMotion = false;
        const listen = (target, type, handler, options) => {
            if (!target) return;
            target.addEventListener(type, handler, options);
            state.listeners.push(() => target.removeEventListener(type, handler, options));
        };
        listen(character, "pointerdown", event => beginDrag(state, event));
        listen(character, "click", event => pet(state, event), true);
        listen(character, "lostpointercapture", event => endDrag(state, event));
        listen(character, "pointerenter", () => { if (!state.chasing) state.hovering = true; });
        listen(character, "pointerleave", () => { state.hovering = false; });
        listen(character, "focus", () => { state.keyboardFocus = character.matches(":focus-visible"); });
        listen(character, "blur", () => { state.keyboardFocus = false; });
        listen(root.querySelector(".mascot-menu-toggle"), "pointerenter", () => { state.hovering = true; });
        listen(root.querySelector(".mascot-menu-toggle"), "pointerleave", () => { state.hovering = false; });
        listen(window, "pointermove", event => movePointer(state, event), { passive:false });
        listen(window, "pointerup", event => endDrag(state, event));
        listen(window, "pointercancel", event => endDrag(state, event));
        listen(window, "blur", () => {
            if (state.chasing || state.carrying) finishChase(state, performance.now());
            if (state.dragging) endDrag(state, { pointerId:state.dragPointerId, type:"pointercancel" });
            state.hovering = false; state.keyboardFocus = false;
        });
        listen(document, "pointerdown", event => reactToSystem(state, event), true);
        listen(document, "visibilitychange", () => { if (document.hidden && (state.chasing || state.carrying)) finishChase(state, performance.now()); });
        listen(window, "keydown", event => {
            if (event.key === "Escape") finishChase(state, performance.now());
        });
        listen(window, "pagehide", () => releaseCursor(state));
        listen(window, "resize", () => { clampPosition(state); applyPosition(state); });
        if (window.visualViewport) {
            listen(window.visualViewport, "resize", () => { clampPosition(state); applyPosition(state); });
            listen(window.visualViewport, "scroll", () => { clampPosition(state); applyPosition(state); });
        }
        active = state;
        restore(state);
        setMood(state, state.selectedMood);
        applyPosition(state);
        state.animationFrame = requestAnimationFrame(tick);
    }

    function detach(root) {
        const state = active;
        if (!state || state.root !== root) return;
        releaseCursor(state);
        save(state);
        state.destroyed = true;
        cancelAnimationFrame(state.animationFrame);
        state.listeners.forEach(remove => remove());
        active = null;
    }

    window.netwatchMascot = {
        attach, detach, command,
        getDockPreference: dockPreference,
        setDock(root, corner) {
            if (active?.root !== root || !corners.includes(corner)) return;
            const state = active;
            finishChase(state, performance.now());
            state.perch = null; state.pendingPerch = null;
            state.dockCorner = corner;
            state.mode = "calm"; state.selectedMood = "sit";
            root.classList.toggle("is-docked", corner !== "free");
            try { localStorage.setItem("netwatch-nero-dock", corner); } catch { }
            applyPosition(state); save(state);
        },
        getTipsEnabled() { try { return localStorage.getItem("netwatch-nero-tips") === "true"; } catch { return false; } },
        setTipsEnabled(enabled) { try { localStorage.setItem("netwatch-nero-tips", String(!!enabled)); } catch { } },
        motionEnabled() { return active ? !active.reducedMotion : true; },
        setAnimation(root, enabled) {
            if (active?.root !== root) return;
            active.animationChoice = !!enabled;
            active.reducedMotion = !enabled;
            try { localStorage.setItem("netwatch-nero-animated", String(!!enabled)); } catch { }
            if (!enabled) { active.entering = false; finishChase(active, performance.now()); }
            setMood(active, active.mode === "stay" ? active.selectedMood : "sit");
            syncControls(active);
            save(active);
        },
        layout(root) { if (active?.root === root) { setMood(active, active.mood); layout(active); } }
    };
})();
