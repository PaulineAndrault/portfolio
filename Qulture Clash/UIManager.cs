using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class UIManager : NetworkBehaviour
{
    #region Vars
    [Header("UI Server Panels")]
    public GameObject BlindTestPanel;
    public GameObject QuestionsPanel;
    public GameObject ThemeChoicePanel;
    
    public GameObject VictoryPanel;
    public TMP_Text VictoryText;
    public TMP_Text VictoryTeamText;

    [Header("Question Panel")]
    [Header("Elements in Question Panel")]
    public GameObject SelectDifficultyPanel;
    public GameObject QuestionScreen;
    public ListOfSprites QuestionBackground;
    public DifficultyButton[] DifficultyButtons;
    public Image[] AnswerButtons;
    public Sprite NormalAnswerButton;
    public Sprite RightAnswerButton;

    [Header("Texts in Question Panel / Select Difficulty")]
    public TMP_Text TeamTurn;
    public TMP_Text SubTheme;
    public TMP_Text Subject;
    [Header("Texts in Question Panel / Question Screen")]
    public TMP_Text SubThemeQuestionScreen;
    public TMP_Text Intitule;
    public TMP_Text[] Answers;

    [Header("Blind Test Panel")]
    public ListOfSprites BackgroundBlindTest;
    public GameObject[] RulesPanel;
    public GameObject ExtraitPanel;

    [Header("UI Client Panels")]
    public GameObject ClientBlindTestPanel;
    public GameObject ClientQuestionsPanel;
    public GameObject ClientVictoryPanel;

    [Header("Elements in Client Background")]
    public TMP_Text ClientTeamName;
    public Image ClientBackgroundColor;

    [Header("Texts in Client Question Panel")]
    public TMP_Text ClientIntitule;
    public GameObject[] ClientAnswerButtons;

    #endregion

    #region Singleton
    public static UIManager S;
    private void Awake()
    {
        if (S == null)
        {
            S = this;
        }
        else
            Destroy(gameObject);
    }
    #endregion

    #region Init
    private void Start()
    {
        if(NetworkManager.IsServer)
            DisplayUIElement(BlindTestPanel);

        else if(NetworkManager.IsClient)
        {
            ChangeText(ClientTeamName, "Team " + GameManager.Singleton.TeamName);
            Debug.Log($"Meeple color count {GameManager.Singleton.MeepleColors.Count}");
            Debug.Log($"ID {GameManager.Singleton.TeamId}");
            Debug.Log($"index {(int)GameManager.Singleton.TeamId - 1}");
            ClientBackgroundColor.color = GameManager.Singleton.MeepleColors[((int)GameManager.Singleton.TeamId - 1)];
            ClientBackgroundColor.color = new Color(ClientBackgroundColor.color.r, ClientBackgroundColor.color.g, ClientBackgroundColor.color.b, 0.92f);
        }
    }

    [ClientRpc]
    public void ReinitUIAfterReconnexionClientRpc(ulong reconnectingPlayerId, ulong teamId, string teamName)
    {
        if (NetworkManager.Singleton.LocalClientId != reconnectingPlayerId) return;
        ChangeText(ClientTeamName, "Team " + teamName);
        ClientBackgroundColor.color = GameManager.Singleton.MeepleColors[((int)teamId - 1)];
        ClientBackgroundColor.color = new Color(ClientBackgroundColor.color.r, ClientBackgroundColor.color.g, ClientBackgroundColor.color.b, 0.92f);
    }

    #endregion

    #region Utilities (server and clients)

    public void DisplayUIElement(GameObject element, bool active = true)
    {
        if (element != null)
            element.SetActive(active);
    }

    public void ChangeText(TMP_Text text, string newText)
    {
        if (text != null)
            text.text = newText;
    }

    #endregion

    #region Client UI Transitions

    [ClientRpc]
    public void EndBlindTestClientRpc()
    {
        DisplayUIElement(ClientBlindTestPanel, false);
    }

    [ClientRpc]
    public void EndQuestionClientRpc()
    {
        Debug.Log("ui client rpc");
        DisplayUIElement(ClientQuestionsPanel, false);
    }

    [ClientRpc]
    public void ShowRightAnswerClientRpc(int index)
    {
        if(index == -1)     // Reinit
        {
            for (int i = 0; i < ClientAnswerButtons.Length; i++)
                ClientAnswerButtons[i].SetActive(true);
        }
        else
        {
            for (int i = 0; i < ClientAnswerButtons.Length; i++)
                ClientAnswerButtons[i].SetActive(i == index ? true : false);
        }
    }

    [ClientRpc]
    void VictoryClientRpc()
    {
        DisplayUIElement(ClientQuestionsPanel, false);
        DisplayUIElement(ClientVictoryPanel);
    }
    #endregion

    #region Server UI Transitions

    public void UpdateSelectDifficultyPanel(string teamName, SubTheme subTheme, string subject)
    {
        // Update Team turn
        ChangeText(TeamTurn, $"Tour de {teamName}");

        // Update Theme and Subject
        ChangeText(SubTheme, subTheme.ToString());
        ChangeText(Subject, subject);

        // Update difficulty buttons status
        foreach (DifficultyButton btn in DifficultyButtons)
            btn.CheckDifficultyAvailability();

        // Open SelectDifficultyPanel
        DisplayUIElement(SelectDifficultyPanel);
        DisplayUIElement(QuestionsPanel);
        DisplayUIElement(BlindTestPanel, false);
    }

    public void UpdateQuestionScreen(Question q, int[] answerOrder, int themeIndex)
    {
        QuestionBackground.ChangeSprite(themeIndex);
        ChangeText(SubThemeQuestionScreen, $"Question {q.SubTheme}");
        ChangeText(Intitule, q.QuestionIntitule);

        int j = 0;
        for (int i = 0; i < answerOrder.Length; i++)
        {
            if (answerOrder[i] == 0)
            {
                ChangeText(Answers[i], q.RightAnswer);
                j++;
            }
            else
                ChangeText(Answers[i], q.FalseAnswers[answerOrder[i] - 1]);
        }

        DisplayUIElement(QuestionScreen);
        DisplayUIElement(SelectDifficultyPanel, false);
    }

    public void ShowRightAnswer(int index)
    {
        for (int i = 0; i < AnswerButtons.Length; i++)
            AnswerButtons[i].sprite = i == index ? RightAnswerButton : NormalAnswerButton;

        ShowRightAnswerClientRpc(index);
    }

    public void UpdateBlindTestScreen(int eventIndex)
    {
        // change background of panel
        BackgroundBlindTest.ChangeSprite(eventIndex);

        // activate right Rules panel
        DisplayUIElement(RulesPanel[eventIndex]);

        DisplayUIElement(BlindTestPanel);
        DisplayUIElement(QuestionsPanel, false);
    }

    public void Victory()
    {
        DisplayUIElement(QuestionsPanel, false);
        DisplayUIElement(VictoryPanel);
        VictoryClientRpc();
    }
    
    #endregion
}
