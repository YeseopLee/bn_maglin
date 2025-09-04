using System;
using System.Collections.Generic;
using UnityEngine;
using Maglin.Cards;
using Maglin.Relics;

namespace Maglin.Core
{
    /// <summary>
    /// 게임 세이브 데이터 구조
    /// </summary>
    [Serializable]
    public class GameSaveData
    {
        [Header("세이브 정보")]
        public string saveVersion = "1.0";
        public string saveDateTime;
        public float playTime;

        [Header("층 진행 정보")]
        public int currentFloor = 1;
        public string currentFloorType = "Start";
        public string gameState = "MainMenu";

        [Header("현재 층 SO 정보")]
        public string currentBattleStageId;
        public string currentBattleId;
        public string currentEventId;

        [Header("플레이어 상태")]
        public PlayerSaveData playerData;

        [Header("게임 설정")]
        public bool debugMode = false;

        public GameSaveData()
        {
            saveDateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            playerData = new PlayerSaveData();
        }

        /// <summary>
        /// 유효한 세이브 데이터인지 확인
        /// </summary>
        public bool IsValid()
        {
            return currentFloor >= 1 &&
                   playerData != null &&
                   !string.IsNullOrEmpty(saveVersion);
        }
    }

    /// <summary>
    /// 플레이어 상태 세이브 데이터
    /// </summary>
    [Serializable]
    public class PlayerSaveData
    {
        [Header("스탯")]
        public int currentHealth = 100;
        public int maxHealth = 100;
        public int currentMana = 30;
        public int maxMana = 30;
        public int currentGold = 0;
        public int manaRecoveryPerTurn = 10;
        public int maxHandSize = 5;

        [Header("덱 정보")]
        public List<string> deckCardIds = new List<string>();
        public List<int> deckCardCounts = new List<int>();

        [Header("유물 정보")]
        public List<string> relicIds = new List<string>();

        [Header("애니메이션 상태")]
        public string currentAnimationState = "Idle";

        public PlayerSaveData()
        {
            // 기본 스타터 덱 설정 (나중에 CardManager에서 실제 스타터 덱으로 초기화)
            deckCardIds = new List<string>();
            deckCardCounts = new List<int>();
            relicIds = new List<string>();
        }

        /// <summary>
        /// 유효한 플레이어 데이터인지 확인
        /// </summary>
        public bool IsValid()
        {
            return maxHealth > 0 &&
                   maxMana >= 0 &&
                   currentHealth >= 0 &&
                   currentMana >= 0 &&
                   currentGold >= 0;
        }
    }

    /// <summary>
    /// 카드 데이터 (덱용)
    /// </summary>
    [Serializable]
    public class CardSaveData
    {
        public string cardId;
        public int count;

        public CardSaveData(string id, int cardCount)
        {
            cardId = id;
            count = cardCount;
        }
    }

    /// <summary>
    /// SO 참조를 위한 ID 매핑 헬퍼
    /// </summary>
    public static class SaveDataHelper
    {
        /// <summary>
        /// FloorType을 문자열로 변환
        /// </summary>
        public static string FloorTypeToString(FloorType floorType)
        {
            return floorType.ToString();
        }

        /// <summary>
        /// 문자열을 FloorType으로 변환
        /// </summary>
        public static FloorType StringToFloorType(string floorTypeString)
        {
            if (Enum.TryParse<FloorType>(floorTypeString, out FloorType result))
            {
                return result;
            }
            return FloorType.Normal;
        }

        /// <summary>
        /// GameState를 문자열로 변환
        /// </summary>
        public static string GameStateToString(GameState gameState)
        {
            return gameState.ToString();
        }

        /// <summary>
        /// 문자열을 GameState로 변환
        /// </summary>
        public static GameState StringToGameState(string gameStateString)
        {
            if (Enum.TryParse<GameState>(gameStateString, out GameState result))
            {
                return result;
            }
            return GameState.MainMenu;
        }

        /// <summary>
        /// ScriptableObject의 이름을 ID로 사용
        /// </summary>
        public static string GetSOId(ScriptableObject so)
        {
            return so != null ? so.name : "";
        }

        /// <summary>
        /// 플레이어 애니메이션 상태를 문자열로 변환
        /// </summary>
        public static string AnimationStateToString(Player.PlayerManager.PlayerAnimationState state)
        {
            return state.ToString();
        }

        /// <summary>
        /// 문자열을 플레이어 애니메이션 상태로 변환
        /// </summary>
        public static Player.PlayerManager.PlayerAnimationState StringToAnimationState(string stateString)
        {
            if (Enum.TryParse<Player.PlayerManager.PlayerAnimationState>(stateString, out var result))
            {
                return result;
            }
            return Player.PlayerManager.PlayerAnimationState.Idle;
        }
    }
}
