using System;
using Unity.Netcode;
using Unity.Collections;

[Serializable]
public struct PlayerData : INetworkSerializable, IEquatable<PlayerData>
{
    public FixedString32Bytes clientId;
    public FixedString32Bytes playerName;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref clientId);
        serializer.SerializeValue(ref playerName);
    }

    public bool Equals(PlayerData other)
    {
        return clientId == other.clientId;
    }
}