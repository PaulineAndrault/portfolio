using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState
{
    // States used by Clients :
    ClientLocked,
    ClientUnlocked,
    // States used by Server :
    ServerTurn,
    ClientTurn,         // question phase
    ClientEvent,        // blind test phase
    ServerListening,     // waiting for oral answer
    ThemeChoice,
    WaitForGameMasterInput
}

public class GameManager : NetworkBehaviour
{
    #region Vars
    [SerializeField, Header("Game Parameters")]
    public int MaxPlayers = 10;
    public List<GameObject> PrefabMeeples = new List<GameObject>();
    public GameObject PrefabMeeple;
    public List<Color> MeepleColors = new List<Color>();
    
    [Header("Public variables")]
    public GameState CurrentState;

    // Server vars
    public List<Team> Teams = new List<Team>();
    public int PlayerTurnIndex;     // used to know whose turn it is. Between 0 and NbOfPlayers.
    public List<Theme> CurrentThemes = new List<Theme>();
    public int CurrentEventIndex = -1;     // -1 == intro blind test

    // Client vars
    public ulong TeamId;
    public string TeamName;
    // For reco purpose
    public bool Reconnexion;
    public bool TryingToReconnect;
    public string ReconnectingTeam;
    #endregion

    #region Singleton pattern
    public static GameManager Singleton;
    private void Awake()
    {
        if (Singleton != null)
        {
            Destroy(gameObject);
        }
        else
        {
            Singleton = this;
            DontDestroyOnLoad(this);
        }
    }
    #endregion

    #region OnSpawn - change current game state, check max players limit

    // Server and Client connexion
    public override void OnNetworkSpawn()
    {
        if (IsServer)
            OnSpawnServer();
        else
            OnSpawnClient();
    }

    private void OnSpawnServer()
    {
        CurrentState = GameState.ServerTurn;
    }

    private void OnSpawnClient()
    {
        CurrentState = GameState.ClientLocked;

        if (TryingToReconnect)
        {
            AskToRejoinServerRpc(NetworkManager.Singleton.LocalClientId, ReconnectingTeam);
        }
    }
    #endregion

    #region Reconnection System

    [ServerRpc(RequireOwnership = false)]
    public void AskToRejoinServerRpc(ulong id, string teamName)
    {
        // Create a Team
        Team newTeam = new Team(id, teamName);

        // Find Team with same name
        int oldTeamIndex = -1;
        for (int i = 0; i < Teams.Count; i++)
        {
            if (Teams[i].TeamName == teamName)
            {
                oldTeamIndex = i;
                break;
            }
        }

        if (oldTeamIndex == -1)
        {
            Debug.Log("Pas de correspondance");
            return;
        }

        Team oldTeam = Teams[oldTeamIndex];

        // Update new team with old team data
        newTeam.ClientId = oldTeam.ClientId;
        newTeam.Score = oldTeam.Score;
        newTeam.Color = oldTeam.Color;
        newTeam.Meeple = oldTeam.Meeple;
        newTeam.TeamOrder = oldTeam.TeamOrder;
        newTeam.FirstBlindTestScore = oldTeam.FirstBlindTestScore;
        newTeam.Event1Score = oldTeam.Event1Score;
        newTeam.Event2Score = oldTeam.Event2Score;
        newTeam.Event3Score = oldTeam.Event3Score;
        newTeam.CurrentEventScore = oldTeam.CurrentEventScore;

        // Remove old, add new
        Debug.Log("old team index :" + Teams.IndexOf(oldTeam));
        Teams.RemoveAt(oldTeamIndex);
        Teams.Insert(oldTeamIndex, newTeam);
        Debug.Log("new team index :" + Teams.IndexOf(newTeam));

        // update client Game Manager
        EndReconnexionClientRpc(id, newTeam.ClientId, teamName);

        // update UI on client side
        UIManager.S.ReinitUIAfterReconnexionClientRpc(id, newTeam.ClientId, teamName);
    }

    [ClientRpc]
    void EndReconnexionClientRpc(ulong id, ulong teamId, string teamName)
    {
        if (NetworkManager.Singleton.LocalClientId != id) return;

        TeamId = teamId;
        TeamName = teamName;
        TryingToReconnect = false;
    }
    #endregion

    #region Server Init
    private void OnEnable()
    {
        SceneManager.sceneLoaded += InitializeScene;
    }
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= InitializeScene;
    }
    private void InitializeScene(Scene scene, LoadSceneMode mode)
    {
        if(scene.name == "Game")
        {
            InitGameScene();
        }
    }
    public void InitGameScene()
    {
        if (!IsServer) return;

        // Ajouter les thèmes Art et Nature aux thèmes choisis
        AddTheme(Theme.Nature);
        AddTheme(Theme.Art);

        // Instantier les meeples des teams
        for (int i = 0; i < Teams.Count; i++)
        {
            GameObject meeple = Instantiate(PrefabMeeple);
            meeple.GetComponentInChildren<MeshRenderer>().material.color = MeepleColors[i];
            meeple.GetComponent<Rigidbody>().position = new Vector3(2*i, 0, 2*i);
            meeple.GetComponent<MeepleMovement>().MeepleTeam = Teams[i];
            Teams[i].Meeple = meeple.GetComponent<MeepleMovement>();
        }
    }

    #endregion

    private void Update()
    {
        // Test environment - Skip first round of blind test and topics choice.
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            CurrentThemes.Add(Theme.Ezio);
            CurrentThemes.Add(Theme.Hermione);
            CurrentThemes.Add(Theme.Karadoc);
            StartCoroutine(TransitionBlindTestToQuiz());
        }

        if(!IsServer && Reconnexion && SceneManager.GetActiveScene().name == "Game")
        {
            Reconnexion = false;
            TryingToReconnect = true;
            NetworkManager.Singleton.StartClient();
        }
    }

    #region Gameplay

    // Changer ordre des joueurs selon leur score au premier blind test
    public void ChangeTeamOrder()
    {
        if (CurrentEventIndex != -1) return;
        Teams.Sort(
        delegate (Team t1, Team t2)
        {
            return t2.FirstBlindTestScore.CompareTo(t1.FirstBlindTestScore);
        }
        );
    }

    public void AddTheme(Theme theme)
    {
        if (CurrentThemes.Count >= 5) return;

        CurrentThemes.Add(theme);
        if(CurrentThemes.Count == 5)
        {
            StartCoroutine(TransitionBlindTestToQuiz());
        }
    }

    IEnumerator TransitionBlindTestToQuiz()
    {
        // Générer la liste de questions avec les thèmes choisis
        FindObjectOfType<CSVReader>().ReadCSV();
        
        yield return new WaitForSeconds(3);

        // Masquer la vue Theme / Afficher la vue Board
        UIManager.S.DisplayUIElement(UIManager.S.BlindTestPanel, false);
        UIManager.S.DisplayUIElement(UIManager.S.ThemeChoicePanel, false);

        // Transformer les cases du plateau
        Transform caseContainer = GameObject.FindGameObjectWithTag("CaseContainer").transform;
        for (int i = 0; i < caseContainer.childCount; i++)
        {
            StandardCase tile = caseContainer.GetChild(i).GetComponent<StandardCase>();
            if (!tile.BaseTile)
            {
                yield return new WaitForSeconds(.2f);
                // SFX
                tile.TransformCase();
            }
        }

        // Changer les panels Client
        UIManager.S.EndBlindTestClientRpc();

        // On écoute le SPACE pour lancer la première question
        CurrentState = GameState.WaitForGameMasterInput;
    }

    public IEnumerator TransitionQuizToEvent(GameObject e1, GameObject e2, GameObject e3)
    {
        Debug.Log("Transition Quiz To Event");
        CurrentEventIndex++;

        // SFX
        AudioManager.S.PlaySound(0);

        // VFX
        GameObject[] eventFx = new GameObject[3] { e1, e2, e3 };
        eventFx[CurrentEventIndex].transform.GetChild(0).gameObject.SetActive(true);
        yield return new WaitForSeconds(1);
        eventFx[CurrentEventIndex].transform.GetChild(1).gameObject.SetActive(false);

        yield return new WaitForSeconds(7);

        UIManager.S.UpdateBlindTestScreen(CurrentEventIndex);
    }

    public void StartVictory()
    {
        // Changer ordre des joueurs selon leur score 
        Teams.Sort(
            delegate (Team t1, Team t2)
            {
                return t2.Score.CompareTo(t1.Score);
            }
            );

        StartCoroutine(VictoryCoroutine());
    }

    IEnumerator VictoryCoroutine()
    {
        UIManager.S.Victory();
        
        string scoreText = "";
        for (int i = 0; i < Teams.Count; i++)
        {
            yield return new WaitForSeconds(1.5f);
            scoreText += $"{Teams[i].TeamName}: {Teams[i].Score} points<br>";
            UIManager.S.ChangeText(UIManager.S.VictoryText, scoreText);
        }
        
        yield return new WaitForSeconds(3);
        UIManager.S.ChangeText(UIManager.S.VictoryTeamText, $"Victoire de la team {Teams[0].TeamName}");
    }
    #endregion

    #region Utilities
    public Team FindTeamWithId(ulong id)
    {
        foreach(Team team in Teams)
        {
            if (team.ClientId == id)
                return team;
        }

        return null;
    }
    #endregion
}
