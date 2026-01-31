// classes/MNK.js
import { buildInjections, buildToggles } from "../js/ui-builder.js";

const INJECTIONS = [
    {
        id: "SixSidedStar",
        label: "SSS",
        icon: "fa-solid fa-star",
        color: "text-warning",
    },
    {
        id: "RiddleOfEarth",
        label: "Earth",
        icon: "fa-solid fa-shield-halved",
        color: "text-info",
    },
    {
        id: "Mantra",
        label: "Mantra",
        icon: "fa-solid fa-hands-praying",
        color: "text-accent",
    },
    {
        id: "TrueNorth",
        label: "T.North",
        icon: "fa-solid fa-compass",
        color: "text-base-content",
    },
    {
        id: "Sprint",
        label: "Sprint",
        icon: "fa-solid fa-person-running",
        color: "text-success",
    },
    {
        id: "Bloodbath",
        label: "Blood",
        icon: "fa-solid fa-droplet",
        color: "text-error",
    },
    {
        id: "SecondWind",
        label: "Wind",
        icon: "fa-solid fa-heart",
        color: "text-success",
    },
];

const LOGIC_TOGGLES = [
    { id: "op_aoe", label: "AoE Mode", color: "toggle-info" },
    { id: "op_truenorth", label: "Auto TrueNorth", color: "toggle-success" },
    { id: "op_pb", label: "Perfect Balance", color: "toggle-primary" },
    { id: "op_rof", label: "Riddle of Fire", color: "toggle-error" },
    { id: "op_bh", label: "Brotherhood", color: "toggle-warning" },
    { id: "op_row", label: "Riddle of Wind", color: "toggle-success" },
    { id: "op_fc", label: "Auto Chakra", color: "toggle-accent" },
];

export function render(container) {
    const html = buildInjections(INJECTIONS) + buildToggles(LOGIC_TOGGLES);
    container.innerHTML = html;
}

export function updateState(data) {
    // Si necesitas lógica específica de UI para MNK cuando llegan datos
    // Ej: Pintar barra de Chakra (Si decidimos agregar gauges visuales)
}
