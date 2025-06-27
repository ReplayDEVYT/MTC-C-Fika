﻿using Fika.Core.Networking.Packets;
using System.Collections.Generic;

namespace Fika.Core.Networking
{
    public class WorldPacket : IReusable
    {
        public List<RagdollPacketStruct> RagdollPackets { get; set; }
        public List<ArtilleryPacketStruct> ArtilleryPackets { get; set; }
        public List<GrenadeDataPacketStruct> GrenadePackets { get; set; }
        public List<AirplaneDataPacketStruct> SyncObjectPackets { get; set; }
        public List<LootSyncStruct> LootSyncStructs { get; set; }

        public bool HasData
        {
            get
            {
                return RagdollPackets.Count > 0
                    || ArtilleryPackets.Count > 0
                    || GrenadePackets.Count > 0
                    || SyncObjectPackets.Count > 0
                    || LootSyncStructs.Count > 0;
            }
        }

        public void Flush()
        {
            RagdollPackets.Clear();
            ArtilleryPackets.Clear();
            GrenadePackets.Clear();
            SyncObjectPackets.Clear();
            LootSyncStructs.Clear();
        }
    }
}
