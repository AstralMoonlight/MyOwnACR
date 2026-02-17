// js/loader.js

const Loader = {
    currentJobId: -1,

    // MAPA DE TRABAJOS: ID -> Nombre de Carpeta
    jobMap: {
        19: "PLD",
        20: "MNK",
        21: "WAR",
        22: "DRG",
        23: "BRD", // Bardo
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
        34: "SAM", // Samurai
        35: "RDM",
        36: "BLU",
        37: "GNB",
        38: "DNC",
        39: "RPR",
        40: "SGE",
        41: "VPR",
        42: "PCT",
    },

    // Carga inicial de módulos comunes
    init: async function () {
        console.log("[Loader] Cargando módulos comunes...");
        try {
            await Promise.all([
                this.loadModule("common/header.html", "module-header"),
                this.loadModule("common/controls.html", "module-controls"),
                this.loadModule("common/config_panel.html", "module-config"),
                this.loadModule("common/console.html", "module-console"),
            ]);
            console.log("[Loader] Módulos comunes listos.");
        } catch (e) {
            console.error("[Loader] Error cargando comunes:", e);
        }
    },

    // Función genérica para fetch e inyección
    loadModule: async function (path, targetId) {
        const target = document.getElementById(targetId);
        if (!target) return;

        try {
            // Timestamp para evitar caché del navegador mientras desarrollamos (?t=...)
            const response = await fetch(`modules/${path}?t=${Date.now()}`);
            if (!response.ok) throw new Error(`HTTP ${response.status}`);
            const html = await response.text();
            target.innerHTML = html;
        } catch (err) {
            console.warn(`[Loader] No se pudo cargar ${path}:`, err);
            target.innerHTML = `<div class="p-4 text-center opacity-30 text-xs border border-dashed border-base-content/20 rounded">Módulo no encontrado: ${path}</div>`;
        }
    },

    // Cambio dinámico de Job
    switchJob: async function (jobId) {
        // Si es el mismo job, no recargamos
        if (this.currentJobId === jobId) return;
        this.currentJobId = jobId;

        const jobFolder = this.jobMap[jobId];

        console.log(
            `[Loader] Cambiando a Job ID: ${jobId} (${jobFolder || "Desconocido"})`,
        );

        if (jobFolder) {
            // Intentamos cargar los módulos específicos
            await this.loadModule(
                `jobs/${jobFolder}/injections.html`,
                "module-job-injections",
            );
            await this.loadModule(
                `jobs/${jobFolder}/logic.html`,
                "module-job-logic",
            );
        } else {
            // Limpiar si no hay job reconocido
            document.getElementById("module-job-injections").innerHTML = "";
            document.getElementById("module-job-logic").innerHTML = "";
        }

        // Actualizar etiqueta en el Header (si ya cargó)
        const badge = document.getElementById("header-job-badge");
        if (badge) {
            badge.innerText = jobFolder || "UNK";
            badge.classList.remove("opacity-0");
        }
    },
};
