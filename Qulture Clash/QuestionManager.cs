using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using TMPro;
using System;
using System.Linq;

public class QuestionManager : NetworkBehaviour
{
    #region Vars
    [Header("UI Elements")]     // to be managed by UIManager later
    [SerializeField] Transform _timerBar;
    [SerializeField] AnswerCard[] _clientAnswerCards;
    [SerializeField] TMP_Text[] _clientAnswerTexts;

    // Server vars
    [Header("Current game vars")]
    float _startTimestamp;
    Theme _currentTheme;
    SubTheme _currentSubTheme;
    Team _currentTeam;
    string _currentSubject;
    int _rightAnswerIndex;
    List<Question> _currentQuestionList = new List<Question>();
    [SerializeField] NetworkVariable<Question> CurrentQuestion = new NetworkVariable<Question>();
    [SerializeField] List<Answer> _currentPlayerAnswers = new List<Answer>();
    bool endGame;
    [SerializeField] bool eventTriggered;

    [Header("Events var")]
    [SerializeField] int _firstEventCase = 10;
    [SerializeField] int _secondEventCase = 25;
    [SerializeField] int _thirdEventCase = 40;
    [SerializeField] bool _firstEvent;
    [SerializeField] bool _secondEvent;
    [SerializeField] bool _thirdEvent;
    [SerializeField] GameObject _firstEventObject;
    [SerializeField] GameObject _secondEventObject;
    [SerializeField] GameObject _thirdEventObject;

    // Timer 
    bool _timeIsUp;
    bool _timerIsOn;
    float _timerValue;

    // Refs
    [Header("List of Questions")]
    public QuestionsList QList;
    Transform _caseContainer;

    public List<Question> CurrentQuestionList { get => _currentQuestionList; set => _currentQuestionList = value; }
    #endregion

    #region Init
    private void Awake()
    {
        _caseContainer = GameObject.FindGameObjectWithTag("CaseContainer").transform;
    }
    #endregion

    private void Update()
    {
        if (!IsServer) return;

        if (Input.GetKeyDown(KeyCode.Space) && GameManager.Singleton.CurrentState == GameState.WaitForGameMasterInput)
        {
            StartNewQuestion();
        }

        if (GameManager.Singleton.CurrentState == GameState.ClientTurn)
        {
            UpdateTimer();
            CheckPlayerQcmAnswers();
        }
    }

    #region Send new Question methods

    // After blind test, the current state is WaitForGameMasterInput.
    // Need to be in WaitForGameMasterInput for starting New Question.
    public void StartNewQuestion()
    {
        if (!IsServer) return;
        
        // Find current team tile and theme
        _currentTeam = GameManager.Singleton.Teams[GameManager.Singleton.PlayerTurnIndex];
        _currentTheme = _caseContainer.GetChild(_currentTeam.Score).GetComponent<StandardCase>().TileTheme;

        // Random sur Primary / Secondary topic
        bool primary = UnityEngine.Random.value < 0.8f;
        _currentSubTheme = FindSubThemeByTheme(_currentTheme, primary);
        // If the SubTheme is empty, we pick the !primary topic
        if(NumberOfQuestionsInSubtheme(_currentSubTheme) == 0)
            _currentSubTheme = FindSubThemeByTheme(_currentTheme, !primary);

        // Get a random subject and create the list of questions
        GetListOfQuestionsBySubject();  // update _currentQuestionList

        // Update UI
        UIManager.S.UpdateSelectDifficultyPanel(_currentTeam.TeamName, _currentSubTheme, _currentSubject);
    }

    public void SelectDifficulty(int level)
    {
        // Choose the question with the selected level of difficulty
        List<Question> questions = _currentQuestionList.Where(x => x.Difficulty == level).ToList();
        CurrentQuestion.Value = questions[UnityEngine.Random.Range(0, questions.Count)];

        StartQuestion();
        // Later : delete subjects if some criterias are met (less than x questions left in the subjects, etc.)
    }

    void StartQuestion()
    {
        if (!IsServer) return;

        // Shuffle answers
        int[] answerOrderTemp = ShuffleAnswers(new int[4] { 0, 1, 2, 3 });
        int[] answerOrder = ShuffleAnswers(answerOrderTemp);
        _rightAnswerIndex = System.Array.IndexOf(answerOrder, 0);
        Debug.Log("right answer index : " + _rightAnswerIndex);     // Test environment : right answer is displayed in the console

        // Update on Server side
        UIManager.S.UpdateQuestionScreen(CurrentQuestion.Value, answerOrder, ThemeIndex(CurrentQuestion.Value.Theme));
        GameManager.Singleton.CurrentState = GameState.ClientTurn;
        StartTimer();

        // Update on Clients' side
        DisplayQcmClientRpc(answerOrder, CurrentQuestion.Value);
    }

    [ClientRpc]
    private void DisplayQcmClientRpc(int[] answerOrder, Question currentQuestion)
    {
        if (IsServer) return;

        // Change question text
        UIManager.S.ChangeText(UIManager.S.ClientIntitule, currentQuestion.QuestionIntitule);

        // Update answers
        for (int i = 0; i < answerOrder.Length; i++)
        {
            if (answerOrder[i] == 0)
            {
                _clientAnswerCards[i].IsRightAnswer = true;
                _clientAnswerTexts[i].text = currentQuestion.RightAnswer;
            }
            else
            {
                _clientAnswerCards[i].IsRightAnswer = false;
                _clientAnswerTexts[i].text = currentQuestion.FalseAnswers[answerOrder[i] - 1];
            }
        }

        // Display cards
        UIManager.S.DisplayUIElement(UIManager.S.ClientQuestionsPanel);

        // Authorize click (game status)
        GameManager.Singleton.CurrentState = GameState.ClientUnlocked;
    }
    #endregion

    #region Timer
    void StartTimer()
    {
        _timerValue = 30;
        _timerIsOn = true;
        _timerBar.localScale = Vector3.one;
    }
    void UpdateTimer()
    {
        if (!_timerIsOn) return;
        if (_timerValue <= 0)
            EndTimer();
        else
        {
            _timerValue -= Time.deltaTime;
            _timerBar.localScale = new Vector3(_timerBar.localScale.x - Time.deltaTime / 30, _timerBar.localScale.y, _timerBar.localScale.z);
        }
    }
    void EndTimer()
    {
        _timerIsOn = false;
        _timerBar.localScale = new Vector3(0, _timerBar.localScale.y, _timerBar.localScale.z);
        _timeIsUp = true;   // Permet de passer à DisplayScore sans tous les inputs des joueurs
    }
    #endregion

    #region Answering methods

    // Client method called by AnswerCard button
    public void SendAnswer(bool isRightAnswer)
    {
        if (!IsClient) return;

        GameManager.Singleton.CurrentState = GameState.ClientLocked;
        Answer answer = new Answer(GameManager.Singleton.TeamId, isRightAnswer, Time.time - _startTimestamp);
        RegisterPlayerAnswerServerRpc(answer);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RegisterPlayerAnswerServerRpc(Answer answer)
    {
        _currentPlayerAnswers.Add(answer);
    }
    #endregion

    #region End a question and display scores

    private void CheckPlayerQcmAnswers()
    {
        if (!IsServer) return;
        
        if (_currentPlayerAnswers.Count >= GameManager.Singleton.Teams.Count || _timeIsUp)
        {
            if (_timeIsUp)
                _timeIsUp = false;
            GameManager.Singleton.CurrentState = GameState.ServerTurn;
            StartCoroutine(DisplayQcmAnswers());
        }
    }

    IEnumerator DisplayQcmAnswers()
    {
        yield return new WaitForSeconds(2);

        // Update UI
        UIManager.S.ShowRightAnswer(_rightAnswerIndex);

        yield return new WaitForSeconds(4);

        UIManager.S.DisplayUIElement(UIManager.S.QuestionsPanel, false);
        UIManager.S.DisplayUIElement(UIManager.S.QuestionScreen, false);
        UIManager.S.EndQuestionClientRpc();

        yield return new WaitForSeconds(0.4f);

        // Calcul score joueurs : la team dont c'est le tour avance d'autant de cases que la difficulté de laquestion, les autres avancent de +1
        endGame = false;
        eventTriggered = false;

        foreach(Answer answer in _currentPlayerAnswers)
        {
            // Récupérer la Team qui a le même ID que l'answer
            Team team = GameManager.Singleton.FindTeamWithId(answer.TeamId);
            
            if (answer.IsRight)
                yield return StartCoroutine(AddScoreToTeam(team, GameManager.Singleton.PlayerTurnIndex == team.TeamOrder ? CurrentQuestion.Value.Difficulty : 1));
        }

        // Changer l'ordre des joueurs
        GameManager.Singleton.PlayerTurnIndex = (GameManager.Singleton.PlayerTurnIndex + 1) % GameManager.Singleton.Teams.Count;

        // Reinit questions
        // Checker s'il reste des questions dans la caté. Sinon, transformer les cases en une caté inexploitée
        if (NumberOfQuestionsInTheme(CurrentQuestion.Value.Theme) == 0)
            TransformBoard();
        // Supprimer la question current
        QList.Questions.Remove(CurrentQuestion.Value);
        _currentPlayerAnswers.Clear();
        UIManager.S.ShowRightAnswer(-1);

        if (endGame)
        {
            GameManager.Singleton.StartVictory();
            StopCoroutine(DisplayQcmAnswers());
        }

        if (eventTriggered)
        {
            GameManager.Singleton.CurrentState = GameState.ClientEvent;
            eventTriggered = false;
            StartCoroutine(GameManager.Singleton.TransitionQuizToEvent(_firstEventObject, _secondEventObject, _thirdEventObject));
        }

        else
            GameManager.Singleton.CurrentState = GameState.WaitForGameMasterInput;
    }
    
    public IEnumerator AddScoreToTeam(Team team, int score)
    {
        team.Score += score;

        // Déplacer le meeple
        team.Meeple.StartMoving();

        yield return new WaitForSeconds(0.5f);

        // Check if victory
        if (team.Score >= _caseContainer.childCount - 1)
            endGame = true;

        // Check if event triggered
        CheckEvents(team.Score);
    }

    void CheckEvents(int score)
    {
        if (!_firstEvent && score >= _firstEventCase)
        {
            _firstEvent = true;
            eventTriggered = true;
        }
        else if (!_secondEvent && score >= _secondEventCase)
        {
            _secondEvent = true;
            eventTriggered = true;
        }
        else if (!_thirdEvent && score >= _thirdEventCase)
        {
            _thirdEvent = true;
            eventTriggered = true;
        }
    }
    #endregion

    #region Utilities

    void GetListOfQuestionsBySubject()
    {
        // Reinit liste
        _currentQuestionList.Clear();

        // On récupère une liste de Sujets avec le bon sous thème
        List<Question> subThemeQuestions = QList.Questions.Where(x => x.SubTheme == _currentSubTheme).ToList();
        Debug.Log("Nb de questions dans le sous thème " + _currentSubTheme + " : " + subThemeQuestions.Count);

        // On crée la liste de tous les sujets du subtheme
        List<string> subjectsList = new List<string>();
        foreach (Question q in subThemeQuestions)
        {
            if (!subjectsList.Contains(q.Subject))
            {
                subjectsList.Add(q.Subject);
                Debug.Log("Nouveau sujet : " + q.Subject);
            }
        }
        Debug.Log("Nb de sujets :" + subjectsList.Count);

        // Retirer le sujet de la manche précédente si applicable
        if (subjectsList.Contains(_currentSubject) && subjectsList.Count > 1)
            subjectsList.Remove(_currentSubject);

        // Random sur le sujet
        _currentSubject = subjectsList[UnityEngine.Random.Range(0, subjectsList.Count)];
        Debug.Log("sujet choisi : " + _currentSubject);

        // On récupère une liste de Questions avec le bon sujet
        _currentQuestionList = subThemeQuestions.Where(x => x.Subject == _currentSubject).ToList();
        Debug.Log("Nb de question avec ce sujet :" + _currentQuestionList.Count);
    }
    int[] ShuffleAnswers(int[] input)
    {
        int[] answerOrder = input;
        var rng = new System.Random();
        int n = answerOrder.Length;
        while (n > 1)
        {
            int k = rng.Next(n--);
            var temp = answerOrder[n];
            answerOrder[n] = answerOrder[k];
            answerOrder[k] = temp;
        }
        return answerOrder;
    }
    int NumberOfQuestionsInSubtheme(SubTheme sub)
    {
        List<Question> subList = QList.Questions.Where(x => x.SubTheme == sub).ToList();
        return subList.Count;
    }
    int NumberOfQuestionsInTheme(Theme theme)
    {
        List<Question> themeList = QList.Questions.Where(x => x.Theme == theme).ToList();
        return themeList.Count;
    }
    Theme FindUnusedTheme()
    {
        for (int i = 0; i < 7; i++)
        {
            if (!GameManager.Singleton.CurrentThemes.Contains(FindThemeByIndex(i)))
                return FindThemeByIndex(i);
        }
        return Theme.Nature;
    }
    SubTheme FindSubThemeByTheme(Theme theme, bool primaryTheme)
    {
        SubTheme subTheme;

        switch (_currentTheme)
        {
            case Theme.Nature:
                subTheme = SubTheme.Nature;
                break;
            case Theme.Art:
                subTheme = SubTheme.Art;
                break;
            case Theme.Ezio:
                if (primaryTheme)
                    subTheme = SubTheme.Ezio;
                else
                    subTheme = SubTheme.Histoire;
                break;
            case Theme.Hermione:
                if (primaryTheme)
                    subTheme = SubTheme.Hermione;
                else
                    subTheme = SubTheme.Fayotte;
                break;
            case Theme.Mulan:
                if (primaryTheme)
                    subTheme = SubTheme.Mulan;
                else
                    subTheme = SubTheme.Asie;
                break;
            case Theme.Obiwan:
                if (primaryTheme)
                    subTheme = SubTheme.Obiwan;
                else
                    subTheme = SubTheme.Politique;
                break;
            case Theme.Karadoc:
                if (primaryTheme)
                    subTheme = SubTheme.Karadoc;
                else
                    subTheme = SubTheme.Bouffe;
                break;
            default:
                subTheme = SubTheme.Nature;
                break;
        }
        return subTheme;
    }
    public int SubThemeIndex(SubTheme subTheme)
    {
        switch (subTheme)
        {
            case SubTheme.Nature:
                return 0;
            case SubTheme.Art:
                return 1;
            case SubTheme.Ezio:
                return 2;
            case SubTheme.Histoire:
                return 3;
            case SubTheme.Hermione:
                return 4;
            case SubTheme.Fayotte:
                return 5;
            case SubTheme.Mulan:
                return 6;
            case SubTheme.Asie:
                return 7;
            case SubTheme.Obiwan:
                return 8;
            case SubTheme.Politique:
                return 9;
            case SubTheme.Karadoc:
                return 10;
            case SubTheme.Bouffe:
                return 11;
            default:
                return 0;
        }
    }
    public SubTheme FindSubThemeByIndex(int index)
    {
        switch (index)
        {
            case 0:
                return SubTheme.Nature;
            case 1:
                return SubTheme.Art;
            case 2:
                return SubTheme.Ezio;
            case 3:
                return SubTheme.Histoire;
            case 4:
                return SubTheme.Hermione;
            case 5:
                return SubTheme.Fayotte;
            case 6:
                return SubTheme.Mulan;
            case 7:
                return SubTheme.Asie;
            case 8:
                return SubTheme.Obiwan;
            case 9:
                return SubTheme.Politique;
            case 10:
                return SubTheme.Karadoc;
            case 11:
                return SubTheme.Bouffe;
            default:
                return 0;
        }
    }
    public int ThemeIndex(Theme theme)
    {
        switch (theme)
        {
            case Theme.Nature:
                return 0;
            case Theme.Art:
                return 1;
            case Theme.Ezio:
                return 2;
            case Theme.Hermione:
                return 3;
            case Theme.Mulan:
                return 4;
            case Theme.Obiwan:
                return 5;
            case Theme.Karadoc:
                return 6;
            default:
                return 0;
        }
    }

    Theme FindThemeByIndex(int index)
    {
        switch (index)
        {
            case 0:
                return Theme.Nature;
            case 1:
                return Theme.Art;
            case 2:
                return Theme.Ezio;
            case 3:
                return Theme.Hermione;
            case 4:
                return Theme.Mulan;
            case 5:
                return Theme.Obiwan;
            case 6:
                return Theme.Karadoc;
            default:
                return 0;
        }
    }
    void TransformBoard()
    {
        // Récupérer un thème inutilisé
        Theme theme = FindUnusedTheme();
        // Transformer les cases du thème current en des cases du thème inutilisé
        Transform caseContainer = GameObject.FindGameObjectWithTag("CaseContainer").transform;
        for (int i = 0; i < caseContainer.childCount; i++)
        {
            caseContainer.GetChild(i).GetComponent<StandardCase>().SwitchTheme(CurrentQuestion.Value.Theme, theme);
        }
    }
    #endregion
}
