using System.Collections.Generic;
using System.IO;
using MPAPI.Interfaces.Packets;

namespace DadsDecals.Multiplayer
{
    // Hand-written serialisation keeps the wire format explicit; bump Sync.Protocol when it changes.
    // Protocol 2 (0.5.1-beta): guest edits, permissions, picture transfer.

    /// <summary>Guest → host, once the guest's world has loaded: "I have Dad's Decals".</summary>
    public sealed class HelloPacket : ISerializablePacket
    {
        public int Protocol;
        public string Version = "";

        public void Serialize(BinaryWriter w) { w.Write(Protocol); w.Write(Version); }
        public void Deserialize(BinaryReader r) { Protocol = r.ReadInt32(); Version = r.ReadString(); }
    }

    /// <summary>Host → guest, the reply to Hello: versions and what the guest may do. Layouts follow if the protocols match.</summary>
    public sealed class WelcomePacket : ISerializablePacket
    {
        public int Protocol;
        public string HostVersion = "";
        public bool GuestsCanEdit;
        public bool Blocked;

        public void Serialize(BinaryWriter w) { w.Write(Protocol); w.Write(HostVersion); w.Write(GuestsCanEdit); w.Write(Blocked); }
        public void Deserialize(BinaryReader r) { Protocol = r.ReadInt32(); HostVersion = r.ReadString(); GuestsCanEdit = r.ReadBoolean(); Blocked = r.ReadBoolean(); }
    }

    /// <summary>Host → guest: the host changed "guests can edit" or this guest's blocked state.</summary>
    public sealed class PermissionsPacket : ISerializablePacket
    {
        public bool GuestsCanEdit;
        public bool Blocked;

        public void Serialize(BinaryWriter w) { w.Write(GuestsCanEdit); w.Write(Blocked); }
        public void Deserialize(BinaryReader r) { GuestsCanEdit = r.ReadBoolean(); Blocked = r.ReadBoolean(); }
    }

    /// <summary>A car's whole layout (JSON) and the SHA-1 of each picture it uses.</summary>
    public abstract class LayoutPacketBase : ISerializablePacket
    {
        public string CarGuid = "";
        public int Version;
        public string Json = "";
        public Dictionary<string, string> Hashes = new Dictionary<string, string>();

        public virtual void Serialize(BinaryWriter w)
        {
            w.Write(CarGuid);
            w.Write(Version);
            w.Write(Json);
            w.Write(Hashes.Count);
            foreach (var kv in Hashes) { w.Write(kv.Key); w.Write(kv.Value); }
        }

        public virtual void Deserialize(BinaryReader r)
        {
            CarGuid = r.ReadString();
            Version = r.ReadInt32();
            Json = r.ReadString();
            var n = r.ReadInt32();
            if (n < 0 || n > 4096) throw new InvalidDataException("too many pictures");
            Hashes = new Dictionary<string, string>(n);
            for (var i = 0; i < n; i++) Hashes[r.ReadString()] = r.ReadString();
        }
    }

    /// <summary>Host → guest(s): the current layout of one car.</summary>
    public sealed class CarLayoutPacket : LayoutPacketBase { }

    /// <summary>Guest → host: "here's my edit of this car, made on top of version <see cref="LayoutPacketBase.Version"/>".</summary>
    public sealed class CarLayoutEditPacket : LayoutPacketBase { }

    /// <summary>Host → guest: the edit wasn't accepted (Reason); here's the host's current layout to go back to.</summary>
    public sealed class EditRejectedPacket : LayoutPacketBase
    {
        public string Reason = "";

        public override void Serialize(BinaryWriter w) { base.Serialize(w); w.Write(Reason); }
        public override void Deserialize(BinaryReader r) { base.Deserialize(r); Reason = r.ReadString(); }
    }

    /// <summary>Either way: "please send me these pictures" (SHA-1s).</summary>
    public sealed class ImageRequestPacket : ISerializablePacket
    {
        public List<string> Hashes = new List<string>();

        public void Serialize(BinaryWriter w) { w.Write(Hashes.Count); foreach (var h in Hashes) w.Write(h); }

        public void Deserialize(BinaryReader r)
        {
            var n = r.ReadInt32();
            if (n < 0 || n > 4096) throw new InvalidDataException("too many pictures");
            Hashes = new List<string>(n);
            for (var i = 0; i < n; i++) Hashes.Add(r.ReadString());
        }
    }

    /// <summary>Either way: one piece of a picture (≤ 32 KB).</summary>
    public sealed class ImageChunkPacket : ISerializablePacket
    {
        public const int ChunkSize = 32 * 1024;

        public string Hash = "";
        public int Index;
        public int Count;
        public byte[] Bytes = new byte[0];

        public void Serialize(BinaryWriter w) { w.Write(Hash); w.Write(Index); w.Write(Count); w.Write(Bytes.Length); w.Write(Bytes); }

        public void Deserialize(BinaryReader r)
        {
            Hash = r.ReadString();
            Index = r.ReadInt32();
            Count = r.ReadInt32();
            var len = r.ReadInt32();
            if (len < 0 || len > ChunkSize) throw new InvalidDataException("chunk too big");
            Bytes = r.ReadBytes(len);
        }
    }
}
