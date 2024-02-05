using BepInEx.Logging;
using Comfort.Common;
using EFT;
using StayInTarkov.Configuration;
using StayInTarkov.Coop.Matchmaker;
using StayInTarkov.Coop.Players;
using StayInTarkov.Networking;
using StayInTarkov.UI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static StayInTarkov.Coop.Components.CoopGameComponents.CoopGameComponent;

namespace StayInTarkov.Coop.Components.CoopGameComponents
{
    public class CoopGameGUIComponent : MonoBehaviour
    {

        GUIStyle middleLabelStyle;
        GUIStyle middleLargeLabelStyle;
        GUIStyle normalLabelStyle;

        private ISITGame LocalGameInstance { get; } = Singleton<ISITGame>.Instance;
        private CoopGameComponent CoopGameComponent { get { return CoopGameComponent.GetCoopGameComponent(); } }

        private ConcurrentDictionary<string, CoopPlayer> Players => CoopGameComponent?.Players;

        private IEnumerable<EFT.Player> PlayerUsers => CoopGameComponent?.PlayerUsers;

        private EFT.Player[] PlayerBots => CoopGameComponent?.PlayerBots;

        private SITConfig SITConfig => CoopGameComponent?.SITConfig;

        float screenScale = 1.0f;
        private ManualLogSource Logger;

        Camera GameCamera { get; set; }

        int GuiX = 10;
        int GuiWidth = 400;
        public int ServerPing => CoopGameComponent.ServerPing;

        private PaulovTMPManager TMPManager { get; } = new PaulovTMPManager();
        private GameObject _endGameMessageGO;

        void Awake()
        {
            // ----------------------------------------------------
            // Create a BepInEx Logger for CoopGameComponent
            Logger = BepInEx.Logging.Logger.CreateLogSource($"{nameof(CoopGameGUIComponent)}");
            Logger.LogDebug($"{nameof(CoopGameGUIComponent)}:Awake");

            _endGameMessageGO = TMPManager.InstantiateTarkovTextLabel("_EndGameMessage", "", 20, new Vector3(0, (Screen.height / 2) - 120, 0));
        }

        void OnGUI()
        {


            if (normalLabelStyle == null)
            {
                normalLabelStyle = new GUIStyle(GUI.skin.label);
                normalLabelStyle.fontSize = 16;
                normalLabelStyle.fontStyle = FontStyle.Bold;
            }
            if (middleLabelStyle == null)
            {
                middleLabelStyle = new GUIStyle(GUI.skin.label);
                middleLabelStyle.fontSize = 18;
                middleLabelStyle.fontStyle = FontStyle.Bold;
                middleLabelStyle.alignment = TextAnchor.MiddleCenter;
            }
            if (middleLargeLabelStyle == null)
            {
                middleLargeLabelStyle = new GUIStyle(middleLabelStyle);
                middleLargeLabelStyle.fontSize = 24;
            }

            var rect = new Rect(GuiX, 5, GuiWidth, 100);
            rect = DrawPing(rect);

            GUIStyle style = GUI.skin.label;
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = 13;

            var w = 0.5f; // proportional width (0..1)
            var h = 0.2f; // proportional height (0..1)
            var rectEndOfGameMessage = Rect.zero;
            rectEndOfGameMessage.x = (float)(Screen.width * (1 - w)) / 2;
            rectEndOfGameMessage.y = (float)(Screen.height * (1 - h)) / 2 + Screen.height / 3;
            rectEndOfGameMessage.width = Screen.width * w;
            rectEndOfGameMessage.height = Screen.height * h;

            var numberOfPlayersDead = PlayerUsers.Count(x => !x.HealthController.IsAlive);

            if (LocalGameInstance == null)
                return;

            var coopGame = LocalGameInstance as CoopGame;
            if (coopGame == null)
                return;

            rect = DrawSITStats(rect, numberOfPlayersDead, coopGame);

            var quitState = CoopGameComponent.GetQuitState();
            switch (quitState)
            {
                case EQuitState.YourTeamIsDead:
                    GUI.Label(rectEndOfGameMessage, StayInTarkovPlugin.LanguageDictionary["RAID_TEAM_DEAD"].ToString(), middleLargeLabelStyle);
                    break;
                case EQuitState.YouAreDead:
                    GUI.Label(rectEndOfGameMessage, StayInTarkovPlugin.LanguageDictionary["RAID_PLAYER_DEAD_SOLO"].ToString(), middleLargeLabelStyle);
                    break;
                case EQuitState.YouAreDeadAsHost:
                    GUI.Label(rectEndOfGameMessage, StayInTarkovPlugin.LanguageDictionary["RAID_PLAYER_DEAD_HOST"].ToString(), middleLargeLabelStyle);
                    break;
                case EQuitState.YouAreDeadAsClient:
                    GUI.Label(rectEndOfGameMessage, StayInTarkovPlugin.LanguageDictionary["RAID_PLAYER_DEAD_CLIENT"].ToString(), middleLargeLabelStyle);
                    break;
                case EQuitState.YourTeamHasExtracted:
                    GUI.Label(rectEndOfGameMessage, StayInTarkovPlugin.LanguageDictionary["RAID_TEAM_EXTRACTED"].ToString(), middleLargeLabelStyle);
                    break;
                case EQuitState.YouHaveExtractedOnlyAsHost:
                    GUI.Label(rectEndOfGameMessage, StayInTarkovPlugin.LanguageDictionary["RAID_PLAYER_EXTRACTED_HOST"].ToString(), middleLargeLabelStyle);
                    break;
                case EQuitState.YouHaveExtractedOnlyAsClient:
                    GUI.Label(rectEndOfGameMessage, StayInTarkovPlugin.LanguageDictionary["RAID_PLAYER_EXTRACTED_CLIENT"].ToString(), middleLargeLabelStyle);
                    break;
            }

            //OnGUI_DrawPlayerList(rect);
            //OnGUI_DrawPlayerFriendlyTags(rect);
            //OnGUI_DrawPlayerEnemyTags(rect);

        }

        private Rect DrawPing(Rect rect)
        {
            if (!PluginConfigSettings.Instance.CoopSettings.SETTING_ShowSITStatistics)
                return rect;

            rect.y = 5;
            GUI.Label(rect, $"SIT Coop: " + (SITMatchmaking.IsClient ? "CLIENT" : "SERVER"));
            rect.y += 15;

            // PING ------
            GUI.contentColor = Color.white;
            GUI.contentColor = ServerPing >= AkiBackendCommunication.PING_LIMIT_HIGH ? Color.red : ServerPing >= AkiBackendCommunication.PING_LIMIT_MID ? Color.yellow : Color.green;
            GUI.Label(rect, $"RTT:{ServerPing}");
            rect.y += 15;
            GUI.Label(rect, $"Host RTT:{ServerPing + AkiBackendCommunication.Instance.HostPing}");
            rect.y += 15;
            GUI.contentColor = Color.white;

            if (AkiBackendCommunication.Instance.HighPingMode)
            {
                GUI.contentColor = Color.red;
                GUI.Label(rect, $"!HIGH PING MODE!");
                GUI.contentColor = Color.white;
                rect.y += 15;
            }

            return rect;
        }

        private Rect DrawSITStats(Rect rect, int numberOfPlayersDead, CoopGame coopGame)
        {
            if (!PluginConfigSettings.Instance.CoopSettings.SETTING_ShowSITStatistics)
                return rect;

            var numberOfPlayersAlive = PlayerUsers.Count(x => x.HealthController.IsAlive);
            // gathering extracted
            var numberOfPlayersExtracted = coopGame.ExtractedPlayers.Count;
            GUI.Label(rect, $"Players (Alive): {numberOfPlayersAlive}");
            rect.y += 15;
            GUI.Label(rect, $"Players (Dead): {numberOfPlayersDead}");
            rect.y += 15;
            GUI.Label(rect, $"Players (Extracted): {numberOfPlayersExtracted}");
            rect.y += 15;
            GUI.Label(rect, $"Bots: {PlayerBots.Length}");
            rect.y += 15;
            return rect;
        }

        private void OnGUI_DrawPlayerFriendlyTags(Rect rect)
        {
            if (SITConfig == null)
            {
                Logger.LogError("SITConfig is null?");
                return;
            }

            if (!SITConfig.showPlayerNameTags)
            {
                return;
            }

            if (FPSCamera.Instance == null)
                return;

            if (Players == null)
                return;

            if (PlayerUsers == null)
                return;

            if (Camera.current == null)
                return;

            if (!Singleton<GameWorld>.Instantiated)
                return;


            if (FPSCamera.Instance.SSAA != null && FPSCamera.Instance.SSAA.isActiveAndEnabled)
                screenScale = FPSCamera.Instance.SSAA.GetOutputWidth() / (float)FPSCamera.Instance.SSAA.GetInputWidth();

            var ownPlayer = Singleton<GameWorld>.Instance.MainPlayer;
            if (ownPlayer == null)
                return;

            foreach (var pl in PlayerUsers)
            {
                if (pl == null)
                    continue;

                if (pl.HealthController == null)
                    continue;

                if (pl.IsYourPlayer && pl.HealthController.IsAlive)
                    continue;

                Vector3 aboveBotHeadPos = pl.PlayerBones.Pelvis.position + Vector3.up * (pl.HealthController.IsAlive ? 1.1f : 0.3f);
                Vector3 screenPos = Camera.current.WorldToScreenPoint(aboveBotHeadPos);
                if (screenPos.z > 0)
                {
                    rect.x = screenPos.x * screenScale - rect.width / 2;
                    rect.y = Screen.height - (screenPos.y + rect.height / 2) * screenScale;

                    GUIStyle labelStyle = middleLabelStyle;
                    labelStyle.fontSize = 14;
                    float labelOpacity = 1;
                    float distanceToCenter = Vector3.Distance(screenPos, new Vector3(Screen.width, Screen.height, 0) / 2);

                    if (distanceToCenter < 100)
                    {
                        labelOpacity = distanceToCenter / 100;
                    }

                    if (ownPlayer.HandsController.IsAiming)
                    {
                        labelOpacity *= 0.5f;
                    }

                    if (pl.HealthController.IsAlive)
                    {
                        var maxHealth = pl.HealthController.GetBodyPartHealth(EBodyPart.Common).Maximum;
                        var currentHealth = pl.HealthController.GetBodyPartHealth(EBodyPart.Common).Current / maxHealth;
                        labelStyle.normal.textColor = new Color(2.0f * (1 - currentHealth), 2.0f * currentHealth, 0, labelOpacity);
                    }
                    else
                    {
                        labelStyle.normal.textColor = new Color(255, 0, 0, labelOpacity);
                    }

                    var distanceFromCamera = Math.Round(Vector3.Distance(Camera.current.gameObject.transform.position, pl.Position));
                    GUI.Label(rect, $"{pl.Profile.Nickname} {distanceFromCamera}m", labelStyle);
                }
            }
        }

        private void OnGUI_DrawPlayerEnemyTags(Rect rect)
        {
            if (SITConfig == null)
            {
                Logger.LogError("SITConfig is null?");
                return;
            }

            if (!SITConfig.showPlayerNameTagsForEnemies)
            {
                return;
            }

            if (FPSCamera.Instance == null)
                return;

            if (Players == null)
                return;

            if (PlayerUsers == null)
                return;

            if (Camera.current == null)
                return;

            if (!Singleton<GameWorld>.Instantiated)
                return;


            if (FPSCamera.Instance.SSAA != null && FPSCamera.Instance.SSAA.isActiveAndEnabled)
                screenScale = FPSCamera.Instance.SSAA.GetOutputWidth() / (float)FPSCamera.Instance.SSAA.GetInputWidth();

            var ownPlayer = Singleton<GameWorld>.Instance.MainPlayer;
            if (ownPlayer == null)
                return;

            foreach (var pl in PlayerBots)
            {
                if (pl == null)
                    continue;

                if (pl.HealthController == null)
                    continue;

                if (!pl.HealthController.IsAlive)
                    continue;

                Vector3 aboveBotHeadPos = pl.Position + Vector3.up * (pl.HealthController.IsAlive ? 1.5f : 0.5f);
                Vector3 screenPos = Camera.current.WorldToScreenPoint(aboveBotHeadPos);
                if (screenPos.z > 0)
                {
                    rect.x = screenPos.x * screenScale - rect.width / 2;
                    rect.y = Screen.height - screenPos.y * screenScale - 15;

                    var distanceFromCamera = Math.Round(Vector3.Distance(Camera.current.gameObject.transform.position, pl.Position));
                    GUI.Label(rect, $"{pl.Profile.Nickname} {distanceFromCamera}m", middleLabelStyle);
                    rect.y += 15;
                    GUI.Label(rect, $"X", middleLabelStyle);
                }
            }
        }

        private void OnGUI_DrawPlayerList(Rect rect)
        {
            if (!PluginConfigSettings.Instance.CoopSettings.SETTING_DEBUGShowPlayerList)
                return;

            rect.y += 15;

            if (Singleton<GameWorld>.Instance != null)
            {
                var players = Singleton<GameWorld>.Instance.RegisteredPlayers.ToList();
                players.AddRange(Players.Values);
                players = players.Distinct(x => x.ProfileId).ToList();

                rect.y += 15;
                GUI.Label(rect, $"Players [{players.Count}]:");
                rect.y += 15;
                foreach (var p in players)
                {
                    GUI.Label(rect, $"{p.Profile.Nickname}:{(p.IsAI ? "AI" : "Player")}:{(p.HealthController.IsAlive ? "Alive" : "Dead")}");
                    rect.y += 15;
                }

                players.Clear();
                players = null;
            }
        }
    }
}