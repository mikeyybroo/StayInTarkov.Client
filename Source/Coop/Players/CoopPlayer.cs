﻿using BepInEx.Logging;
using Comfort.Common;
using EFT;
using EFT.HealthSystem;
using EFT.Interactive;
using EFT.InventoryLogic;
using StayInTarkov.Coop.Components.CoopGameComponents;
using StayInTarkov.Coop.Controllers;
using StayInTarkov.Coop.Controllers.CoopInventory;
using StayInTarkov.Coop.Matchmaker;
using StayInTarkov.Coop.NetworkPacket;
using StayInTarkov.Coop.Player;
using StayInTarkov.Coop.Player.FirearmControllerPatches;
using StayInTarkov.Coop.Player.Proceed;
using StayInTarkov.Coop.Web;
using StayInTarkov.Core.Player;
using StayInTarkov.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

namespace StayInTarkov.Coop.Players
{
    public class CoopPlayer : LocalPlayer
    {
        protected ManualLogSource BepInLogger { get; set; }

        public static async Task<LocalPlayer>
            Create(int playerId
            , Vector3 position
            , Quaternion rotation
            , string layerName
            , string prefix
            , EPointOfView pointOfView
            , Profile profile
            , bool aiControl
            , EUpdateQueue updateQueue
            , EUpdateMode armsUpdateMode
            , EUpdateMode bodyUpdateMode
            , CharacterControllerSpawner.Mode characterControllerMode
            , Func<float> getSensitivity, Func<float> getAimingSensitivity
            , IFilterCustomization filter
            , AbstractQuestController questController = null
            , AbstractAchievementsController achievementsController = null
            , bool isYourPlayer = false
            , bool isClientDrone = false)
        {
            CoopPlayer player = null;

            if (isClientDrone)
            {
                player = EFT.Player.Create<CoopPlayerClient>(
                    ResourceBundleConstants.PLAYER_BUNDLE_NAME
                    , playerId
                    , position
                    , updateQueue
                    , armsUpdateMode
                    , bodyUpdateMode
                    , characterControllerMode
                    , getSensitivity
                    , getAimingSensitivity
                    , prefix
                    , aiControl);
                player.name = profile.Nickname;
            }
            else
            {
                // Aki won't allow this to happen here :(
                //var oldEquipmentId = profile.Inventory.Equipment.Id.ToCharArray().ToString();
                //var newEquipmentId = new MongoID(true);
                //profile.Inventory.Equipment.Id = newEquipmentId;
                //foreach (var eq in profile.Inventory.GetAllEquipmentItems())
                //{
                //    if (eq.Parent != null && eq.Parent.Item != null && eq.Parent.Item.Id == oldEquipmentId)
                //    {
                //        eq.Parent.Item.Id = newEquipmentId;
                //    }
                //}

                player = Create<CoopPlayer>(
                    ResourceBundleConstants.PLAYER_BUNDLE_NAME
                    , playerId
                    , position
                    , updateQueue
                    , armsUpdateMode
                    , bodyUpdateMode
                    , characterControllerMode
                    , getSensitivity
                    , getAimingSensitivity
                    , prefix
                    , aiControl);
               
            }
            player.IsYourPlayer = isYourPlayer;
            player.BepInLogger = BepInEx.Logging.Logger.CreateLogSource("CoopPlayer");

            InventoryController inventoryController = isYourPlayer && !isClientDrone
                ? new CoopInventoryController(player, profile, true)
                : new CoopInventoryControllerForClientDrone(player, profile, true);

            // Quest Controller from 0.13
            //if (questController == null && isYourPlayer)
            //{
            //    questController = new QuestController(profile, inventoryController, StayInTarkovHelperConstants.BackEndSession, fromServer: true);
            //    questController.Run();
            //}

            // Quest Controller instantiate
            if (isYourPlayer)
            {
                questController = PlayerFactory.GetQuestController(profile, inventoryController);
                player.BepInLogger.LogDebug($"{nameof(questController)} Instantiated");
            }



            // Achievement Controller instantiate
            if (isYourPlayer)
            {
                achievementsController = PlayerFactory.GetAchievementController(profile, inventoryController);
                player.BepInLogger.LogDebug($"{nameof(achievementsController)} Instantiated");
            }

            IStatisticsManager statsManager = isYourPlayer ? PlayerFactory.GetStatisticsManager(player) : new NullStatisticsManager();
            player.BepInLogger.LogDebug($"{nameof(statsManager)} Instantiated with type {statsManager.GetType()}");

            await player
                .Init(rotation, layerName, pointOfView, profile, inventoryController
                , new CoopHealthController(profile.Health, player, inventoryController, profile.Skills, aiControl)
                //, new AbstractStatisticsManager1()
                , statsManager
                , questController
                , achievementsController
                , filter
                , aiControl || isClientDrone ? EVoipState.NotAvailable : EVoipState.Available
                , aiControl
                , async: false);

            //statsManager.Init(player);

            player._handsController = EmptyHandsController.smethod_5<EmptyHandsController>(player);
            player._handsController.Spawn(1f, delegate
            {
            });
            player.AIData = new AIData(null, player);
            player.AggressorFound = false;
            player._animators[0].enabled = true;
            if (!player.IsYourPlayer)
            {
                player._armsUpdateQueue = EUpdateQueue.Update;
            }

            // If this is a Client Drone add Player Replicated Component
            if (isClientDrone)
            {
                var prc = player.GetOrAddComponent<PlayerReplicatedComponent>();
                prc.IsClientDrone = true;
            }

            return player;
        }

        /// <summary>
        /// A way to block the same Damage Info being run multiple times on this Character
        /// TODO: Fix this at source. Something is replicating the same Damage multiple times!
        /// </summary>
        private HashSet<DamageInfo> PreviousDamageInfos { get; } = new();
        private HashSet<string> PreviousSentDamageInfoPackets { get; } = new();
        private HashSet<string> PreviousReceivedDamageInfoPackets { get; } = new();
        public bool IsFriendlyBot { get; internal set; }

        public override void ApplyDamageInfo(DamageInfo damageInfo, EBodyPart bodyPartType, EBodyPartColliderType colliderType, float absorbed)
        {
            // Quick check?
            if (PreviousDamageInfos.Any(x =>
                x.Damage == damageInfo.Damage
                && x.SourceId == damageInfo.SourceId
                && x.Weapon != null && damageInfo.Weapon != null && x.Weapon.Id == damageInfo.Weapon.Id
                && x.Player != null && damageInfo.Player != null && x.Player == damageInfo.Player
                ))
                return;

            PreviousDamageInfos.Add(damageInfo);

            //BepInLogger.LogInfo($"{nameof(ApplyDamageInfo)}:{this.ProfileId}:{DateTime.Now.ToString("T")}");
            //base.ApplyDamageInfo(damageInfo, bodyPartType, absorbed, headSegment);

            if (CoopGameComponent.TryGetCoopGameComponent(out var coopGameComponent))
            {
                // If we are not using the Client Side Damage, then only run this on the server
                if (SITMatchmaking.IsServer && !coopGameComponent.SITConfig.useClientSideDamageModel)
                    SendDamageToAllClients(damageInfo, bodyPartType, colliderType, absorbed);
                else
                    SendDamageToAllClients(damageInfo, bodyPartType, colliderType, absorbed);
            }
        }

        private void SendDamageToAllClients(DamageInfo damageInfo, EBodyPart bodyPartType, EBodyPartColliderType bodyPartColliderType, float absorbed)
        {
            Dictionary<string, object> packet = new();
            damageInfo.HitCollider = null;
            damageInfo.HittedBallisticCollider = null;
            Dictionary<string, string> playerDict = new();
            if (damageInfo.Player != null)
            {
                playerDict.Add("d.p.aid", damageInfo.Player.iPlayer.Profile.AccountId);
                playerDict.Add("d.p.id", damageInfo.Player.iPlayer.ProfileId);
            }

            damageInfo.Player = null;
            Dictionary<string, string> weaponDict = new();

            if (damageInfo.Weapon != null)
            {
                packet.Add("d.w.tpl", damageInfo.Weapon.TemplateId);
                packet.Add("d.w.id", damageInfo.Weapon.Id);
            }
            damageInfo.Weapon = null;

            packet.Add("d", damageInfo.SITToJson());
            packet.Add("d.p", playerDict);
            packet.Add("d.w", weaponDict);
            packet.Add("bpt", bodyPartType.ToString());
            packet.Add("bpct", bodyPartColliderType.ToString());
            packet.Add("ab", absorbed.ToString());
            packet.Add("m", "ApplyDamageInfo");

            // -----------------------------------------------------------
            // An attempt to stop the same packet being sent multiple times
            if (PreviousSentDamageInfoPackets.Contains(packet.ToJson()))
                return;

            PreviousSentDamageInfoPackets.Add(packet.ToJson());
            // -----------------------------------------------------------

            AkiBackendCommunicationCoop.PostLocalPlayerData(this, packet);
        }

        public void ReceiveDamageFromServer(Dictionary<string, object> dict)
        {
            StartCoroutine(ReceiveDamageFromServerCR(dict));
        }

        public IEnumerator ReceiveDamageFromServerCR(Dictionary<string, object> dict)
        {
            if (PreviousReceivedDamageInfoPackets.Contains(dict.ToJson()))
                yield break;

            PreviousReceivedDamageInfoPackets.Add(dict.ToJson());

            //BepInLogger.LogDebug("ReceiveDamageFromServer");
            //BepInLogger.LogDebug(dict.ToJson());

            Enum.TryParse<EBodyPart>(dict["bpt"].ToString(), out var bodyPartType);
            Enum.TryParse<EBodyPartColliderType>(dict["bpct"].ToString(), out var bodyPartColliderType);
            var absorbed = float.Parse(dict["ab"].ToString());

            var damageInfo = Player_ApplyShot_Patch.BuildDamageInfoFromPacket(dict);
            damageInfo.HitCollider = Player_ApplyShot_Patch.GetCollider(this, damageInfo.BodyPartColliderType);

            if (damageInfo.DamageType == EDamageType.Bullet && IsYourPlayer)
            {
                float handsShake = 0.05f;
                float cameraShake = 0.4f;
                float absorbedDamage = absorbed + damageInfo.Damage;

                switch (bodyPartType)
                {
                    case EBodyPart.Head:
                        handsShake = 0.1f;
                        cameraShake = 1.3f;
                        break;
                    case EBodyPart.LeftArm:
                    case EBodyPart.RightArm:
                        handsShake = 0.15f;
                        cameraShake = 0.5f;
                        break;
                    case EBodyPart.LeftLeg:
                    case EBodyPart.RightLeg:
                        cameraShake = 0.3f;
                        break;
                }

                ProceduralWeaponAnimation.ForceReact.AddForce(Mathf.Sqrt(absorbedDamage) / 10, handsShake, cameraShake);
                if (FPSCamera.Instance.EffectsController.TryGetComponent(out FastBlur fastBlur))
                {
                    fastBlur.enabled = true;
                    fastBlur.Hit(MovementContext.PhysicalConditionIs(EPhysicalCondition.OnPainkillers) ? absorbedDamage : bodyPartType == EBodyPart.Head ? absorbedDamage * 6 : absorbedDamage * 3);
                }
            }

            base.ApplyDamageInfo(damageInfo, bodyPartType, bodyPartColliderType, absorbed);
            //base.ShotReactions(damageInfo, bodyPartType);

            yield break;

        }

        public override void OnSkillLevelChanged(AbstractSkill skill)
        {
            //base.OnSkillLevelChanged(skill);
        }

        public override void OnWeaponMastered(MasterSkill masterSkill)
        {
            //base.OnWeaponMastered(masterSkill);
        }

        public override void Heal(EBodyPart bodyPart, float value)
        {
            base.Heal(bodyPart, value);
        }

        //public override PlayerHitInfo ApplyShot(DamageInfo damageInfo, EBodyPart bodyPartType, ShotId shotId)
        //{
        //    return base.ApplyShot(damageInfo, bodyPartType, shotId);
        //}

        //public void ReceiveApplyShotFromServer(Dictionary<string, object> dict)
        //{
        //    Logger.LogDebug("ReceiveApplyShotFromServer");
        //    Enum.TryParse<EBodyPart>(dict["bpt"].ToString(), out var bodyPartType);
        //    Enum.TryParse<EHeadSegment>(dict["hs"].ToString(), out var headSegment);
        //    var absorbed = float.Parse(dict["ab"].ToString());

        //    var damageInfo = Player_ApplyShot_Patch.BuildDamageInfoFromPacket(dict);
        //    damageInfo.HitCollider = Player_ApplyShot_Patch.GetCollider(this, damageInfo.BodyPartColliderType);

        //    var shotId = new ShotId();
        //    if (dict.ContainsKey("ammoid") && dict["ammoid"] != null)
        //    {
        //        shotId = new ShotId(dict["ammoid"].ToString(), 1);
        //    }

        //    base.ApplyShot(damageInfo, bodyPartType, shotId);
        //}

        public override Corpse CreateCorpse()
        {
            return base.CreateCorpse();
        }

        public override void OnItemAddedOrRemoved(Item item, ItemAddress location, bool added)
        {
            base.OnItemAddedOrRemoved(item, location, added);
        }

        private Vector2 LastRotationSent = Vector2.zero;

        public override void Rotate(Vector2 deltaRotation, bool ignoreClamp = false)
        {
            if (
                FirearmController_SetTriggerPressed_Patch.LastPress.ContainsKey(ProfileId)
                && FirearmController_SetTriggerPressed_Patch.LastPress[ProfileId] == true
                && LastRotationSent != Rotation
                )
            {
                Dictionary<string, object> rotationPacket = new Dictionary<string, object>();
                rotationPacket.Add("m", "PlayerRotate");
                rotationPacket.Add("x", Rotation.x);
                rotationPacket.Add("y", Rotation.y);
                AkiBackendCommunicationCoop.PostLocalPlayerData(this, rotationPacket);
                LastRotationSent = Rotation;
            }

            base.Rotate(deltaRotation, ignoreClamp);
        }

        public void ReceiveRotate(Vector2 rotation, bool ignoreClamp = false)
        {
            var prc = GetComponent<PlayerReplicatedComponent>();
            if (prc == null || !prc.IsClientDrone)
                return;

            Rotation = rotation;
            //prc.ReplicatedRotation = rotation; 

        }


        //public override void Move(Vector2 direction)
        //{
        //    var prc = GetComponent<PlayerReplicatedComponent>();
        //    if (prc == null)
        //        return;

        //    base.Move(direction);

        //    if (prc.IsClientDrone)
        //        return;


        //}

        public override void Proceed(FoodDrink foodDrink, float amount, Callback<IMedsController> callback, int animationVariant, bool scheduled = true)
        {
            base.Proceed(foodDrink, amount, callback, animationVariant, scheduled);

            //PlayerProceedFoodDrinkPacket playerProceedFoodDrinkPacket = new(this.ProfileId, foodDrink.Id, foodDrink.TemplateId, amount, animationVariant, scheduled, "ProceedFoodDrink");
            //GameClient.SendDataToServer(playerProceedFoodDrinkPacket.Serialize());

            //BepInLogger.LogInfo($"{nameof(Proceed)} FoodDrink");
            //BepInLogger.LogInfo($"{playerProceedFoodDrinkPacket.ToJson()}");
        }

        public override void OnPhraseTold(EPhraseTrigger @event, TaggedClip clip, TagBank bank, Speaker speaker)
        {
            base.OnPhraseTold(@event, clip, bank, speaker);

            if (IsYourPlayer)
            {
                Dictionary<string, object> packet = new()
                {
                    { "event", @event.ToString() },
                    { "index", clip.NetId },
                    { "m", "Say" }
                };
                AkiBackendCommunicationCoop.PostLocalPlayerData(this, packet);
            }
        }

        public void ReceiveSay(EPhraseTrigger trigger, int index)
        {
            BepInLogger.LogDebug($"{nameof(ReceiveSay)}({trigger},{index})");

            var prc = GetComponent<PlayerReplicatedComponent>();
            if (prc == null || !prc.IsClientDrone)
                return;

            Speaker.PlayDirect(trigger, index);
        }

        public override void OnDestroy()
        {
            BepInLogger.LogDebug("OnDestroy()");

            base.OnDestroy();
        }

        public virtual void ReceivePlayerStatePacket(PlayerStatePacket playerStatePacket)
        {
           
        }

        protected virtual void Interpolate()
        {

        }

        public override void UpdateTick()
        {
            base.UpdateTick();
        }

    }
}
