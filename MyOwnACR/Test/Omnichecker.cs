using System;
using Lumina.Excel;
using Lumina.Excel.Sheets; // Asegúrate de que este es el namespace correcto en tu versión
using Dalamud.Plugin.Services;

// Esto desactiva las advertencias molestas de los guiones bajos por ahora
#pragma warning disable IDE1006 

namespace MyOwnACR.Test
{
    public class Omnichecker
    {
        private readonly IDataManager _dataManager;
        private readonly ExcelSheet<BNpcBase> _bNpcSheet;
        private readonly ExcelSheet<ModelChara> _modelCharaSheet;

        public Omnichecker(IDataManager dataManager)
        {
            _dataManager = dataManager;
            _bNpcSheet = _dataManager.GetExcelSheet<BNpcBase>();
            _modelCharaSheet = _dataManager.GetExcelSheet<ModelChara>();
        }

        public unsafe string AnalyzeTarget(uint bNpcId)
        {
            // Verificamos si las hojas existen (por seguridad)
            if (_bNpcSheet == null || _modelCharaSheet == null)
                return "Error: Hojas de Excel no cargadas.";

            // 1. Buscamos el NPC
            // TryGetRow es mejor para evitar crashes si el ID es 0 o inválido
            if (!_bNpcSheet.TryGetRow(bNpcId, out var npcRow))
                return $"Error: No se encontró BNpcBase para ID {bNpcId}";

            // 2. Obtenemos el ID del Modelo desde el NPC
            var modelId = npcRow.ModelChara.RowId;

            // 3. Buscamos el Modelo
            if (!_modelCharaSheet.TryGetRow(modelId, out var modelRow))
                return $"Error: No se encontró ModelChara para ID {modelId}";

            // 4. Devolvemos los datos para investigar
            // CORRECCIÓN: Usamos 'npcRow.Scale' en lugar de 'modelRow.ModelScale'
            return $"[OmniChecker] NPC:{bNpcId} | ModelID:{modelId} | Type:{modelRow.Type} | Scale:{npcRow.Scale}";
        }
    }
}
