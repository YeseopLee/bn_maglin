// using UnityEngine;
// using UnityEditor;
// using System.IO;
// using System.Reflection;
// using Maglin.Cards;
// using Maglin.Enemy;
// using Maglin.Battle;

// namespace Maglin.Editor
// {
//     /// <summary>
//     /// 테스트용 ScriptableObject 데이터를 생성하는 에디터 도구
//     /// </summary>
//     public class TestDataGenerator : EditorWindow
//     {
//         [MenuItem("Tools/Generate Test Data")]
//         public static void ShowWindow()
//         {
//             GetWindow<TestDataGenerator>("Test Data Generator");
//         }

//         private void OnGUI()
//         {
//             GUILayout.Label("테스트 데이터 생성 도구", EditorStyles.boldLabel);

//             EditorGUILayout.Space();

//             if (GUILayout.Button("기본 카드 5장 생성", GUILayout.Height(30)))
//             {
//                 GenerateTestCards();
//             }

//             if (GUILayout.Button("기본 몬스터 1마리 생성", GUILayout.Height(30)))
//             {
//                 GenerateTestMonster();
//             }

//             if (GUILayout.Button("전투 스테이지 생성", GUILayout.Height(30)))
//             {
//                 GenerateTestBattleStage();
//             }

//             if (GUILayout.Button("전투 SO 생성", GUILayout.Height(30)))
//             {
//                 GenerateTestBattle();
//             }

//             EditorGUILayout.Space();

//             if (GUILayout.Button("모든 테스트 데이터 생성", GUILayout.Height(40)))
//             {
//                 GenerateAllTestData();
//             }

//             EditorGUILayout.Space();
//             EditorGUILayout.HelpBox("턴제 전투 테스트를 위한 기본 데이터를 생성합니다.", MessageType.Info);
//         }

//         /// <summary>
//         /// 모든 테스트 데이터 생성
//         /// </summary>
//         private void GenerateAllTestData()
//         {
//             GenerateTestCards();
//             GenerateTestMonster();
//             GenerateTestBattleStage();
//             GenerateTestBattle();

//             Debug.Log("[TestDataGenerator] 모든 테스트 데이터 생성 완료!");
//         }

//         /// <summary>
//         /// 테스트 카드 5장 생성
//         /// </summary>
//         private void GenerateTestCards()
//         {
//             // 디렉토리 생성
//             string cardPath = "Assets/ScriptableObjects/Cards";
//             Directory.CreateDirectory(cardPath);

//             // 카드 1: 파이어볼
//             var fireball = CreateInstance<CardSO>();
//             fireball.CardName = "파이어볼";
//             fireball.Description = "적에게 화염 피해를 입힙니다.";
//             fireball.ManaCost = 2;
//             fireball.Type = CardType.Attack;
//             fireball.Element = ElementType.Fire;
//             fireball.AttackPower = 15;
//             fireball.TargetType = TargetType.SingleEnemy;

//             AssetDatabase.CreateAsset(fireball, $"{cardPath}/Fireball.asset");

//             // 카드 2: 아이스 샤드
//             var iceSherd = CreateInstance<CardSO>();
//             iceSherd.CardName = "아이스 샤드";
//             iceSherd.Description = "적에게 얼음 피해를 입히고 느려지게 합니다.";
//             iceSherd.ManaCost = 2;
//             iceSherd.Type = CardType.Attack;
//             iceSherd.Element = ElementType.Water;
//             iceSherd.AttackPower = 12;
//             iceSherd.TargetType = TargetType.SingleEnemy;

//             AssetDatabase.CreateAsset(iceSherd, $"{cardPath}/IceSherd.asset");

//             // 카드 3: 힐링 포션
//             var healingPotion = CreateInstance<CardSO>();
//             healingPotion.CardName = "힐링 포션";
//             healingPotion.Description = "체력을 회복합니다.";
//             healingPotion.ManaCost = 1;
//             healingPotion.Type = CardType.Heal;
//             healingPotion.Element = ElementType.Earth;
//             healingPotion.HealAmount = 20;
//             healingPotion.TargetType = TargetType.Self;

//             AssetDatabase.CreateAsset(healingPotion, $"{cardPath}/HealingPotion.asset");

//             // 카드 4: 바람 칼날
//             var windBlade = CreateInstance<CardSO>();
//             windBlade.CardName = "바람 칼날";
//             windBlade.Description = "적을 관통하는 바람 공격입니다.";
//             windBlade.ManaCost = 3;
//             windBlade.Type = CardType.Attack;
//             windBlade.Element = ElementType.Wind;
//             windBlade.AttackPower = 18;
//             windBlade.TargetType = TargetType.AllEnemies;

//             AssetDatabase.CreateAsset(windBlade, $"{cardPath}/WindBlade.asset");

//             // 카드 5: 전기 충격
//             var lightningBolt = CreateInstance<CardSO>();
//             lightningBolt.CardName = "전기 충격";
//             lightningBolt.Description = "적에게 전기 피해를 입힙니다.";
//             lightningBolt.ManaCost = 2;
//             lightningBolt.Type = CardType.Attack;
//             lightningBolt.Element = ElementType.Lightning;
//             lightningBolt.AttackPower = 16;
//             lightningBolt.TargetType = TargetType.SingleEnemy;

//             AssetDatabase.CreateAsset(lightningBolt, $"{cardPath}/LightningBolt.asset");

//             AssetDatabase.SaveAssets();
//             AssetDatabase.Refresh();

//             Debug.Log("[TestDataGenerator] 테스트 카드 5장 생성 완료!");
//         }

//         /// <summary>
//         /// 테스트 몬스터 생성
//         /// </summary>
//         private void GenerateTestMonster()
//         {
//             // 디렉토리 생성
//             string monsterPath = "Assets/ScriptableObjects/Enemies";
//             Directory.CreateDirectory(monsterPath);

//             // 고블린 몬스터
//             var goblin = CreateInstance<EnemySO>();
//             goblin.EnemyName = "고블린";
//             goblin.MaxHealth = 50;
//             goblin.AttackPower = 8;
//             goblin.Element = ElementType.Earth;
//             goblin.MovementType = MovementType.Forward;
//             goblin.AttackType = AttackType.Melee;
//             goblin.AttackRange = 1;
//             goblin.MovementRange = 1;
//             goblin.WidthSize = 1;
//             goblin.HeightSize = 1;
//             goblin.ThinkingTime = 1.0f;

//             AssetDatabase.CreateAsset(goblin, $"{monsterPath}/Goblin.asset");

//             AssetDatabase.SaveAssets();
//             AssetDatabase.Refresh();

//             Debug.Log("[TestDataGenerator] 테스트 몬스터 생성 완료!");
//         }

//         /// <summary>
//         /// 테스트 전투 스테이지 생성
//         /// </summary>
//         private void GenerateTestBattleStage()
//         {
//             // 디렉토리 생성
//             string stagePath = "Assets/ScriptableObjects/BattleStage";
//             Directory.CreateDirectory(stagePath);

//             // 1층 전투 스테이지
//             var floor1Stage = CreateInstance<BattleStageSO>();
//             floor1Stage.FloorNumber = 1;
//             floor1Stage.StageName = "1층 - 초보자 구역";
//             floor1Stage.StageDescription = "초보 모험가들을 위한 쉬운 전투 구역입니다.";

//             // 가능한 전투들 배열 생성 (나중에 BattleSO 생성 후 연결)
//             floor1Stage.PossibleBattles = new BattleSO[0]; // 빈 배열로 시작

//             AssetDatabase.CreateAsset(floor1Stage, $"{stagePath}/Floor1_Stage.asset");

//             AssetDatabase.SaveAssets();
//             AssetDatabase.Refresh();

//             Debug.Log("[TestDataGenerator] 테스트 전투 스테이지 생성 완료!");
//         }

//         /// <summary>
//         /// 테스트 전투 SO 생성
//         /// </summary>
//         private void GenerateTestBattle()
//         {
//             // 디렉토리 생성
//             string battlePath = "Assets/ScriptableObjects/Battles";
//             Directory.CreateDirectory(battlePath);

//             // 고블린과의 전투
//             var goblinBattle = CreateInstance<BattleSO>();
//             goblinBattle.BattleName = "고블린 조우";
//             goblinBattle.BattleDescription = "야생 고블린과의 전투입니다.";

//             // 몬스터 배치 정보 생성
//             var goblinPlacement = new EnemyPlacement
//             {
//                 startingPosition = 8, // 필드 우측에 배치 (8번 슬롯)
//                 enemyData = null, // 생성된 고블린 SO를 연결해야 함
//                 isFixed = true,
//                 customHealth = -1
//             };

//             goblinBattle.EnemyPlacements = new EnemyPlacement[] { goblinPlacement };

//             // 필드 효과 없음
//             goblinBattle.InitialFieldEffect = null;

//             AssetDatabase.CreateAsset(goblinBattle, $"{battlePath}/GoblinBattle.asset");

//             // 생성된 고블린 SO 찾아서 연결
//             string goblinAssetPath = "Assets/ScriptableObjects/Enemies/Goblin.asset";
//             var goblin = AssetDatabase.LoadAssetAtPath<EnemySO>(goblinAssetPath);

//             if (goblin != null)
//             {
//                 goblinBattle.EnemyPlacements[0].enemyData = goblin;
//                 EditorUtility.SetDirty(goblinBattle);
//             }

//             // 생성된 BattleStage에 이 전투 연결
//             string stageAssetPath = "Assets/ScriptableObjects/BattleStage/Floor1_Stage.asset";
//             var floor1Stage = AssetDatabase.LoadAssetAtPath<BattleStageSO>(stageAssetPath);

//             if (floor1Stage != null)
//             {
//                 // BattleSpawnData 배열로 설정
//                 var battleSpawnData = new BattleSpawnData
//                 {
//                     battleData = goblinBattle,
//                     spawnWeight = 1.0f,
//                     minFloor = 1,
//                     maxFloor = 5,
//                     isUnique = false
//                 };

//                 // AvailableBattles는 private이므로 리플렉션으로 설정
//                 var field = typeof(BattleStageSO).GetField("availableBattles",
//                     System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

//                 if (field != null)
//                 {
//                     field.SetValue(floor1Stage, new BattleSpawnData[] { battleSpawnData });
//                 }

//                 EditorUtility.SetDirty(floor1Stage);
//             }

//             AssetDatabase.SaveAssets();
//             AssetDatabase.Refresh();

//             Debug.Log("[TestDataGenerator] 테스트 전투 SO 생성 완료!");
//         }
//     }
// }