using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using TMPro;
using System;
using System.Linq;

public class BlindTestManager : NetworkBehaviour
{
    #region Vars
    // Server vars
    public int CurrentEventIndex = -1;  // -1 for intro blind test

    [Header("Music params")]
    public List<AudioClip> Clips = new List<AudioClip>();
    public List<AudioClip> IntroClips = new List<AudioClip>();
    public List<AudioClip> Event1Clips = new List<AudioClip>();
    public List<AudioClip> Event2Clips = new List<AudioClip>();
    public List<AudioClip> Event3Clips = new List<AudioClip>();
    AudioSource _musicSource;
    int _currentClip = 0;

    [Header("UI Elements")]
    public GameObject _nextSongButton;
    [SerializeField] TMP_Text _songNumberText;
    [SerializeField] TMP_Text _answerText;
    [SerializeField] TMP_Text _timerText;
    [SerializeField] GameObject _timer;
    [SerializeField] GameObject _clientBuzzerContainer;
    [SerializeField] GameObject _serverAnswerPanel;
    [SerializeField] GameObject _serverAnswerButtons;
    [SerializeField] GameObject _serverDisplayThemeChoiceButton;

    // Timer param
    [SerializeField] float _timerValue;
    [SerializeField] bool _timerIsPaused;

    // Answer vars
    [SerializeField] Team _currentBuzzTeam;
    Color _answerTextColor;

    QuestionManager _qm;

    public int CurrentClip { get => _currentClip; set => _currentClip = value; }
    #endregion

    #region Init
    private void Awake()
    {
        _musicSource = GetComponent<AudioSource>();
        _qm = FindObjectOfType<QuestionManager>();
        _nextSongButton.SetActive(true);
        _serverAnswerPanel.SetActive(false);
        _serverAnswerButtons.SetActive(true);
        _timer.SetActive(false);
        _serverDisplayThemeChoiceButton.SetActive(false);
        _answerText.text = "";
        _songNumberText.text = "";
        _answerTextColor = _answerText.color;
    }
    #endregion

    private void Update()
    {
        if (!_timerIsPaused && _timerValue > 0)
            Timer();

        if(GameManager.Singleton.CurrentState == GameState.ServerListening)
        {
            if (Input.GetButtonDown("Zero"))
                AnswerServerButton(0);
            if (Input.GetButtonDown("One"))
                AnswerServerButton(1);
            if (Input.GetButtonDown("Two"))
                AnswerServerButton(2);
            if (Input.GetKeyDown(KeyCode.Space))
                NextSongButton();
        }
    }

    #region Server buttons : play next song, validate answer, stop blind test

    public void ReinitBlindTestForNewEvent(int index)
    {
        CurrentEventIndex = index;

        foreach(Team team in GameManager.Singleton.Teams)
        {
            team.CurrentEventScore = 0;
        }

        _currentClip = 0;

        // replace audio in Clips by one of the list of audios
        Clips.Clear();
        if(CurrentEventIndex == 0)
            Clips = Event1Clips;
        else if (CurrentEventIndex == 1)
            Clips = Event2Clips;
        else if (CurrentEventIndex == 2)
            Clips = Event3Clips;
    }

    // Bouton Serveur pour lancer un extrait
    public void NextSongButton()
    {
        if (!IsServer) return;

        if (!AudioManager.S.isPaused)
            AudioManager.S.PauseMusic();

        if (_currentClip >= Clips.Count)
        {
            _timer.SetActive(false);
            StopBlindTest();
            return;
        }

        _currentBuzzTeam = null;
        _serverAnswerPanel.SetActive(false);

        _songNumberText.text = $"Extrait {_currentClip + 1}"; 

        _musicSource.Stop();
        _musicSource.clip = Clips[_currentClip];
        _musicSource.Play();

        GameManager.Singleton.CurrentState = GameState.ClientEvent;

        StartBuzzerClientRpc();

        _timerValue = Clips[_currentClip].length;
        _timer.SetActive(true);
        _timerIsPaused = false;
        _nextSongButton.SetActive(false);
        
        _currentClip++;
    }

    // Boutons server "bon / faux"
    public void AnswerServerButton(int score)
    {
        _musicSource.UnPause();
        // couper son buzzer
        AudioManager.S.StopSound();

        if (score >= 1)
        {
            _answerText.color = _answerTextColor;
            string title = _musicSource.clip.name;
            title = title.Replace("_", "'");
            _answerText.text = $"{title}";        

            // donne les points au client
            if(CurrentEventIndex == -1)
                _currentBuzzTeam.FirstBlindTestScore += score;
            else
                _currentBuzzTeam.CurrentEventScore += score;

            // éteindre l'audio après x sec ? ou pas
            // afficher bouton lancer un extrait
            _nextSongButton.SetActive(true);
        }
        else if(score == 0)
        {
            // Pas d'écran intermédiaire / de coroutine ici,
            // car le maitre du jeu annonce que c'est faux. Cliquer sur 0 pour relancer l'extrait

            // Masquer panel
            _serverAnswerPanel.SetActive(false);
            _serverAnswerButtons.SetActive(true);

            // Relancer timer
            _timer.SetActive(true);
            _timerIsPaused = false;

            // Unlock buzzers (with malus for team who buzzed)
            UnlockBuzzerClientRpc(_currentBuzzTeam.ClientId);

            GameManager.Singleton.CurrentState = GameState.ClientEvent;
        }
        _serverAnswerButtons.SetActive(false);
    }

    void StopBlindTest()
    {
        if (!IsServer) return;
        Debug.Log("stop blind test");
        _nextSongButton.SetActive(false);

        // Changer ordre des teams selon score du blind test intro
        if(CurrentEventIndex == -1)
            GameManager.Singleton.ChangeTeamOrder();

        StartCoroutine(EndEventCoroutine());
    }

    public IEnumerator EndEventCoroutine()
    {
        _answerText.text = "";
        // Afficher la liste des scores et Enregistrer les scores dans Team

        // Cas du blind test intro
        if(CurrentEventIndex == -1)
        {
            for (int i = 0; i < GameManager.Singleton.Teams.Count; i++)
            {
                yield return new WaitForSeconds(1);

                Team team = GameManager.Singleton.Teams[i];
                team.TeamOrder = i;
                _answerText.text += $"{team.TeamName} : {team.FirstBlindTestScore} points<br>";
            }

            yield return new WaitForSeconds(1);

            _serverDisplayThemeChoiceButton.SetActive(true);
            GameManager.Singleton.CurrentState = GameState.ServerTurn;
        }

        // Cas des Events
        else
        {
            // Récupérer la liste des Teams triées selon leur CurrentEventScore
            List<Team> teamList = GameManager.Singleton.Teams.OrderByDescending(x => x.CurrentEventScore).ToList();

            for (int i = 0; i < teamList.Count; i++)
            {
                yield return new WaitForSeconds(1);

                Team team = teamList[i];

                // Display score
                _answerText.text += $"{team.TeamName} : {team.CurrentEventScore} points<br>";

                // Maj score dans Team
                if (CurrentEventIndex == 0)
                    team.Event1Score = team.CurrentEventScore;
                else if (CurrentEventIndex == 1)
                    team.Event2Score = team.CurrentEventScore;
                else if (CurrentEventIndex == 2)
                    team.Event3Score = team.CurrentEventScore;
            }
    
            yield return new WaitForSeconds(1);

            // Reinit texte rép
            _answerText.text = "";
            // fermer blind test panel
            UIManager.S.DisplayUIElement(UIManager.S.BlindTestPanel, false);
            // fermer extrait screen
            UIManager.S.DisplayUIElement(UIManager.S.ExtraitPanel, false);

            // déplacer les meeples et actualiser le score
            // noter : impossible de déclencehr un nouvel event ou la fin du jeu en fin d'event
            for (int i = 0; i < 3; i++)
            {
                if (teamList.Count - 1 < i) break;
                yield return StartCoroutine(_qm.AddScoreToTeam(teamList[i], 5 - i));
            }

            GameManager.Singleton.CurrentState = GameState.WaitForGameMasterInput;
        }

        _musicSource.Stop();
        AudioManager.S.PauseMusic(false);
    }

    #endregion

    #region Client Buzzer buttons

    public void BuzzerClientButton()
    {
        if (!IsClient || GameManager.Singleton.CurrentState != GameState.ClientUnlocked) return;

        BuzzerServerRpc(GameManager.Singleton.TeamId);
        GameManager.Singleton.CurrentState = GameState.ClientLocked;
        _clientBuzzerContainer.SetActive(false);
    }

    [ServerRpc (RequireOwnership = false)]
    void BuzzerServerRpc(ulong id) 
    {
        if (!IsServer) return;
        if (GameManager.Singleton.CurrentState != GameState.ClientEvent) return;

        // We change the state to "listening" to prevent other buzzers
        GameManager.Singleton.CurrentState = GameState.ServerListening;

        // Pause the music and the timer
        _timerIsPaused = true;
        _timer.SetActive(false); ;
        _musicSource.Pause();

        // sound buzzer
        AudioManager.S.PlaySound(1);

        // We lock every buzzers
        LockBuzzerClientRpc();

        // Change the anwser text color to the team color
        // Display the name of the team who buzzed
        _currentBuzzTeam = GameManager.Singleton.FindTeamWithId(id);
        _answerText.text = $"<size=115>{_currentBuzzTeam.TeamName} a buzzé";

        // Display Right / False buttons (or keyboard shortcuts only ?)
        _serverAnswerPanel.SetActive(true);
        _serverAnswerButtons.SetActive(true);
    }
    #endregion

    #region Lock / unlock buzzers on client's side
    [ClientRpc]
    private void StartBuzzerClientRpc()
    {
        if (!IsClient) return;

        // Display the buzzer button
        _clientBuzzerContainer.SetActive(true);

        // Autoriser le clic (game status)
        GameManager.Singleton.CurrentState = GameState.ClientUnlocked;
    }

    [ClientRpc]
    void LockBuzzerClientRpc()
    {
        GameManager.Singleton.CurrentState = GameState.ClientLocked;
        _clientBuzzerContainer.SetActive(false);
    }

    [ClientRpc]
    void UnlockBuzzerClientRpc(ulong id)
    {
        if (IsServer) return;
        _clientBuzzerContainer.SetActive(true);
        if (GameManager.Singleton.TeamId == id)
            StartCoroutine(UnlockBuzzerLater());
        else
            GameManager.Singleton.CurrentState = GameState.ClientUnlocked;
    }

    private IEnumerator UnlockBuzzerLater()
    {
        yield return new WaitForSeconds(3);
        // afficher le malus sur l'écran ?
        GameManager.Singleton.CurrentState = GameState.ClientUnlocked;
    }

    #endregion

    #region Timer
    // Timer serveur
    void Timer()
    {
        _timerValue -= Time.deltaTime;
        _timerText.text = $"{Mathf.CeilToInt(_timerValue)}";

        if(_timerValue <= 0)
        {
            _timerText.text = "0";
            GameManager.Singleton.CurrentState = GameState.ServerListening;
            _nextSongButton.SetActive(true);
            // le game master choisit entre laisser du temps pour buzzer OU passer à l'extrait suivant
            _timer.SetActive(false);
        }
    }
    #endregion

}
