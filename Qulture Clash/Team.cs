using UnityEngine;
using Unity.Netcode;

[System.Serializable]
public class Team : INetworkSerializable
{
    public ulong ClientId = 0;
    public string TeamName = "Player";
    public int Score = 0;
    public Color Color = Color.black;
    public MeepleMovement Meeple;
    public int TeamOrder;
    public int FirstBlindTestScore = 0;
    public int Event1Score = 0;
    public int Event2Score = 0;
    public int Event3Score = 0;
    public int CurrentEventScore = 0;

    public Team()
    {

    }

    public Team(ulong id, string name)
    {
        ClientId = id;
        TeamName = name;
        Score = 0;
        Color = GameManager.Singleton.MeepleColors[(int)id - 1];
        TeamOrder = (int)id - 1;
        FirstBlindTestScore = 0;
        Event1Score = 0;
        Event2Score = 0;
        Event3Score = 0;
        CurrentEventScore = 0;
}

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref TeamName);
        serializer.SerializeValue(ref Score);
        serializer.SerializeValue(ref Color);
        serializer.SerializeValue(ref TeamOrder);
        serializer.SerializeValue(ref FirstBlindTestScore);
        serializer.SerializeValue(ref Event1Score);
        serializer.SerializeValue(ref Event2Score);
        serializer.SerializeValue(ref Event3Score);
        serializer.SerializeValue(ref CurrentEventScore);
    }
}
