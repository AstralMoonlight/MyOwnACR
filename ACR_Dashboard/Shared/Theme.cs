using System.Drawing;
using System.Windows.Forms;

namespace ACR_Dashboard.Shared
{
    public static class Theme
    {
        // Paleta de colores (Tipo CSS variables)
        public static Color BackDark = Color.FromArgb(30, 30, 35);
        public static Color BackLight = Color.FromArgb(45, 45, 50);
        public static Color TextWhite = Color.FromArgb(240, 240, 240);
        public static Color AccentBlue = Color.FromArgb(0, 122, 204);
        public static Color DangerRed = Color.FromArgb(220, 53, 69);
        public static Color SuccessGreen = Color.FromArgb(40, 167, 69);

        // Función para aplicar estilo "Botón Moderno"
        public static void StyleButton(Button btn, Color? hoverColor = null)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.BackColor = BackLight;
            btn.ForeColor = TextWhite;
            btn.Cursor = Cursors.Hand;
            btn.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            btn.Size = new Size(80, 60); // Tamaño fijo o dinámico

            // Efecto Hover simple
            btn.MouseEnter += (s, e) => btn.BackColor = hoverColor ?? AccentBlue;
            btn.MouseLeave += (s, e) => btn.BackColor = BackLight;
        }
    }
}
