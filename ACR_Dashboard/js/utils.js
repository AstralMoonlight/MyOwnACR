// js/utils.js

const Utils = {
    // Formatear números grandes (Ej: 15000 -> "15k")
    compactNum: (num) => {
        return Intl.NumberFormat("en-US", {
            notation: "compact",
            maximumFractionDigits: 1,
        }).format(num);
    },

    // Helpers para leer/escribir Inputs de forma segura
    getCheck: (id) => {
        const el = document.getElementById(id);
        return el ? el.checked : false;
    },

    setCheck: (id, val) => {
        const el = document.getElementById(id);
        if (el) el.checked = val;
    },

    getInt: (id) => {
        const el = document.getElementById(id);
        return el ? parseInt(el.value) || 0 : 0;
    },

    setVal: (id, val) => {
        const el = document.getElementById(id);
        if (el) el.value = val;
    },

    // Generar opciones para un <select> desde un objeto
    generateOptions: (dict, current) => {
        return Object.entries(dict)
            .map(
                ([k, v]) =>
                    `<option value="${k}" ${k == current ? "selected" : ""}>${v}</option>`,
            )
            .join("");
    },

    // UI Helpers
    toggleTheme: () => {
        const html = document.querySelector("html");
        const current = html.getAttribute("data-theme");
        html.setAttribute(
            "data-theme",
            current === "night" ? "winter" : "night",
        );
    },

    openConfigModal: () => {
        ACR.send("get_config");
        document.getElementById("config_modal").showModal();
    },

    // [NUEVO] Función para copiar el contenido de la consola
    copyConsole: () => {
        const c = document.getElementById("module-console-output");
        if (!c) return;

        // Capturamos el texto. innerText respeta los saltos de línea de los divs.
        const text = c.innerText;

        if (!text || text.includes("Esperando conexión...")) return;

        navigator.clipboard
            .writeText(text)
            .then(() => {
                Utils.logToConsole(
                    "Contenido copiado al portapapeles",
                    "text-info",
                );
            })
            .catch((err) => {
                console.error("Error al copiar:", err);
                Utils.logToConsole("Error al copiar log", "text-error");
            });
    },

    // Loguear en la consola visual del dashboard
    logToConsole: (text, colorClass = "") => {
        const c = document.getElementById("module-console-output");
        if (!c) return;

        // Limpiar mensaje de espera
        if (
            c.firstElementChild &&
            c.firstElementChild.classList.contains("italic")
        )
            c.innerHTML = "";

        const line = document.createElement("div");
        line.className = `console-line ${colorClass}`;

        const d = new Date();
        const time =
            d.toLocaleTimeString("es-CL", { hour12: false }) +
            "." +
            String(d.getMilliseconds()).padStart(3, "0");

        line.innerHTML = `<span class="opacity-40 text-[0.6rem] mr-2 font-bold font-mono">[${time}]</span>${text}`;
        c.appendChild(line);

        // Scroll automático al final
        c.scrollTop = c.scrollHeight;

        // Limitar historial a 100 líneas
        if (c.children.length > 100) c.removeChild(c.firstChild);
    },
};
