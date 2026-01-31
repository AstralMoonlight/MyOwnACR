// js/app.js

const HOST = "127.0.0.1:5055";
let ws;
let currentJobId = -1;
let currentModule = null;
let isBotRunning = false; // Estado local del bot

// Mapa de IDs de Job a nombres de archivo
const JOB_MAP = {
    20: "MNK",
    23: "BRD",
    34: "SAM",
};

const ACTIONS_NAME = {
    53: "Bootshine",
    54: "True Strike",
    56: "Snap Punch",
    74: "Dragon Kick",
    61: "Twin Snakes",
    66: "Demolish",
    36945: "Leaping Opo",
    36946: "Rising Raptor",
    36947: "Pouncing Coeurl",
    25764: "Blitz",
    110: "Perfect Bal.",
    7396: "Riddle Fire",
    97: "Rain of Death",
    98: "Burst Shot",
    100: "Venomous Bite",
    114: "Mage's Ballad",
};

const JOB_NAMES = {
    19: "PLD",
    20: "MNK",
    21: "WAR",
    22: "DRG",
    23: "BRD",
    24: "WHM",
    25: "BLM",
    26: "ACN",
    27: "SMN",
    28: "SCH",
    29: "ROG",
    30: "NIN",
    31: "MCH",
    32: "DRK",
    33: "AST",
    34: "SAM",
    35: "RDM",
    36: "BLU",
    37: "GNB",
    38: "DNC",
    39: "RPR",
    40: "SGE",
    41: "VPR",
    42: "PCT",
};

// --- EXPORTAR FUNCIONES AL HTML (WINDOW) ---
window.app = {
    toggleBot: () => {
        const cmd = isBotRunning ? "STOP" : "START";
        sendJson(cmd);
        // Feedback visual inmediato
        const btn = document.querySelector("#btn-toggle-bot");
        if (btn) {
            btn.classList.add("scale-95");
            setTimeout(() => btn.classList.remove("scale-95"), 100);
        }
    },

    forceAction: (btn) => {
        const action = btn.getAttribute("data-action");
        if (!action) return;
        sendJson("force_action", action);

        btn.classList.add("border-accent");
        setTimeout(() => btn.classList.remove("border-accent"), 200);
    },

    updateOps: (el) => collectAndSendOps(el),

    clearConsole: () => {
        const c = document.getElementById("debug-console");
        c.innerHTML =
            '<div class="opacity-30 italic text-center mt-10">Consola limpia</div>';
    },

    copyConsole: () => {
        const c = document.getElementById("debug-console");
        const text = c.innerText;
        navigator.clipboard.writeText(text).then(() => {
            const btn = document.querySelector(
                'button[onclick="app.copyConsole()"]'
            );
            if (btn) {
                const original = btn.innerHTML;
                btn.innerHTML = '<i class="fa-solid fa-check"></i> Copiado';
                setTimeout(() => (btn.innerHTML = original), 1500);
            }
        });
    },

    toggleTheme: () => {
        const html = document.querySelector("html");
        const current = html.getAttribute("data-theme");
        html.setAttribute(
            "data-theme",
            current === "night" ? "winter" : "night"
        );
    },

    openConfig: () => {
        sendJson("get_config");
        document.getElementById("config_modal").showModal();
    },
};

// --- WEBSOCKET LOGIC ---

function connect() {
    ws = new WebSocket(`ws://${HOST}`);

    ws.onopen = () => {
        updateStatus("CONECTADO", "text-success");
        sendJson("get_config");
        sendJson("get_openers");
    };

    ws.onclose = () => {
        updateStatus("RECONECTANDO...", "text-warning");
        setTimeout(connect, 2000);
    };

    ws.onerror = (err) => {
        console.error("WS Error", err);
        ws.close();
    };

    ws.onmessage = (e) => {
        try {
            const msg = JSON.parse(e.data);
            handleMessage(msg);
        } catch (err) {
            console.error("Error parsing MSG:", err);
        }
    };
}

async function handleMessage(msg) {
    if (msg.type === "status") {
        updateDashboard(msg.data);
        if (msg.data.job && msg.data.job !== currentJobId) {
            await loadJobModule(msg.data.job);
        }
    } else if (msg.type === "log") logConsole(msg.data);
    else if (msg.type === "opener_list") loadOpenerList(msg.data);
    else if (msg.type === "potion_list") loadPotionList(msg.data);
    else if (msg.type === "config_data") loadConfigToUI(msg.data);
}

// --- LOGICA MODULAR ---

async function loadJobModule(jobId) {
    currentJobId = jobId;
    const jobName = JOB_NAMES[jobId] || "UNK";
    const mappedFile = JOB_MAP[jobId];
    const container = document.getElementById("dynamic-job-area");

    const badge = document.getElementById("header-job");
    badge.innerText = jobName;
    badge.classList.remove("opacity-0");

    if (!mappedFile) {
        container.innerHTML = `<div class="alert alert-warning shadow-sm"><i class="fa-solid fa-triangle-exclamation"></i><span>Clase ${jobName} no tiene módulo UI definido.</span></div>`;
        return;
    }

    container.innerHTML =
        '<div class="flex justify-center p-10"><span class="loading loading-spinner loading-lg text-primary"></span></div>';

    try {
        const module = await import(`../classes/${mappedFile}.js`);
        currentModule = module;
        module.render(container);
        sendJson("get_openers");
        sendJson("get_config"); // Recargar config para sincronizar los nuevos toggles
    } catch (e) {
        console.error(e);
        container.innerHTML = `<div class="alert alert-error">Error cargando módulo ../classes/${mappedFile}.js</div>`;
    }
}

// --- HELPERS UI ---

function updateStatus(text, colorClass) {
    const el = document.getElementById("connection-status");
    el.innerText = text;
    el.className = `text-[0.6rem] block leading-none font-bold uppercase ${colorClass}`;
}

function updateDashboard(d) {
    // 1. Sincronizar Estado Bot
    isBotRunning = d.is_running;
    if (d.is_running)
        document.body.classList.add(
            "border-l-4",
            "border-r-4",
            "border-primary"
        );
    else
        document.body.classList.remove(
            "border-l-4",
            "border-r-4",
            "border-primary"
        );

    const btn = document.querySelector("#btn-toggle-bot");
    if (btn) {
        if (d.is_running) {
            btn.innerHTML = '<i class="fa-solid fa-stop"></i> STOP';
            btn.classList.remove("btn-success");
            btn.classList.add("btn-error", "animate-pulse");
        } else {
            btn.innerHTML = '<i class="fa-solid fa-play"></i> START';
            btn.classList.remove("btn-error", "animate-pulse");
            btn.classList.add("btn-success");
        }
    }

    // 2. Info del Objetivo
    document.getElementById("target").innerText = d.target || "--";
    const hpPct =
        d.target_max_hp > 0
            ? Math.round((d.target_hp / d.target_max_hp) * 100)
            : 0;
    document.getElementById("hp-bar").style.width = hpPct + "%";
    document.getElementById("hp-abs").innerText = `${compactNum(
        d.target_hp
    )}/${compactNum(d.target_max_hp)}`;

    // 3. Highlight botones de inyección
    document.querySelectorAll(".btn-force").forEach((btn) => {
        const actionName = btn.getAttribute("data-action");
        if (actionName === d.queued_action && d.queued_action !== "") {
            btn.classList.add("ring-2", "ring-accent", "animate-pulse");
        } else {
            btn.classList.remove("ring-2", "ring-accent", "animate-pulse");
        }
    });

    // 4. [NUEVO] Sincronización Inversa (Backend -> Frontend)
    // Si el backend envía el estado de las variables en el paquete de estado, actualizamos los toggles.
    // Esto hace que si pulsas la tecla física, el checkbox se mueva solo.

    // Globales
    if (d.SaveCD !== undefined) setCheck("op_savecd", d.SaveCD);
    if (d.UseMemoryInput !== undefined) setCheck("op_memory", d.UseMemoryInput);
    if (d.AoE_Enabled !== undefined) setCheck("op_aoe", d.AoE_Enabled);
    if (d.TrueNorth_Auto !== undefined)
        setCheck("op_truenorth", d.TrueNorth_Auto);

    // Específicos (Intento de mapeo automático)
    // Nota: El backend debe enviar estas propiedades en el JSON de estado para que funcione.
    if (d.UsePB !== undefined) setCheck("op_pb", d.UsePB);
    if (d.UseRoF !== undefined) setCheck("op_rof", d.UseRoF);
    if (d.UseBrotherhood !== undefined) setCheck("op_bh", d.UseBrotherhood);
    if (d.AutoSong !== undefined) setCheck("op_autosong", d.AutoSong);
    if (d.UseMeikyo !== undefined) setCheck("op_meikyo", d.UseMeikyo);
    // Agrega aquí cualquier otra propiedad que tu C# envíe en el tick de estado
}

function loadConfigToUI(cfg) {
    if (cfg.Operation) {
        const op = cfg.Operation;
        // Globales
        setCheck("op_savecd", op.SaveCD);
        setCheck("op_memory", op.UseMemoryInput);
        setCheck("op_useopener", op.UseOpener);

        // Dinámicos (Intentamos setear todo lo que encontremos)
        setCheck("op_aoe", op.AoE_Enabled);
        setCheck("op_truenorth", op.TrueNorth_Auto);

        setCheck("op_pb", op.UsePB);
        setCheck("op_rof", op.UseRoF);
        setCheck("op_bh", op.UseBrotherhood);
        setCheck("op_row", op.UseRoW);
        setCheck("op_fc", op.UseForbiddenChakra);

        setCheck("op_meikyo", op.UseMeikyo);
        setCheck("op_iki", op.UseIkishoten);
        setCheck("op_kaeshi", op.UseKaeshi);
        setCheck("op_meditate", op.UseMeditate);
        setCheck("op_hagakure", op.UseHagakure);

        setCheck("op_autosong", op.AutoSong);

        // Selectores
        const selOp = document.getElementById("opener_select");
        if (selOp && op.SelectedOpener) selOp.value = op.SelectedOpener;

        const selPot = document.getElementById("potion_select");
        if (selPot && op.SelectedPotionId) selPot.value = op.SelectedPotionId;
    }
}

function collectAndSendOps(triggerElement = null) {
    const data = {
        SaveCD: getCheck("op_savecd"),
        UseMemoryInput: getCheck("op_memory"),
        UseOpener: getCheck("op_useopener"),
        SelectedOpener: getValue("opener_select"),
        UsePotion: getCheck("op_usepotion"),
        SelectedPotionId: parseInt(getValue("potion_select")) || 0,
    };

    document.querySelectorAll('input[id^="op_"]').forEach((el) => {
        const key = el.id.replace("op_", "");

        // --- MAPEO COMÚN ---
        if (key === "aoe") data.AoE_Enabled = el.checked;
        else if (key === "truenorth") data.TrueNorth_Auto = el.checked;
        // --- MAPEO MNK ---
        else if (key === "pb") data.UsePB = el.checked;
        else if (key === "rof") data.UseRoF = el.checked;
        else if (key === "bh") data.UseBrotherhood = el.checked;
        else if (key === "row") data.UseRoW = el.checked;
        else if (key === "fc") data.UseForbiddenChakra = el.checked;
        // --- MAPEO SAM ---
        else if (key === "meikyo") data.UseMeikyo = el.checked;
        else if (key === "iki") data.UseIkishoten = el.checked;
        else if (key === "kaeshi") data.UseKaeshi = el.checked;
        else if (key === "meditate") data.UseMeditate = el.checked;
        else if (key === "hagakure") data.UseHagakure = el.checked;
        // --- MAPEO BRD ---
        else if (key === "autosong") data.AutoSong = el.checked;
    });

    sendJson("save_operation", data);

    // Log Detallado Inteligente
    if (triggerElement) {
        let labelText = triggerElement.id;
        const parentLabel = triggerElement.closest("label");

        if (parentLabel) {
            const textSpan =
                parentLabel.querySelector(".label-text") ||
                parentLabel.querySelector("span.font-bold");
            if (textSpan) labelText = textSpan.innerText.trim();
        }

        if (triggerElement.tagName === "SELECT") {
            const optionText =
                triggerElement.options[triggerElement.selectedIndex].text;
            logConsole(`CAMBIO: ${labelText} -> ${optionText}`, "text-info");
        } else if (triggerElement.type === "checkbox") {
            const status = triggerElement.checked ? "ACTIVADO" : "DESACTIVADO";
            const color = triggerElement.checked
                ? "text-success"
                : "text-error";
            logConsole(`${status}: ${labelText}`, color);
        }
    }
}

// --- UTILS ---

function logConsole(text, colorClass = "") {
    const c = document.getElementById("debug-console");
    if (c.firstElementChild && c.firstElementChild.classList.contains("italic"))
        c.innerHTML = "";

    const line = document.createElement("div");
    line.className = `border-b border-white/5 py-0.5 ${colorClass}`;
    const time = new Date().toLocaleTimeString().split(" ")[0];
    line.innerHTML = `<span class="opacity-40 text-[0.6rem] mr-2 font-bold">[${time}]</span>${text}`;

    c.appendChild(line);
    c.scrollTop = c.scrollHeight;

    if (c.children.length > 50) c.removeChild(c.firstChild);
}

function sendJson(cmd, data = null) {
    if (ws && ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify({ cmd: cmd, data: data }));
    }
}

function loadOpenerList(list) {
    const sel = document.getElementById("opener_select");
    if (!sel) return;
    const currentVal = sel.value;
    sel.innerHTML = '<option value="Ninguno">Ninguno</option>';
    list.forEach((name) => {
        const opt = document.createElement("option");
        opt.value = name;
        opt.innerText = name;
        sel.appendChild(opt);
    });
    if (list.includes(currentVal)) sel.value = currentVal;
}

function loadPotionList(list) {
    const sel = document.getElementById("potion_select");
    if (!sel) return;
    sel.innerHTML = '<option value="0">Ninguna</option>';
    list.forEach((p) => {
        const opt = document.createElement("option");
        opt.value = p.Id;
        opt.innerText = p.Name;
        sel.appendChild(opt);
    });
}

function getCheck(id) {
    const el = document.getElementById(id);
    return el ? el.checked : false;
}

function setCheck(id, val) {
    const el = document.getElementById(id);
    // IMPORTANTE: Solo actualizamos si el valor es diferente para evitar parpadeos
    if (el && el.checked !== val) {
        el.checked = val;
        // NO disparamos eventos aquí para evitar loops infinitos
    }
}

function getValue(id) {
    const el = document.getElementById(id);
    return el ? el.value : "";
}

function compactNum(num) {
    return Intl.NumberFormat("en-US", {
        notation: "compact",
        maximumFractionDigits: 1,
    }).format(num);
}

// Iniciar
connect();
