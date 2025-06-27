﻿using Comfort.Common;
using EFT.BufferZone;
using Fika.Core.Coop.Utils;
using Fika.Core.Networking;
using Fika.Core.Patching;
using LiteNetLib;
using System.Reflection;

namespace Fika.Core.Coop.Patches
{
    public class BufferInnerZone_ChangeZoneInteractionAvailability_Patch : FikaPatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(BufferInnerZone).GetMethod(nameof(BufferInnerZone.ChangeZoneInteractionAvailability));
        }

        [PatchPostfix]
        public static void Postfix(bool isAvailable, EBufferZoneData changesDataType)
        {
            if (FikaBackendUtils.IsClient)
            {
                return;
            }

            BufferZonePacket packet = new(changesDataType)
            {
                Available = isAvailable
            };

            Singleton<FikaServer>.Instance.SendDataToAll(ref packet, DeliveryMethod.ReliableOrdered);
        }
    }
}
