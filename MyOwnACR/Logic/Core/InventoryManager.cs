using FFXIVClientStructs.FFXIV.Client.Game;

namespace MyOwnACR.Logic.Core
{
    public static class InventoryManager
    {
        /// <summary>
        /// Intenta usar UNA poción específica por su ID.
        /// </summary>
        public unsafe static bool UseSpecificPotion(ActionManager* am, uint potionId)
        {
            if (am == null || potionId == 0) return false;

            // Verificar si tenemos ESE item específico y si el CD está listo
            if (am->GetActionStatus(ActionType.Item, potionId) == 0)
            {
                am->UseAction(ActionType.Item, potionId, 65535); // 65535 = Self
                return true;
            }
            return false;
        }

        /// <summary>
        /// Verifica el cooldown usando el ID seleccionado.
        /// </summary>
        public unsafe static bool IsPotionReady(ActionManager* am, uint potionId)
        {
            if (potionId == 0) return false;

            // Verificamos el Recast Time real
            float total = am->GetRecastTime(ActionType.Item, potionId);
            float elapsed = am->GetRecastTimeElapsed(ActionType.Item, potionId);

            // Si Total > 0 y Elapsed < Total, está en CD.
            if (total > 0 && elapsed < total) return false;

            return true;
        }
    }
}
