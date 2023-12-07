using MelonLoader;
using MyBhapticsTactsuit;
using UnityEngine;
using HarmonyLib;
using Il2CppVertigo.AZS2.Client;
using static MelonLoader.MelonLogger;
using Il2CppVertigo.VRShooter;
using Il2CppVertigo.Interactables;
using Il2CppVertigo.ECS;
using Il2CppVertigo;
using System.Reflection.Metadata;
using System.Numerics;

[assembly: MelonInfo(typeof(ArizonaSunshine2_bhaptics.ArizonaSunshine2_bhaptics), "ArizonaSunshine2_bhaptics", "1.0.0", "Astien & Florian Fahrenberger")]
[assembly: MelonGame("Vertigo Games", "ArizonaSunshine2")]


namespace ArizonaSunshine2_bhaptics
{
    public class ArizonaSunshine2_bhaptics : MelonMod
    {
        public static TactsuitVR tactsuitVr = null!;

        public override void OnInitializeMelon()
        {
            tactsuitVr = new TactsuitVR();
            tactsuitVr.PlaybackHaptics("HeartBeat");
        }

        private static KeyValuePair<float, float> getAngleAndShift(Vector3 playerPosition, Vector3 hit, Quaternion playerRotation)
        {
            // bhaptics pattern starts in the front, then rotates to the left. 0° is front, 90° is left, 270° is right.
            // y is "up", z is "forward" in local coordinates
            Vector3 patternOrigin = new Vector3(0f, 0f, 1f);
            Vector3 hitPosition = hit - playerPosition;
            Quaternion myPlayerRotation = playerRotation;
            Vector3 playerDir = myPlayerRotation.eulerAngles;
            // get rid of the up/down component to analyze xz-rotation
            Vector3 flattenedHit = new Vector3(hitPosition.x, 0f, hitPosition.z);

            // get angle. .Net < 4.0 does not have a "SignedAngle" function...
            float hitAngle = Vector3.Angle(flattenedHit, patternOrigin);
            // check if cross product points up or down, to make signed angle myself
            Vector3 crossProduct = Vector3.Cross(flattenedHit, patternOrigin);
            if (crossProduct.y > 0f) { hitAngle *= -1f; }
            // relative to player direction
            float myRotation = hitAngle - playerDir.y;
            // switch directions (bhaptics angles are in mathematically negative direction)
            myRotation *= -1f;
            // convert signed angle into [0, 360] rotation
            if (myRotation < 0f) { myRotation = 360f + myRotation; }


            // up/down shift is in y-direction
            // in Shadow Legend, the torso Transform has y=0 at the neck,
            // and the torso ends at roughly -0.5 (that's in meters)
            // so cap the shift to [-0.5, 0]...
            float hitShift = hitPosition.y;
            //tactsuitVr.LOG("HitShift: " + hitShift);
            float upperBound = 0.5f;
            float lowerBound = -0.5f;
            if (hitShift > upperBound) { hitShift = 0.5f; }
            else if (hitShift < lowerBound) { hitShift = -0.5f; }
            // ...and then spread/shift it to [-0.5, 0.5]
            else { hitShift = (hitShift - lowerBound) / (upperBound - lowerBound) - 0.5f; }

            //tactsuitVr.LOG("Relative x-z-position: " + relativeHitDir.x.ToString() + " "  + relativeHitDir.z.ToString());
            //tactsuitVr.LOG("HitAngle: " + hitAngle.ToString());
            //tactsuitVr.LOG("HitShift: " + hitShift.ToString());

            // No tuple returns available in .NET < 4.0, so this is the easiest quickfix
            return new KeyValuePair<float, float>(myRotation, hitShift);
        }


        [HarmonyPatch(typeof(ClientPlayerHealthModule), "ApplyDamage", new Type[] { typeof(uint), typeof(uint), typeof(int), typeof(int), typeof(Il2CppSystem.Nullable<Vector3>), typeof(bool) })]
        public class bhaptics_Damage
        {
            [HarmonyPostfix]
            public static void Postfix(ClientPlayerHealthModule __instance, int hitBoneIndex, Il2CppSystem.Nullable<Vector3> hitOrigin, bool isKilled)
            {
                if (!__instance.identityModule.IsLocal) return;

                if (isKilled)
                {
                    tactsuitVr.StopThreads();
                    tactsuitVr.PlaybackHaptics("HeartBeatDeath");
                }
                if (__instance.HealthValue < __instance.MaxHealth * 0.25f && __instance.HealthValue > 0) tactsuitVr.StartHeartBeat();
                else tactsuitVr.StopHeartBeat();
                Vector3 hitPosition = hitOrigin.Value;
                Vector3 playerPosition = __instance.transformModule.HeadPosition;
                Quaternion playerRotation = __instance.transformModule.ChestRotation;
                var angleShift = getAngleAndShift(playerPosition, hitPosition, playerRotation);
                tactsuitVr.PlayBackHit("Slash", angleShift.Key, angleShift.Value);
                tactsuitVr.PlaybackHaptics("DamageVisor");
            }
        }

        [HarmonyPatch(typeof(ClientPlayerHealthModule), "ApplyHeal", new Type[] { typeof(uint), typeof(int) })]
        public class bhaptics_Heal
        {
            [HarmonyPostfix]
            public static void Postfix(ClientPlayerHealthModule __instance, uint healerId, int healAmountPrecise)
            {
                if (!__instance.identityModule.IsLocal) return;

                if (__instance.HealthValue >= __instance.MaxHealth * 0.25f) tactsuitVr.StopHeartBeat();
                tactsuitVr.PlaybackHaptics("Healing");
            }
        }

        [HarmonyPatch(typeof(ProjectileShootStrategyBehaviourData), "PlayShootHapticsForHand", new Type[] { typeof(AZS2Hand) })]
        public class bhaptics_Recoil
        {
            [HarmonyPostfix]
            public static void Postfix(ProjectileShootStrategyBehaviourData __instance, AZS2Hand hand)
            {
                if (__instance.shootStrategy.item.IsGrabbedLocally)
                {
                    string weapon = "Pistol";
                    if (__instance.shootStrategy.hasSpreadPattern) weapon = "Shotgun";
                    bool isRightHand = (hand.IsRightHand);
                    tactsuitVr.Recoil(weapon, isRightHand);
                }
            }
        }

        [HarmonyPatch(typeof(ClientExplosiveItemFeature), "Explode")]
        public class bhaptics_ClientExplosiveItemFeature
        {
            [HarmonyPostfix]
            public static void Postfix(ClientExplosiveItemFeature __instance)
            {
                tactsuitVr.PlaybackHaptics("ExplosionBelly");
                tactsuitVr.PlaybackHaptics("ExplosionFeet");
                tactsuitVr.PlaybackHaptics("ExplosionFace");
            }
        }

        [HarmonyPatch(typeof(HolsterHandleSlotBehaviour), "HandleOnInteractableRemovedEvent")]
        public class bhaptics_HandleOnInteractableRemovedEvent
        {
            [HarmonyPostfix]
            public static void Postfix(
                HolsterHandleSlotBehaviour __instance,
                InteractableSlot<InteractableHandle> slot,
                  InteractableHandle handle)
            {
                if (PawnUtils.IsLocalPawnSlot(handle.Slot))
                {
                    tactsuitVr.PlaybackHaptics("HolsterRemove" + __instance.slotType.ToString());
                }
            }
        }

        /*
         * As this is triggers when weapons swaps side and very often, I don't think it is relevant.
         * 
        [HarmonyPatch(typeof(HolsterHandleSlotBehaviour), "OnInteractableInserted")]
        public class bhaptics_OnInteractableInserted
        {
            [HarmonyPostfix]
            public static void Postfix(
                HolsterHandleSlotBehaviour __instance, 
                InteractableSlot<InteractableHandle> slot, 
                InteractableHandle handle)
            {
                tactsuitVr.LOG("Holster INSERT local ? " + PawnUtils.IsLocalPawnSlot(slot.Interactable.Slot));
                if (PawnUtils.IsLocalPawnSlot(handle.Slot))
                {
                    tactsuitVr.PlaybackHaptics("HolsterInsert" + __instance.slotType.ToString());
                }
            }
        }
        */

        [HarmonyPatch(typeof(AmmoPouchResourceViewBehaviour), "HandleOnResourceValueChanged")]
        public class bhaptics_HandleOnResourceValueChanged
        {
            [HarmonyPostfix]
            public static void Postfix(AmmoPouchResourceViewBehaviour __instance, uint resourceId, uint oldValue, uint newValue)
            {
                if (oldValue != newValue)
                {
                    tactsuitVr.PlaybackHaptics("HolsterInsertChest");
                }
            }
        }

        [HarmonyPatch(typeof(DogPetHandleBehaviour), "SetFullyAttached")]
        public class bhaptics_PetDoggo
        {
            [HarmonyPostfix]
            public static void Postfix(DogPetHandleBehaviour __instance, AZS2Hand hand)
            {
                Entity ent = PawnUtils.GetPawnForHand(hand);
                IInteractableSlot slot = PawnUtils.GetSlotForHand(ent, hand);

                if (PawnUtils.IsLocalHand(slot))
                {
                    if (hand.IsRightHand)
                    {
                        tactsuitVr.PlaybackHaptics("RecoilHands_R", 0.25f);
                        tactsuitVr.PlaybackHaptics("RecoilPistolVest_R", 0.25f);
                        tactsuitVr.PlaybackHaptics("RecoilArms_R", 0.25f);
                    }
                    else
                    {
                        tactsuitVr.PlaybackHaptics("RecoilHands_L", 0.25f);
                        tactsuitVr.PlaybackHaptics("RecoilPistolVest_L", 0.25f);
                        tactsuitVr.PlaybackHaptics("RecoilArms_L", 0.25f);
                    }
                }
            }
        }

        [HarmonyPatch(typeof(AmmoItemFeatureBehaviourData), "HandleOnGrabbedEvent")]
        public class bhaptics_HandleOnGrabbedEvent
        {
            [HarmonyPostfix]
            public static void Postfix(AmmoItemFeatureBehaviourData __instance, Entity pawn, Hand hand)
            {
                if (PawnUtils.IsLocalPawnSlot(PawnUtils.GetSlotForHand(pawn, hand)))
                {
                    if (hand.IsRightHand)
                    {
                        tactsuitVr.PlaybackHaptics("GrabItemVestRight");
                        tactsuitVr.PlaybackHaptics("GrabItemArmsRight");
                    }
                    else
                    {
                        tactsuitVr.PlaybackHaptics("GrabItemVestLeft");
                        tactsuitVr.PlaybackHaptics("GrabItemArmsLeft");
                    }
                }
            }
        }
    }
}