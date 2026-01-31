// js/ui-builder.js

export function buildInjections(actions) {
    // Genera la cuadrícula de botones de inyección manual
    let html = `<div class="card bg-base-100 shadow-sm"><div class="card-body p-4">
        <h3 class="text-xs font-black opacity-50 uppercase mb-3 flex items-center gap-2">
            <i class="fa-solid fa-bolt text-warning"></i> Inyecciones Manuales
        </h3>
        <div class="grid grid-cols-4 sm:grid-cols-5 gap-2">`;

    actions.forEach((act) => {
        html += `
        <button class="btn btn-force h-12 min-h-0 relative overflow-hidden group" 
                data-action="${act.id}" onclick="app.forceAction(this)">
            <div class="flex flex-col items-center leading-none gap-1 z-10">
                <i class="${act.icon} ${act.color || ""} text-sm"></i>
                <span class="text-[0.6rem] font-bold">${act.label}</span>
            </div>
        </button>`;
    });

    html += `</div></div></div>`;
    return html;
}

export function buildToggles(toggles) {
    // Genera los toggles de lógica (RoF, PB, Songs, etc)
    let html = `<div class="card bg-base-100 shadow-sm"><div class="card-body p-4">
        <h3 class="text-xs font-black opacity-50 uppercase mb-3 flex items-center gap-2">
            <i class="fa-solid fa-microchip text-info"></i> Lógica de Combate
        </h3>
        <div class="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-3">`;

    toggles.forEach((tog) => {
        html += `
        <div class="form-control bg-base-200 rounded-lg p-2 border border-base-content/5">
            <label class="label cursor-pointer p-0 gap-2">
                <span class="label-text text-[0.7rem] font-bold uppercase truncate w-full" title="${
                    tog.label
                }">${tog.label}</span>
                <input type="checkbox" class="toggle toggle-xs ${
                    tog.color || "toggle-primary"
                }" 
                       id="${tog.id}" onchange="app.updateOps(this)" />
            </label>
        </div>`;
    });

    // Selectores especiales (Opener, Poción)
    html += `
        <div class="col-span-full mt-2 flex gap-2">
            <select id="opener_select" class="select select-bordered select-xs w-full font-mono" onchange="app.updateOps(this)">
                <option value="Ninguno">Opener: Ninguno</option>
            </select>
            <select id="potion_select" class="select select-bordered select-xs w-full font-mono" onchange="app.updateOps(this)">
                <option value="0">Poción: Ninguna</option>
            </select>
        </div>
    `;

    html += `</div></div></div>`;
    return html;
}
