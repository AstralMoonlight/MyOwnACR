namespace MyOwnACR.GameData.Common
{
    /// <summary>
    /// Categoría principal de la poción (Stat principal).
    /// </summary>
    public enum PotionStat
    {
        None = 0,
        Strength = 1,    // Melee DPS (MNK, DRG, SAM, RPR, VPR) + Tanks
        Dexterity = 2,   // Phys Ranged (BRD, MCH, DNC) + Ninja
        Intelligence = 3,// Casters (BLM, SMN, RDM, PCT)
        Mind = 4,        // Healers
        Vitality = 5     // Generalmente no usada en rotaciones DPS, pero existe.
    }
}
