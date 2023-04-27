using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum Theme
{
    Nature,
    Art,
    Ezio,
    Hermione,
    Mulan,
    Obiwan,
    Karadoc
}
public enum SubTheme
{
    Nature,
    Art,
    Ezio,
    Histoire,
    Hermione,
    Fayotte,
    Mulan,
    Asie,
    Obiwan,
    Politique,
    Karadoc,
    Bouffe
}

public enum QuestionType
{
    StandardQcm,
    Buzzer,
    OpenQuestion
}

[System.Serializable]
public class QuestionsList
{
    public List<Question> Questions = new List<Question>();
}

[System.Serializable]
public class Question : INetworkSerializable
{
    public Theme Theme;
    public SubTheme SubTheme;
    public string Subject;
    public int Difficulty;
    public string QuestionIntitule;
    public string RightAnswer;
    public string[] FalseAnswers = new string[3];
    public bool IsDone;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Theme);
        serializer.SerializeValue(ref SubTheme);
        serializer.SerializeValue(ref Subject);
        serializer.SerializeValue(ref Difficulty);
        serializer.SerializeValue(ref QuestionIntitule);
        serializer.SerializeValue(ref RightAnswer);
        int length = 0;
        if(!serializer.IsReader)
        {
            length = FalseAnswers.Length;
        }
        serializer.SerializeValue(ref length);

        if (serializer.IsReader)
        {
            FalseAnswers = new string[length];
        }
        for (int i = 0; i < length; i++)
        {
            serializer.SerializeValue(ref FalseAnswers[i]);
        }
        serializer.SerializeValue(ref IsDone);
    }
}

[System.Serializable]
public class Answer : INetworkSerializable
{
    public ulong TeamId;
    public bool IsRight;
    public float Timestamp;

    public Answer() { }
    public Answer(ulong id, bool isRight, float timestamp)
    {
        TeamId = id;
        IsRight = isRight;
        Timestamp = timestamp;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref TeamId);
        serializer.SerializeValue(ref IsRight);
        serializer.SerializeValue(ref Timestamp);
    }

}
