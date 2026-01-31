// classes/BRD.js
import { buildInjections, buildToggles } from "../js/ui-builder.js";

// 1. Botones de Inyección Manual
const INJECTIONS = [
    {
        id: "Sprint",
        label: "Sprint",
        icon: "fa-solid fa-person-running",
        color: "text-success",
    },
    {
        id: "Troubadour",
        label: "Troubadour",
        icon: "fa-solid fa-shield-heart",
        color: "text-info",
    }, // Mitigación
    {
        id: "NaturesMinne",
        label: "Minne",
        icon: "fa-solid fa-heart-circle-plus",
        color: "text-success",
    }, // Buff Curación
    {
        id: "WardensPaean",
        label: "Warden",
        icon: "fa-solid fa-wand-magic-sparkles",
        color: "text-accent",
    }, // Cleanse (Esna)
    {
        id: "RepellingShot",
        label: "Repelling",
        icon: "fa-solid fa-backward",
        color: "text-warning",
    }, // Salto atrás
    {
        id: "HeadGraze",
        label: "Interrupt",
        icon: "fa-solid fa-scissors",
        color: "text-error",
    }, // Interrupción
    {
        id: "SecondWind",
        label: "2nd Wind",
        icon: "fa-solid fa-wind",
        color: "text-success",
    },
    {
        id: "Bloodbath",
        label: "Blood",
        icon: "fa-solid fa-droplet",
        color: "text-error",
    },
];

// 2. Toggles de Lógica
const LOGIC_TOGGLES = [
    // Estándar
    { id: "op_aoe", label: "AoE Mode", color: "toggle-info" },

    // Específico BRD
    // "AutoSong" controlará si el bot gestiona la rotación de Minuet/Ballad/Paeon automáticamente
    { id: "op_autosong", label: "Auto Songs", color: "toggle-warning" },
];

// 3. Renderizado
export function render(container) {
    const html = buildInjections(INJECTIONS) + buildToggles(LOGIC_TOGGLES);
    container.innerHTML = html;
}

// 4. Actualización de estado (Opcional por ahora)
export function updateState(data) {
    // Aquí podrías añadir lógica para iluminar la canción activa si el backend envía esa data
}
