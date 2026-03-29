/* MULTIbalancer — Procon Plugin
   Copyright 2013, by PapaCharlie9

   Permission is hereby granted, free of charge, to any person or organization
   obtaining a copy of the software and accompanying documentation covered by
   this license (the "Software") to use, reproduce, display, distribute,
   execute, and transmit the Software, and to prepare derivative works of the
   Software, and to permit third-parties to whom the Software is furnished to
   do so, without restriction.

   THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
   IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
   FITNESS FOR A PARTICULAR PURPOSE, TITLE AND NON-INFRINGEMENT. IN NO EVENT
   SHALL THE COPYRIGHT HOLDERS OR ANYONE DISTRIBUTING THE SOFTWARE BE LIABLE
   FOR ANY DAMAGES OR OTHER LIABILITY, WHETHER IN CONTRACT, TORT OR OTHERWISE,
   ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
   DEALINGS IN THE SOFTWARE.
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;
using System.Web;
using System.Windows.Forms;
using System.Xml;

using PRoCon.Core;
using PRoCon.Core.Battlemap;
using PRoCon.Core.Maps;
using PRoCon.Core.Players;
using PRoCon.Core.Players.Items;
using PRoCon.Core.Plugin;
using PRoCon.Core.Plugin.Commands;

using CapturableEvent = PRoCon.Core.Events.CapturableEvents;
using EventType = PRoCon.Core.Events.EventType;

namespace PRoConEvents
{
    public partial class MULTIbalancer : PRoConPluginAPI, IPRoConPluginInterface
    {
        /* Enums */

        public enum GameVersion { BF3, BF4, BFH };

        public enum MessageType { Warning, Error, Exception, Normal, Debug };

        public enum PresetItems { Standard, Aggressive, Passive, Intensify, Retain, BalanceOnly, UnstackOnly, None };

        public enum Speed { Click_Here_For_Speed_Names, Stop, Slow, Adaptive, Fast, Unstack };

        public enum DefineStrong { RoundScore, RoundSPM, RoundKills, RoundKDR, PlayerRank, RoundKPM, BattlelogSPM, BattlelogKDR, BattlelogKPM };

        public enum PluginState { Disabled, JustEnabled, Active, Error, Reconnected };

        public enum GameState { RoundEnding, RoundStarting, Playing, Warmup, Unknown };

        public enum MoveType { Balance, Unstack, Unswitch };

        public enum ForbidBecause { None, MovedByBalancer, ToWinning, ToBiggest, DisperseByRank, DisperseByList, DisperseByClan };

        public enum Phase { Early, Mid, Late };

        public enum Population { Low, Medium, High };

        public enum UnstackState { Off, SwappedStrong, SwappedWeak };

        public enum FetchState { New, InQueue, Requesting, Aborted, Succeeded, Failed };

        public enum Scope { SameTeam, SameSquad, Total, TeamOne, TeamTwo, TeamThree, TeamFour };

        public enum UnswitchChoice { Always, Never, LatePhaseOnly };

        public enum BattlelogStats { ClanTagOnly, AllTime, Reset };

        public enum DivideByChoices { None, ClanTag, DispersalGroup };

        public enum ScrambleStatus { Success, Failure, PartialSuccess, CompletelyFull };

        public enum ChatScope { Global, Team, Squad, Player };

        public enum IGCommand { None, Add, Delete, List, New };

        public enum ForceMove { Newest, Weakest, Random };


        /* Constants & Statics */

        public const double SWAP_TIMEOUT = 600; // in seconds

        public const double MODEL_TIMEOUT = 24 * 60; // in minutes

        public const int CRASH_COUNT_HEURISTIC = 24; // player count difference signifies a crash

        public const int MIN_UPDATE_USAGE_COUNT = 20; // minimum number of plugin updates in use

        public const double CHECK_FOR_UPDATES_MINS = 12 * 60; // 12 hours

        public const double MIN_ADAPT_FAST = 30.0;

        public const String INVALID_NAME_TAG_GUID = "////////";

        public static String[] TEAM_NAMES = new String[] { "None", "US", "RU" };

        public static String[] BF4_TEAM_NAMES = new String[] { "US", "RU", "CN" }; // Indexed by faction code!

        public static String[] RUSH_NAMES = new String[] { "None", "Attacking", "Defending" };

        public static String[] SQUAD_NAMES = new String[] { "None",
      "Alpha", "Bravo", "Charlie", "Delta", "Echo", "Foxtrot", "Golf", "Hotel",
      "India", "Juliet", "Kilo", "Lima", "Mike", "November", "Oscar", "Papa",
      "Quebec", "Romeo", "Sierra", "Tango", "Uniform", "Victor", "Whiskey", "Xray",
      "Yankee", "Zulu", "Haggard", "Sweetwater", "Preston", "Redford", "Faith", "Celeste"
    };

        public const String DEFAULT_LIST_ITEM = "[-- name, tag, or EA_GUID --]";

        public static String[] ROLE_NAMES = new String[] { "PLAYER", "SPECTATOR", "COMMANDER", "MOBILE COMMANDER" };


        public const uint WL_BALANCE = 1 << 0; // B option
        public const uint WL_UNSTACK = 1 << 1; // U option
        public const uint WL_SWITCH = 1 << 2; // S option
        public const uint WL_DISPERSE = 1 << 3; // D option
        public const uint WL_RANK = 1 << 4; // R option

        public const uint WL_ALL = (WL_BALANCE | WL_UNSTACK | WL_SWITCH | WL_DISPERSE | WL_RANK);

        public const int MIN_SAMPLE_COUNT = 15;

        public const int FACTION_US = 0;
        public const int FACTION_RU = 1;
        public const int FACTION_CN = 2;

        public const int ROLE_PLAYER = 0;
        public const int ROLE_SPECTATOR = 1;
        public const int ROLE_COMMANDER_PC = 2;
        public const int ROLE_COMMANDER_MOBILE = 3;


        /* Inherited:
            this.PunkbusterPlayerInfoList = new Dictionary<String, CPunkbusterInfo>();
            this.FrostbitePlayerInfoList = new Dictionary<String, CPlayerInfo>();
        */

        // General
        private bool fIsEnabled;
        private bool fFinalizerActive = false;
        private bool fAborted = false;
        private Dictionary<String, String> fModeToSimple = null;
        private Dictionary<String, Int32> fPendingTeamChange = null;
        private Thread fMoveThread = null;
        private Thread fFetchThread = null;
        private Thread fListPlayersThread = null;
        private Thread fScramblerThread = null;
        private Thread fTimerThread = null;
        private List<String> fReservedSlots = null;
        private bool fRefreshCommand = false;
        private int fServerUptime = -1;
        private bool fServerCrashed = false; // because fServerUptime >  fServerInfo.ServerUptime
        private DateTime fLastBalancedTimestamp = DateTime.MinValue;
        private DateTime fEnabledTimestamp = DateTime.MinValue;
        private String fLastMsg = null;
        private DateTime fLastVersionCheckTimestamp = DateTime.MinValue;
        private double fTimeOutOfJoint = 0;
        private List<PlayerModel>[] fDebugScramblerBefore = null;
        private List<PlayerModel>[] fDebugScramblerAfter = null;
        private List<PlayerModel>[] fDebugScramblerStartRound = null;
        private DelayedRequest fUpdateThreadLock;
        private DateTime fLastServerInfoTimestamp;
        private String fHost;
        private String fPort;
        private List<String> fRushMap3Stages = null;
        private List<String> fRushMap5Stages = null;
        private int[] fGroupAssignments = null; // index is group number, value is team id
        private List<String>[] fDispersalGroups;
        private bool fNeedPlayerListUpdate = false;
        private bool fWhileScrambling = false;
        private DelayedRequest fExtrasLock;
        private List<String> fExtraNames = null;
        private bool fGotLogin = false;
        private Dictionary<String, String> fDebugScramblerSuspects = null;
        private DelayedRequest fUpdateTicketsRequest = null;
        private Queue<double>[] fAverageTicketLoss = null;
        private Histogram fTicketLossHistogram = null;
        private double fTotalRoundEndingSeconds = 0;
        private double fTotalRoundEndingRounds = 0;
        private bool fRevealSettings = false;
        private bool fShowRiskySettings = false;
        private bool fTestFastBalance = false;
        private DateTime fLastFastMoveTimestamp = DateTime.MinValue;
        private bool fTestClanDispersal = false;
        private bool fTestMBCommand = false;

        // BF4
        private int fMaxSquadSize = 4;
        private GameVersion fGameVersion = GameVersion.BF3;

        // Data model
        private List<String> fAllPlayers = null;
        private Dictionary<String, PlayerModel> fKnownPlayers = null;
        private PluginState fPluginState;
        private GameState fGameState;
        private CServerInfo fServerInfo;
        private List<PlayerModel> fTeam1 = null;
        private List<PlayerModel> fTeam2 = null;
        private List<PlayerModel> fTeam3 = null;
        private List<PlayerModel> fTeam4 = null;
        private List<String> fUnassigned = null;
        private DateTime fRoundStartTimestamp;
        private DateTime fRoundOverTimestamp;
        private Dictionary<String, MoveInfo> fMoving = null;
        private Queue<MoveInfo> fMoveQ = null;
        private List<String> fReassigned = null;
        private int[] fTickets = null;
        private DateTime fListPlayersTimestamp;
        private Queue<DelayedRequest> fListPlayersQ = null;
        private Dictionary<String, String> fFriendlyMaps = null;
        private Dictionary<String, String> fFriendlyModes = null;
        private double fMaxTickets = -1;
        private double fRushMaxTickets = -1; // not normalized
        private List<TeamScore> fFinalStatus = null;
        private bool fIsFullRound = false;
        private UnstackState fUnstackState = UnstackState.Off;
        private DateTime fFullUnstackSwapTimestamp;
        private int fRushStage = 0;
        private double fRushPrevAttackerTickets = 0;
        private double fRushAttackerStageLoss = 0;
        private double fRushAttackerStageSamples = 0;
        private List<MoveInfo> fMoveStash = null;
        private int fUnstackGroupCount = 0;
        private PriorityQueue fPriorityFetchQ = null;
        private bool fIsCacheEnabled = false;
        private DelayedRequest fScramblerLock = null;
        private int fWinner = 0;
        private bool fStageInProgress = false;
        private Dictionary<Int32, List<String>> fFriends;
        private List<String> fAllFriends;
        private List<DelayedRequest> fTimerRequestList = null;
        private DateTime fLastValidationTimestamp;
        private int[] fFactionByTeam = null;
        private double fRoundTimeLimit = 1.0;
        private bool fScrambleByCommand = false;
        private bool fDisableUnswitcherByRemote = false;
        private DateTime fLastAutoChatTimestamp;

        // Operational statistics
        private int fReassignedRound = 0;
        private int fBalancedRound = 0;
        private int fUnstackedRound = 0;
        private int fUnswitchedRound = 0;
        private int fExcludedRound = 0;
        private int fExemptRound = 0;
        private int fFailedRound = 0;
        private int fTotalRound = 0;
        private bool fBalanceIsActive = false;
        private int fRoundsEnabled = 0;
        private int fGrandTotalQuits = 0;
        private int fGrandRageQuits = 0;
        private int fTotalQuits = 0;
        private int fRageQuits = 0;
        private int fPlayerCount = 0;
        private int fBF4CommanderCount = 0;
        private int fBF4SpectatorCount = 0;

        // Settings support
        private Dictionary<int, Type> fEasyTypeDict = null;
        private Dictionary<int, Type> fBoolDict = null;
        private Dictionary<int, Type> fListStrDict = null;
        private Dictionary<String, PerModeSettings> fPerMode = null;

        // Settings
        public int SettingsVersion;
        public PresetItems Preset;
        public bool EnableUnstacking;
        public bool EnableAdminKillForFastBalance;
        public ForceMove SelectFastBalanceBy;
        public bool EnableSettingsWizard;
        public String WhichMode;
        public bool MetroIsInMapRotation;
        public int MaximumPlayersForMode;
        public int LowestMaximumTicketsForMode;
        public int HighestMaximumTicketsForMode;
        public PresetItems PreferredStyleOfBalancing;
        public bool ApplySettingsChanges;

        public int DebugLevel;
        public int MaximumServerSize;
        public bool EnableBattlelogRequests;
        public int MaximumRequestRate;
        public double WaitTimeout;
        public BattlelogStats WhichBattlelogStats;
        public int MaxTeamSwitchesByStrongPlayers; // disabled
        public int MaxTeamSwitchesByWeakPlayers; // disabled
        public double UnlimitedTeamSwitchingDuringFirstMinutesOfRound;
        public bool Enable2SlotReserve; // disabled
        public bool EnablerecruitCommand; // disabled
        public bool EnableWhitelistingOfReservedSlotsList;
        public String[] Whitelist;
        public List<String> fSettingWhitelist;
        public String[] DisperseEvenlyList;
        public List<String> fSettingDisperseEvenlyList;
        public String[] FriendsList;
        public List<String> fSettingFriendsList;
        public double SecondsUntilAdaptiveSpeedBecomesFast;
        public bool EnableInGameCommands;
        public bool ReassignNewPlayers;

        public bool OnWhitelist;
        public bool OnFriendsList;
        public bool ApplyFriendsListToTeam;
        public bool TopScorers;
        public bool SameClanTagsInSquad;
        public bool SameClanTagsInTeam;
        public bool SameClanTagsForRankDispersal;
        public bool LenientRankDispersal;
        public double MinutesAfterJoining;
        public double MinutesAfterBeingMoved;
        public bool JoinedEarlyPhase; // disabled
        public bool JoinedMidPhase; // disabled
        public bool JoinedLatePhase; // disabled

        public double[] EarlyPhaseTicketPercentageToUnstack;
        public double[] MidPhaseTicketPercentageToUnstack;
        public double[] LatePhaseTicketPercentageToUnstack;
        public bool EnableTicketLossRateLogging;
        public Speed SpellingOfSpeedNamesReminder;
        public Speed[] EarlyPhaseBalanceSpeed;
        public Speed[] MidPhaseBalanceSpeed;
        public Speed[] LatePhaseBalanceSpeed;

        public bool OnlyByCommand; // true means hide override/hide OnlyOnNewMaps and OnlyOnFinalTicketPercentage
        public bool OnlyOnNewMaps; // false means scramble every round
        public double OnlyOnFinalTicketPercentage; // 0 means scramble regardless of final score
        public DefineStrong ScrambleBy;
        public bool KeepSquadsTogether;
        public bool KeepClanTagsInSameTeam;
        public bool KeepFriendsInSameTeam;
        public DivideByChoices DivideBy;
        public String ClanTagToDivideBy;
        public double DelaySeconds;

        public bool QuietMode;
        public double YellDurationSeconds;
        public String BadBecauseMovedByBalancer;
        public String BadBecauseWinningTeam;
        public String BadBecauseBiggestTeam;
        public String BadBecauseRank;
        public String BadBecauseDispersalList;
        public String BadBecauseClan; // DCE
        public String ChatMovedForBalance;
        public String YellMovedForBalance;
        public String ChatMovedToUnstack;
        public String YellMovedToUnstack;
        public String ChatDetectedBadTeamSwitch;
        public String YellDetectedBadTeamSwitch;
        public String ChatDetectedGoodTeamSwitch;
        public String YellDetectedGoodTeamSwitch;
        public String ChatAfterUnswitching;
        public String YellAfterUnswitching;
        public String TeamsWillBeScrambled;
        public String ChatAutobalancing;
        public String YellAutobalancing;

        public String ShowInLog; // legacy variable, if defined as String.Empty, settings are pre-v1
        public String ShowCommandInLog; // command line to show info in plugin.log
        public bool LogChat;
        public bool EnableLoggingOnlyMode;
        public bool EnableExternalLogging;
        public String ExternalLogSuffix;
        public bool EnableRiskyFeatures;

        public bool EnableImmediateUnswitch;
        public bool ForbidSwitchAfterAutobalance; // legacy pre-v1
        public bool ForbidSwitchToWinningTeam; // legacy pre-v1
        public bool ForbidSwitchToBiggestTeam; // legacy pre-v1
        public bool ForbidSwitchAfterDispersal; // legacy pre-v1
        public UnswitchChoice ForbidSwitchingAfterAutobalance;
        public UnswitchChoice ForbidSwitchingToWinningTeam;
        public UnswitchChoice ForbidSwitchingToBiggestTeam;
        public UnswitchChoice ForbidSwitchingAfterDispersal;

        // Properties
        public String FriendlyMap
        {
            get
            {
                if (fServerInfo == null) return "???";
                String r = null;
                return (fFriendlyMaps.TryGetValue(fServerInfo.Map, out r)) ? r : fServerInfo.Map;
            }
        }
        public String FriendlyMode
        {
            get
            {
                if (fServerInfo == null) return "???";
                String r = null;
                return (fFriendlyModes.TryGetValue(fServerInfo.GameMode, out r)) ? r : fServerInfo.GameMode;
            }
        }


        /* Constructor */

        public MULTIbalancer()
        {
            /* Private members */
            fIsEnabled = false;
            fFinalizerActive = false;
            fAborted = false;
            fPluginState = PluginState.Disabled;
            fGameState = GameState.Unknown;
            fServerInfo = null;
            fRefreshCommand = false;
            fServerUptime = 0;
            fServerCrashed = false;
            fDebugScramblerBefore = new List<PlayerModel>[2] { new List<PlayerModel>(), new List<PlayerModel>() };
            fDebugScramblerAfter = new List<PlayerModel>[2] { new List<PlayerModel>(), new List<PlayerModel>() };
            fDebugScramblerStartRound = new List<PlayerModel>[2] { new List<PlayerModel>(), new List<PlayerModel>() };

            fBalancedRound = 0;
            fUnstackedRound = 0;
            fUnswitchedRound = 0;
            fExcludedRound = 0;
            fExemptRound = 0;
            fFailedRound = 0;
            fTotalRound = 0;
            fBalanceIsActive = false;
            fRoundsEnabled = 0;
            fGrandTotalQuits = 0;
            fGrandRageQuits = 0;
            fTotalQuits = 0;
            fRageQuits = 0;
            fPlayerCount = 0;
            fBF4CommanderCount = 0;
            fBF4SpectatorCount = 0;

            fMoveThread = null;
            fFetchThread = null;
            fListPlayersThread = null;
            fScramblerThread = null;
            fTimerThread = null;

            fModeToSimple = new Dictionary<String, String>();

            fEasyTypeDict = new Dictionary<int, Type>();
            fEasyTypeDict.Add(0, typeof(int));
            fEasyTypeDict.Add(1, typeof(Int16));
            fEasyTypeDict.Add(2, typeof(Int32));
            fEasyTypeDict.Add(3, typeof(Int64));
            fEasyTypeDict.Add(4, typeof(float));
            fEasyTypeDict.Add(5, typeof(long));
            fEasyTypeDict.Add(6, typeof(String));
            fEasyTypeDict.Add(7, typeof(String));
            fEasyTypeDict.Add(8, typeof(double));

            fBoolDict = new Dictionary<int, Type>();
            fBoolDict.Add(0, typeof(Boolean));
            fBoolDict.Add(1, typeof(bool));

            fListStrDict = new Dictionary<int, Type>();
            fListStrDict.Add(0, typeof(String[]));

            fPerMode = new Dictionary<String, PerModeSettings>();

            fAllPlayers = new List<String>();
            fKnownPlayers = new Dictionary<String, PlayerModel>();
            fTeam1 = new List<PlayerModel>();
            fTeam2 = new List<PlayerModel>();
            fTeam3 = new List<PlayerModel>();
            fTeam4 = new List<PlayerModel>();
            fUnassigned = new List<String>();
            fRoundStartTimestamp = DateTime.MinValue;
            fRoundOverTimestamp = DateTime.MinValue;
            fListPlayersTimestamp = DateTime.MinValue;
            fFullUnstackSwapTimestamp = DateTime.MinValue;
            fLastValidationTimestamp = DateTime.MinValue;
            fListPlayersQ = new Queue<DelayedRequest>();

            fPendingTeamChange = new Dictionary<String, Int32>();
            fMoving = new Dictionary<String, MoveInfo>();
            fMoveQ = new Queue<MoveInfo>();
            fReassigned = new List<String>();
            fReservedSlots = new List<String>();
            fTickets = new int[5] { 0, 0, 0, 0, 0 };
            fFriendlyMaps = new Dictionary<String, String>();
            fFriendlyModes = new Dictionary<String, String>();
            fMaxTickets = -1;
            fRushMaxTickets = -1;
            fLastBalancedTimestamp = DateTime.MinValue;
            fEnabledTimestamp = DateTime.MinValue;
            fFinalStatus = null;
            fIsFullRound = false;
            fUnstackState = UnstackState.Off;
            fLastMsg = null;
            fRushStage = 0;
            fRushPrevAttackerTickets = 0;
            fRushAttackerStageLoss = 0;
            fRushAttackerStageSamples = 0;
            fMoveStash = new List<MoveInfo>();
            fLastVersionCheckTimestamp = DateTime.MinValue;
            fTimeOutOfJoint = 0;
            fUnstackGroupCount = 0;
            fPriorityFetchQ = new PriorityQueue(this);
            fIsCacheEnabled = false;
            fScramblerLock = new DelayedRequest();
            fWinner = 0;
            fUpdateThreadLock = new DelayedRequest();
            fLastServerInfoTimestamp = DateTime.Now;
            fStageInProgress = false;
            fHost = String.Empty;
            fPort = String.Empty;
            fRushMap3Stages = new List<String>(new String[11] { "MP_007", "XP4_Quake", "XP5_002", "MP_012", "XP4_Rubble", "MP_Damage", "XP0_Caspian", "XP0_Firestorm", "XP1_001" /* BF4 */, "XP1_003" /* BF4 */, "XP2_003" });
            fRushMap5Stages = new List<String>(new String[6] { "MP_013", "XP3_Valley", "MP_017", "XP5_001", "MP_Prison", "MP_Siege" });
            fGroupAssignments = new int[5] { 0, 0, 0, 0, 0 };
            fDispersalGroups = new List<String>[5] { null, new List<String>(), new List<String>(), new List<String>(), new List<String>() };
            fNeedPlayerListUpdate = false;
            fFriends = new Dictionary<Int32, List<String>>();
            fAllFriends = new List<String>();
            fWhileScrambling = false;
            fExtrasLock = new DelayedRequest();
            fExtraNames = new List<String>();
            fGotLogin = false;
            fDebugScramblerSuspects = new Dictionary<String, String>();
            fTimerRequestList = new List<DelayedRequest>();
            fAverageTicketLoss = new Queue<double>[3] { null, new Queue<double>(), new Queue<double>() };
            fTicketLossHistogram = new Histogram();
            fFactionByTeam = new int[5] { -1, -1, -1, -1, -1 };
            fRevealSettings = false;
            fShowRiskySettings = false;
            fLastFastMoveTimestamp = DateTime.MinValue;
            fRoundTimeLimit = 1.0;
            fScrambleByCommand = false;
            fDisableUnswitcherByRemote = false;
            fLastAutoChatTimestamp = DateTime.MinValue;

            /* Settings */

            /* ===== SECTION 0 - Presets ===== */

            SettingsVersion = 1;
            Preset = PresetItems.Standard;
            EnableUnstacking = false;
            EnableAdminKillForFastBalance = false;
            SelectFastBalanceBy = ForceMove.Newest;
            EnableSettingsWizard = false;
            WhichMode = "Conquest Large";
            MetroIsInMapRotation = false;
            MaximumPlayersForMode = 64;
            LowestMaximumTicketsForMode = 300;
            HighestMaximumTicketsForMode = 400;
            PreferredStyleOfBalancing = PresetItems.Standard;
            ApplySettingsChanges = false;

            /* ===== SECTION 1 - Settings ===== */

            DebugLevel = 2;
            MaximumServerSize = 64;
            EnableBattlelogRequests = true;
            MaximumRequestRate = 10; // in 20 seconds
            WaitTimeout = 30; // seconds
            WhichBattlelogStats = BattlelogStats.ClanTagOnly;
            MaxTeamSwitchesByStrongPlayers = 1;
            MaxTeamSwitchesByWeakPlayers = 2;
            UnlimitedTeamSwitchingDuringFirstMinutesOfRound = 5.0;
            Enable2SlotReserve = false;
            EnablerecruitCommand = false;
            EnableWhitelistingOfReservedSlotsList = true;
            Whitelist = new String[] { DEFAULT_LIST_ITEM };
            fSettingWhitelist = new List<String>(Whitelist);
            DisperseEvenlyList = new String[] { DEFAULT_LIST_ITEM };
            fSettingDisperseEvenlyList = new List<String>(DisperseEvenlyList);
            FriendsList = new String[] { DEFAULT_LIST_ITEM };
            fSettingFriendsList = new List<String>();
            SecondsUntilAdaptiveSpeedBecomesFast = 3 * 60; // 3 minutes default
            EnableInGameCommands = true;
            ReassignNewPlayers = true;
            EnableTicketLossRateLogging = false;

            /* ===== SECTION 2 - Exclusions ===== */

            OnWhitelist = true;
            OnFriendsList = false;
            ApplyFriendsListToTeam = false;
            TopScorers = true;
            SameClanTagsInSquad = true;
            SameClanTagsInTeam = false;
            SameClanTagsForRankDispersal = false;
            LenientRankDispersal = false;
            MinutesAfterJoining = 5;
            MinutesAfterBeingMoved = 90; // 1.5 hours
            JoinedEarlyPhase = true;
            JoinedMidPhase = true;
            JoinedLatePhase = false;


            /* ===== SECTION 3 - Round Phase & Population Settings ===== */

            EarlyPhaseTicketPercentageToUnstack = new double[3] { 0, 120, 120 };
            MidPhaseTicketPercentageToUnstack = new double[3] { 0, 120, 120 };
            LatePhaseTicketPercentageToUnstack = new double[3] { 0, 0, 0 };

            SpellingOfSpeedNamesReminder = Speed.Click_Here_For_Speed_Names;

            EarlyPhaseBalanceSpeed = new Speed[3] { Speed.Fast, Speed.Adaptive, Speed.Adaptive };
            MidPhaseBalanceSpeed = new Speed[3] { Speed.Fast, Speed.Adaptive, Speed.Adaptive };
            LatePhaseBalanceSpeed = new Speed[3] { Speed.Stop, Speed.Stop, Speed.Stop };

            /* ===== SECTION 4 - Scrambler ===== */

            OnlyByCommand = false;
            OnlyOnNewMaps = true; // false means scramble every round
            OnlyOnFinalTicketPercentage = 120; // 0 means scramble regardless of final score
            ScrambleBy = DefineStrong.RoundScore;
            KeepSquadsTogether = true;
            KeepClanTagsInSameTeam = true;
            KeepFriendsInSameTeam = false;
            DivideBy = DivideByChoices.None;
            ClanTagToDivideBy = String.Empty;
            DelaySeconds = 50;

            /* ===== SECTION 5 - Messages ===== */

            QuietMode = false; // false: chat is global, true: chat is private. Yells are always private
            YellDurationSeconds = 10;
            BadBecauseMovedByBalancer = "autobalance moved you to the %toTeam% team";
            BadBecauseWinningTeam = "switching to the winning team is not allowed";
            BadBecauseBiggestTeam = "switching to the biggest team is not allowed";
            BadBecauseRank = "this server splits high rank players between teams";
            BadBecauseDispersalList = "you're on the list of players to split between teams";
            BadBecauseClan = "players with same clan tags are split up"; // DCE
            ChatMovedForBalance = "*** MOVED %name% for balance ...";
            YellMovedForBalance = "Moved %name% for balance ...";
            ChatMovedToUnstack = "*** MOVED %name% to unstack teams ...";
            YellMovedToUnstack = "Moved %name% to unstack teams ...";
            ChatDetectedBadTeamSwitch = "%name%, you can't switch to team %fromTeam%: %reason%, sending you back ...";
            YellDetectedBadTeamSwitch = "You can't switch to the %fromTeam% team: %reason%, sending you back!";
            ChatDetectedGoodTeamSwitch = "%name%, thanks for helping out the %toTeam% team!";
            YellDetectedGoodTeamSwitch = "Thanks for helping out the %toTeam% team!";
            ChatAfterUnswitching = "%name%, please stay on the %toTeam% team for the rest of this round";
            YellAfterUnswitching = "Please stay on the %toTeam% team for the rest of this round";
            TeamsWillBeScrambled = "*** Teams will be SCRAMBLED next round!";
            ChatAutobalancing = "Preparing to autobalance ... (%technicalDetails%)";
            YellAutobalancing = String.Empty; // no yell by default

            /* ===== SECTION 6 - Unswitcher ===== */

            EnableImmediateUnswitch = true;
            ForbidSwitchingAfterAutobalance = UnswitchChoice.Always;
            ForbidSwitchingToWinningTeam = UnswitchChoice.Always;
            ForbidSwitchingToBiggestTeam = UnswitchChoice.Always;
            ForbidSwitchingAfterDispersal = UnswitchChoice.Always;

            /* ===== SECTION 7 - TBD ===== */

            /* ===== SECTION 8 - Per-Mode Settings ===== */

            /* ===== SECTION 9 - Debug Settings ===== */

            ShowInLog = INVALID_NAME_TAG_GUID;
            ShowCommandInLog = String.Empty;
            LogChat = true;
            EnableLoggingOnlyMode = false;
            EnableExternalLogging = false;
            ExternalLogSuffix = "_mb.log";
            EnableRiskyFeatures = false;
        }

        public MULTIbalancer(PresetItems preset) : this()
        {
            switch (preset)
            {
                case PresetItems.Standard:
                    // EarlyPhaseTicketPercentageToUnstack = new double[3]     {  0,120,120};
                    // MidPhaseTicketPercentageToUnstack = new double[3]       {  0,120,120};
                    // LatePhaseTicketPercentageToUnstack = new double[3]      {  0,  0,  0};
                    // EarlyPhaseBalanceSpeed = new Speed[3]   {     Speed.Fast, Speed.Adaptive, Speed.Adaptive};
                    // MidPhaseBalanceSpeed = new Speed[3]     {     Speed.Fast, Speed.Adaptive, Speed.Adaptive};
                    // LatePhaseBalanceSpeed = new Speed[3]    {     Speed.Stop,     Speed.Stop,     Speed.Stop};
                    // ForbidSwitchingAfterAutobalance = UnswitchChoice.Always;
                    // ForbidSwitchingToWinningTeam = UnswitchChoice.Always;
                    // ForbidSwitchingToBiggestTeam = UnswitchChoice.Always;
                    // ForbidSwitchingAfterDispersal = UnswitchChoice.Always;
                    // EnableImmediateUnswitch = true;
                    // 
                    // foreach (String mode in fPerMode.Keys) {
                    //      fPerMode[mode].PercentOfTopOfTeamIsStrong = 50;
                    // }
                    break;

                case PresetItems.Aggressive:

                    OnWhitelist = true;
                    OnFriendsList = false;
                    ApplyFriendsListToTeam = false;
                    TopScorers = false;
                    SameClanTagsInSquad = false;
                    SameClanTagsInTeam = false;
                    SameClanTagsForRankDispersal = false;
                    LenientRankDispersal = false;
                    MinutesAfterJoining = 0;
                    MinutesAfterBeingMoved = 0;
                    JoinedEarlyPhase = false;
                    JoinedMidPhase = false;
                    JoinedLatePhase = false;

                    EarlyPhaseTicketPercentageToUnstack = new double[3] { 110, 110, 110 };
                    MidPhaseTicketPercentageToUnstack = new double[3] { 110, 110, 110 };
                    LatePhaseTicketPercentageToUnstack = new double[3] { 110, 110, 110 };

                    EarlyPhaseBalanceSpeed = new Speed[3] { Speed.Fast, Speed.Fast, Speed.Fast };
                    MidPhaseBalanceSpeed = new Speed[3] { Speed.Fast, Speed.Fast, Speed.Fast };
                    LatePhaseBalanceSpeed = new Speed[3] { Speed.Fast, Speed.Fast, Speed.Fast };

                    ForbidSwitchingAfterAutobalance = UnswitchChoice.Always;
                    ForbidSwitchingToWinningTeam = UnswitchChoice.Always;
                    ForbidSwitchingToBiggestTeam = UnswitchChoice.Always;
                    ForbidSwitchingAfterDispersal = UnswitchChoice.Always;
                    EnableImmediateUnswitch = true;

                    // Does not count for automatic detection of preset
                    foreach (String mode in fPerMode.Keys)
                    {
                        fPerMode[mode].PercentOfTopOfTeamIsStrong = 50;
                    }

                    break;

                case PresetItems.Passive:

                    OnWhitelist = true;
                    OnFriendsList = true;
                    ApplyFriendsListToTeam = true;
                    TopScorers = true;
                    SameClanTagsInSquad = true;
                    SameClanTagsInTeam = true;
                    SameClanTagsForRankDispersal = true;
                    LenientRankDispersal = true;
                    MinutesAfterJoining = 15;
                    MinutesAfterBeingMoved = 12 * 60; // 12 hours
                    JoinedEarlyPhase = true;
                    JoinedMidPhase = true;
                    JoinedLatePhase = true;

                    EarlyPhaseTicketPercentageToUnstack = new double[3] { 0, 0, 200 };
                    MidPhaseTicketPercentageToUnstack = new double[3] { 0, 200, 200 };
                    LatePhaseTicketPercentageToUnstack = new double[3] { 0, 0, 0 };

                    EarlyPhaseBalanceSpeed = new Speed[3] { Speed.Slow, Speed.Slow, Speed.Slow };
                    MidPhaseBalanceSpeed = new Speed[3] { Speed.Slow, Speed.Slow, Speed.Slow };
                    LatePhaseBalanceSpeed = new Speed[3] { Speed.Stop, Speed.Stop, Speed.Stop };

                    ForbidSwitchingAfterAutobalance = UnswitchChoice.Always;
                    ForbidSwitchingToWinningTeam = UnswitchChoice.Always;
                    ForbidSwitchingToBiggestTeam = UnswitchChoice.Always;
                    ForbidSwitchingAfterDispersal = UnswitchChoice.Never;
                    EnableImmediateUnswitch = false;

                    // Does not count for automatic detection of preset
                    foreach (String mode in fPerMode.Keys)
                    {
                        fPerMode[mode].PercentOfTopOfTeamIsStrong = 50;
                    }

                    break;

                case PresetItems.Intensify:

                    OnWhitelist = true;
                    OnFriendsList = false;
                    ApplyFriendsListToTeam = false;
                    TopScorers = true;
                    SameClanTagsInSquad = false;
                    SameClanTagsInTeam = false;
                    SameClanTagsForRankDispersal = false;
                    LenientRankDispersal = false;
                    MinutesAfterJoining = 0;
                    MinutesAfterBeingMoved = 0;
                    JoinedEarlyPhase = false;
                    JoinedMidPhase = false;
                    JoinedLatePhase = true;

                    EarlyPhaseTicketPercentageToUnstack = new double[3] { 110, 120, 120 };
                    MidPhaseTicketPercentageToUnstack = new double[3] { 120, 120, 120 };
                    LatePhaseTicketPercentageToUnstack = new double[3] { 0, 0, 0 };

                    // TBD: Needs Speed.OverBalance (similar to Fast, but puts more players on losing team)
                    EarlyPhaseBalanceSpeed = new Speed[3] { Speed.Adaptive, Speed.Adaptive, Speed.Adaptive };
                    MidPhaseBalanceSpeed = new Speed[3] { Speed.Adaptive, Speed.Adaptive, Speed.Adaptive };
                    LatePhaseBalanceSpeed = new Speed[3] { Speed.Stop, Speed.Stop, Speed.Stop };

                    ForbidSwitchingAfterAutobalance = UnswitchChoice.Always;
                    ForbidSwitchingToWinningTeam = UnswitchChoice.Always;
                    ForbidSwitchingToBiggestTeam = UnswitchChoice.Never;
                    ForbidSwitchingAfterDispersal = UnswitchChoice.Always;
                    EnableImmediateUnswitch = true;

                    foreach (String mode in fPerMode.Keys)
                    {
                        fPerMode[mode].PercentOfTopOfTeamIsStrong = 25;
                    }

                    break;

                case PresetItems.Retain:

                    OnWhitelist = true;
                    OnFriendsList = true;
                    ApplyFriendsListToTeam = true;
                    TopScorers = true;
                    SameClanTagsInSquad = true;
                    SameClanTagsInTeam = false;
                    SameClanTagsForRankDispersal = true;
                    LenientRankDispersal = true;
                    MinutesAfterJoining = 15;
                    MinutesAfterBeingMoved = 2 * 60; // 2 hours
                    JoinedEarlyPhase = true;
                    JoinedMidPhase = true;
                    JoinedLatePhase = true;

                    EarlyPhaseTicketPercentageToUnstack = new double[3] { 0, 0, 150 };
                    MidPhaseTicketPercentageToUnstack = new double[3] { 0, 150, 200 };
                    LatePhaseTicketPercentageToUnstack = new double[3] { 0, 0, 0 };

                    EarlyPhaseBalanceSpeed = new Speed[3] { Speed.Slow, Speed.Adaptive, Speed.Slow };
                    MidPhaseBalanceSpeed = new Speed[3] { Speed.Slow, Speed.Adaptive, Speed.Slow };
                    LatePhaseBalanceSpeed = new Speed[3] { Speed.Stop, Speed.Stop, Speed.Stop };

                    ForbidSwitchingAfterAutobalance = UnswitchChoice.Never;
                    ForbidSwitchingToWinningTeam = UnswitchChoice.Always;
                    ForbidSwitchingToBiggestTeam = UnswitchChoice.Never;
                    ForbidSwitchingAfterDispersal = UnswitchChoice.Never;
                    EnableImmediateUnswitch = true;

                    foreach (String mode in fPerMode.Keys)
                    {
                        fPerMode[mode].PercentOfTopOfTeamIsStrong = 5;
                    }

                    break;

                case PresetItems.BalanceOnly:

                    OnWhitelist = true;
                    OnFriendsList = false;
                    ApplyFriendsListToTeam = false;
                    TopScorers = true;
                    SameClanTagsInSquad = true;
                    SameClanTagsInTeam = false;
                    SameClanTagsForRankDispersal = true;
                    LenientRankDispersal = false;
                    MinutesAfterJoining = 5;
                    MinutesAfterBeingMoved = 90;
                    JoinedEarlyPhase = true;
                    JoinedMidPhase = true;
                    JoinedLatePhase = false;

                    EarlyPhaseTicketPercentageToUnstack = new double[3] { 0, 0, 0 };
                    MidPhaseTicketPercentageToUnstack = new double[3] { 0, 0, 0 };
                    LatePhaseTicketPercentageToUnstack = new double[3] { 0, 0, 0 };

                    EarlyPhaseBalanceSpeed = new Speed[3] { Speed.Adaptive, Speed.Adaptive, Speed.Adaptive };
                    MidPhaseBalanceSpeed = new Speed[3] { Speed.Adaptive, Speed.Adaptive, Speed.Adaptive };
                    LatePhaseBalanceSpeed = new Speed[3] { Speed.Stop, Speed.Stop, Speed.Stop };

                    ForbidSwitchingAfterAutobalance = UnswitchChoice.Always;
                    ForbidSwitchingToWinningTeam = UnswitchChoice.Never;
                    ForbidSwitchingToBiggestTeam = UnswitchChoice.Never;
                    ForbidSwitchingAfterDispersal = UnswitchChoice.Never;
                    EnableImmediateUnswitch = true;

                    foreach (String mode in fPerMode.Keys)
                    {
                        fPerMode[mode].PercentOfTopOfTeamIsStrong = 0;
                    }

                    break;

                case PresetItems.UnstackOnly:

                    OnWhitelist = true;
                    OnFriendsList = false;
                    ApplyFriendsListToTeam = false;
                    TopScorers = true;
                    SameClanTagsInSquad = true;
                    SameClanTagsInTeam = false;
                    SameClanTagsForRankDispersal = true;
                    LenientRankDispersal = false;
                    MinutesAfterJoining = 5;
                    MinutesAfterBeingMoved = 90;
                    JoinedEarlyPhase = true;
                    JoinedMidPhase = true;
                    JoinedLatePhase = false;

                    EarlyPhaseTicketPercentageToUnstack = new double[3] { 0, 120, 120 };
                    MidPhaseTicketPercentageToUnstack = new double[3] { 120, 120, 120 };
                    LatePhaseTicketPercentageToUnstack = new double[3] { 0, 0, 0 };

                    EarlyPhaseBalanceSpeed = new Speed[3] { Speed.Unstack, Speed.Unstack, Speed.Unstack };
                    MidPhaseBalanceSpeed = new Speed[3] { Speed.Unstack, Speed.Unstack, Speed.Unstack };
                    LatePhaseBalanceSpeed = new Speed[3] { Speed.Unstack, Speed.Unstack, Speed.Unstack };


                    ForbidSwitchingAfterAutobalance = UnswitchChoice.Never;
                    ForbidSwitchingToWinningTeam = UnswitchChoice.Always;
                    ForbidSwitchingToBiggestTeam = UnswitchChoice.Never;
                    ForbidSwitchingAfterDispersal = UnswitchChoice.Always;
                    EnableImmediateUnswitch = true;

                    // Does not count for automatic detection of preset
                    foreach (String mode in fPerMode.Keys)
                    {
                        fPerMode[mode].PercentOfTopOfTeamIsStrong = 50;
                    }

                    break;

                case PresetItems.None:
                    break;
                default:
                    break;
            }
        }



        public String GetPluginDescription()
        {
            return MULTIbalancerUtils.HTML_DOC;
        }

    } // end MULTIbalancer

} // end namespace PRoConEvents
