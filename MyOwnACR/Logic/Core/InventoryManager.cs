using FFXIVClientStructs.FFXIV.Client.Game;

namespace MyOwnACR.Logic.Core
{
    public static class InventoryManager
    {
        private const ulong TargetSelf = 65535;

        // ==================================================================================
        // NUEVO: CONTAR ITEMS
        // ==================================================================================
        public unsafe static int GetItemCount(uint itemId)
        {
            // Accedemos al Singleton del InventoryManager del juego
            var manager = FFXIVClientStructs.FFXIV.Client.Game.InventoryManager.Instance();
            if (manager == null) return 0;

            // GetInventoryItemCount devuelve la suma de NQ y HQ en inventario regular.
            // Argumentos: (ItemId, isHq, checkEquipped, checkArmory)
            // Ponemos false en isHq para que sume ambos si es posible, o chequeamos ambos.
            // Nota: En versiones recientes de ClientStructs, suele sumar todo si no especificas.

            return manager->GetInventoryItemCount(itemId, false, false, false);
        }

        // ==================================================================================
        // USO DE ITEMS
        // ==================================================================================
        public unsafe static bool UseSpecificPotion(ActionManager* am, uint potionId)
        {
            if (am == null || potionId == 0) return false;

            // GetActionStatus ya valida si tienes cantidad > 0.
            // Si devuelve 0, es que se puede usar AHORA MISMO.
            if (am->GetActionStatus(ActionType.Item, potionId) == 0)
            {
                am->UseAction(ActionType.Item, potionId, TargetSelf);
                return true;
            }
            return false;
        }

        // ==================================================================================
        // VERIFICACIÓN DE ESTADO (CORREGIDO)
        // ==================================================================================
        public unsafe static bool IsPotionReady(ActionManager* am, uint potionId)
        {
            if (am == null || potionId == 0) return false;

            // 1. CHEQUEO DE CANTIDAD (CRUCIAL)
            // Antes decíamos "Si CD == 0, está lista". ERROR.
            // Si CD == 0 pero tienes 0 items, IsPotionReady devolvía true y rompía la lógica.
            if (GetItemCount(potionId) <= 0) return false;

            // 2. CHEQUEO DE COOLDOWN
            float total = am->GetRecastTime(ActionType.Item, potionId);
            float elapsed = am->GetRecastTimeElapsed(ActionType.Item, potionId);

            if (total > 0 && elapsed < total) return false;

            return true;
        }
    }
}
