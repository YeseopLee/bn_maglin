using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Maglin.Core;

namespace Maglin.Audio
{
    /// <summary>
    /// 사운드 타입
    /// </summary>
    public enum SoundType
    {
        BGM,        // 배경음악
        SFX,        // 효과음
        UI,         // UI 사운드
        Voice       // 음성
    }

    /// <summary>
    /// 오디오 클립 정보
    /// </summary>
    [System.Serializable]
    public class AudioClipData
    {
        [Header("클립 정보")]
        public string clipName;
        public AudioClip audioClip;
        public SoundType soundType;

        [Header("재생 설정")]
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.1f, 3f)] public float pitch = 1f;
        public bool loop = false;
        public bool playOnAwake = false;

        [Header("3D 사운드 설정")]
        public bool is3D = false;
        [Range(0f, 1f)] public float spatialBlend = 0f;
        public float minDistance = 1f;
        public float maxDistance = 500f;
    }

    /// <summary>
    /// 사운드 관리 매니저
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        #region Singleton Implementation
        private static AudioManager _instance;

        public static AudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<AudioManager>();

                    if (_instance == null)
                    {
                        GameObject audioManagerObject = new GameObject("AudioManager");
                        _instance = audioManagerObject.AddComponent<AudioManager>();
                        DontDestroyOnLoad(audioManagerObject);
                    }
                }
                return _instance;
            }
        }

        #endregion

        #region Events
        /// <summary>
        /// BGM 변경 이벤트
        /// </summary>
        public static event Action<string> OnBGMChanged;

        /// <summary>
        /// 음량 변경 이벤트
        /// </summary>
        public static event Action<SoundType, float> OnVolumeChanged;

        /// <summary>
        /// 음소거 상태 변경 이벤트
        /// </summary>
        public static event Action<bool> OnMuteStateChanged;
        #endregion

        #region Fields
        [Header("오디오 소스")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource[] sfxSources;
        [SerializeField] private int maxSFXSources = 10;

        [Header("오디오 클립 목록")]
        [SerializeField] private List<AudioClipData> audioClips = new List<AudioClipData>();

        [Header("음량 설정")]
        [Range(0f, 1f)][SerializeField] private float masterVolume = 1f;
        [Range(0f, 1f)][SerializeField] private float bgmVolume = 0.7f;
        [Range(0f, 1f)][SerializeField] private float sfxVolume = 1f;
        [Range(0f, 1f)][SerializeField] private float uiVolume = 1f;
        [Range(0f, 1f)][SerializeField] private float voiceVolume = 1f;

        [Header("페이드 설정")]
        [SerializeField] private float bgmFadeDuration = 1f;
        [SerializeField] private AnimationCurve fadeInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        [SerializeField] private AnimationCurve fadeOutCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);

        [Header("기타 설정")]
        [SerializeField] private bool muteAll = false;
        [SerializeField] private bool pauseAudioWhenPaused = true;

        [Header("디버그")]
        [SerializeField] private bool debugMode = false;

        // 사운드 관리
        private Dictionary<string, AudioClipData> clipLookup = new Dictionary<string, AudioClipData>();
        private Queue<AudioSource> availableSFXSources = new Queue<AudioSource>();
        private List<AudioSource> activeSFXSources = new List<AudioSource>();

        // BGM 관리
        private string currentBGM = "";
        private Coroutine bgmFadeCoroutine;

        // 초기화 관련
        private bool isInitialized = false;
        #endregion

        #region Properties
        /// <summary>
        /// 마스터 음량
        /// </summary>
        public float MasterVolume
        {
            get => masterVolume;
            set
            {
                masterVolume = Mathf.Clamp01(value);
                UpdateAllVolumes();
                SaveVolumeSettings();
            }
        }

        /// <summary>
        /// BGM 음량
        /// </summary>
        public float BGMVolume
        {
            get => bgmVolume;
            set
            {
                bgmVolume = Mathf.Clamp01(value);
                UpdateBGMVolume();
                OnVolumeChanged?.Invoke(SoundType.BGM, bgmVolume);
                SaveVolumeSettings();
            }
        }

        /// <summary>
        /// 효과음 음량
        /// </summary>
        public float SFXVolume
        {
            get => sfxVolume;
            set
            {
                sfxVolume = Mathf.Clamp01(value);
                OnVolumeChanged?.Invoke(SoundType.SFX, sfxVolume);
                SaveVolumeSettings();
            }
        }

        /// <summary>
        /// UI 사운드 음량
        /// </summary>
        public float UIVolume
        {
            get => uiVolume;
            set
            {
                uiVolume = Mathf.Clamp01(value);
                OnVolumeChanged?.Invoke(SoundType.UI, uiVolume);
                SaveVolumeSettings();
            }
        }

        /// <summary>
        /// 음성 음량
        /// </summary>
        public float VoiceVolume
        {
            get => voiceVolume;
            set
            {
                voiceVolume = Mathf.Clamp01(value);
                OnVolumeChanged?.Invoke(SoundType.Voice, voiceVolume);
                SaveVolumeSettings();
            }
        }

        /// <summary>
        /// 음소거 상태
        /// </summary>
        public bool IsMuted
        {
            get => muteAll;
            set
            {
                muteAll = value;
                UpdateAllVolumes();
                OnMuteStateChanged?.Invoke(muteAll);
                SaveVolumeSettings();
            }
        }

        /// <summary>
        /// 현재 재생 중인 BGM
        /// </summary>
        public string CurrentBGM => currentBGM;

        /// <summary>
        /// BGM이 재생 중인지 여부
        /// </summary>
        public bool IsBGMPlaying => bgmSource != null && bgmSource.isPlaying;
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            // 싱글톤 인스턴스 확인 및 설정
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeAudioManager();
            }
            else if (_instance != this)
            {
                Debug.LogWarning("[AudioManager] 중복된 AudioManager 감지됨. 삭제합니다.");
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            if (_instance == this)
            {
                // 다른 매니저들과의 연결 설정
                SetupManagerConnections();
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseAudioWhenPaused)
            {
                if (pauseStatus)
                {
                    PauseAllAudio();
                }
                else
                {
                    ResumeAllAudio();
                }
            }
        }
        #endregion

        #region Initialization
        /// <summary>
        /// AudioManager 초기화
        /// </summary>
        private void InitializeAudioManager()
        {
            if (debugMode)
                Debug.Log("[AudioManager] 오디오 매니저 초기화 시작");

            // 오디오 소스 설정
            SetupAudioSources();

            // 클립 검색 캐시 구축
            BuildClipLookup();

            // 저장된 설정 로드
            LoadVolumeSettings();

            isInitialized = true;

            if (debugMode)
                Debug.Log("[AudioManager] 오디오 매니저 초기화 완료");
        }

        /// <summary>
        /// 오디오 소스 설정
        /// </summary>
        private void SetupAudioSources()
        {
            // BGM 소스 설정
            if (bgmSource == null)
            {
                GameObject bgmObject = new GameObject("BGM Source");
                bgmObject.transform.SetParent(transform);
                bgmSource = bgmObject.AddComponent<AudioSource>();
                bgmSource.loop = true;
                bgmSource.playOnAwake = false;
            }

            // SFX 소스들 설정
            if (sfxSources == null || sfxSources.Length == 0)
            {
                sfxSources = new AudioSource[maxSFXSources];

                for (int i = 0; i < maxSFXSources; i++)
                {
                    GameObject sfxObject = new GameObject($"SFX Source {i + 1}");
                    sfxObject.transform.SetParent(transform);
                    sfxSources[i] = sfxObject.AddComponent<AudioSource>();
                    sfxSources[i].playOnAwake = false;
                    availableSFXSources.Enqueue(sfxSources[i]);
                }
            }

            if (debugMode)
                Debug.Log($"[AudioManager] 오디오 소스 설정 완료 - BGM: 1개, SFX: {maxSFXSources}개");
        }

        /// <summary>
        /// 클립 검색 캐시 구축
        /// </summary>
        private void BuildClipLookup()
        {
            clipLookup.Clear();

            foreach (var clipData in audioClips)
            {
                if (clipData.audioClip != null && !string.IsNullOrEmpty(clipData.clipName))
                {
                    clipLookup[clipData.clipName] = clipData;
                }
            }

            if (debugMode)
                Debug.Log($"[AudioManager] 클립 캐시 구축 완료: {clipLookup.Count}개");
        }

        /// <summary>
        /// 다른 매니저들과의 연결 설정
        /// </summary>
        private void SetupManagerConnections()
        {
            if (debugMode)
                Debug.Log("[AudioManager] 매니저 간 연결 설정 시작");

            // GameManager 이벤트 구독
            if (GameManager.Instance != null)
            {
                GameManager.OnGameStateChanged += OnGameStateChanged;
            }

            // BattleManager 이벤트 구독
            if (Battle.BattleManager.Instance != null)
            {
                Battle.BattleManager.OnBattleStarted += OnBattleStarted;
                Battle.BattleManager.OnBattleEnded += OnBattleEnded;
                Battle.BattleManager.OnCardsUsed += OnCardsUsed;
            }

            // PlayerManager 이벤트 구독
            if (Player.PlayerManager.Instance != null)
            {
                Player.PlayerManager.OnPlayerDeath += OnPlayerDeath;
            }

            // UIManager 이벤트 구독
            if (UI.UIManager.Instance != null)
            {
                UI.UIManager.OnPanelChanged += OnUIChanged;
            }

            if (debugMode)
                Debug.Log("[AudioManager] 매니저 간 연결 설정 완료");
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// 게임 상태 변경 시 호출
        /// </summary>
        private void OnGameStateChanged(GameState previousState, GameState newState)
        {
            if (debugMode)
                Debug.Log($"[AudioManager] 게임 상태 변경: {previousState} -> {newState}");

            // 게임 상태에 따른 BGM 변경
            string targetBGM = GetBGMForGameState(newState);
            if (!string.IsNullOrEmpty(targetBGM))
            {
                PlayBGM(targetBGM);
            }
        }

        /// <summary>
        /// 전투 시작 시 호출
        /// </summary>
        private void OnBattleStarted(Battle.BattleSO battleData)
        {
            PlaySFX("BattleStart");
        }

        /// <summary>
        /// 전투 종료 시 호출
        /// </summary>
        private void OnBattleEnded(bool victory)
        {
            if (victory)
            {
                PlaySFX("Victory");
            }
            else
            {
                PlaySFX("Defeat");
            }
        }

        /// <summary>
        /// 카드 사용 시 호출
        /// </summary>
        private void OnCardsUsed(Cards.CardSO[] cards)
        {
            // 카드 사용 사운드 재생
            foreach (var card in cards)
            {
                if (card.Sound != null)
                {
                    PlaySFX(card.Sound);
                }
            }
        }

        /// <summary>
        /// 플레이어 사망 시 호출
        /// </summary>
        private void OnPlayerDeath()
        {
            PlaySFX("PlayerDeath");
        }

        /// <summary>
        /// UI 변경 시 호출
        /// </summary>
        private void OnUIChanged(UI.UIPanel previousPanel, UI.UIPanel newPanel)
        {
            PlaySFX("UITransition");
        }

        /// <summary>
        /// 게임 상태에 따른 BGM 반환
        /// </summary>
        private string GetBGMForGameState(GameState gameState)
        {
            switch (gameState)
            {
                case GameState.MainMenu:
                    return "MainMenuBGM";
                case GameState.Battle:
                    return "BattleBGM";
                case GameState.Shop:
                    return "ShopBGM";
                case GameState.Victory:
                    return "VictoryBGM";
                case GameState.GameOver:
                    return "GameOverBGM";
                default:
                    return "";
            }
        }
        #endregion

        #region BGM Management
        /// <summary>
        /// BGM 재생
        /// </summary>
        public void PlayBGM(string clipName, bool fadeIn = true)
        {
            if (!clipLookup.ContainsKey(clipName))
            {
                if (debugMode)
                    Debug.LogWarning($"[AudioManager] BGM 클립을 찾을 수 없습니다: {clipName}");
                return;
            }

            var clipData = clipLookup[clipName];
            if (clipData.soundType != SoundType.BGM)
            {
                Debug.LogWarning($"[AudioManager] BGM이 아닌 클립입니다: {clipName}");
                return;
            }

            if (currentBGM == clipName && bgmSource.isPlaying)
            {
                if (debugMode)
                    Debug.Log($"[AudioManager] 이미 재생 중인 BGM입니다: {clipName}");
                return;
            }

            if (debugMode)
                Debug.Log($"[AudioManager] BGM 재생: {clipName}");

            currentBGM = clipName;

            if (bgmFadeCoroutine != null)
            {
                StopCoroutine(bgmFadeCoroutine);
            }

            if (fadeIn && bgmSource.isPlaying)
            {
                bgmFadeCoroutine = StartCoroutine(CrossfadeBGM(clipData));
            }
            else
            {
                bgmFadeCoroutine = StartCoroutine(PlayBGMWithFade(clipData, fadeIn));
            }

            OnBGMChanged?.Invoke(clipName);
        }

        /// <summary>
        /// BGM 정지
        /// </summary>
        public void StopBGM(bool fadeOut = true)
        {
            if (!bgmSource.isPlaying)
            {
                return;
            }

            if (debugMode)
                Debug.Log("[AudioManager] BGM 정지");

            currentBGM = "";

            if (bgmFadeCoroutine != null)
            {
                StopCoroutine(bgmFadeCoroutine);
            }

            if (fadeOut)
            {
                bgmFadeCoroutine = StartCoroutine(FadeOutBGM());
            }
            else
            {
                bgmSource.Stop();
            }

            OnBGMChanged?.Invoke("");
        }

        /// <summary>
        /// BGM 페이드 인 재생
        /// </summary>
        private IEnumerator PlayBGMWithFade(AudioClipData clipData, bool fadeIn)
        {
            bgmSource.clip = clipData.audioClip;
            bgmSource.loop = clipData.loop;
            bgmSource.pitch = clipData.pitch;

            if (fadeIn)
            {
                bgmSource.volume = 0f;
                bgmSource.Play();

                float elapsed = 0f;
                float targetVolume = GetVolumeForType(SoundType.BGM) * clipData.volume;

                while (elapsed < bgmFadeDuration)
                {
                    elapsed += Time.deltaTime;
                    float progress = elapsed / bgmFadeDuration;
                    bgmSource.volume = targetVolume * fadeInCurve.Evaluate(progress);
                    yield return null;
                }

                bgmSource.volume = targetVolume;
            }
            else
            {
                bgmSource.volume = GetVolumeForType(SoundType.BGM) * clipData.volume;
                bgmSource.Play();
            }
        }

        /// <summary>
        /// BGM 크로스 페이드
        /// </summary>
        private IEnumerator CrossfadeBGM(AudioClipData newClipData)
        {
            float originalVolume = bgmSource.volume;

            // 페이드 아웃
            float elapsed = 0f;
            while (elapsed < bgmFadeDuration * 0.5f)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / (bgmFadeDuration * 0.5f);
                bgmSource.volume = originalVolume * fadeOutCurve.Evaluate(progress);
                yield return null;
            }

            // 새 BGM 설정
            bgmSource.clip = newClipData.audioClip;
            bgmSource.loop = newClipData.loop;
            bgmSource.pitch = newClipData.pitch;
            bgmSource.Play();

            // 페이드 인
            elapsed = 0f;
            float targetVolume = GetVolumeForType(SoundType.BGM) * newClipData.volume;
            while (elapsed < bgmFadeDuration * 0.5f)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / (bgmFadeDuration * 0.5f);
                bgmSource.volume = targetVolume * fadeInCurve.Evaluate(progress);
                yield return null;
            }

            bgmSource.volume = targetVolume;
        }

        /// <summary>
        /// BGM 페이드 아웃
        /// </summary>
        private IEnumerator FadeOutBGM()
        {
            float originalVolume = bgmSource.volume;
            float elapsed = 0f;

            while (elapsed < bgmFadeDuration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / bgmFadeDuration;
                bgmSource.volume = originalVolume * fadeOutCurve.Evaluate(progress);
                yield return null;
            }

            bgmSource.volume = 0f;
            bgmSource.Stop();
        }
        #endregion

        #region SFX Management
        /// <summary>
        /// 효과음 재생 (클립 이름으로)
        /// </summary>
        public void PlaySFX(string clipName, float volumeScale = 1f)
        {
            if (!clipLookup.ContainsKey(clipName))
            {
                if (debugMode)
                    Debug.LogWarning($"[AudioManager] SFX 클립을 찾을 수 없습니다: {clipName}");
                return;
            }

            var clipData = clipLookup[clipName];
            PlaySFX(clipData.audioClip, clipData.soundType, volumeScale * clipData.volume, clipData.pitch);
        }

        /// <summary>
        /// 효과음 재생 (AudioClip으로)
        /// </summary>
        public void PlaySFX(AudioClip clip, SoundType soundType = SoundType.SFX, float volumeScale = 1f, float pitch = 1f)
        {
            if (clip == null) return;

            var audioSource = GetAvailableSFXSource();
            if (audioSource == null)
            {
                if (debugMode)
                    Debug.LogWarning("[AudioManager] 사용 가능한 SFX 소스가 없습니다.");
                return;
            }

            audioSource.clip = clip;
            audioSource.volume = GetVolumeForType(soundType) * volumeScale;
            audioSource.pitch = pitch;
            audioSource.loop = false;
            audioSource.Play();

            StartCoroutine(ReleaseSFXSource(audioSource, clip.length / pitch));

            if (debugMode)
                Debug.Log($"[AudioManager] SFX 재생: {clip.name}");
        }

        /// <summary>
        /// 사용 가능한 SFX 소스 가져오기
        /// </summary>
        private AudioSource GetAvailableSFXSource()
        {
            if (availableSFXSources.Count > 0)
            {
                var source = availableSFXSources.Dequeue();
                activeSFXSources.Add(source);
                return source;
            }

            // 모든 소스가 사용 중이면 가장 오래된 것을 중단하고 재사용
            if (activeSFXSources.Count > 0)
            {
                var oldestSource = activeSFXSources[0];
                oldestSource.Stop();
                activeSFXSources.RemoveAt(0);
                activeSFXSources.Add(oldestSource);
                return oldestSource;
            }

            return null;
        }

        /// <summary>
        /// SFX 소스 해제
        /// </summary>
        private IEnumerator ReleaseSFXSource(AudioSource source, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (activeSFXSources.Contains(source))
            {
                activeSFXSources.Remove(source);
                availableSFXSources.Enqueue(source);
            }
        }

        /// <summary>
        /// 모든 SFX 정지
        /// </summary>
        public void StopAllSFX()
        {
            foreach (var source in sfxSources)
            {
                if (source.isPlaying)
                {
                    source.Stop();
                }
            }

            activeSFXSources.Clear();
            availableSFXSources.Clear();

            foreach (var source in sfxSources)
            {
                availableSFXSources.Enqueue(source);
            }

            if (debugMode)
                Debug.Log("[AudioManager] 모든 SFX 정지");
        }
        #endregion

        #region Volume Management
        /// <summary>
        /// 타입에 따른 음량 반환
        /// </summary>
        private float GetVolumeForType(SoundType soundType)
        {
            if (muteAll) return 0f;

            float typeVolume = soundType switch
            {
                SoundType.BGM => bgmVolume,
                SoundType.SFX => sfxVolume,
                SoundType.UI => uiVolume,
                SoundType.Voice => voiceVolume,
                _ => 1f
            };

            return masterVolume * typeVolume;
        }

        /// <summary>
        /// 모든 음량 업데이트
        /// </summary>
        private void UpdateAllVolumes()
        {
            UpdateBGMVolume();
            // SFX는 재생 시점에 음량이 적용되므로 별도 업데이트 불필요
        }

        /// <summary>
        /// BGM 음량 업데이트
        /// </summary>
        private void UpdateBGMVolume()
        {
            if (bgmSource != null && currentBGM != "" && clipLookup.ContainsKey(currentBGM))
            {
                var clipData = clipLookup[currentBGM];
                bgmSource.volume = GetVolumeForType(SoundType.BGM) * clipData.volume;
            }
        }

        /// <summary>
        /// 음량 설정 저장
        /// </summary>
        private void SaveVolumeSettings()
        {
            PlayerPrefs.SetFloat("MasterVolume", masterVolume);
            PlayerPrefs.SetFloat("BGMVolume", bgmVolume);
            PlayerPrefs.SetFloat("SFXVolume", sfxVolume);
            PlayerPrefs.SetFloat("UIVolume", uiVolume);
            PlayerPrefs.SetFloat("VoiceVolume", voiceVolume);
            PlayerPrefs.SetInt("MuteAll", muteAll ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 음량 설정 로드
        /// </summary>
        private void LoadVolumeSettings()
        {
            masterVolume = PlayerPrefs.GetFloat("MasterVolume", 1f);
            bgmVolume = PlayerPrefs.GetFloat("BGMVolume", 0.7f);
            sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 1f);
            uiVolume = PlayerPrefs.GetFloat("UIVolume", 1f);
            voiceVolume = PlayerPrefs.GetFloat("VoiceVolume", 1f);
            muteAll = PlayerPrefs.GetInt("MuteAll", 0) == 1;

            UpdateAllVolumes();

            if (debugMode)
                Debug.Log("[AudioManager] 음량 설정 로드 완료");
        }
        #endregion

        #region Audio Control
        /// <summary>
        /// 모든 오디오 일시정지
        /// </summary>
        public void PauseAllAudio()
        {
            if (bgmSource != null && bgmSource.isPlaying)
            {
                bgmSource.Pause();
            }

            foreach (var source in sfxSources)
            {
                if (source.isPlaying)
                {
                    source.Pause();
                }
            }

            if (debugMode)
                Debug.Log("[AudioManager] 모든 오디오 일시정지");
        }

        /// <summary>
        /// 모든 오디오 재개
        /// </summary>
        public void ResumeAllAudio()
        {
            if (bgmSource != null)
            {
                bgmSource.UnPause();
            }

            foreach (var source in sfxSources)
            {
                source.UnPause();
            }

            if (debugMode)
                Debug.Log("[AudioManager] 모든 오디오 재개");
        }

        /// <summary>
        /// 모든 오디오 정지
        /// </summary>
        public void StopAllAudio()
        {
            StopBGM(false);
            StopAllSFX();

            if (debugMode)
                Debug.Log("[AudioManager] 모든 오디오 정지");
        }
        #endregion

        #region Debug
        /// <summary>
        /// 디버그 정보 출력
        /// </summary>
        [ContextMenu("Debug Info")]
        public void PrintDebugInfo()
        {
            Debug.Log($"=== AudioManager Debug Info ===");
            Debug.Log($"Master Volume: {masterVolume:F2}");
            Debug.Log($"BGM Volume: {bgmVolume:F2}");
            Debug.Log($"SFX Volume: {sfxVolume:F2}");
            Debug.Log($"UI Volume: {uiVolume:F2}");
            Debug.Log($"Voice Volume: {voiceVolume:F2}");
            Debug.Log($"Muted: {muteAll}");
            Debug.Log($"Current BGM: {(string.IsNullOrEmpty(currentBGM) ? "None" : currentBGM)}");
            Debug.Log($"BGM Playing: {IsBGMPlaying}");
            Debug.Log($"Active SFX Sources: {activeSFXSources.Count}/{maxSFXSources}");
            Debug.Log($"Registered Clips: {clipLookup.Count}");
        }

        /// <summary>
        /// 테스트 BGM 재생 (디버그용)
        /// </summary>
        [ContextMenu("Test BGM")]
        public void TestBGM()
        {
            if (!debugMode)
            {
                Debug.LogWarning("[AudioManager] TestBGM은 디버그 모드에서만 사용 가능합니다.");
                return;
            }

            if (clipLookup.Count > 0)
            {
                foreach (var clip in clipLookup.Values)
                {
                    if (clip.soundType == SoundType.BGM)
                    {
                        PlayBGM(clip.clipName);
                        break;
                    }
                }
            }
        }
        #endregion
    }
}