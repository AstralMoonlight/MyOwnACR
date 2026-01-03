// Archivo: MyOwnACR/KeyCodes.cs
using System;

namespace MyOwnACR
{
    public enum HotbarType
    {
        Barra1_Base,
        Barra2_Ctrl,
        Barra3_Shift,
        Barra4_Alt,
        Barra5_CtrlAlt
    }

    [Serializable]
    public class KeyBind
    {
        public byte Key { get; set; }
        public HotbarType Bar { get; set; }
        public KeyBind() { }
        public KeyBind(byte key, HotbarType bar) { Key = key; Bar = bar; }
    }

    public static class Keys
    {
        // Números
        public const byte Num0 = 0x30;
        public const byte Num1 = 0x31;
        public const byte Num2 = 0x32;
        public const byte Num3 = 0x33;
        public const byte Num4 = 0x34;
        public const byte Num5 = 0x35;
        public const byte Num6 = 0x36;
        public const byte Num7 = 0x37;
        public const byte Num8 = 0x38;
        public const byte Num9 = 0x39;

        // Letras
        public const byte A = 0x41;
        public const byte B = 0x42;
        public const byte C = 0x43;
        public const byte D = 0x44;
        public const byte E = 0x45;
        public const byte F = 0x46;
        public const byte G = 0x47;
        public const byte H = 0x48;
        public const byte I = 0x49;
        public const byte J = 0x4A;
        public const byte K = 0x4B;
        public const byte L = 0x4C;
        public const byte M = 0x4D;
        public const byte N = 0x4E;
        public const byte O = 0x4F;
        public const byte P = 0x50; // <--- AGREGADA
        public const byte Q = 0x51;
        public const byte R = 0x52;
        public const byte S = 0x53;
        public const byte T = 0x54;
        public const byte U = 0x55;
        public const byte V = 0x56;
        public const byte W = 0x57;
        public const byte X = 0x58;
        public const byte Y = 0x59; // <--- AGREGADA
        public const int Z = 0x5A;

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
        public const byte MenorQue = 0xE2;
        public const byte Barrita = 0xDC;
    }
}
