﻿using EFT.SynchronizableObjects;
using Fika.Core.Coop.Utils;
using Fika.Core.Patching;
using System.Reflection;

namespace Fika.Core.Coop.Patches
{
    public class SyncObjectProcessorClass_GetSyncObjectStrategyByType_Patch : FikaPatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(SyncObjectProcessorClass).GetMethod(nameof(SyncObjectProcessorClass.GetSyncObjectStrategyByType), BindingFlags.Static | BindingFlags.Public);
        }

        [PatchPrefix]
        public static bool Prefix(SynchronizableObjectType type, ref ISynchronizableObject __result)
        {
            switch (type)
            {
                case SynchronizableObjectType.Tripwire:
                    __result = new GClass2454();
                    break;
                case SynchronizableObjectType.AirPlane:
                    __result = new AirplaneLogicClass(FikaBackendUtils.IsServer);
                    break;
                case SynchronizableObjectType.AirDrop:
                    __result = new AirdropLogicClass(FikaBackendUtils.IsServer);
                    break;
                default:
                    break;
            }

            return false;
        }
    }
}
