using MyOwnACR.GameData;

namespace MyOwnACR.Logic.Jobs.Bard.Skills
{
    public static class DotLogic
    {
        public static uint GetAction(
            bool hasStorm, bool hasCaustic,
            float stormTime, float causticTime,
            float ragingStrikesTimeLeft, // <--- Dato clave para snapshot
            int level)
        {
            // IDs dinámicos
            uint stormAction = (level >= 64) ? BRD_IDs.Stormbite : BRD_IDs.Windbite;
            uint causticAction = (level >= 64) ? BRD_IDs.CausticBite : BRD_IDs.VenomousBite;

            // 1. APLICACIÓN INICIAL
            if (!hasStorm && level >= 30) return stormAction;
            if (!hasCaustic && level >= 6) return causticAction;

            if (level < 56) // Pre-Iron Jaws
            {
                if (stormTime < 3.0f) return stormAction;
                if (causticTime < 3.0f) return causticAction;
                return 0;
            }

            // 2. LÓGICA IRON JAWS

            // A. Refresco Obligatorio (Pandemic) - Se van a caer
            if (stormTime < 5.0f || causticTime < 5.0f)
            {
                return BRD_IDs.IronJaws;
            }

            // B. SNAPSHOTTING (Maximizar Daño)
            // Si tenemos el buff de Raging Strikes activo, y le queda poco tiempo (< 3s),
            // refrescamos los DoTs AHORA para que se queden potenciados otros 45s,
            // aunque a los actuales les quede tiempo.
            if (ragingStrikesTimeLeft > 0 && ragingStrikesTimeLeft < 4.0f) // 4s ventana seguridad
            {
                // Solo vale la pena si los DoTs no están recién puestos (ej. > 40s)
                if (stormTime < 40.0f)
                {
                    return BRD_IDs.IronJaws;
                }
            }

            return 0;
        }
    }
}
