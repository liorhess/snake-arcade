(function () {
    "use strict";

    var canvas = document.getElementById("snakeCanvas");
    var ctx = canvas.getContext("2d");
    var overlay = document.getElementById("boardOverlay");
    var overlayTitle = document.getElementById("overlayTitle");
    var overlayMessage = document.getElementById("overlayMessage");
    var overlayActionBtn = document.getElementById("overlayActionBtn");
    var statusDot = document.getElementById("statusDot");
    var statusText = document.getElementById("statusText");
    var btnMute = document.getElementById("btnMute");
    var btnPause = document.getElementById("btnPause");
    var lblScore = document.getElementById("lblScore");
    var lblHighScore = document.getElementById("lblHighScore");
    var lblApples = document.getElementById("lblApples");
    var lblSpeed = document.getElementById("lblSpeed");
    var txtPlayerName = document.getElementById("txtPlayerName");
    var ddlDifficulty = document.getElementById("ddlDifficulty");
    var chkWrapWalls = document.getElementById("chkWrapWalls");
    var leaderboardBody = document.getElementById("leaderboardBody");

    var state = null;
    var pendingDirections = [];
    var tickTimer = null;
    var isTicking = false;
    var muted = false;
    var audioCtx = null;
    var prevScore = 0;

    function apiRequest(url, method, payload) {
        var options = {
            method: method || "GET",
            credentials: "same-origin",
            headers: {}
        };

        if (payload !== undefined) {
            options.headers["Content-Type"] = "application/json; charset=utf-8";
            options.body = JSON.stringify(payload);
        }

        return fetch(url, options).then(function (response) {
            if (!response.ok) {
                throw new Error("Server returned HTTP " + response.status);
            }
            return response.json();
        });
    }

    function playTone(freq, duration, type, gainValue) {
        if (muted) {
            return;
        }
        try {
            var AudioContextCtor = window.AudioContext || window.webkitAudioContext;
            if (!AudioContextCtor) {
                return;
            }
            if (!audioCtx) {
                audioCtx = new AudioContextCtor();
            }
            if (audioCtx.state === "suspended") {
                audioCtx.resume();
            }

            var osc = audioCtx.createOscillator();
            var gain = audioCtx.createGain();
            osc.type = type || "sine";
            osc.frequency.setValueAtTime(freq, audioCtx.currentTime);
            gain.gain.setValueAtTime(gainValue || 0.08, audioCtx.currentTime);
            gain.gain.exponentialRampToValueAtTime(0.001, audioCtx.currentTime + duration);

            osc.connect(gain);
            gain.connect(audioCtx.destination);
            osc.start();
            osc.stop(audioCtx.currentTime + duration);
        } catch (e) {
            // Ignore audio errors on restricted devices
        }
    }

    function clearLoop() {
        if (tickTimer !== null) {
            clearTimeout(tickTimer);
            tickTimer = null;
        }
    }

    function scheduleNextTick(delayMs) {
        clearLoop();
        if (!state || state.status !== "Running") {
            return;
        }
        tickTimer = setTimeout(runTick, Math.max(16, delayMs));
    }

    function runTick() {
        if (isTicking || !state || state.status !== "Running") {
            return;
        }

        isTicking = true;
        var startedAt = performance.now();
        var dirsToSend = pendingDirections.splice(0, 3);

        apiRequest("/api/game/tick", "POST", { directions: dirsToSend })
            .then(function (newState) {
                isTicking = false;
                handleStateSounds(newState);
                applyState(newState);

                if (newState.status === "Running") {
                    var elapsed = performance.now() - startedAt;
                    scheduleNextTick(newState.tickMs - elapsed);
                }
            })
            .catch(function (err) {
                isTicking = false;
                statusText.textContent = "Connection hiccup: " + err.message;
                if (state && state.status === "Running") {
                    scheduleNextTick(300);
                }
            });
    }

    function handleStateSounds(newState) {
        if (!newState) {
            return;
        }

        if (newState.score > prevScore) {
            var delta = newState.score - prevScore;
            if (delta >= 30) {
                playTone(660, 0.09, "triangle", 0.1);
                setTimeout(function () { playTone(880, 0.14, "triangle", 0.1); }, 85);
            } else {
                playTone(540, 0.08, "sine", 0.08);
            }
        } else if (state && state.status === "Running" && newState.status === "GameOver") {
            playTone(220, 0.16, "sawtooth", 0.08);
            setTimeout(function () { playTone(145, 0.25, "sawtooth", 0.08); }, 130);
        } else if (state && state.status === "Running" && newState.status === "Won") {
            playTone(523, 0.1, "triangle", 0.09);
            setTimeout(function () { playTone(659, 0.1, "triangle", 0.09); }, 100);
            setTimeout(function () { playTone(784, 0.22, "triangle", 0.1); }, 200);
        }

        prevScore = newState.score;
    }

    function applyState(newState) {
        state = newState;

        lblScore.textContent = newState.score;
        lblHighScore.textContent = newState.highScore;
        lblApples.textContent = newState.applesEaten;
        lblSpeed.textContent = newState.difficulty;
        btnPause.textContent = newState.status === "Paused" ? "Resume" : "Pause";

        statusText.textContent = newState.message || "";
        statusDot.className = "status-dot " + newState.status.toLowerCase();

        updateOverlay(newState);
        renderBoard();

        if (newState.leaderboardUpdated) {
            loadLeaderboard();
        }
    }

    function updateOverlay(s) {
        if (s.status === "Running") {
            overlay.classList.add("hidden");
            return;
        }

        overlay.classList.remove("hidden");

        if (s.status === "Ready") {
            overlayTitle.textContent = "Ready to Play";
            overlayMessage.textContent = "Press Start Game, use Arrow / WASD keys, or swipe on the board to begin.";
            overlayActionBtn.textContent = "Start Game";
        } else if (s.status === "Paused") {
            overlayTitle.textContent = "Game Paused";
            overlayMessage.textContent = "Score: " + s.score + " · Take a breather and jump back in!";
            overlayActionBtn.textContent = "Resume";
        } else if (s.status === "GameOver") {
            overlayTitle.textContent = "Game Over";
            overlayMessage.textContent = (s.message || "") + " Final score: " + s.score + " (" + s.applesEaten + " apples).";
            overlayActionBtn.textContent = "Play Again";
        } else if (s.status === "Won") {
            overlayTitle.textContent = "You Won!";
            overlayMessage.textContent = "Perfect board clear! Final score: " + s.score + ".";
            overlayActionBtn.textContent = "Play Again";
        }
    }

    function renderLeaderboard(entries) {
        leaderboardBody.innerHTML = "";

        if (!entries || entries.length === 0) {
            var emptyTr = document.createElement("tr");
            var emptyTd = document.createElement("td");
            emptyTd.colSpan = 5;
            emptyTd.className = "empty-row";
            emptyTd.textContent = "No scores recorded yet. Play a game to claim #1!";
            emptyTr.appendChild(emptyTd);
            leaderboardBody.appendChild(emptyTr);
            return;
        }

        entries.forEach(function (entry, idx) {
            var tr = document.createElement("tr");

            var tdRank = document.createElement("td");
            tdRank.textContent = String(idx + 1);

            var tdPlayer = document.createElement("td");
            tdPlayer.textContent = entry.playerName;

            var tdScore = document.createElement("td");
            tdScore.className = "col-score";
            tdScore.textContent = String(entry.score);

            var tdDiff = document.createElement("td");
            tdDiff.textContent = entry.difficulty;

            var tdMode = document.createElement("td");
            tdMode.textContent = entry.mode;

            tr.appendChild(tdRank);
            tr.appendChild(tdPlayer);
            tr.appendChild(tdScore);
            tr.appendChild(tdDiff);
            tr.appendChild(tdMode);
            leaderboardBody.appendChild(tr);
        });
    }

    function loadLeaderboard() {
        return apiRequest("/api/scores?limit=10", "GET")
            .then(function (data) {
                renderLeaderboard(data.scores);
                if (!state || state.score < data.highScore) {
                    lblHighScore.textContent = data.highScore;
                }
            })
            .catch(function () {
                // Keep existing table if fetch briefly fails
            });
    }

    function clearScores() {
        apiRequest("/api/scores", "DELETE")
            .then(function () {
                renderLeaderboard([]);
                lblHighScore.textContent = state ? Math.max(0, state.score) : 0;
            })
            .catch(function (err) {
                statusText.textContent = "Could not clear scores: " + err.message;
            });
    }

    function drawRoundedRect(x, y, w, h, r) {
        ctx.beginPath();
        ctx.moveTo(x + r, y);
        ctx.arcTo(x + w, y, x + w, y + h, r);
        ctx.arcTo(x + w, y + h, x, y + h, r);
        ctx.arcTo(x, y + h, x, y, r);
        ctx.arcTo(x, y, x + w, y, r);
        ctx.closePath();
    }

    function renderBoard() {
        if (!state) {
            return;
        }

        var cols = state.gridWidth;
        var rows = state.gridHeight;
        var cellW = canvas.width / cols;
        var cellH = canvas.height / rows;

        for (var y = 0; y < rows; y++) {
            for (var x = 0; x < cols; x++) {
                ctx.fillStyle = (x + y) % 2 === 0 ? "#0c1324" : "#10192e";
                ctx.fillRect(x * cellW, y * cellH, cellW, cellH);
            }
        }

        if (state.food) {
            drawApple(state.food.x, state.food.y, cellW, cellH, "#f43f5e", "#fda4af", false, 1);
        }

        if (state.bonusFood) {
            var ratio = Math.max(0, Math.min(1, state.bonusTicksRemaining / 35));
            drawApple(state.bonusFood.x, state.bonusFood.y, cellW, cellH, "#fbbf24", "#fef08a", true, ratio);
        }

        var snake = state.snake || [];
        var len = snake.length;
        for (var i = len - 1; i >= 0; i--) {
            var seg = snake[i];
            var isHead = i === 0;
            var pad = isHead ? 1.5 : 2.5;
            var px = seg.x * cellW + pad;
            var py = seg.y * cellH + pad;
            var pw = cellW - pad * 2;
            var ph = cellH - pad * 2;

            var t = len > 1 ? i / (len - 1) : 0;
            var rCol = Math.round(34 + t * 8);
            var gCol = Math.round(211 - t * 65);
            var bCol = Math.round(115 + t * 50);

            ctx.fillStyle = isHead ? "#4ade80" : "rgb(" + rCol + "," + gCol + "," + bCol + ")";
            drawRoundedRect(px, py, pw, ph, isHead ? 7 : 5);
            ctx.fill();

            if (isHead) {
                drawEyes(seg.x * cellW, seg.y * cellH, cellW, cellH, state.direction);
            }
        }
    }

    function drawApple(gx, gy, cellW, cellH, fillColor, shineColor, isBonus, ratio) {
        var cx = gx * cellW + cellW / 2;
        var cy = gy * cellH + cellH / 2;
        var radius = Math.min(cellW, cellH) * (isBonus ? 0.4 : 0.36);

        ctx.save();
        ctx.shadowColor = fillColor;
        ctx.shadowBlur = isBonus ? 16 : 10;

        ctx.beginPath();
        ctx.arc(cx, cy, radius, 0, Math.PI * 2);
        ctx.fillStyle = fillColor;
        ctx.fill();
        ctx.restore();

        ctx.beginPath();
        ctx.arc(cx - radius * 0.3, cy - radius * 0.3, radius * 0.24, 0, Math.PI * 2);
        ctx.fillStyle = shineColor;
        ctx.fill();

        ctx.beginPath();
        ctx.ellipse(cx + radius * 0.22, cy - radius * 0.92, radius * 0.32, radius * 0.16, -0.5, 0, Math.PI * 2);
        ctx.fillStyle = "#4ade80";
        ctx.fill();

        if (isBonus) {
            ctx.beginPath();
            ctx.arc(cx, cy, radius + 3.5, -Math.PI / 2, -Math.PI / 2 + Math.PI * 2 * ratio);
            ctx.strokeStyle = "#fde047";
            ctx.lineWidth = 2;
            ctx.stroke();
        }
    }

    function drawEyes(x, y, w, h, dir) {
        var eye1, eye2;
        var offset = w * 0.24;
        var forward = w * 0.26;
        var cx = x + w / 2;
        var cy = y + h / 2;

        if (dir === "Up") {
            eye1 = { x: cx - offset, y: cy - forward };
            eye2 = { x: cx + offset, y: cy - forward };
        } else if (dir === "Down") {
            eye1 = { x: cx - offset, y: cy + forward };
            eye2 = { x: cx + offset, y: cy + forward };
        } else if (dir === "Left") {
            eye1 = { x: cx - forward, y: cy - offset };
            eye2 = { x: cx - forward, y: cy + offset };
        } else {
            eye1 = { x: cx + forward, y: cy - offset };
            eye2 = { x: cx + forward, y: cy + offset };
        }

        [eye1, eye2].forEach(function (eye) {
            ctx.beginPath();
            ctx.arc(eye.x, eye.y, 2.8, 0, Math.PI * 2);
            ctx.fillStyle = "#052e16";
            ctx.fill();
        });
    }

    function startGame(initialDir) {
        clearLoop();
        pendingDirections = initialDir ? [initialDir] : [];
        prevScore = 0;

        var playerName = txtPlayerName.value || "Player";
        var difficulty = ddlDifficulty.value || "Normal";
        var wrapWalls = chkWrapWalls.checked;

        if (document.activeElement && typeof document.activeElement.blur === "function") {
            document.activeElement.blur();
        }

        apiRequest("/api/game/start", "POST", {
            playerName: playerName,
            difficulty: difficulty,
            wrapWalls: wrapWalls
        }).then(function (newState) {
            applyState(newState);
            scheduleNextTick(newState.tickMs);
        }).catch(function (err) {
            statusText.textContent = "Could not start game: " + err.message;
        });
    }

    function togglePause() {
        if (!state || (state.status !== "Running" && state.status !== "Paused")) {
            return;
        }

        clearLoop();
        apiRequest("/api/game/pause", "POST", {}).then(function (newState) {
            applyState(newState);
            if (newState.status === "Running") {
                scheduleNextTick(newState.tickMs);
            }
        });
    }

    function isOppositeDirection(a, b) {
        return (a === "Up" && b === "Down")
            || (a === "Down" && b === "Up")
            || (a === "Left" && b === "Right")
            || (a === "Right" && b === "Left");
    }

    function queueDirection(dir) {
        if (!state) {
            return;
        }

        if (state.status === "Ready") {
            if (isOppositeDirection("Right", dir)) {
                startGame();
            } else {
                startGame(dir);
            }
            return;
        }

        if (state.status !== "Running") {
            return;
        }

        if (pendingDirections.length < 3) {
            var referenceDir = pendingDirections.length > 0
                ? pendingDirections[pendingDirections.length - 1]
                : state.direction;

            if (dir !== referenceDir && !isOppositeDirection(referenceDir, dir)) {
                pendingDirections.push(dir);
            }
        }
    }

    function handleOverlayAction() {
        if (state && state.status === "Paused") {
            togglePause();
        } else {
            startGame();
        }
    }

    function toggleMute() {
        muted = !muted;
        btnMute.textContent = muted ? "🔇" : "🔊";
    }

    function isTypingInField(target) {
        if (!target) {
            return false;
        }
        var tag = target.tagName;
        return (tag === "INPUT" && target.type === "text") || tag === "TEXTAREA" || tag === "SELECT";
    }

    document.addEventListener("keydown", function (e) {
        if (isTypingInField(e.target)) {
            if (e.key === "Enter") {
                e.preventDefault();
                startGame();
            }
            return;
        }

        var keyMap = {
            ArrowUp: "Up",
            w: "Up",
            W: "Up",
            ArrowDown: "Down",
            s: "Down",
            S: "Down",
            ArrowLeft: "Left",
            a: "Left",
            A: "Left",
            ArrowRight: "Right",
            d: "Right",
            D: "Right"
        };

        if (keyMap[e.key]) {
            e.preventDefault();
            queueDirection(keyMap[e.key]);
        } else if (e.key === " " || e.code === "Space") {
            e.preventDefault();
            togglePause();
        } else if (e.key === "Enter") {
            e.preventDefault();
            handleOverlayAction();
        }
    });

    // Touch swipe gesture support on the game canvas
    var touchStartX = null;
    var touchStartY = null;
    var activeTouchId = null;
    var MIN_SWIPE_DISTANCE = 24;

    function resetTouchTracking() {
        touchStartX = null;
        touchStartY = null;
        activeTouchId = null;
    }

    canvas.addEventListener("touchstart", function (e) {
        if (!e.touches || e.touches.length !== 1) {
            resetTouchTracking();
            return;
        }

        e.preventDefault();
        var touch = e.touches[0];
        activeTouchId = touch.identifier;
        touchStartX = touch.clientX;
        touchStartY = touch.clientY;
    }, { passive: false });

    canvas.addEventListener("touchmove", function (e) {
        if (activeTouchId !== null) {
            e.preventDefault();
        }
    }, { passive: false });

    canvas.addEventListener("touchend", function (e) {
        if (activeTouchId === null || touchStartX === null || touchStartY === null) {
            return;
        }

        var endedTouch = null;
        for (var i = 0; i < e.changedTouches.length; i++) {
            if (e.changedTouches[i].identifier === activeTouchId) {
                endedTouch = e.changedTouches[i];
                break;
            }
        }

        if (!endedTouch) {
            return;
        }

        e.preventDefault();
        var dx = endedTouch.clientX - touchStartX;
        var dy = endedTouch.clientY - touchStartY;
        var absX = Math.abs(dx);
        var absY = Math.abs(dy);

        resetTouchTracking();

        if (Math.max(absX, absY) < MIN_SWIPE_DISTANCE) {
            return;
        }

        var swipeDir = absX >= absY
            ? (dx > 0 ? "Right" : "Left")
            : (dy > 0 ? "Down" : "Up");

        queueDirection(swipeDir);
    }, { passive: false });

    canvas.addEventListener("touchcancel", resetTouchTracking, { passive: true });

    window.SnakeApp = {
        startGame: startGame,
        togglePause: togglePause,
        queueDirection: queueDirection,
        handleOverlayAction: handleOverlayAction,
        toggleMute: toggleMute,
        clearScores: clearScores
    };

    Promise.all([
        apiRequest("/api/game/state", "GET"),
        loadLeaderboard()
    ]).then(function (results) {
        var initialState = results[0];
        prevScore = initialState.score || 0;
        applyState(initialState);
    }).catch(function (err) {
        statusText.textContent = "Failed to initialize game state: " + err.message;
    });
})();
