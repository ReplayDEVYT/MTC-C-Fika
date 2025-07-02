﻿using LiteNetLib.Utils;

namespace Fika.Core.Networking
{
    public struct CorpsePositionPacket : INetSerializable
    {
        public RagdollPacketStruct Data;

        public void Deserialize(NetDataReader reader)
        {
            Data = new()
            {
                Id = reader.GetInt(),
                Position = reader.GetVector3(),
                Done = reader.GetBool()
            };

            if (Data.Done)
            {
                Data.TransformSyncs = new GStruct111[12];
                for (int i = 0; i < 12; i++)
                {
                    Data.TransformSyncs[i] = new()
                    {
                        Position = reader.GetVector3(),
                        Rotation = reader.GetQuaternion()
                    };
                }
            }
        }

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(Data.Id);
            writer.Put(Data.Position);
            writer.Put(Data.Done);

            if (Data.Done && Data.TransformSyncs != null)
            {
                GStruct111[] transforms = Data.TransformSyncs;
                for (int i = 0; i < 12; i++)
                {
                    writer.Put(transforms[i].Position);
                    writer.Put(transforms[i].Rotation);
                }
            }
        }
    }
}
