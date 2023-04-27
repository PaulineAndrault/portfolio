using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;
using Unity.Netcode.Transports.UTP;
using UnityEngine.SceneManagement;

public class Lobby : NetworkBehaviour
{
    #region Vars
    [Header("UI Elements")]
    [SerializeField] private GameObject _serverCanvas;
    [SerializeField] private GameObject _clientCanvas;
    [SerializeField] private GameObject _serverWaitingScreen;
    [SerializeField] private GameObject _clientWaitingScreen;
    [SerializeField] private GameObject _currentTeamsServer;
    [SerializeField] private GameObject _welcomeTextClient;

    [Header("Network")]
    [SerializeField] private UnityTransport _transport;

    // Client vars
    [SerializeField] private string _teamName = "Player";
    [SerializeField] bool reconnexion;
    #endregion

    #region Spawn

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
        if(_serverWaitingScreen != null) _serverWaitingScreen.SetActive(true);
        if(_serverCanvas != null) _serverCanvas.SetActive(false);
    }

    private void OnSpawnClient()
    {
        // Note : in case of RECONNECTION, the player spawns directly on the Game scene (no Lobby instance in Game scene). See the OnSpawnClient() in GameManager class.
        AddNewTeamServerRpc(NetworkManager.Singleton.LocalClientId, _teamName);
        
        if(_clientWaitingScreen != null) _clientWaitingScreen.SetActive(true);
        if(_clientCanvas != null) _clientCanvas.SetActive(false);
    }
    #endregion

    #region Server Lobby

    public void JoinAsServer()
    {
        if (IsClient) return;
        NetworkManager.Singleton.StartServer();
    }

    // Connexion input
    public void SetIpAddress(string input)
    {
        _transport.ConnectionData.Address = input;
    }

    public void SetPort(string input)
    {
        int port;
        int.TryParse(input, out port);
        _transport.ConnectionData.Port = (ushort)port;
    }

    public void StartGame()
    {
        if (!IsServer) return;
        NetworkManager.SceneManager.LoadScene("Game", LoadSceneMode.Single);
    }
    #endregion

    #region Client Lobby

    // Input field method
    public void ChangeTeamName(string newName)
    {
        _teamName = newName;
    }

    // Button method
    public void JoinGame()
    {
        if (_teamName != "")
            NetworkManager.Singleton.StartClient();
    }

    public void RejoinGame()
    {
        if (_teamName != "")
        {
            GameManager.Singleton.Reconnexion = true;
            GameManager.Singleton.ReconnectingTeam = _teamName;
            SceneManager.LoadScene("Game");
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void AddNewTeamServerRpc(ulong id, string teamName)
    {
        // Create a Team
        Team team = new Team(id, teamName);

        // Pass it to the Game Manager
        GameManager.Singleton.Teams.Add(team);

        if(_currentTeamsServer != null)
        {
            TextMeshProUGUI textContainer = _currentTeamsServer.transform.GetChild((int)id - 1).GetComponentInChildren<TextMeshProUGUI>();
            textContainer.text = teamName;
        }

        // Update the Team on the client
        UpdateTeamListClientRpc(id, teamName);
    }

    [ClientRpc]
    private void UpdateTeamListClientRpc(ulong id, string teamName)
    {
        if (NetworkManager.Singleton.LocalClientId != id) return;

        GameManager.Singleton.TeamId = id;
        GameManager.Singleton.TeamName = teamName;
        string yourColor = "";
        switch (id)
        {
            case 1:
                yourColor = "rouge";
                break;
            case 2:
                yourColor = "bleu";
                break;
            case 3:
                yourColor = "vert";
                break;
            case 4:
                yourColor = "bleu clair";
                break;
            case 5:
                yourColor = "orange";
                break;
            case 6:
                yourColor = "rose";
                break;
            case 7:
                yourColor = "violet";
                break;
            case 8:
                yourColor = "marron";
                break;
            case 9:
                yourColor = "noir";
                break;
            case 10:
                yourColor = "gris";
                break;
            default:
                break;
        }

        if(_welcomeTextClient != null)
        {
            TMP_Text textContainer = _welcomeTextClient.GetComponent<TMP_Text>();
            //textContainer.color = GameManager.Singleton.MeepleColors[((int)id - 1)];
            textContainer.text = $"Bienvenue <b>{teamName}</b> ! Votre couleur est le {yourColor}.";
        }
    }
    #endregion
}
