// Archivo: Network/CommandProcessor.cs
// Descripción: Procesa TODOS los comandos JSON del Dashboard.
// IMPORTANTE: Incluye manejo de Pociones, Openers y Configuración completa.

using MyOwnACR.Logic.Core;
using MyOwnACR.Models;
using MyOwnACR.GameData;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Keys;
using Dalamud.Plugin.Services; // Para IFramework

namespace MyOwnACR.Network
{
    public class CommandProcessor
    {
        private readonly Plugin _plugin;

        public CommandProcessor(Plugin plugin)
        {
            _plugin = plugin;
        }

        public void HandleCommand(string json)
        {
            try
            {
                var cmd = JObject.Parse(json);
                var type = cmd["cmd"]?.ToString() ?? "";

                switch (type)
                {
                    case "START":
                        _plugin.SetRunning(true);
                        _plugin.FocusGame();
                        _plugin.SendLog("Recibido comando START desde Web");
                        break;

                    case "STOP":
                        _plugin.SetRunning(false);
                        _plugin.SendLog("Recibido comando STOP desde Web");
                        break;

                    case "force_action":
                        var actionName = cmd["data"]?.ToString() ?? "";
                        RotationManager.Instance.QueueManualAction(actionName);
                        _plugin.FocusGame();
                        _plugin.SendLog($"Acción forzada: {actionName}");
                        break;

                    case "get_config":
                        SendConfigData();
                        break;

                    case "save_operation":
                        var newOp = cmd["data"]?.ToObject<OperationalSettings>();
                        if (newOp != null)
                        {
                            _plugin.Config.Operation = newOp;
                            _plugin.Config.Save();

                            // Actualizar Opener
                            if (!string.IsNullOrEmpty(newOp.SelectedOpener) && newOp.SelectedOpener != "Ninguno")
                            {
                                OpenerManager.Instance.SelectOpener(newOp.SelectedOpener);
                                _plugin.SendLog($"[SYSTEM] Opener actualizado: {newOp.SelectedOpener}");
                            }
                            else
                            {
                                // OpenerManager.Instance.Reset(); // Opcional
                            }
                            _plugin.FocusGame();
                        }
                        break;

                    case "save_global":
                        var keyStr = cmd["data"]?["ToggleKey"]?.ToString() ?? "F8";
                        if (Enum.TryParse(keyStr, out VirtualKey k))
                        {
                            _plugin.Config.ToggleHotkey = k;
                            _plugin.Config.Save();
                            _plugin.SendLog($"Hotkey global actualizada a: {k}");
                        }
                        break;

                    case "save_config":
                        // Aquí guardamos la config específica del Job (Monk por ahora)
                        // Si añades Bard, deberás manejarlo aquí también o hacerlo dinámico.
                        var newMonk = cmd["data"]?.ToObject<JobConfigs.JobConfig_MNK>();
                        if (newMonk != null)
                        {
                            _plugin.Config.Monk = newMonk;
                            _plugin.Config.Save();
                        }
                        break;

                    case "save_survival":
                        var newSurv = cmd["data"]?.ToObject<SurvivalConfig>();
                        if (newSurv != null)
                        {
                            _plugin.Config.Survival = newSurv;
                            _plugin.Config.Save();
                        }
                        break;

                    case "get_openers":
                        var openerList = OpenerManager.Instance.GetOpenerNames();
                        _plugin.SendJson("opener_list", openerList);
                        break;

                    case "get_potions":
                        // Esta operación requiere acceso al hilo principal del juego (DataManager/ObjectTable)
                        // Usamos el Framework de Dalamud para encolarla de forma segura.
                        Plugin.Framework.RunOnTick(() =>
                        {
                            try
                            {
                                var player = Plugin.ObjectTable.LocalPlayer;
                                if (player != null)
                                {
                                    var jobId = player.ClassJob.RowId;
                                    var mainStat = JobPotionMapping.GetMainStat(jobId);
                                    var potionsDict = Potion_IDs.GetListForStat(mainStat);

                                    var list = potionsDict
                                        .Select(kv => new { Name = kv.Key, Id = kv.Value })
                                        .ToList();

                                    _plugin.SendJson("potion_list", list);
                                }
                                else
                                {
                                    _plugin.SendJson("potion_list", Array.Empty<object>());
                                }
                            }
                            catch (Exception ex)
                            {
                                Plugin.Log.Error(ex, "Error obteniendo pociones en hilo principal");
                            }
                        });
                        break;
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "Error procesando comando JSON");
            }
        }

        private void SendConfigData()
        {
            var payload = new
            {
                Monk = _plugin.Config.Monk,
                Bard = _plugin.Config.Bard, // Asegúrate de que esto exista en tu clase Configuration
                Survival = _plugin.Config.Survival,
                Operation = _plugin.Config.Operation,
                Global = new { ToggleKey = _plugin.Config.ToggleHotkey.ToString() }
            };
            _plugin.SendJson("config_data", payload);
        }
    }
}
