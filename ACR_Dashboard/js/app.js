// js/app.js

const ACR = {
    socket: null,
    HOST: "ws://127.0.0.1:5055",

    state: {
        isRunning: false,
        lastJobId: -1,
        cachedOpeners: [],
        config: null, // Aquí se guarda la copia local de la configuración completa
    },

    init: async function () {
        await Loader.init();
        this.connect();
    },

    connect: function () {
        this.socket = new WebSocket(this.HOST);

        this.socket.onopen = () => {
            Utils.logToConsole("Conectado al Servidor ACR", "text-success");
            document.getElementById("connection-status").innerText =
                "CONECTADO";
            document
                .getElementById("connection-status")
                .classList.replace("text-error", "text-success");

            this.send("get_config");
            this.send("get_openers");
        };

        this.socket.onmessage = (event) => {
            try {
                const msg = JSON.parse(event.data);
                this.handleMessage(msg);
            } catch (e) {
                console.error("JSON Error:", e);
            }
        };

        this.socket.onclose = () => {
            document.getElementById("connection-status").innerText =
                "RECONECTANDO...";
            document
                .getElementById("connection-status")
                .classList.replace("text-success", "text-error");
            setTimeout(() => this.connect(), 2000);
        };
    },

    send: function (cmd, data = null) {
        if (this.socket && this.socket.readyState === WebSocket.OPEN) {
            this.socket.send(JSON.stringify({ cmd, data }));
        }
    },

    handleMessage: function (msg) {
        switch (msg.type) {
            case "status":
                this.updateDashboard(msg.data);
                break;
            case "log":
                Utils.logToConsole(msg.data);
                break;
            case "potion_list":
                this.populatePotions(msg.data);
                break;
            case "opener_list":
                this.state.cachedOpeners = msg.data;
                this.filterOpeners();
                break;
            case "config_data":
                this.state.config = msg.data; // Guardamos la config maestra
                this.applyConfigToUI(); // Aplicamos visualmente
                break;
        }
    },

    // --- APLICAR CONFIGURACIÓN GUARDADA A LA UI ---
    applyConfigToUI: function () {
        const cfg = this.state.config;
        if (!cfg) return;

        // 1. Configuración Global (Common)
        if (cfg.Operation) {
            Utils.setCheck("op_savecd", cfg.Operation.SaveCD);
            Utils.setCheck("op_useopener", cfg.Operation.UseOpener);
            Utils.setCheck("op_usepotion", cfg.Operation.UsePotion);

            const opSel = document.getElementById("opener_select");
            if (opSel && opSel.value === "Ninguno")
                opSel.value = cfg.Operation.SelectedOpener;

            const potSel = document.getElementById("potion_select");
            if (potSel && potSel.value === "0")
                potSel.value = cfg.Operation.SelectedPotionId;
        }

        // 2. Configuración Específica del Job Actual
        this.restoreJobSettings();
    },

    // RESTAURAR CHECKBOXES AL CARGAR O CAMBIAR DE JOB
    restoreJobSettings: function () {
        const cfg = this.state.config;
        const jobId = this.state.lastJobId;
        if (!cfg || jobId === -1) return;

        // BARDO (23)
        if (jobId === 23 && cfg.Bard) {
            Utils.setCheck("brd_autosong", cfg.Bard.AutoSong);
            Utils.setCheck("brd_useapex", cfg.Bard.UseApexArrow);
            Utils.setCheck("brd_ironjaws", cfg.Bard.AutoIronJaws);
            Utils.setCheck("brd_alignbuffs", cfg.Bard.AlignBuffs);
        }

        // SAMURAI (34)
        if (jobId === 34 && cfg.Samurai) {
            Utils.setCheck("sam_meikyo", cfg.Samurai.UseMeikyo);
            Utils.setCheck("sam_hagakure", cfg.Samurai.UseHagakure);
            Utils.setCheck("sam_truenorth", cfg.Samurai.UseTrueNorth);
            Utils.setCheck("sam_kenki", cfg.Samurai.SpendKenki);
        }

        // MONK omitido por ahora
    },

    updateDashboard: function (d) {
        if (d.is_running !== this.state.isRunning) {
            this.state.isRunning = d.is_running;
            if (d.is_running) document.body.classList.add("is-running");
            else document.body.classList.remove("is-running");
        }

        // Detectar cambio de Job
        if (d.job && d.job !== this.state.lastJobId) {
            this.state.lastJobId = d.job;

            // Cargar los nuevos módulos HTML y luego restaurar settings
            Loader.switchJob(d.job).then(() => {
                this.send("get_potions");
                this.filterOpeners();
                this.restoreJobSettings(); // <--- CLAVE: Restaurar visualmente los toggles
            });
        }

        // Sincronizar Toggle SaveCD
        const saveToggle = document.getElementById("op_savecd");
        if (
            saveToggle &&
            typeof d.save_cd !== "undefined" &&
            saveToggle.checked !== d.save_cd
        ) {
            saveToggle.checked = d.save_cd;
            if (this.state.config && this.state.config.Operation) {
                this.state.config.Operation.SaveCD = d.save_cd;
            }
        }

        // Botones en cola
        document
            .querySelectorAll(".btn-queued")
            .forEach((b) => b.classList.remove("btn-queued"));
        if (d.queued_action) {
            const btn = document.querySelector(
                `button[data-action="${d.queued_action}"]`,
            );
            if (btn) btn.classList.add("btn-queued");
        }
    },

    // --- GUARDAR CAMBIOS ---

    // 1. Guardar Operaciones Generales
    updateOps: function () {
        const data = {
            SaveCD: Utils.getCheck("op_savecd"),
            UseOpener: Utils.getCheck("op_useopener"),
            SelectedOpener:
                document.getElementById("opener_select")?.value || "Ninguno",
            UsePotion: Utils.getCheck("op_usepotion"),
            SelectedPotionId:
                parseInt(document.getElementById("potion_select")?.value) || 0,
        };
        this.send("save_operation", data);

        if (this.state.config)
            this.state.config.Operation = {
                ...this.state.config.Operation,
                ...data,
            };
    },

    // 2. Guardar Configuración de JOB
    updateJobConfig: function () {
        const jobId = this.state.lastJobId;
        if (jobId === -1) return;

        let jobData = null;

        // Bardo
        if (jobId === 23) {
            jobData = {
                AutoSong: Utils.getCheck("brd_autosong"),
                UseApexArrow: Utils.getCheck("brd_useapex"),
                AutoIronJaws: Utils.getCheck("brd_ironjaws"),
                AlignBuffs: Utils.getCheck("brd_alignbuffs"),
            };
            // Actualizar memoria local para que no se pierda al cambiar de pestaña
            if (this.state.config && this.state.config.Bard) {
                this.state.config.Bard = {
                    ...this.state.config.Bard,
                    ...jobData,
                };
            }
        }

        // Samurai
        if (jobId === 34) {
            jobData = {
                UseMeikyo: Utils.getCheck("sam_meikyo"),
                UseHagakure: Utils.getCheck("sam_hagakure"),
                UseTrueNorth: Utils.getCheck("sam_truenorth"),
                SpendKenki: Utils.getCheck("sam_kenki"),
            };
            if (this.state.config && this.state.config.Samurai) {
                this.state.config.Samurai = {
                    ...this.state.config.Samurai,
                    ...jobData,
                };
            }
        }

        // Enviar al servidor para guardar en archivo JSON
        this.send("save_config", jobData);
        Utils.logToConsole("Configuración guardada", "text-info");
    },

    toggleBot: function (state) {
        this.send(state ? "START" : "STOP");
    },
    forceAction: function (name) {
        if (navigator.vibrate) navigator.vibrate(50);
        this.send("force_action", name);
    },

    populatePotions: function (list) {
        const sel = document.getElementById("potion_select");
        if (!sel) return;
        const currentVal = sel.value;
        sel.innerHTML = '<option value="0">Ninguna</option>';
        list.forEach((p) => {
            const opt = document.createElement("option");
            opt.value = p.Id;
            opt.innerText = p.Name;
            sel.appendChild(opt);
        });
        sel.value =
            currentVal !== "0"
                ? currentVal
                : this.state.config?.Operation?.SelectedPotionId || "0";
    },

    filterOpeners: function () {
        const list = this.state.cachedOpeners;
        const sel = document.getElementById("opener_select");
        if (!list || !sel) return;
        const jobName = Loader.jobMap[this.state.lastJobId] || "";
        const filtered = list.filter((name) =>
            name.toUpperCase().includes(jobName),
        );
        sel.innerHTML = '<option value="Ninguno">Ninguno</option>';
        (filtered.length > 0 ? filtered : []).forEach((name) => {
            const opt = document.createElement("option");
            opt.value = name;
            opt.innerText = name;
            sel.appendChild(opt);
        });
        const savedOpener = this.state.config?.Operation?.SelectedOpener;
        if (
            savedOpener &&
            (filtered.includes(savedOpener) || savedOpener === "Ninguno")
        ) {
            sel.value = savedOpener;
        }
    },
};

document.addEventListener("DOMContentLoaded", () => ACR.init());
