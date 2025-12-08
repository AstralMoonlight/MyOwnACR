// Archivo: MyOwnACR/KeyCodes.cs
// Descripción: Contiene las definiciones de teclas personalizadas y estructuras base proporcionadas por el usuario.
// FIX: Se renombra la propiedad 'Modifiers' a 'Bar' para solucionar el error CS1061.

using System;

namespace MyOwnACR
{
    // Tipos de Barra (Tus 5 Hotbars)
    public enum HotbarType
    {
        Barra1_Base,       // Sin modificador
        Barra2_Ctrl,       // Ctrl + Tecla
        Barra3_Shift,      // Shift + Tecla
        Barra4_Alt,        // Alt + Tecla
        Barra5_CtrlAlt     // Ctrl + Alt + Tecla
    }

    /// <summary>
    /// Clase contenedora para una asignación de tecla.
    /// Mantenida para compatibilidad con JobConfigs (MNK.cs).
    /// </summary>
    [Serializable]
    public class KeyBind
    {
        public byte Key { get; set; }

        // CAMBIO: Renombrado de 'Modifiers' a 'Bar' para coincidir con la lógica existente (MNK_Logic.cs, Survival.cs)
        public HotbarType Bar { get; set; }

        public KeyBind() { }

        public KeyBind(byte key, HotbarType bar)
        {
            Key = key;
            Bar = bar;
        }
    }

    // Tus Teclas Base (Códigos Virtuales de Windows)
    public static class Keys
    {
        // Números
        public const byte Num1 = 0x31;
        public const byte Num2 = 0x32;
        public const byte Num3 = 0x33;
        public const byte Num4 = 0x34;
        public const byte Num5 = 0x35;
        public const byte Num6 = 0x36;

        // Letras
        public const byte Q = 0x51;
        public const byte W = 0x57;
        public const byte E = 0x45;
        public const byte R = 0x52;
        public const byte T = 0x54;
        public const byte F = 0x46;
        public const byte G = 0x47;
        public const byte Z = 0x5A;
        public const byte X = 0x58;
        public const byte C = 0x43;
        public const byte V = 0x56;

        // Símbolos Especiales (Teclado Español/ISO)
        public const byte Barrita = 0xDC; // VK_OEM_5 (La barra | o º)
        public const byte MenorQue = 0xE2; // VK_OEM_102 (< >)

        // Teclas F
        public const byte F1 = 0x70;
        public const byte F2 = 0x71;
        public const byte F3 = 0x72;
        public const byte F4 = 0x73;
        public const byte F5 = 0x74;
        public const byte F6 = 0x75;
        public const byte F7 = 0x76;
        public const byte F8 = 0x77;
        public const byte F9 = 0x78;
        public const byte F10 = 0x79;
        public const byte F11 = 0x7A;
        public const byte F12 = 0x7B;

        // Otros
        public const byte Space = 0x20;
        public const byte Escape = 0x1B;
    }
}
