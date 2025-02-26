﻿using EFT;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using System;
using System.Collections.Generic;
using static Ragfair;
using UnityEngine;
using Comfort.Common;

namespace StayInTarkov.Coop
{
    public class CoopPlayerStatisticsManager : AStatisticsManagerForPlayer
    {
        BepInEx.Logging.ManualLogSource Logger { get; set; }
        public CoopPlayerStatisticsManager(Profile profile) : base()
        {
            Logger = BepInEx.Logging.Logger.CreateLogSource(nameof(CoopPlayerStatisticsManager));
            Profile_0 = profile;
        }

        public new void Init(Profile profile, IHealthController healthController)
        {
            Profile_0 = profile;
            IHealthController_0 = healthController;
            NotificationManagerClass.DisplayMessageNotification($"{nameof(CoopPlayerStatisticsManager)}{nameof(Init)}");
            Logger.LogInfo($"{nameof(CoopPlayerStatisticsManager)}:{nameof(Init)}");

        }

        public override void BeginStatisticsSession()
        {
            Logger.LogInfo($"{nameof(CoopPlayerStatisticsManager)}:{nameof(BeginStatisticsSession)}");

            //Logger.LogDebug(Profile_0.ToJson());
            player_0.OnSpecialPlaceVisited += OnSpecialPlaceVisited;

            base.BeginStatisticsSession();
            //if (coroutine_0 != null)
            //{
            //    StaticManager.KillCoroutine(coroutine_0);
            //}
            ////coroutine_0 = StaticManager.BeginCoroutine(method_10());
            //ProfileStats eftStats = base.Profile_0.EftStats;
            ////eftStats.LastSessionDate = GClass1292.UtcNowUnixInt;
            ////dateTime_0 = GClass1292.UtcNow;
            //eftStats.Victims.Clear();
            //eftStats.Aggressor = null;
            //eftStats.SessionExperienceMult = 0f;
            //eftStats.ExperienceBonusMult = 0f;
            //eftStats.TotalSessionExperience = 0;
            ////hashSet_0.Clear();
            ////hashSet_1.Clear();
            ////method_20(player_0.Profile.Inventory);
            ////base.BeginStatisticsSession();
            ////dictionary_0 = method_19();
            ////player_0.OnPlayerDead += method_17;
            ////player_0.OnSpecialPlaceVisited += method_16;
            ////player_0.GClass2754_0.OnItemFound += method_13;
            ////action_1 = GClass2908.Instance.SubscribeOnEvent<InvokedEvent3>(method_14);
            //player_0.GClass2755_0.OnItemFound += (Item i) =>
            //{

            //};

            //if (eftStats.SessionCounters == null)
            //{
            //    eftStats.SessionCounters = new OverallAccountStats();
            //}
            //else
            //{
            //    eftStats.SessionCounters.Clear();
            //}
            //if (eftStats.DamageHistory == null)
            //{
            //    eftStats.DamageHistory = new DamageHistory();
            //}
            //else
            //{
            //    eftStats.DamageHistory.Clear();
            //}
            //StartDamageHistory();
        }

        private void OnSpecialPlaceVisited(string arg1, int arg2)
        {
            Logger.LogInfo($"{nameof(CoopPlayerStatisticsManager)}:{nameof(OnSpecialPlaceVisited)}");
            if(Profile_0 == null)
            {
                Logger.LogError($"${nameof(Profile_0)} is Null");
                return;
            }

            if (Profile_0.EftStats == null)
            {
                Logger.LogError($"${nameof(Profile_0.EftStats)} is Null");
                return;
            }

            if (Profile_0.EftStats.OverallCounters == null)
            {
                Logger.LogError($"${nameof(Profile_0.EftStats.OverallCounters)} is Null");
                return;
            }

            OverallAccountStats overallCounters = base.Profile_0.EftStats.OverallCounters;

            if (GClass1350_0 == null)
            {
                Logger.LogError($"${nameof(GClass1350_0)} is Null");
                return;
            }
        }

        public override void ExperienceGained(float experience)
        {
            //if (experience > 0)
            //{
            //    NotificationManagerClass.DisplayMessageNotification(experience.ToString());
            //}
            base.ExperienceGained(experience);
        }

        public override void ShowStatNotification(LocalizationKey localizationKey1, LocalizationKey localizationKey2, int value)
        {
            if (value > 0)
            {
                NotificationManagerClass.DisplayNotification(new AbstractNotification46(localizationKey1, localizationKey2, value));
            }
        }
    }
}
