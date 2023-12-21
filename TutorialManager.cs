using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ʃ�丮�� ���� Ŭ����
/// �� Ʃ�丮���� Check, Show, End�� ����
/// ���� �þ�� Interface�� ���� �ʿ䰡 ����
/// </summary>
public class TutorialManager : MonoBehaviourSingleton<TutorialManager>
{
    private UIManager uiManager;
    private GamePlayData gamePlayData;
    private Player player;
    private Map map;

    private void Awake()
    {
        InitSingleton(this);
    }

    private void Start()
    {
        InitTutorial();
    }

    private void InitTutorial()
    {
        // ���� �� �ʱ�ȭ
        uiManager = UIManager.singleton;
        gamePlayData = ProgramManager.singleton.saveData.gamePlayData;
        map = GameSceneManager.singleton.map;
        player = GameSceneManager.singleton.map.player;

        if (GameManager.singleton.gameFlowState != GameFlowState.Normal)
            return;

        if (!gamePlayData.GetTutorialCheck(TutorialType.Tutorial_Invasion))
        {
            uiManager.inGameUI.townInvasionButton.gameObject.SetActive(false);
            UIManager.singleton.inGameUI.SetDungeonButton(false);
        }



        // �̺�Ʈ ����
        GameSceneManager.singleton.OnSceneStarted += CheckStartTutorial;
        GameSceneManager.singleton.OnFirstAssignEnded += CheckBoardStartTutorial;

        UIManager.singleton.tileSlotMachineUI.OnSpinEndHandler += CheckTileSymbolTutorial;

        UIManager.singleton.inGameUI.symbolSelectUI.OnShuffleEnded += CheckSelectSymbolTutorial;
        UIManager.singleton.inGameUI.symbolSelectUI.OnCloseUIHandler += CheckEndTutorial;

        StartTile startTile = (StartTile)map.tileList.Find(x => x.tileType == TileType.StartTile);
        startTile.OnStartTileFlow += CheckInvasionTutorial;
    }



    #region GameStartTutorial
    private void CheckStartTutorial()
    {
        if (!gamePlayData.GetTutorialCheck(TutorialType.Tutorial_GameStart))
            StartCoroutine(_ShowStartTutorial());
        else
        {
            // Tutorial_GameStart �� Tutorial_BoardStart�� �����ϱ� �ʰ� ������ �ϴ� ���
            CheckBoardStartTutorial();
        }
    }

    private IEnumerator _ShowStartTutorial()
    {
        yield return new WaitForEndOfFrame();

        uiManager.SetInput(false);

        yield return new WaitForSeconds(2f);

        uiManager.SetInput(true);

        uiManager.tutorialUI.OnNextTutorialText += ShowTileSlotMachineUI;
        uiManager.tutorialUI.Show(TutorialType.Tutorial_GameStart);

        void ShowTileSlotMachineUI(int index)
        {
            if(index == 1)
            {
                uiManager.tutorialUI.OnNextTutorialText -= ShowTileSlotMachineUI;

                uiManager.tileSlotMachineUI.OnOkButtonClickedHandler += EndStartTutorial;
                uiManager.tileSlotMachineUI.gameObject.SetActive(true);
            }
        }
    }

    private void EndStartTutorial(List<MinimapTile> resultTileList)
    {
        gamePlayData.SetTutorialCheck(TutorialType.Tutorial_GameStart, true);
        ProgramManager.singleton.SaveData();

        GameSceneManager.singleton.StartFirstAssgin(resultTileList);
    }
    #endregion



    #region TileSymbolTutorial
    private void CheckTileSymbolTutorial()
    {
        if (!gamePlayData.GetTutorialCheck(TutorialType.Tutorial_TileSymbol))
            StartCoroutine(_ShowTileSymbolTutorial());
    }

    private IEnumerator _ShowTileSymbolTutorial()
    {
        yield return new WaitForEndOfFrame();

        uiManager.SetInput(false);

        yield return new WaitForSeconds(1.5f);

        uiManager.SetInput(true);

        uiManager.tutorialUI.Show(TutorialType.Tutorial_TileSymbol);    
        uiManager.tutorialUI.SetTutorialImage(TutorialImage.TileExplain);
        
        uiManager.tutorialUI.OnNextTutorialText += ShowTileExplain;
        uiManager.tutorialUI.OnTutorialTextEnd += EndTileSymbolTutorial;

        void ShowTileExplain(int index)
        {
            if(index == 1)
            {
                uiManager.tutorialUI.OnNextTutorialText -= ShowTileExplain;

                uiManager.tutorialUI.SetTutorialImage(TutorialImage.SymbolExplain);
            }
        }
    }

    private void EndTileSymbolTutorial()
    {
        uiManager.tutorialUI.SetTutorialImage(TutorialImage.None);

        gamePlayData.SetTutorialCheck(TutorialType.Tutorial_TileSymbol, true);
        ProgramManager.singleton.SaveData();
    }
    #endregion



    #region BoardStartTutorial
    private void CheckBoardStartTutorial()
    {
        if (!gamePlayData.GetTutorialCheck(TutorialType.Tutorial_BoardStart))
            StartCoroutine(_ShowBoardStartTutorial());
    }

    private IEnumerator _ShowBoardStartTutorial()
    {
        yield return new WaitForEndOfFrame();

        uiManager.SetInput(false);

        yield return new WaitForSeconds(1f);

        uiManager.SetInput(true);

        uiManager.tutorialUI.OnTutorialTextEnd += EndBoardStartTutorial;
        uiManager.tutorialUI.Show(TutorialType.Tutorial_BoardStart);
    }

    private void EndBoardStartTutorial()
    {
        uiManager.tutorialUI.OnTutorialTextEnd -= EndBoardStartTutorial;

        gamePlayData.SetTutorialCheck(TutorialType.Tutorial_BoardStart, true);
        ProgramManager.singleton.SaveData();
    }
    #endregion



    #region SelectSymbolTutorial
    private void CheckSelectSymbolTutorial()
    {
        if (!gamePlayData.GetTutorialCheck(TutorialType.Tutorial_SelectSymbol))
            StartCoroutine(_ShowSelectSymbolTutorial());
    }

    private IEnumerator _ShowSelectSymbolTutorial()
    {
        yield return new WaitForEndOfFrame();

        uiManager.SetInput(false);

        yield return new WaitForSeconds(0.5f);

        uiManager.SetInput(true);

        uiManager.tutorialUI.Show(TutorialType.Tutorial_SelectSymbol);

        uiManager.tutorialUI.OnNextTutorialText += ShowLineExplain;
        uiManager.tutorialUI.OnTutorialTextEnd += EndSelectSymbolTutorial;
        
        void ShowLineExplain(int index)
        {
            if(index == 1)
            {
                uiManager.tutorialUI.OnNextTutorialText -= ShowLineExplain;

                uiManager.tutorialUI.SetTutorialImage(TutorialImage.LineExplain);
            }
        }
    }

    private void EndSelectSymbolTutorial()
    {
        uiManager.tutorialUI.OnTutorialTextEnd -= EndSelectSymbolTutorial;

        gamePlayData.SetTutorialCheck(TutorialType.Tutorial_SelectSymbol, true);
        ProgramManager.singleton.SaveData();
    }
    #endregion



    #region InvasionTutorial
    private void CheckInvasionTutorial()
    {
        if (!gamePlayData.GetTutorialCheck(TutorialType.Tutorial_Invasion))
            StartCoroutine(_ShowInvasionTutorial());
    }

    private IEnumerator _ShowInvasionTutorial()
    {
        UIManager.singleton.inGameUI.townInvasionButton.gameObject.SetActive(true);
        UIManager.singleton.inGameUI.SetDungeonButton(true);

        uiManager.SetInput(false);

        yield return new WaitForSeconds(map.spawnMonsterDelay);

        Time.timeScale = 0;

        uiManager.SetInput(true);

        uiManager.tutorialUI.Show(TutorialType.Tutorial_Invasion);

        uiManager.tutorialUI.OnNextTutorialText += ShowInvasionExplane;
        uiManager.tutorialUI.OnTutorialTextEnd += EndInvasionTutorial;

        void ShowInvasionExplane(int index)
        {
            if (index == 1)
            {
                uiManager.tutorialUI.SetTutorialImage(TutorialImage.InvasionHole);
            }

            if (index == 3)
            {
                uiManager.tutorialUI.OnNextTutorialText -= ShowInvasionExplane;

                uiManager.tutorialUI.SetTutorialImage(TutorialImage.InvasionExplain);
            }
        }
    }

    private void EndInvasionTutorial()
    {
        uiManager.tutorialUI.OnTutorialTextEnd -= EndInvasionTutorial;
        uiManager.tutorialUI.SetTutorialImage(TutorialImage.None);

        gamePlayData.SetTutorialCheck(TutorialType.Tutorial_Invasion, true);
        ProgramManager.singleton.SaveData();

        Time.timeScale = 1;

        CheckEndTutorial();
    }
    #endregion



    #region EndTutorial
    private void CheckEndTutorial()
    {
        // End ���� �� �߰�!

        if (!gamePlayData.GetTutorialCheck(TutorialType.Tutorial_End) &&
            gamePlayData.CheckEndAllTutorial())
        {
            StartCoroutine(_ShowEndTutorial());
        }
    }

    private IEnumerator _ShowEndTutorial()
    {
        uiManager.SetInput(false);

        yield return new WaitForSeconds(0.3f);

        Time.timeScale = 0;

        uiManager.SetInput(true);

        uiManager.tutorialUI.OnTutorialTextEnd += EndEndTutorial;
        uiManager.tutorialUI.Show(TutorialType.Tutorial_End);
    }

    private void EndEndTutorial()
    {
        uiManager.tutorialUI.OnTutorialTextEnd -= EndEndTutorial;

        gamePlayData.SetTutorialCheck(TutorialType.Tutorial_End, true);
        ProgramManager.singleton.SaveData();

        Time.timeScale = 1;
    }
    #endregion



}
