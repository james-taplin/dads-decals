using System.Collections.Generic;
using System.IO;
using MPAPI.Interfaces.Packets;

namespace DadsDecals.Multiplayer
{
    // Hand-written serialisation keeps the wire format explicit; bump Sync.Protocol when it changes.

    /// <summary>Guest → host, once the guest's world has loaded: "I have Dad's Decals".</summary>
    public sealed class HelloPacket : ISerializablePacket
    {
        public int Protocol;
        public string Version = "";

        public void Serialize(BinaryWriter w)
        {
            w.Write(Protocol);
            w.Write(Version);
        }

        public void Deserialize(BinaryReader r)
        {
            Protocol = r.ReadInt32();
            Version = r.ReadString();
        }
    }

    /// <summary>Host → guest, the reply to Hello. Layouts follow if the protocols match.</summary>
    public sealed class WelcomePacket : ISerializablePacket
    {
        public int Protocol;
        public string HostVersion = "";

        public void Serialize(BinaryWriter w)
        {
            w.Write(Protocol);
            w.Write(HostVersion);
        }

        public void Deserialize(BinaryReader r)
        {
            Protocol = r.ReadInt32();
            HostVersion = r.ReadString();
        }
    }

    /// <summary>Host → guest: one car's whole layout (JSON) and the SHA-1 of each image it uses.</summary>
    public sealed class CarLayoutPacket : ISerializablePacket
    {
        public string CarGuid = "";
        public int Version;
        public string Json = "";
        public Dictionary<string, string> Hashes = new Dictionary<string, string>();

        public void Serialize(BinaryWriter w)
        {
            w.Write(CarGuid);
            w.Write(Version);
            w.Write(Json);
            w.Write(Hashes.Count);
            foreach (var kv in Hashes)
            {
                w.Write(kv.Key);
                w.Write(kv.Value);
            }
        }

        public void Deserialize(BinaryReader r)
        {
            CarGuid = r.ReadString();
            Version = r.ReadInt32();
            Json = r.ReadString();
            var n = r.ReadInt32();
            Hashes = new Dictionary<string, string>(n);
            for (var i = 0; i < n; i++) Hashes[r.ReadString()] = r.ReadString();
        }
    }
}
