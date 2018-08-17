using Pangolin;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using VectorHelpers;
using static ModuleAnimateGeneric;

namespace Pangolin
{
    [KSPAddon(KSPAddon.Startup.Flight, false /*once*/)]
    public class CollisionManagerExtended : MonoBehaviour
    {
        private static void printf(string format, params object[] a)
        {
            int i = 0;
            string s = (format is string) ? System.Text.RegularExpressions.Regex.Replace((string)format, "%[sdi%]",
              match => match.Value == "%%" ? "%" : i < a.Length ? (a[i++] != null ? a[i - 1].ToString() : "null") : match.Value) : format.ToString();
            Debug.Log("CollisionManagerExtended: " + s);
        }

        class VesselList
        {
            // A nested list of vessels, parts and colliders
            public class VesselInfo
            {
                public Vessel vessel;
                public class PartInfo
                {
                    public Part part;
                    public int collisionMode;
                    public List<Collider> colliderList = new List<Collider>();
                }
                public List<PartInfo> partList = new List<PartInfo>();
            }
            public List<VesselInfo> vesselList = new List<VesselInfo>();
        }

        // Gather up info on all colliders in physics range and order them
        private VesselList GetAllVesselColliders()
        {
            VesselList vesselList = new VesselList();
            vesselList.vesselList.Clear();

            // Not sure why we want to do this...
            bool hasEVA = false;
            foreach (Vessel vessel in FlightGlobals.VesselsLoaded)
                if (vessel.isEVA)
                    hasEVA = true;

            // Iterate through vessels in physics range, parts, and colliders
            foreach (Vessel vessel in FlightGlobals.VesselsLoaded)
            {
                VesselList.VesselInfo vesselInfo = new VesselList.VesselInfo();
                vesselInfo.vessel = vessel;
                vesselList.vesselList.Add(vesselInfo);

                foreach (Part part in vessel.parts)
                {
                    VesselList.VesselInfo.PartInfo partInfo = new VesselList.VesselInfo.PartInfo();
                    partInfo.part = part;
                    vesselInfo.partList.Add(partInfo);

                    // Check if there is an MechanicsToolkit part module in this part
                    partInfo.collisionMode = 0;

                    foreach (PartModule partModule in part.Modules)
                        if (partModule.moduleName == "MechanicsToolkit")
                        {
                            MechanicsToolkit mechanicsToolkit = (MechanicsToolkit)partModule;
                            partInfo.collisionMode = mechanicsToolkit.collisionMode;
                        }

                    Collider[] componentsInChildren = part.partTransform.GetComponentsInChildren<Collider>(hasEVA);
                    if (componentsInChildren != null)
                    {
                        for (int k = 0; k < componentsInChildren.Length; k++)
                        {
                            Collider collider = componentsInChildren[k];

                            bool addCollider = (collider.gameObject.activeInHierarchy && collider.enabled)
                             || ((hasEVA && (collider.tag == "Ladder" || collider.tag == "Airlock")));

                            /*
                            printf("Collider: %s.%s, %s, %s, %s, %s -> %s",
                                part.name,
                                collider.gameObject.name,
                                collider.gameObject.activeInHierarchy,
                                collider.enabled,
                                hasEVA,
                                collider.tag,
                                addCollider);
                            */

                            if (addCollider)
                            {
                                partInfo.colliderList.Add(collider);
                            }
                        }
                    }
                }
            }
            return vesselList;
        }

        private void OnCollisionIgnoreUpdate()
        {
            VesselList vesselList = GetAllVesselColliders();

            // Iterate through the created list of vessels/parts/colliders and enable collisions between them
            for (int v1 = 0; v1 < vesselList.vesselList.Count; v1++)
            {
                for (int v2 = v1; v2 < vesselList.vesselList.Count; v2++)
                {
                    VesselList.VesselInfo vessel1 = vesselList.vesselList[v1];
                    VesselList.VesselInfo vessel2 = vesselList.vesselList[v2];

                    for (int p1 = 0; p1 < vessel1.partList.Count; p1++)
                    {
                        VesselList.VesselInfo.PartInfo part1 = vessel1.partList[p1];

                        for (int p2 = p1 + 1; p2 < vessel2.partList.Count; p2++)
                        {
                            VesselList.VesselInfo.PartInfo part2 = vessel2.partList[p2];

                            bool ignore = false;

                            if (vessel1 == vessel2)
                            {
                                /*
                                bool adjacent = false;
                                // Enable collisions for adjacent parts?
                                if (part1.part.parent == part2.part ||
                                    part2.part.parent == part1.part)
                                    adjacent = true;
                                */
                                // A matrix enabling collisions depending on the settings of the two parts
                                bool[][] active = new bool[][]
                                {
                                    /*
                                    new bool[] { false,     false,      !adjacent,  true},
                                    new bool[] { false,     true,       !adjacent,  true },
                                    new bool[] { !adjacent, !adjacent,  !adjacent,  true },
                                    new bool[] { true,      true,       true,       true },
                                    */
                                    new bool[] { false,     false,      true },
                                    new bool[] { false,     true,       true },
                                    new bool[] { true,      true,       true },
                                };

                                if (!active[(int)part1.collisionMode][(int)part2.collisionMode])
                                    ignore = true;

                                /*
                                printf("ignore: %s/%s, %s, %s, %s->%s,%s",
                                    part1.part.name,
                                    part2.part.name,
                                    part1.collisionMode,
                                    part2.collisionMode,
                                    adjacent,
                                    active[(int)part1.collisionMode][(int)part2.collisionMode],
                                    ignore);
                                */
                            }

                            for (int c1 = 0; c1 < part1.colliderList.Count; c1++)
                            {
                                for (int c2 = 0; c2 < part2.colliderList.Count; c2++)
                                {
                                    Collider collider1 = part1.colliderList[c1];
                                    Collider collider2 = part2.colliderList[c2];

                                    Physics.IgnoreCollision(collider1, collider2, ignore);

                                    /*
                                    printf("IgnoreCollision: %s.%s (%s %s)/%s.%s (%s %s)",
                                        collider1.attachedRigidbody ? collider1.attachedRigidbody.name : null,
                                        collider1.name,
                                        collider1.gameObject.name,
                                        collider1.gameObject.layer,
                                        collider2.attachedRigidbody ? collider2.attachedRigidbody.name : null,
                                        collider2.name,
                                        collider2.gameObject.name,
                                        collider2.gameObject.layer);
                                    */
                                }
                            }
                        }
                    }
                }
            }
            /*
            printf(" Physics.GetIgnoreLayerCollision(0,0): %s",
                 Physics.GetIgnoreLayerCollision(0, 0));
            */
        }

        private void Start()
        {
            GameEvents.OnCollisionIgnoreUpdate = new EventVoid("OnCollisionIgnoreUpdate");
            GameEvents.OnCollisionIgnoreUpdate.Add(OnCollisionIgnoreUpdate);
        }
    }

    public class MechanicsToolkit : PartModule
    {
        private static void printf(string format, params object[] a)
        {
            int i = 0;
            string s = (format is string) ? System.Text.RegularExpressions.Regex.Replace((string)format, "%[sdi%]",
              match => match.Value == "%%" ? "%" : i < a.Length ? (a[i++] != null ? a[i - 1].ToString() : "null") : match.Value) : format.ToString();
            Debug.Log("MechanicsToolkit: " + s);
        }

        [KSPField(guiActiveEditor = true, guiActive = true, isPersistant = true, guiName = "Collisions")]
        [UI_ChooseOption(controlEnabled = true,
            /*
            options = new[] { "None", "Only active", "All but adjacent", "All parts", },
            display = new[] { "None", "Only active", "All but adjacent", "All parts", }
            */
            options = new[] { "None", "Only active", "All parts", },
            display = new[] { "None", "Only active", "All parts", }
        )]
        public int collisionMode = 0;

        [KSPField(isPersistant = true, guiName = "Animation stopper", guiActiveEditor = true, guiActive = true, advancedTweakable = true)]
        [UI_Toggle(disabledText = "Inactive", enabledText = "Active")]
        public bool animationStopper = false;

        [KSPField(isPersistant = true, guiName = "Unlock X", guiActiveEditor = true, guiActive = true, advancedTweakable = true)]
        [UI_Toggle(disabledText = "Locked", enabledText = "Unlocked")]
        public bool unlockX = false;

        [KSPField(isPersistant = true, guiName = "Unlock Y", guiActiveEditor = true, guiActive = true, advancedTweakable = true)]
        [UI_Toggle(disabledText = "Locked", enabledText = "Unlocked")]
        public bool unlockY = false;

        [KSPField(isPersistant = true, guiName = "Unlock Z", guiActiveEditor = true, guiActive = true, advancedTweakable = true)]
        [UI_Toggle(disabledText = "Locked", enabledText = "Unlocked")]
        public bool unlockZ = false;

        [KSPField(isPersistant = true, guiName = "Unlock angular X", guiActiveEditor = true, guiActive = true, advancedTweakable = true)]
        [UI_Toggle(disabledText = "Locked", enabledText = "Unlocked")]
        public bool unlockAngularX = false;

        [KSPField(isPersistant = true, guiName = "Unlock angular Y", guiActiveEditor = true, guiActive = true, advancedTweakable = true)]
        [UI_Toggle(disabledText = "Locked", enabledText = "Unlocked")]
        public bool unlockAngularY = false;

        [KSPField(isPersistant = true, guiName = "Unlock angular Z", guiActiveEditor = true, guiActive = true, advancedTweakable = true)]
        [UI_Toggle(disabledText = "Locked", enabledText = "Unlocked")]
        public bool unlockAngularZ = false;

        class ConfigurableJointInfo
        {
            public JointDrive xDrive;
            public JointDrive yDrive;
            public JointDrive zDrive;
            public ConfigurableJointMotion xMotion;
            public ConfigurableJointMotion yMotion;
            public ConfigurableJointMotion zMotion;
            public JointDrive angularXDrive;
            public JointDrive angularYZDrive;
            public ConfigurableJointMotion angularXMotion;
            public ConfigurableJointMotion angularYMotion;
            public ConfigurableJointMotion angularZMotion;
        }
        ConfigurableJointInfo originalJoint;

        JointDrive jointDriveUnlocked;

        ModuleAnimateGeneric moduleAnimateGeneric;
        PartModule animatedAttachment;

        private void InitCollisionMode()
        {
            // Add a callback when changing collision mode
            Fields[nameof(collisionMode)].uiControlFlight.onFieldChanged = OnCollisionModeChanged;
            Fields[nameof(collisionMode)].guiActive = (part.physicalSignificance == Part.PhysicalSignificance.FULL);
        }

        class ColliderInfo
        {
            public Collider collider;
            public PosRot posRot;
        }
        List<ColliderInfo> colliderInfos;

        PartModule GetPartModule(string name)
        {
            // Check if this part has any generic animations
            foreach (PartModule partModule in part.Modules)
                if (partModule.moduleName == name)
                    return partModule;
            return null;
        }

        private void InitAnimationStopper()
        {
            // Check if this part has any generic animations
            moduleAnimateGeneric = (ModuleAnimateGeneric) GetPartModule("ModuleAnimateGeneric");
            animatedAttachment = GetPartModule("AnimatedAttachment");

            printf("InitAnimationStopper: %s",
                animatedAttachment != null ? animatedAttachment.name : "null");

            if (!moduleAnimateGeneric)
            {
                Fields[nameof(animationStopper)].guiActive = false;
                Fields[nameof(animationStopper)].guiActiveEditor = false;
                return;
            }

            // Save a list of all the colliders to keep track of the position 
            colliderInfos = new List<ColliderInfo>();
            foreach (Collider collider in part.GetPartColliders())
            {
                ColliderInfo colliderInfo = new ColliderInfo();
                colliderInfo.collider = collider;
                colliderInfo.posRot = PosRot.GetPosRot(collider.transform, part);
                colliderInfos.Add(colliderInfo);
            }
        }

        private void InitJointUnlocker()
        {
            if (part.attachJoint)
            {
                // Hold on copying joint settings until AnimatedAttachment had a chance to initalize it, if installed
                if (animatedAttachment)
                    if (part.attachJoint.Joint.name != "AnimatedAttachment")
                        return;

                printf("Copying joint");

                // Add a callback for changes int the joint setting
                Fields[nameof(unlockX)].uiControlFlight.onFieldChanged = OnJointChanged;
                Fields[nameof(unlockY)].uiControlFlight.onFieldChanged = OnJointChanged;
                Fields[nameof(unlockZ)].uiControlFlight.onFieldChanged = OnJointChanged;
                Fields[nameof(unlockAngularX)].uiControlFlight.onFieldChanged = OnJointChanged;
                Fields[nameof(unlockAngularY)].uiControlFlight.onFieldChanged = OnJointChanged;
                Fields[nameof(unlockAngularZ)].uiControlFlight.onFieldChanged = OnJointChanged;

                Fields[nameof(unlockX)].guiActive = true;
                Fields[nameof(unlockY)].guiActive = true;
                Fields[nameof(unlockZ)].guiActive = true;
                Fields[nameof(unlockAngularX)].guiActive = true;
                Fields[nameof(unlockAngularY)].guiActive = true;
                Fields[nameof(unlockAngularZ)].guiActive = true;

                // Create a new joint with settings from the cfg file or user selection
                ConfigurableJoint joint = part.attachJoint.Joint;
                originalJoint = new ConfigurableJointInfo();

                printf("InitJointUnlocker: %s %s %s %s",
                    joint.xDrive,
                    joint.xMotion,
                    originalJoint.xDrive.positionSpring,
                    originalJoint.xMotion);

                originalJoint.xDrive = joint.xDrive;
                originalJoint.yDrive = joint.yDrive;
                originalJoint.zDrive = joint.zDrive;
                originalJoint.angularXDrive = joint.angularXDrive;
                originalJoint.angularYZDrive = part.attachJoint.Joint.angularYZDrive;

                originalJoint.xMotion = joint.xMotion;
                originalJoint.yMotion = joint.yMotion;
                originalJoint.zMotion = joint.zMotion;

                originalJoint.angularXMotion = joint.angularXMotion;
                originalJoint.angularYMotion = joint.angularYMotion;
                originalJoint.angularZMotion = joint.angularZMotion;

                // Create an empty joint drive for unlocked parts
                jointDriveUnlocked = new JointDrive();
                jointDriveUnlocked.maximumForce = 0;
                jointDriveUnlocked.positionDamper = 0;
                jointDriveUnlocked.positionSpring = 0;
            }
            else
            {
                // Can we hide the unlock options for physicsless parts?
                Fields[nameof(unlockX)].guiActive = false;
                Fields[nameof(unlockY)].guiActive = false;
                Fields[nameof(unlockZ)].guiActive = false;
                Fields[nameof(unlockAngularX)].guiActive = false;
                Fields[nameof(unlockAngularY)].guiActive = false;
                Fields[nameof(unlockAngularZ)].guiActive = false;
            }
        }

        public override void OnStartFinished(PartModule.StartState state)
        {
            base.OnStartFinished(state);

            printf("OnStartFinished");

            InitCollisionMode();
            InitAnimationStopper();
        }

        void FixedUpdate()
        {
            if (originalJoint == null)
                InitJointUnlocker();
        }

        ColliderInfo GetColliderInfo(Collider collider)
        {
            foreach (ColliderInfo colliderInfo in colliderInfos)
                if (colliderInfo.collider == collider)
                    return colliderInfo;
            return null;
        }

        void OnCollisionStay(Collision collisionInfo)
        {
            //if (collisionInfo.rigidbody != part.Rigidbody || !moduleAnimateGeneric)
            if (!moduleAnimateGeneric || !animationStopper)
                return;

            if (!moduleAnimateGeneric.IsMoving())
                return;

            foreach (ContactPoint contact in collisionInfo.contacts)
            {
                ColliderInfo thisColliderInfo = GetColliderInfo(contact.thisCollider);

                PosRot currentPosRot = PosRot.GetPosRot(thisColliderInfo.collider.transform, part);
                Vector3 movement = currentPosRot.position - thisColliderInfo.posRot.position;
                double angle = Quaternion.Angle(currentPosRot.rotation, thisColliderInfo.posRot.rotation);

                thisColliderInfo.posRot = currentPosRot;

                // Set a minimum level
                if (movement.IsSmallerThan(0.1f) && angle < 0.1)
                    continue;

                printf("Animation stopped by a collision between %s and %s (movement %s, angle %s)",
                    thisColliderInfo.collider.name,
                    collisionInfo.collider.name,
                    movement,
                    angle);

                float time = moduleAnimateGeneric.animTime;
                if (moduleAnimateGeneric.revClampPercent)
                    time = 1 - time;
                moduleAnimateGeneric.deployPercent = 100.0f * time;
                moduleAnimateGeneric.allowDeployLimit = true;
            }
        }

        private void OnCollisionModeChanged(BaseField field, object obj)
        {
            // Force a reevaluation of the collider interactions
            GameEvents.OnCollisionIgnoreUpdate.Fire();
        }

        private void OnJointChanged(BaseField field, object obj)
        {
            printf("OnJointChanged: %s, %s, %s",
                field, obj, part.attachJoint);

            if (!part.attachJoint)
                return;

            ConfigurableJoint joint = part.attachJoint.Joint;

            printf("Joint: %s %s",
                part.attachJoint.Joint,
                part.attachJoint.Joint ? part.attachJoint.Joint.name : "null");

            part.attachJoint.Joint.name = "MechanicsToolkit";

            printf("Unlocks: %s, %s, %s",
                originalJoint.xMotion,
                originalJoint.yMotion,
                originalJoint.zMotion);

            printf("Unlocks: %s, %s, %s",
                originalJoint.xDrive,
                originalJoint.yDrive,
                originalJoint.zDrive);

            printf("Unlocks: %s, %s, %s",
                originalJoint.angularXMotion,
                originalJoint.angularYMotion,
                originalJoint.angularZMotion);

            printf("Unlocks: %s, %s",
                originalJoint.angularXDrive,
                originalJoint.angularYZDrive);

            // Unlock the rotation
            if (unlockX)
            {
                originalJoint.xMotion = joint.xMotion;
                originalJoint.xDrive = joint.xDrive;
                joint.xMotion = ConfigurableJointMotion.Free;
                joint.xDrive = jointDriveUnlocked;
            }
            else
            {
                joint.xMotion = originalJoint.xMotion;
                joint.xDrive = originalJoint.xDrive;
            }

            if (unlockY)
            {
                originalJoint.yMotion = joint.yMotion;
                originalJoint.yDrive = joint.yDrive;
                joint.yMotion = ConfigurableJointMotion.Free;
                joint.yDrive = jointDriveUnlocked;
            }
            else
            {
                joint.yMotion = originalJoint.yMotion;
                joint.yDrive = originalJoint.yDrive;
            }

            if (unlockZ)
            {
                originalJoint.zMotion = joint.zMotion;
                originalJoint.zDrive = joint.zDrive;
                joint.zMotion = ConfigurableJointMotion.Free;
                joint.zDrive = jointDriveUnlocked;
            }
            else
            {
                joint.zMotion = originalJoint.zMotion;
                joint.zDrive = originalJoint.zDrive;
            }

            if (unlockAngularX)
            {
                originalJoint.angularXMotion = joint.angularXMotion;
                originalJoint.angularXDrive = joint.angularXDrive;
                joint.angularXMotion = ConfigurableJointMotion.Free;
                joint.angularXDrive = jointDriveUnlocked;
            }
            else
            {
                joint.angularXMotion = originalJoint.angularXMotion;
                joint.angularXDrive = originalJoint.angularXDrive;
            }

            if (unlockAngularY)
            {
                originalJoint.angularYMotion = joint.angularYMotion;
                joint.angularYMotion = ConfigurableJointMotion.Free;
            }
            else
            {
                joint.angularYMotion = originalJoint.angularYMotion;
            }

            if (unlockAngularZ)
            {
                originalJoint.angularZMotion = joint.angularZMotion;
                joint.angularZMotion = ConfigurableJointMotion.Free;
            }
            else
            {
                joint.angularZMotion = originalJoint.angularZMotion;
            }

            if (unlockAngularY || unlockAngularZ)
            {
                originalJoint.angularYZDrive = joint.angularYZDrive;
                joint.angularYZDrive = jointDriveUnlocked;
            }
            else
            {
                joint.angularYZDrive = originalJoint.angularYZDrive;
            }
        }
    }
}