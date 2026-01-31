import http.server
import socketserver
import webbrowser
import os
import sys
import time
from threading import Timer

# --- CONFIGURACIÓN ---
PORT = 8000
FOLDER_NAME = "dashboard"
URL = f"http://localhost:{PORT}"

def open_browser():
    """Abre el navegador después de una pequeña espera para asegurar que el server esté listo."""
    print(f"🚀 Abriendo dashboard en: {URL}")
    webbrowser.open(URL)

def run_server():
    # 1. Verificar que la carpeta existe
    if not os.path.exists(FOLDER_NAME):
        print(f"❌ Error: No encuentro la carpeta '{FOLDER_NAME}'.")
        print("   Asegúrate de ejecutar este script desde la raíz del proyecto.")
        return

    # 2. Cambiar el directorio de trabajo a 'dashboard'
    # Esto hace que http://localhost:8000 sirva directamente el index.html
    os.chdir(FOLDER_NAME)

    # 3. Configurar el servidor para reutilizar el puerto (evita errores al reiniciar rápido)
    Handler = http.server.SimpleHTTPRequestHandler
    
    class ReusableTCPServer(socketserver.TCPServer):
        allow_reuse_address = True

    try:
        with ReusableTCPServer(("", PORT), Handler) as httpd:
            print(f"✅ Servidor activo en el puerto {PORT}")
            print("   Presiona Ctrl+C para detenerlo.")
            
            # Programar la apertura del navegador en 1 segundo (en un hilo separado)
            Timer(1, open_browser).start()
            
            # Iniciar el bucle del servidor
            httpd.serve_forever()
            
    except KeyboardInterrupt:
        print("\n🛑 Servidor detenido.")
    except OSError as e:
        if e.errno == 98 or e.errno == 10048: # Address already in use
            print(f"⚠️  El puerto {PORT} está ocupado. ¿Ya tienes el script corriendo?")
        else:
            print(f"❌ Error del servidor: {e}")

if __name__ == "__main__":
    run_server()