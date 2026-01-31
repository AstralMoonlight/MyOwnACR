// classes/SAM.js
import { buildInjections, buildToggles } from "../js/ui-builder.js";

// 1. Botones de Inyección Manual (Habilidades que quieres forzar)
const INJECTIONS = [
    {
        id: "Sprint",
        label: "Sprint",
        icon: "fa-solid fa-person-running",
        color: "text-success",
    },
    {
        id: "TrueNorth",
        label: "TrueNorth",
        icon: "fa-solid fa-compass",
        color: "text-base-content",
    },
    {
        id: "ThirdEye",
        label: "Third Eye",
        icon: "fa-solid fa-eye",
        color: "text-error",
    }, // Tengentsu
    {
        id: "Feint",
        label: "Feint",
        icon: "fa-solid fa-user-slash",
        color: "text-warning",
    },
    {
        id: "Bloodbath",
        label: "Blood",
        icon: "fa-solid fa-droplet",
        color: "text-error",
    },
    {
        id: "SecondWind",
        label: "2nd Wind",
        icon: "fa-solid fa-wind",
        color: "text-success",
    },
    {
        id: "Yaten",
        label: "Yaten",
        icon: "fa-solid fa-backward",
        color: "text-info",
    }, // Backstep
    {
        id: "Gyoten",
        label: "Gyoten",
        icon: "fa-solid fa-forward",
        color: "text-info",
    }, // Gapclose
];

// 2. Toggles de Lógica (Checkboxes de configuración)
// NOTA: Asegúrate de que los IDs (ej: 'op_meikyo') coincidan con lo que esperas en tu C#
// En app.js, la función 'collectAndSendOps' leerá estos IDs y enviará { UseMeikyo: true/false }
const LOGIC_TOGGLES = [
    // Estándar
    { id: "op_aoe", label: "AoE Mode", color: "toggle-info" },
    { id: "op_truenorth", label: "Auto TrueNorth", color: "toggle-success" },

    // Específico SAM
    { id: "op_meikyo", label: "Meikyo Shisui", color: "toggle-error" }, // Burst
    { id: "op_iki", label: "Ikishoten", color: "toggle-warning" }, // Gauge/Burst
    { id: "op_kaeshi", label: "Tsubame Gaeshi", color: "toggle-primary" }, // Double Cast
    { id: "op_meditate", label: "Auto Meditate", color: "toggle-secondary" }, // Downtime
    { id: "op_hagakure", label: "Auto Hagakure", color: "toggle-accent" }, // Sen Cleaning
];

// 3. Función principal de renderizado
export function render(container) {
    // Usamos el builder para generar el HTML estándar
    const html = buildInjections(INJECTIONS) + buildToggles(LOGIC_TOGGLES);
    container.innerHTML = html;
}

// 4. (Opcional) Actualización de estado visual específico
export function updateState(data) {
    // Aquí podrías agregar lógica para iluminar botones de SAM si tienes datos del Kenki
    // Por ahora lo dejamos vacío para que no de error.
}
