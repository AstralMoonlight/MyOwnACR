using System;
using System.Drawing;
using System.Windows.Forms;

namespace ACR_Dashboard.Core
{
    // Todas tus ventanas (Inyecciones, Lógica) heredarán de ESTA clase, no de Form.
    public class OverlayForm : Form
    {
        public OverlayForm()
        {
            // Estilos por defecto para parecer "Gamer/Moderno"
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow; // Borde fino
            this.BackColor = Color.FromArgb(32, 32, 32); // Gris oscuro
            this.ForeColor = Color.WhiteSmoke; // Texto claro
            this.TopMost = true; // Siempre encima del juego
            this.ShowInTaskbar = false; // No llenar la barra de tareas
        }

        // MAGIA DE WINDOWS API: WS_EX_NOACTIVATE
        // Esto impide que la ventana robe el foco al hacer clic en un botón.
        protected override bool ShowWithoutActivation => true;

        private const int WS_EX_NOACTIVATE = 0x08000000;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams param = base.CreateParams;
                param.ExStyle |= WS_EX_NOACTIVATE;
                return param;
            }
        }
    }
}
