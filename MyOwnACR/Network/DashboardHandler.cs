using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Keys;
using MyOwnACR.Logic.Core;
using MyOwnACR.GameData.Common;
using MyOwnACR.JobConfigs;
using MyInventoryManager = MyOwnACR.Logic.Core.InventoryManager;

namespace MyOwnACR.Network
{
    public class DashboardHandler
    {
        private readonly Plugin _plugin;

        public DashboardHandler(Plugin plugin)
        {
            _plugin = plugin;
        }

        public void OnWebMessage(string cmd, string dataJson)
        {
            Plugin.Framework.RunOnTick(() =>
            {
                try { ProcessCommand(cmd, dataJson); }
                catch (Exception ex) { Plugin.Log.Error(ex, $"Error procesando comando web '{cmd}'"); }
            });
        }

        private void ProcessCommand(string cmd, string dataJson)
        {
            switch (cmd)
            {
                case "START":
                    _plugin.SetRunning(true);
                    _plugin.FocusGame();
                    _plugin.SendLog("CMD: START");
                    break;

                case "STOP":
                    _plugin.SetRunning(false);
                    _plugin.SendLog("CMD: STOP");
                    break;

                case "force_action":
                    RotationManager.Instance.QueueManualAction(dataJson);
                    _plugin.FocusGame();
                    _plugin.SendLog($"Inyección: {dataJson}");
                    break;

                case "get_config":
                    SendConfigData();
                    break;

                case "get_openers":
                    _plugin.SendJson("opener_list", OpenerManager.Instance.AvailableOpeners);
                    break;

                case "get_potions":
                    HandleGetPotions();
                    break;

                case "save_operation":
                    var newOp = JsonConvert.DeserializeObject<OperationalSettings>(dataJson);
                    if (newOp != null)
                    {
                        _plugin.Config.Operation = newOp;
                        _plugin.Config.Save();
                        if (!string.IsNullOrEmpty(newOp.SelectedOpener) && newOp.SelectedOpener != "Ninguno")
                            OpenerManager.Instance.SelectOpener(newOp.SelectedOpener);
                        else
                            OpenerManager.Instance.SelectOpener("Ninguno");
                        _plugin.FocusGame();
                    }
                    break;

                case "save_global":
                    var globalObj = JObject.Parse(dataJson);
                    var keyStr = globalObj["ToggleKey"]?.ToString() ?? "F8";
                    if (Enum.TryParse(keyStr, out VirtualKey k))
                    {
                        _plugin.Config.ToggleHotkey = k;
                        _plugin.Config.Save();
                    }
                    break;

                case "save_config":
                    var player = Plugin.ObjectTable.LocalPlayer;
                    if (player != null)
                    {
                        uint jobId = player.ClassJob.RowId;

                        // BARDO
                        if (jobId == 23)
                        {
                            // PopulateObject actualiza solo las propiedades recibidas (toggles) sin borrar las teclas
                            JsonConvert.PopulateObject(dataJson, _plugin.Config.Bard);
                            _plugin.Config.Save();
                        }
                        // SAMURAI
                        else if (jobId == 34)
                        {
                            JsonConvert.PopulateObject(dataJson, _plugin.Config.Samurai);
                            _plugin.Config.Save();
                        }
                        // MONJE (Omitido por ahora como pediste, pero el bloque está listo si lo necesitas)
                        else if (jobId == 20)
                        {
                            JsonConvert.PopulateObject(dataJson, _plugin.Config.Monk);
                            _plugin.Config.Save();
                        }
                    }
                    break;

                case "save_survival":
                    var newSurv = JsonConvert.DeserializeObject<SurvivalConfig>(dataJson);
                    if (newSurv != null) { _plugin.Config.Survival = newSurv; _plugin.Config.Save(); }
                    break;
            }
        }

        private void SendConfigData()
        {
            var payload = new
            {
                Monk = _plugin.Config.Monk,
                Bard = _plugin.Config.Bard,
                Samurai = _plugin.Config.Samurai,
                Survival = _plugin.Config.Survival,
                Operation = _plugin.Config.Operation,
                Global = new { ToggleKey = _plugin.Config.ToggleHotkey.ToString() }
            };
            _plugin.SendJson("config_data", payload);
        }

        private void HandleGetPotions()
        {
            var player = Plugin.ObjectTable.LocalPlayer;
            if (player == null) return;
            var jobId = player.ClassJob.RowId;
            var stat = GetMainStatForJob(jobId);
            if (stat == PotionStat.None)
            {
                _plugin.SendJson("potion_list", new List<object>());
                return;
            }
            var potentialPotions = Potion_IDs.GetListForStat(stat);
            var filteredList = new List<object>();
            foreach (var kvp in potentialPotions)
            {
                if (MyInventoryManager.GetItemCount(kvp.Value) > 0)
                {
                    filteredList.Add(new { Id = kvp.Value, Name = kvp.Key });
                }
            }
            _plugin.SendJson("potion_list", filteredList);
        }

        private PotionStat GetMainStatForJob(uint jobId)
        {
            if (new[] { 19u, 20u, 21u, 22u, 32u, 34u, 37u, 39u }.Contains(jobId)) return PotionStat.Strength;
            if (new[] { 23u, 30u, 31u, 38u, 41u }.Contains(jobId)) return PotionStat.Dexterity;
            if (new[] { 25u, 27u, 35u, 36u, 42u }.Contains(jobId)) return PotionStat.Intelligence;
            if (new[] { 24u, 28u, 33u, 40u }.Contains(jobId)) return PotionStat.Mind;
            return PotionStat.None;
        }
    }
}
