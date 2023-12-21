using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Symbol;
using TMPro;
using UnityEngine.UI;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine.UIElements;

/// <summary>
/// 미니맵 표시, 심볼 슬롯머신, 라인 업그레이드, 심볼 설명창, 심볼 구매 등 기능
/// </summary>
public class SymbolSelectUI : MonoBehaviour
{
    [Header("Need_Top")]
    [SerializeField] private List<MinimapTile> minimapTileList;  //Map.tileList와 맞출 것!
    [SerializeField] private LineInfoUI lineInfoUI;

    [Header("Need_Body")]
    [SerializeField] private TextMeshProUGUI selectTitle;
    [SerializeField] private List<SymbolSlot> symbolSlotList;
    [SerializeField] private SymbolInformationRow symbolInformationRow;

    [Header("Need_Bottom")]
    [SerializeField] private GameObject skipButtonParent;
    [SerializeField] private CurrencyButton buyButton;
    [SerializeField] private CurrencyButton rerollButton;
    [SerializeField] private CurrencyButton tileUpgradeButton;

    [Space(10)]
    [SerializeField] private SymbolProbabilityUI symbolProbabilityUI;

    [Space(10)]
    [SerializeField] private GameObject shuffleBlock;

    private SymbolSlot selectedSymbolSlot;
    private int rerollCount;

    private readonly string sKey_SymbolTurn = "SFX_SymbolTurn";

    public Action OnMinimapButtonClickedHandler;
    public Action OnSymbolBookButtonClickedHandler;

    public Action OnShuffleEnded;

    public Action<SymbolPreData> OnBuyHandler;
    public Action OnCloseUIHandler; //Skip, Buy 모두 포함



    #region Init
    private void Start()
    {
        Init();
    }

    private void Init()
    {
        for (int i = 0; i < minimapTileList.Count; i++)
        {
            minimapTileList[i].OnButtonClickedHandler += OnMiniMapTileClicked;
        }

        for (int i = 0; i < symbolSlotList.Count; i++)
        {
            symbolSlotList[i].SetActiveButton(true);
            symbolSlotList[i].OnSlotClickedHandler += OnSlotClicked;
        }

        lineInfoUI.OnInfoButtonClickedHandler += ShowSymbolProbabilityUI;
        symbolInformationRow.OnSearchComboSymbolsOnBoard += HighlightMinimapTile;

        buyButton.OnButtonClickedHandler += OnBuyButtonClicked;
        rerollButton.OnButtonClickedHandler += OnRerollButtonClicked;
        tileUpgradeButton.OnButtonClickedHandler += OnUpgradeButtonClicked;
    }

    public void ShowUI(Tile tile)
    {
        gameObject.SetActive(true);

        rerollCount = 0;

        InitTopUI(tile);
        InitBodyUI();
        UpdateBottomUI();
    }


    private void InitTopUI(Tile tile)
    {
        Map map = GameSceneManager.singleton.map;

        for (int i = 0; i < minimapTileList.Count; i++)
        {
            minimapTileList[i].Init(map.tileList[i]);

            if (minimapTileList[i].ownedTile == map.currentTile)
                minimapTileList[i].SetBlinkAnimation(true);
            else
                minimapTileList[i].SetBlinkAnimation(false);
        }

        lineInfoUI.Init(tile);
        UtilityManager.singleton.DelayFunc(lineInfoUI.Shine, 0.1f);
    }

    private void InitBodyUI()
    {
        StartCoroutine(ShuffleSymbolSlots());

        symbolInformationRow.gameObject.SetActive(false);
        symbolInformationRow.InitEmpty();

        ReleaseSelectedSymbolSlot();

        SoundManager.singleton.PlaySFX(sKey_SymbolTurn, 0.5f);
    }

    private void UpdateBottomUI()
    {
        if (selectedSymbolSlot == null)
            buyButton.button.interactable = false;
        else
            buyButton.button.interactable = true;

        UpdateBuyButton();
        UpdateRerollButton();
        UpdateTileUpgradeButton();
    }

    private void UpdateBuyButton()
    {
        CurrencyData currencyData = ProgramManager.singleton.saveData.currencyData;

        if (selectedSymbolSlot != null)
        {
            int buyNeedGold = GameCalculator.GetSymbolPrice(selectedSymbolSlot.symbolPreData.grade);
            
            buyButton.Init(buyNeedGold);

            if (currencyData.gold >= buyNeedGold)
                buyButton.SetAvailableColor(true);
            else
                buyButton.SetAvailableColor(false);
        }
        else
        {
            buyButton.Init(0);
            buyButton.SetAvailableColor(true);
        }
    }

    private void UpdateRerollButton()
    {
        CurrencyData currencyData = ProgramManager.singleton.saveData.currencyData;

        int needRerollTicket = rerollCount + 1;

        rerollButton.Init(currencyData.reroll, needRerollTicket);

        if (currencyData.reroll >= needRerollTicket)
            rerollButton.SetAvailableColor(true);
        else
            rerollButton.SetAvailableColor(false);
    }

    private void UpdateTileUpgradeButton()
    {
        CurrencyData currencyData = ProgramManager.singleton.saveData.currencyData;
        LineLevelDataTable lineLevelDataTable = DataManager.singleton.backgroundDB.lineLevelDataTable;

        int lineLevel = lineInfoUI.lineData.level;
        int upgradeNeedGold = lineLevelDataTable.GetNeedGold(lineLevel);

        if(lineLevel == 10)
            tileUpgradeButton.InitMax();
        else
        {
            tileUpgradeButton.Init(upgradeNeedGold);

            if (currencyData.gold >= upgradeNeedGold)
            {
                tileUpgradeButton.SetAvailableColor(true);
                tileUpgradeButton.SetHighlight(true);
            }
            else
            {
                tileUpgradeButton.SetAvailableColor(false);
                tileUpgradeButton.SetHighlight(false);
            }
        }
    }
    #endregion



    #region Control
    private void SelectSymbolSlot(SymbolSlot slot)
    {
        selectedSymbolSlot = slot;

        for (int i = 0; i < symbolSlotList.Count; i++)
        {
            symbolSlotList[i].SetSelect(false);
        }

        slot.SetSelect(true);
    }

    private void ReleaseSelectedSymbolSlot()
    {
        if (selectedSymbolSlot != null)
        {
            selectedSymbolSlot.SetSelect(false);
            selectedSymbolSlot = null;
        }
    }

    private IEnumerator ShuffleSymbolSlots()
    {
        int count = UnityEngine.Random.Range(25, 27);

        int firstStopCount = count - 10;
        int secondStopCount = count - 5;

        float time = 0.02f;

        float totalTime = 0;

        UIManager.singleton.SetInput(false);
        selectTitle.gameObject.SetActive(false);
        shuffleBlock.gameObject.SetActive(true);

        for (int j = 0; j < count; j++)
        {
            SetSymbolSlots(j, firstStopCount, secondStopCount);

            yield return new WaitForSeconds(time);

            totalTime += time;

            if (j > 10)
                time += 0.02f;
        }

        Debug.Log("totalTime : " + (totalTime));

        CheckHighGrade();

        UIManager.singleton.SetInput(true);
        selectTitle.gameObject.SetActive(true);
        shuffleBlock.gameObject.SetActive(false);

        OnShuffleEnded?.Invoke();
    }

    private void SetSymbolSlots(int count, int firstStopCount, int secondStopCount)
    {
        List<SymbolData> exceptionList = new List<SymbolData>();

        for (int i = 0; i < symbolSlotList.Count; i++)
        {
            if (i == 0 && count > firstStopCount)
            {
                CheckSymbolData(exceptionList, symbolSlotList[i]);
                continue;
            }

            if (i == 1 && count > secondStopCount)
            {
                CheckSymbolData(exceptionList, symbolSlotList[i]);
                continue;
            }
                
            SymbolGrade grade = GameCalculator.GetRandomSymbolGrade(lineInfoUI.lineData.level);
            SymbolData data = DataManager.singleton.symbolDB.GetRandomSymbolData(grade, exceptionList);

            symbolSlotList[i].Init(data.symbolPreData);
        }
    }

    private void CheckHighGrade()
    {
        for (int i = 0; i < symbolSlotList.Count; i++)
        {
            if((int)symbolSlotList[i].symbolPreData.grade >= (int)SymbolGrade.Unique)
            {
                symbolSlotList[i].ShowHighGradeEffect();
            }
        }
    }

    private void CheckSymbolData(List<SymbolData> dataList, SymbolSlot symbolSlot)
    {
        SymbolData data = DataManager.singleton.symbolDB.GetSymbolData(symbolSlot.symbolPreData.uniquekey);

        if (!dataList.Contains(data))
            dataList.Add(data);
    }

    private void UpdateSymbolSlotInformation(SymbolPreData symbolPreData, Tile selectedTile = null)
    {
        symbolInformationRow.gameObject.SetActive(true);

        if (symbolPreData != null)
            symbolInformationRow.Init(symbolPreData, selectedTile);
        else
            symbolInformationRow.InitEmpty();
    }
    #endregion



    #region Events
    private void OnSlotClicked(SymbolSlot slot)
    {
        if (selectedSymbolSlot != slot)
        {
            SelectSymbolSlot(slot);
            UpdateSymbolSlotInformation(slot.symbolPreData);
            UpdateBottomUI();
        }
        else
        {
            ReleaseSelectedSymbolSlot();
            UpdateSymbolSlotInformation(null);
            UpdateBottomUI();
        }
    }

    public void OnSymbolSlotListClicked(SymbolSlot symbolSlot)
    {
        ReleaseSelectedSymbolSlot();
        UpdateSymbolSlotInformation(symbolSlot.symbolPreData);
        UpdateBottomUI();
    }

    public void OnMiniMapTileClicked(MinimapTile minimapTile)
    {
        ReleaseSelectedSymbolSlot();
        UpdateSymbolSlotInformation(minimapTile.symbolData.symbolPreData, minimapTile.ownedTile);
        UpdateBottomUI();
    }

    private void HighlightMinimapTile(List<Tile> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            MinimapTile minimapTile = minimapTileList.Find(x => x.ownedTile == list[i]);
            minimapTile.ShowRedHighlight();
        }
    }

    public void OnSkipButtonClicked()
    {
        for (int i = 0; i < symbolSlotList.Count; i++)
        {
            symbolSlotList[i].HideHighGradeEffect();
        }

        gameObject.SetActive(false);

        OnCloseUIHandler?.Invoke();

        SoundManager.singleton.StopSFX(sKey_SymbolTurn);

        SoundManager.singleton.PlaySFX("UI_Button");
    }

    public void OnSymbolBookButtonClicked()
    {
        OnSymbolBookButtonClickedHandler?.Invoke();

        SoundManager.singleton.PlaySFX("UI_Button");
    }

    private void OnBuyButtonClicked(CurrencyButton button)
    {
        CurrencyData currencyData = ProgramManager.singleton.saveData.currencyData;

        int needGold = GameCalculator.GetSymbolPrice(selectedSymbolSlot.symbolPreData.grade);

        if (currencyData.gold >= needGold)
        {
            currencyData.UseGold(needGold);
            ProgramManager.singleton.SaveData();

            gameObject.SetActive(false);
            OnBuyHandler?.Invoke(selectedSymbolSlot.symbolPreData);
            OnCloseUIHandler?.Invoke();

            SoundManager.singleton.StopSFX(sKey_SymbolTurn);
        }
        else
        {
            button.Fail();
        }

        SoundManager.singleton.PlaySFX("UI_Button");
    }

    private void OnRerollButtonClicked(CurrencyButton button)
    {
        CurrencyData currencyData = ProgramManager.singleton.saveData.currencyData;

        int needRerollTicket = rerollCount + 1;

        if (currencyData.reroll >= needRerollTicket)
        {
            currencyData.reroll -= needRerollTicket;
            rerollCount += 1;

            InitBodyUI();

            ProgramManager.singleton.SaveData();

            UpdateBottomUI();
        }
        else
        {
            button.Fail();
        }

        SoundManager.singleton.PlaySFX("UI_Button");
    }

    private void OnUpgradeButtonClicked(CurrencyButton button)
    {
        if (lineInfoUI.lineData.level == 10)
            return;

        CurrencyData currencyData = ProgramManager.singleton.saveData.currencyData;
        LineLevelDataTable lineLevelDataTable = DataManager.singleton.backgroundDB.lineLevelDataTable;

        int lineLevel = lineInfoUI.lineData.level;
        int needGold = lineLevelDataTable.GetNeedGold(lineLevel);

        if (currencyData.gold >= needGold)
        {
            currencyData.UseGold(needGold);
            lineInfoUI.Upgrade();

            ProgramManager.singleton.SaveData();

            ShowHighlightLineTile();

            UpdateBottomUI();
        }
        else
        {
            button.Fail();
        }
    }

    private void ShowHighlightLineTile()
    {
        List<MinimapTile> lineTileList = minimapTileList.FindAll(x => x.ownedTile.tileColor == lineInfoUI.lineData.color);

        for (int i = 0; i < lineTileList.Count; i++)
        {
            lineTileList[i].ShowWhiteHighlight();
        }
    }

    private void ShowSymbolProbabilityUI()
    {
        symbolProbabilityUI.gameObject.SetActive(true);
        symbolProbabilityUI.Init(lineInfoUI.lineData.level);
    }
    #endregion



}
