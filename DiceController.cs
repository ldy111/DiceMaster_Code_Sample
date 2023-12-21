using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

public enum CornerDirection
{
    East,
    West,
    South,
    North
}

public enum DiceRollType
{
    Inside,
    OutsideDynamic,
    OutsideStatic,
}

public class LuckDiceResult
{
    public int gold;
    public int dice;
    public int clover;
}

/// <summary>
/// 주사위 초기화, 수 조절, 굴리기, 결과체크, 카메라에 따른 영역체크 등의 역할 수행
/// </summary>
public class DiceController : MonoBehaviour
{
    [Header("Need")]
    [SerializeField] private List<DotDice> diceList;
    [SerializeField] private Transform rollStartPoint;
    [SerializeField] private Transform rollStartPointLuckDice;

    [Header("Setting")]
    [SerializeField] private List<Vector3> cornerPointList; //CornerDirection에 맞출 것!

    [Space(10)]
    [SerializeField] private int maxDiceAmount = 5;
    [SerializeField] private DiceType initDiceType;
    [SerializeField] private DiceRollType diceRollType;
    [SerializeField] private float maxRollCheckTime = 2f;
    
    //카메라를 따라 굴리는 위치 변경 관련 변수
    //rollPoint는 South 방향을 기본으로 설정하고 Offset 한다.
    [SerializeField] private Vector2 rollPointOffsetLerpZero = new Vector3(0f, -10f);
    [SerializeField] private Vector2 rollPointOffsetLerpOne = new Vector3(10f, 0f);

    [SerializeField] private float minForce = 10;
    [SerializeField] private float maxForce = 15;
    [SerializeField] private float luckDiceMinForce = 15;
    [SerializeField] private float luckDiceMaxForce = 20;

    [Header("Info")]
    [ReadOnly] public DiceType currentDiceType;
    [ReadOnly] public float inverseLerpX;
    [ReadOnly] public float inverseLerpY;

    private readonly string sKey_MainDice = "SFX_MainDice";
    private readonly string sKey_LuckDice = "SFX_LuckDice";

    public Action<int> OnRollEndHandler;
    public Action<LuckDiceResult> OnRollEndLuckDiceHandler;
    public Action OnDoubleDiceHandler;



    #region Init
    private void Awake()
    {
        Init();
    }

    private void Init()
    {
        CheckPreparedDice();
        InitDice();
    }

    private void CheckPreparedDice()
    {
        if (diceList.Count < maxDiceAmount)
        {
            while (diceList.Count < maxDiceAmount)
            {
                DotDice dice = Instantiate(diceList[0], transform);
                dice.gameObject.SetActive(false);
                diceList.Add(dice);
            }
        }
    }

    private void InitDice()
    {
        for (int i = 0; i < diceList.Count; i++)
        {
            diceList[i].Init(initDiceType);
        }
    }
    #endregion



    #region Control
    public void SetDicesActive(bool set)
    {
        for (int i = 0; i < diceList.Count; i++)
        {
            diceList[i].gameObject.SetActive(set);
        }
    }

    public void SetActiveDiceAmount(int amount)
    {
        for (int i = 0; i < diceList.Count; i++)
        {
            diceList[i].gameObject.SetActive(i < amount);
        }
    }

    public void SetDiceType(DiceType diceType)
    {
        currentDiceType = diceType;

        for (int i = 0; i < diceList.Count; i++)
        {
            diceList[i].Init(diceType);
        }
    }
    public void Roll()
    {
        SetDicesActive(true);

        StartCoroutine(_Roll());
    }

    public IEnumerator _Roll()
    {
        if (currentDiceType == DiceType.LuckDice)
            SoundManager.singleton.PlaySFX(sKey_LuckDice, 0.5f);
        else
            SoundManager.singleton.PlaySFX(sKey_MainDice, 0.5f);

        // 굴리기
        for (int i = 0; i < diceList.Count; i++)
        {
            if (diceList[i].gameObject.activeInHierarchy == false)
                continue;

            ForceDice(diceList[i]);

            yield return null;
        }

        // 멈추기 or 최대시간 체크
        float checkTime = 0;

        while (checkTime < maxRollCheckTime)
        {
            checkTime += Time.deltaTime;

            if (!CheckDiceMovement())
                break;

            yield return null;
        }

        CheckRollResult();
    }

    private void ForceDice(DotDice dice)
    {
        Vector3 rollStartPosition = GetRollStartPosition();

        // 랜덤 각도
        dice.transform.position = rollStartPosition;
        dice.transform.Rotate(new Vector3(Random.value * 360, Random.value * 360, Random.value * 360));

        Vector3 force = GetForce();

        dice.myRigidbody.isKinematic = false;
        dice.myRigidbody.AddForce(force, ForceMode.Impulse);
        dice.myRigidbody.AddTorque(force, ForceMode.Impulse);
    }

    private Vector3 GetForce()
    {
        if(!(currentDiceType == DiceType.LuckDice))
            return rollStartPoint.forward * Random.Range(minForce, maxForce);
        else
            return rollStartPointLuckDice.forward * Random.Range(luckDiceMinForce, luckDiceMaxForce);
    }

    private Vector3 GetRollStartPosition()
    {
        Vector3 rollStartPosition;

        if(currentDiceType != DiceType.LuckDice)
        {
            if (diceRollType == DiceRollType.Inside)
                rollStartPosition = GetRollPoint();
            else if (diceRollType == DiceRollType.OutsideDynamic)
                rollStartPosition = GetRollPointOutsideDynamic();
            else
                rollStartPosition = GetRollPointOutsideStatic();
        }
        else
        {
            rollStartPosition = GetLuckDiceRollPoint();
        }

        return rollStartPosition;
    }

    private bool CheckDiceMovement()
    {
        bool isMoving = false;

        for (int i = 0; i < diceList.Count; i++)
        {
            if (!isMoving && diceList[i].gameObject.activeInHierarchy && 
                !(diceList[i].myRigidbody.velocity.sqrMagnitude < 0.0001f || diceList[i].myRigidbody.IsSleeping() == true))
                isMoving = true;
        }

        return isMoving;
    }

    private void CheckRollResult()
    {
        for (int i = 0; i < diceList.Count; i++)
        {
            if (diceList[i].gameObject.activeInHierarchy)
                diceList[i].myRigidbody.isKinematic = true;
        }

        if (currentDiceType == DiceType.LuckDice)
        {
            LuckDiceResult result = GetLuckDiceResult();
            OnRollEndLuckDiceHandler?.Invoke(result);
        }
        else
        {
            int totalValue = GetDiceValue();
            OnRollEndHandler?.Invoke(totalValue);
        }
    }
    #endregion



    #region GetPoint & Force
    // 화면 따라서 주사위를 굴리는 코드
    private Vector3 GetRollPoint()
    {
        Vector3 newPosition = rollStartPoint.position;
        Player player = GameSceneManager.singleton.map.player;

        // World Position 좌표계로 계산
        Vector3 south = cornerPointList[(int)CornerDirection.South];
        Vector3 west = cornerPointList[(int)CornerDirection.West];
        inverseLerpX = UtilityManager.singleton.Vector3InverseLerp(south, west, player.transform.position);
        newPosition.x += Mathf.Lerp(rollPointOffsetLerpZero.x, rollPointOffsetLerpOne.x, inverseLerpX);

        Vector3 east = cornerPointList[(int)CornerDirection.East];
        inverseLerpY = UtilityManager.singleton.Vector3InverseLerp(south, east, player.transform.position);
        newPosition.z += Mathf.Lerp(rollPointOffsetLerpZero.y, rollPointOffsetLerpOne.y, inverseLerpY);

        Vector3 randomOffset = new Vector3(Random.Range(-2f, 2f), Random.Range(-2f, 2f), Random.Range(-2f, 2f));
        newPosition += randomOffset;

        return newPosition;
    }

    private Vector3 GetRollPointOutsideDynamic()
    {
        Map map = GameSceneManager.singleton.map;
        int tileSize = map.tileSize.x;

        // 좌측
        if (map.currentTileIndex < (tileSize * 2) - 2)
        {
            Transform minTile = map.tileList[tileSize].movePoint;
            rollPointOffsetLerpZero.x = minTile.position.x - 2f;
            rollPointOffsetLerpOne.x = minTile.position.x;
            rollPointOffsetLerpZero.y = minTile.position.z;

            float maxZ = map.tileList[tileSize + 2].movePoint.position.z;
            rollPointOffsetLerpOne.y = maxZ;
        }
        // 우측
        else
        {
            Transform minTile = map.tileList[(tileSize * 3) - 6].movePoint;
            rollPointOffsetLerpZero.x = minTile.position.x;
            rollPointOffsetLerpZero.y = minTile.position.z;
            rollPointOffsetLerpOne.y = minTile.position.z + 2;

            float maxX = map.tileList[(tileSize * 3) - 4].movePoint.position.z;
            rollPointOffsetLerpOne.x = maxX;
        }

        return GetRollPoint();
    }

    private Vector3 GetRollPointOutsideStatic()
    {
        Vector3 newPosition = rollStartPoint.position;

        float randomX = Random.Range(-1, 1f);
        float randomZ = Random.Range(-1, 1f);

        Vector3 randomOffset = new Vector3(randomX, 0, randomZ);
        newPosition += randomOffset;

        return newPosition;
    }

    private Vector3 GetLuckDiceRollPoint()
    {
        Vector3 newPosition = rollStartPointLuckDice.transform.position;
        Vector3 randomOffset = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f));

        newPosition += randomOffset;

        return newPosition;
    }
    #endregion



    #region Check
    public void CheckRollDiceAmount(Tile currentTile)
    {
        if (!CheckSpecialTile(currentTile))
        {
            if (!CheckBuff())
            {
                if (currentTile != null)
                    SetActiveDiceAmount(2);
            }
        }
    }

    private bool CheckSpecialTile(Tile currentTile)
    {
        if (GameManager.singleton.gameFlowState == GameFlowState.Invasion)
        {
            SetActiveDiceAmount(5);
            return true;
        }
        else if (currentTile.tileType == TileType.LuckyDiceTile)
        {
            SetActiveDiceAmount(3);
            return true;
        }
        else if (currentTile.tileType == TileType.PrisonTile)
        {
            PrisonTile prisonTile = currentTile as PrisonTile;

            if (prisonTile.onPrisoned)
            {
                SetActiveDiceAmount(2);
                return true;
            }
        }

        return false;
    }

    private bool CheckBuff()
    {
        PlayerData playerData = ProgramManager.singleton.saveData.playerData;

        if (playerData.onOneDice || playerData.onTripleDice || playerData.onFiveDice)
        {
            if (playerData.countOddDiceAmount > 0)
            {
                if (playerData.onOneDice)
                    SetActiveDiceAmount(1);
                else if (playerData.onTripleDice)
                    SetActiveDiceAmount(3);
                else if (playerData.onFiveDice)
                    SetActiveDiceAmount(5);

                playerData.CountOddDice();

                return true;
            }
        }

        return false;
    }

    private int GetDiceValue()
    {
        int value = 0;
        int firstDiceValue = diceList[0].value;
        bool onDoubleDice = true;

        for (int i = 0; i < diceList.Count; i++)
        {
            if (diceList[i].gameObject.activeInHierarchy)
            {
                value += diceList[i].value;

                if (onDoubleDice && firstDiceValue != diceList[i].value)
                    onDoubleDice = false; 
            }
        }

        if (onDoubleDice)
            OnDoubleDiceHandler?.Invoke();

        return value;
    }

    private LuckDiceResult GetLuckDiceResult()
    {
        LuckDiceResult result = new LuckDiceResult();

        for (int i = 0; i < diceList.Count; i++)
        {
            if (diceList[i].gameObject.activeInHierarchy)
            {
                if (diceList[i].value == 3 || diceList[i].value == 4)
                    result.gold++;
                else if (diceList[i].value == 1 || diceList[i].value == 6)
                    result.dice++;
                else
                    result.clover++;
            }
        }

        return result;
    }
    #endregion



}

