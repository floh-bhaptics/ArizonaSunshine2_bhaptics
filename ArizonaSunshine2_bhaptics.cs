using MelonLoader;
using MyBhapticsTactsuit;
using UnityEngine;
using HarmonyLib;
using Il2CppVertigo.AZS2.Client;
using static MelonLoader.MelonLogger;

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

        private static KeyValuePair<float, float> getAngleAndShift(Transform player, Vector3 hit, Quaternion playerRotation)
        {
            // bhaptics pattern starts in the front, then rotates to the left. 0° is front, 90° is left, 270° is right.
            // y is "up", z is "forward" in local coordinates
            Vector3 patternOrigin = new Vector3(0f, 0f, 1f);
            Vector3 hitPosition = hit - player.position;
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
        /*
        Vector3 hitPosition = zombie.Position;
        Transform playerPosition = __instance.Transform;
        Quaternion playerRotation = __instance.BaseRotation;
        Quaternion headRotation = __instance.HeadRotation;
        var angleShift = getAngleAndShift(playerPosition, hitPosition, playerRotation * headRotation);
                if (zombie.Locomotion.IsCrawling)
                {
                    tactsuitVr.PlaybackHaptics("ExplosionFeet");
                    tactsuitVr.PlayBackHit("Slash", angleShift.Key, -0.5f);
                    return;
                }
        tactsuitVr.PlayBackHit("Slash", angleShift.Key, angleShift.Value);
        */


        [HarmonyPatch(typeof(ProjectileShootStrategyBehaviourData), "PlayShootHapticsForHand", new Type[] { typeof(AZS2Hand) })]
        public class bhaptics_Recoil
        {
            [HarmonyPostfix]
            public static void Postfix(ProjectileShootStrategyBehaviourData __instance, AZS2Hand hand)
            {
                string weapon = "Pistol";
                if (__instance.shootStrategy.projectilesPerBurst > 1) weapon = "Shotgun";
                bool isRightHand = (hand.IsRightHand);
                tactsuitVr.Recoil(weapon, isRightHand);
            }
        }


    }
}