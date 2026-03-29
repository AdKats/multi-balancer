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
using System.Xml;

using PRoCon.Core;
using PRoCon.Core.Battlemap;
using PRoCon.Core.Maps;
using PRoCon.Core.Players;
using PRoCon.Core.Players.Items;
using PRoCon.Core.Plugin;
using PRoCon.Core.Plugin.Commands;

namespace PRoConEvents
{

    public partial class MULTIbalancer
    {
        #region Classes
        public class PerModeSettings
        {
            public PerModeSettings() { }

            public PerModeSettings(String simplifiedModeName, GameVersion gameVersion)
            {
                DetermineStrongPlayersBy = DefineStrong.RoundScore;
                PercentOfTopOfTeamIsStrong = 50;
                DisperseEvenlyByRank = 0;
                EnableDisperseEvenlyList = false;
                EnableStrictDispersal = true;
                EnableScrambler = false;
                OnlyMoveWeakPlayers = true;
                isDefault = false;
                EnableTicketLossRatio = false;
                TicketLossSampleCount = 180;
                DisperseEvenlyByClanPlayers = 0;
                EnableLowPopulationAdjustments = false;
                // Rush only
                Stage1TicketPercentageToUnstackAdjustment = 0;
                Stage2TicketPercentageToUnstackAdjustment = 0;
                Stage3TicketPercentageToUnstackAdjustment = 0;
                Stage4And5TicketPercentageToUnstackAdjustment = 0;

                switch (simplifiedModeName)
                {
                    case "Conq Small, Dom, Scav": // BF3
                    case "Conquest Small":
                    case "Domination": // BF4
                        MaxPlayers = (gameVersion == GameVersion.BF4 && simplifiedModeName == "Domination") ? 20 : 32;
                        CheckTeamStackingAfterFirstMinutes = 10;
                        MaxUnstackingSwapsPerRound = 2;
                        NumberOfSwapsPerGroup = 2;
                        DelaySecondsBetweenSwapGroups = SWAP_TIMEOUT;
                        MaxUnstackingTicketDifference = 100;
                        DefinitionOfHighPopulationForPlayers = (gameVersion == GameVersion.BF4 && simplifiedModeName == "Domination") ? 16 : 24;
                        DefinitionOfLowPopulationForPlayers = 8;
                        DefinitionOfEarlyPhaseFromStart = 50; // assuming 200 tickets typical
                        DefinitionOfLatePhaseFromEnd = 50; // assuming 200 tickets typical
                        MetroAdjustedDefinitionOfLatePhase = 100;
                        EnableMetroAdjustments = false;
                        break;
                    case "Conquest Large":
                        MaxPlayers = 64;
                        CheckTeamStackingAfterFirstMinutes = 10;
                        MaxUnstackingSwapsPerRound = 4;
                        NumberOfSwapsPerGroup = 2;
                        DelaySecondsBetweenSwapGroups = SWAP_TIMEOUT;
                        MaxUnstackingTicketDifference = 150;
                        DefinitionOfHighPopulationForPlayers = 48;
                        DefinitionOfLowPopulationForPlayers = 16;
                        DefinitionOfEarlyPhaseFromStart = 100; // assuming 300 tickets typical
                        DefinitionOfLatePhaseFromEnd = 100; // assuming 300 tickets typical
                        EnableMetroAdjustments = false;
                        MetroAdjustedDefinitionOfLatePhase = 200;
                        break;
                    case "CTF":
                        MaxPlayers = 64;
                        CheckTeamStackingAfterFirstMinutes = 5;
                        MaxUnstackingSwapsPerRound = 4;
                        NumberOfSwapsPerGroup = 2;
                        DelaySecondsBetweenSwapGroups = SWAP_TIMEOUT;
                        MaxUnstackingTicketDifference = 0;
                        DefinitionOfHighPopulationForPlayers = 48;
                        DefinitionOfLowPopulationForPlayers = 16;
                        DefinitionOfEarlyPhaseFromStart = 5; // minutes
                        DefinitionOfLatePhaseFromEnd = 5; // minutes
                        break;
                    case "Rush":
                        MaxPlayers = 32;
                        CheckTeamStackingAfterFirstMinutes = 5;
                        MaxUnstackingSwapsPerRound = 2;
                        NumberOfSwapsPerGroup = 2;
                        DelaySecondsBetweenSwapGroups = SWAP_TIMEOUT;
                        MaxUnstackingTicketDifference = 40;
                        DefinitionOfHighPopulationForPlayers = 24;
                        DefinitionOfLowPopulationForPlayers = 8;
                        DefinitionOfEarlyPhaseFromStart = 25; // assuming 75 tickets typical
                        DefinitionOfLatePhaseFromEnd = 25; // assuming 75 tickets typical
                                                           // Rush only
                        Stage1TicketPercentageToUnstackAdjustment = 5;
                        Stage2TicketPercentageToUnstackAdjustment = 30;
                        Stage3TicketPercentageToUnstackAdjustment = 80;
                        Stage4And5TicketPercentageToUnstackAdjustment = -120;
                        SecondsToCheckForNewStage = 10;
                        break;
                    case "Squad Deathmatch":
                        MaxPlayers = (gameVersion == GameVersion.BF4) ? 20 : 16;
                        CheckTeamStackingAfterFirstMinutes = 0;
                        MaxUnstackingSwapsPerRound = 0;
                        NumberOfSwapsPerGroup = 0;
                        DelaySecondsBetweenSwapGroups = 60;
                        MaxUnstackingTicketDifference = 25;
                        DefinitionOfHighPopulationForPlayers = 14;
                        DefinitionOfLowPopulationForPlayers = 8;
                        DefinitionOfEarlyPhaseFromStart = 10; // assuming 50 tickets typical
                        DefinitionOfLatePhaseFromEnd = 10; // assuming 50 tickets typical
                        break;
                    case "Superiority":
                        MaxPlayers = 24;
                        CheckTeamStackingAfterFirstMinutes = 15;
                        MaxUnstackingSwapsPerRound = 2;
                        NumberOfSwapsPerGroup = 2;
                        DelaySecondsBetweenSwapGroups = SWAP_TIMEOUT;
                        MaxUnstackingTicketDifference = 125;
                        DefinitionOfHighPopulationForPlayers = 16;
                        DefinitionOfLowPopulationForPlayers = 8;
                        DefinitionOfEarlyPhaseFromStart = 25; // assuming 100 tickets typical
                        DefinitionOfLatePhaseFromEnd = 25; // assuming 100 tickets typical
                        break;
                    case "Team Deathmatch":
                        MaxPlayers = (gameVersion == GameVersion.BF4) ? 20 : 64;
                        CheckTeamStackingAfterFirstMinutes = 5;
                        MaxUnstackingSwapsPerRound = 4;
                        NumberOfSwapsPerGroup = 2;
                        DelaySecondsBetweenSwapGroups = SWAP_TIMEOUT;
                        MaxUnstackingTicketDifference = 50;
                        DefinitionOfHighPopulationForPlayers = (gameVersion == GameVersion.BF4) ? 16 : 48;
                        DefinitionOfLowPopulationForPlayers = (gameVersion == GameVersion.BF4) ? 8 : 16;
                        DefinitionOfEarlyPhaseFromStart = 20; // assuming 100 tickets typical
                        DefinitionOfLatePhaseFromEnd = 20; // assuming 100 tickets typical
                        break;
                    case "Squad Rush":
                        MaxPlayers = 8;
                        CheckTeamStackingAfterFirstMinutes = 2;
                        MaxUnstackingSwapsPerRound = 1;
                        NumberOfSwapsPerGroup = 1;
                        DelaySecondsBetweenSwapGroups = 60;
                        MaxUnstackingTicketDifference = 10;
                        DefinitionOfHighPopulationForPlayers = 6;
                        DefinitionOfLowPopulationForPlayers = 4;
                        DefinitionOfEarlyPhaseFromStart = 5; // assuming 20 tickets typical
                        DefinitionOfLatePhaseFromEnd = 5; // assuming 20 tickets typical
                                                          // Rush only
                        Stage1TicketPercentageToUnstackAdjustment = 5;
                        Stage2TicketPercentageToUnstackAdjustment = 30;
                        Stage3TicketPercentageToUnstackAdjustment = 80;
                        Stage4And5TicketPercentageToUnstackAdjustment = -120;
                        SecondsToCheckForNewStage = 10;
                        break;
                    case "Gun Master":
                        MaxPlayers = 16;
                        CheckTeamStackingAfterFirstMinutes = 2;
                        MaxUnstackingSwapsPerRound = 2;
                        NumberOfSwapsPerGroup = 2;
                        DelaySecondsBetweenSwapGroups = SWAP_TIMEOUT;
                        MaxUnstackingTicketDifference = 0;
                        DefinitionOfHighPopulationForPlayers = 12;
                        DefinitionOfLowPopulationForPlayers = 6;
                        DefinitionOfEarlyPhaseFromStart = 0;
                        DefinitionOfLatePhaseFromEnd = 0;
                        break;
                    case "Defuse": // BF4
                        MaxPlayers = 10;
                        CheckTeamStackingAfterFirstMinutes = 2;
                        MaxUnstackingSwapsPerRound = 2;
                        NumberOfSwapsPerGroup = 2;
                        DelaySecondsBetweenSwapGroups = SWAP_TIMEOUT;
                        MaxUnstackingTicketDifference = 0;
                        DefinitionOfHighPopulationForPlayers = 8;
                        DefinitionOfLowPopulationForPlayers = 4;
                        DefinitionOfEarlyPhaseFromStart = 0;
                        DefinitionOfLatePhaseFromEnd = 0;
                        break;
                    case "Obliteration": // BF4
                        MaxPlayers = 32;
                        CheckTeamStackingAfterFirstMinutes = 2;
                        MaxUnstackingSwapsPerRound = 2;
                        NumberOfSwapsPerGroup = 2;
                        DelaySecondsBetweenSwapGroups = SWAP_TIMEOUT;
                        MaxUnstackingTicketDifference = 0;
                        DefinitionOfHighPopulationForPlayers = 24;
                        DefinitionOfLowPopulationForPlayers = 8;
                        DefinitionOfEarlyPhaseFromStart = 1;
                        DefinitionOfLatePhaseFromEnd = 1;
                        break;
                    case "Squad Obliteration": // BF4
                        MaxPlayers = 10;
                        CheckTeamStackingAfterFirstMinutes = 2;
                        MaxUnstackingSwapsPerRound = 2;
                        NumberOfSwapsPerGroup = 2;
                        DelaySecondsBetweenSwapGroups = SWAP_TIMEOUT;
                        MaxUnstackingTicketDifference = 0;
                        DefinitionOfHighPopulationForPlayers = 8;
                        DefinitionOfLowPopulationForPlayers = 4;
                        DefinitionOfEarlyPhaseFromStart = 1;
                        DefinitionOfLatePhaseFromEnd = 1;
                        break;
                    case "NS Carrier Large": // BF4
                        MaxPlayers = 64;
                        CheckTeamStackingAfterFirstMinutes = 5;
                        MaxUnstackingSwapsPerRound = 4;
                        NumberOfSwapsPerGroup = 2;
                        DelaySecondsBetweenSwapGroups = SWAP_TIMEOUT;
                        MaxUnstackingTicketDifference = 0;
                        DefinitionOfHighPopulationForPlayers = 48;
                        DefinitionOfLowPopulationForPlayers = 16;
                        DefinitionOfEarlyPhaseFromStart = 5; // minutes
                        DefinitionOfLatePhaseFromEnd = 15; // minutes
                        break;
                    case "NS Carrier Small": // BF4
                        MaxPlayers = 32;
                        CheckTeamStackingAfterFirstMinutes = 5;
                        MaxUnstackingSwapsPerRound = 4;
                        NumberOfSwapsPerGroup = 2;
                        DelaySecondsBetweenSwapGroups = SWAP_TIMEOUT;
                        MaxUnstackingTicketDifference = 0;
                        DefinitionOfHighPopulationForPlayers = 24;
                        DefinitionOfLowPopulationForPlayers = 8;
                        DefinitionOfEarlyPhaseFromStart = 5; // minutes
                        DefinitionOfLatePhaseFromEnd = 15; // minutes
                        break;
                    case "Heist": // BFH
                        MaxPlayers = 32;
                        CheckTeamStackingAfterFirstMinutes = 2;
                        MaxUnstackingSwapsPerRound = 2;
                        NumberOfSwapsPerGroup = 2;
                        DelaySecondsBetweenSwapGroups = SWAP_TIMEOUT;
                        MaxUnstackingTicketDifference = 40;
                        DefinitionOfHighPopulationForPlayers = 24;
                        DefinitionOfLowPopulationForPlayers = 8;
                        DefinitionOfEarlyPhaseFromStart = 20; // assuming 100 tickets typical
                        DefinitionOfLatePhaseFromEnd = 20; // assuming 100 tickets typical
                        MetroAdjustedDefinitionOfLatePhase = 100;
                        EnableMetroAdjustments = false;
                        break;
                    case "Hotwire": // BFH
                        MaxPlayers = 32;
                        CheckTeamStackingAfterFirstMinutes = 10;
                        MaxUnstackingSwapsPerRound = 2;
                        NumberOfSwapsPerGroup = 2;
                        DelaySecondsBetweenSwapGroups = SWAP_TIMEOUT;
                        MaxUnstackingTicketDifference = 100;
                        DefinitionOfHighPopulationForPlayers = 24;
                        DefinitionOfLowPopulationForPlayers = 8;
                        DefinitionOfEarlyPhaseFromStart = 100; // assuming 500 tickets typical
                        DefinitionOfLatePhaseFromEnd = 100; // assuming 500 tickets typical
                        MetroAdjustedDefinitionOfLatePhase = 100;
                        EnableMetroAdjustments = false;
                        break;
                    case "Blood Money": // BFH
                        MaxPlayers = 32;
                        CheckTeamStackingAfterFirstMinutes = 10;
                        MaxUnstackingSwapsPerRound = 2;
                        NumberOfSwapsPerGroup = 2;
                        DelaySecondsBetweenSwapGroups = SWAP_TIMEOUT;
                        MaxUnstackingTicketDifference = 100;
                        DefinitionOfHighPopulationForPlayers = 24;
                        DefinitionOfLowPopulationForPlayers = 8;
                        DefinitionOfEarlyPhaseFromStart = 25; // assuming 150 tickets typical
                        DefinitionOfLatePhaseFromEnd = 25; // assuming 150 tickets typical
                        MetroAdjustedDefinitionOfLatePhase = 100;
                        EnableMetroAdjustments = false;
                        break;
                    case "Bounty Hunter": // BFH
                        MaxPlayers = 20;
                        CheckTeamStackingAfterFirstMinutes = 5;
                        MaxUnstackingSwapsPerRound = 4;
                        NumberOfSwapsPerGroup = 2;
                        DelaySecondsBetweenSwapGroups = SWAP_TIMEOUT;
                        MaxUnstackingTicketDifference = 50;
                        DefinitionOfHighPopulationForPlayers = 16;
                        DefinitionOfLowPopulationForPlayers = 8;
                        DefinitionOfEarlyPhaseFromStart = 20; // assuming 100 tickets typical
                        DefinitionOfLatePhaseFromEnd = 20; // assuming 100 tickets typical
                        break;
                    case "Unknown or New Mode":
                    default:
                        MaxPlayers = 32;
                        CheckTeamStackingAfterFirstMinutes = 10;
                        MaxUnstackingSwapsPerRound = 2;
                        NumberOfSwapsPerGroup = 2;
                        DelaySecondsBetweenSwapGroups = SWAP_TIMEOUT;
                        MaxUnstackingTicketDifference = 0;
                        DefinitionOfHighPopulationForPlayers = 24;
                        DefinitionOfLowPopulationForPlayers = 8;
                        DefinitionOfEarlyPhaseFromStart = 50;
                        DefinitionOfLatePhaseFromEnd = 50;
                        break;
                }
            }

            public int MaxPlayers = 64; // will be corrected later
            public double CheckTeamStackingAfterFirstMinutes = 10;
            public int MaxUnstackingSwapsPerRound = 4;
            public double DelaySecondsBetweenSwapGroups = SWAP_TIMEOUT;
            public DefineStrong DetermineStrongPlayersBy = DefineStrong.RoundScore;
            public int DefinitionOfHighPopulationForPlayers = 48;
            public int DefinitionOfLowPopulationForPlayers = 16;
            public int DefinitionOfEarlyPhaseFromStart = 50;
            public int DefinitionOfLatePhaseFromEnd = 50;
            public int DisperseEvenlyByRank = 145;
            public bool EnableDisperseEvenlyList = false;
            public double PercentOfTopOfTeamIsStrong = 50;
            public int NumberOfSwapsPerGroup = 2;
            public bool EnableScrambler = false;
            public bool EnableMetroAdjustments = false;
            public int MetroAdjustedDefinitionOfLatePhase = 50;
            public bool OnlyMoveWeakPlayers = true;
            public bool EnableStrictDispersal = true;
            public bool EnableTicketLossRatio = false;
            public int TicketLossSampleCount = 180;
            public int MaxUnstackingTicketDifference = 0;
            public int DisperseEvenlyByClanPlayers = 0;
            public bool EnableUnstackingByPlayerStats = false;
            public bool EnableLowPopulationAdjustments = false;
            public double RoutPercentage = 0;

            // Rush only
            public double Stage1TicketPercentageToUnstackAdjustment = 0;
            public double Stage2TicketPercentageToUnstackAdjustment = 0;
            public double Stage3TicketPercentageToUnstackAdjustment = 0;
            public double Stage4And5TicketPercentageToUnstackAdjustment = 0;
            public double SecondsToCheckForNewStage = 10;
            public bool EnableAdvancedRushUnstacking = false;

            public bool isDefault = true; // not a setting
        } // end PerModeSettings

        public class FetchInfo
        {
            public FetchState State;
            public DateTime Since;
            public String RequestType;

            public FetchInfo()
            {
                State = FetchState.New;
                Since = DateTime.Now;
                RequestType = String.Empty;
            }

            public FetchInfo(FetchInfo rhs)
            {
                this.State = rhs.State;
                this.Since = rhs.Since;
                this.RequestType = rhs.RequestType;
            }
        }

        public class PlayerModel
        {
            // Permanent
            public String Name;
            public String EAGUID;

            // Updated on events
            public int Team;
            public int Squad;
            public DateTime FirstSeenTimestamp; // on player join or plugin enable
            public DateTime FirstSpawnTimestamp;
            public DateTime LastSeenTimestamp;
            public double ScoreRound;
            public double KillsRound;
            public double DeathsRound;
            public int Rounds; // incremented OnRoundOverPlayers
            public int Rank;
            public bool IsDeployed;
            public String SpawnChatMessage;
            public String SpawnYellMessage;
            public bool QuietMessage;
            public MoveInfo DelayedMove;
            public int LastMoveTo;
            public int LastMoveFrom;
            public int ScrambledSquad;
            public int OriginalSquad;
            public int Role; // BF4

            // Battlelog
            public String PersonaId;
            public String Tag;
            public bool TagVerified;
            public String FullName { get { return (String.IsNullOrEmpty(Tag) ? Name : "[" + Tag + "]" + Name); } }
            public FetchInfo TagFetchStatus;
            public double KDR;
            public double SPM;
            public double KPM;
            public bool StatsVerified;
            public FetchInfo StatsFetchStatus;

            // Computed
            public double KDRRound;
            public double SPMRound;
            public double KPMRound;

            // Accumulated
            public double ScoreTotal; // not including current round
            public double KillsTotal; // not including current round
            public double DeathsTotal; // not including current round
            public int MovesTotal; // not including current round
            public int MovesByMBTotal; // not include current round

            //  Per-round state
            public int MovesRound; // moves NOT made by this plugin
            public int MovesByMBRound; // moves made by this plugin
            public DateTime MovedTimestamp;
            public DateTime MovedByMBTimestamp;
            public List<DateTime> MovedByMBHistory;

            // Based on settings
            public int DispersalGroup;
            public int Friendex; // index (key) to friend list
            public uint Whitelist; // bitmask flags, see WL_ALL

            // Commands
            public bool Subscribed;

            public PlayerModel()
            {
                Name = null;
                Team = -1;
                Squad = -1;
                EAGUID = String.Empty;
                FirstSeenTimestamp = DateTime.Now;
                FirstSpawnTimestamp = DateTime.MinValue;
                LastSeenTimestamp = DateTime.MinValue;
                Tag = String.Empty;
                TagVerified = false;
                ScoreRound = -1;
                KillsRound = -1;
                DeathsRound = -1;
                Rounds = -1;
                Rank = -1;
                KDRRound = -1;
                SPMRound = -1;
                KPMRound = -1;
                ScoreTotal = 0;
                KillsTotal = 0;
                DeathsTotal = 0;
                MovesTotal = 0;
                MovesByMBTotal = 0;
                IsDeployed = false;
                MovesRound = 0;
                MovesByMBRound = 0;
                MovedTimestamp = DateTime.MinValue;
                MovedByMBTimestamp = DateTime.MinValue;
                MovedByMBHistory = new List<DateTime>();
                SpawnChatMessage = String.Empty;
                SpawnYellMessage = String.Empty;
                QuietMessage = false;
                DelayedMove = null;
                LastMoveTo = 0;
                LastMoveFrom = 0;
                TagFetchStatus = new FetchInfo();
                ScrambledSquad = -1;
                OriginalSquad = -1;
                DispersalGroup = 0;
                Friendex = -1;
                KDR = -1;
                SPM = -1;
                KPM = -1;
                StatsVerified = false;
                PersonaId = String.Empty;
                StatsFetchStatus = new FetchInfo();
                Subscribed = false;
                Whitelist = 0;
                Role = ROLE_PLAYER;
            }

            public PlayerModel(String name, int team) : this()
            {
                Name = name;
                Team = team;
            }

            public void ResetRound()
            {
                ScoreTotal = ScoreTotal + ScoreRound;
                KillsTotal = KillsTotal + KillsRound;
                DeathsTotal = DeathsTotal + DeathsRound;
                MovesTotal = MovesTotal + MovesRound;
                MovesByMBTotal = MovesByMBTotal + MovesByMBRound;
                Rounds = (Rounds > 0) ? Rounds + 1 : 1;

                ScoreRound = -1;
                KillsRound = -1;
                DeathsRound = -1;
                KDRRound = -1;
                SPMRound = -1;
                KPMRound = -1;
                IsDeployed = false;
                SpawnChatMessage = String.Empty;
                SpawnYellMessage = String.Empty;
                QuietMessage = false;
                DelayedMove = null;
                LastMoveTo = 0;
                LastMoveFrom = 0;

                MovesRound = 0;
                MovesByMBRound = 0;
                DispersalGroup = 0;
                MovedTimestamp = DateTime.MinValue;
                // MovedByMBTimestamp reset when minutes exceeds MinutesAfterBeingMoved
            }

            public PlayerModel ClonePlayer()
            {
                PlayerModel lhs = new PlayerModel();
                lhs.Name = this.Name;
                lhs.Team = this.Team;
                lhs.Squad = this.Squad;
                lhs.EAGUID = this.EAGUID;
                lhs.FirstSeenTimestamp = this.FirstSeenTimestamp;
                lhs.FirstSpawnTimestamp = this.FirstSpawnTimestamp;
                lhs.LastSeenTimestamp = this.LastSeenTimestamp;
                lhs.Tag = this.Tag;
                lhs.TagVerified = this.TagVerified;
                lhs.ScoreRound = this.ScoreRound;
                lhs.KillsRound = this.KillsRound;
                lhs.DeathsRound = this.DeathsRound;
                lhs.Rounds = this.Rounds;
                lhs.Rank = this.Rank;
                lhs.KDRRound = this.KDRRound;
                lhs.SPMRound = this.SPMRound;
                lhs.KPMRound = this.KPMRound;
                lhs.ScoreTotal = this.ScoreTotal;
                lhs.KillsTotal = this.KillsTotal;
                lhs.DeathsTotal = this.DeathsTotal;
                lhs.MovesTotal = this.MovesTotal;
                lhs.MovesByMBTotal = this.MovesByMBTotal;
                lhs.IsDeployed = this.IsDeployed;
                lhs.MovesRound = this.MovesRound;
                lhs.MovesByMBRound = this.MovesByMBRound;
                lhs.MovedTimestamp = this.MovedTimestamp;
                lhs.MovedByMBTimestamp = this.MovedByMBTimestamp;
                lhs.MovedByMBHistory = this.MovedByMBHistory;
                lhs.SpawnChatMessage = this.SpawnChatMessage;
                lhs.SpawnYellMessage = this.SpawnYellMessage;
                lhs.QuietMessage = this.QuietMessage;
                lhs.DelayedMove = this.DelayedMove;
                lhs.LastMoveTo = this.LastMoveTo;
                lhs.LastMoveFrom = this.LastMoveFrom;
                lhs.TagFetchStatus = new FetchInfo(this.TagFetchStatus);
                lhs.ScrambledSquad = this.ScrambledSquad;
                lhs.OriginalSquad = this.OriginalSquad;
                lhs.Friendex = this.Friendex;
                lhs.KDR = this.KDR;
                lhs.SPM = this.SPM;
                lhs.KPM = this.KPM;
                lhs.StatsVerified = this.StatsVerified;
                lhs.PersonaId = this.PersonaId;
                lhs.StatsFetchStatus = new FetchInfo(this.StatsFetchStatus);
                lhs.Subscribed = this.Subscribed;
                lhs.Whitelist = this.Whitelist;
                lhs.Role = this.Role;
                return lhs;
            }
        } // end PlayerModel

        class TeamRoster
        {
            public int Team = 0;
            public List<PlayerModel> Roster = null;

            public TeamRoster(int team, List<PlayerModel> roster)
            {
                Team = team;
                Roster = roster;
            }
        } // end TeamRoster

        public class SquadRoster
        {
            public int Squad = 0;
            public double Metric = 0;
            public List<PlayerModel> Roster = null;
            public int ClanTagCount = 0;
            public int DispersalGroup = 0;
            public int WhitelistCount = 0;

            public SquadRoster(int squad)
            {
                Squad = squad;
                Metric = 0;
                Roster = new List<PlayerModel>();
                ClanTagCount = 0;
                DispersalGroup = 0;
                WhitelistCount = 0;
            }

            public SquadRoster(int squad, List<PlayerModel> roster)
            {
                Squad = squad;
                Roster = roster;
                ClanTagCount = 0;
                DispersalGroup = 0;
                WhitelistCount = 0;
            }
        } // end SquadRoster

        public class MoveInfo
        {
            public MoveType For = MoveType.Balance;
            public ForbidBecause Because = ForbidBecause.None;
            public String Name = String.Empty;
            public String Tag = String.Empty;
            public int Source = -1;
            public String SourceName = String.Empty;
            public int Destination = -1;
            public String DestinationName = String.Empty;
            public String ChatBefore = String.Empty;
            public String YellBefore = String.Empty;
            public String ChatAfter = String.Empty;
            public String YellAfter = String.Empty;
            public double Delay = 0;
            public bool Fast = false;
            public bool aborted = false;

            public MoveInfo() { }

            public MoveInfo(String name, String tag, int fromTeam, String fromName, int toTeam, String toName, double delay) : this()
            {
                Name = name;
                Tag = tag;
                Source = fromTeam;
                SourceName = (String.IsNullOrEmpty(fromName)) ? fromTeam.ToString() : fromName;
                Destination = toTeam;
                DestinationName = (String.IsNullOrEmpty(toName)) ? toTeam.ToString() : toName;
                Delay = delay;
            }

            public void Format(MULTIbalancer plugin, String fmt, bool isYell, bool isBefore)
            {
                String expanded = fmt;

                if (String.IsNullOrEmpty(expanded)) return;

                String reason = String.Empty;

                if (For == MoveType.Unswitch)
                {
                    switch (this.Because)
                    {
                        case ForbidBecause.MovedByBalancer:
                            reason = plugin.BadBecauseMovedByBalancer;
                            break;
                        case ForbidBecause.DisperseByList:
                            reason = plugin.BadBecauseDispersalList;
                            break;
                        case ForbidBecause.DisperseByRank:
                            reason = plugin.BadBecauseRank;
                            break;
                        case ForbidBecause.ToBiggest:
                            reason = plugin.BadBecauseBiggestTeam;
                            break;
                        case ForbidBecause.ToWinning:
                            reason = plugin.BadBecauseWinningTeam;
                            break;
                        case ForbidBecause.DisperseByClan: // DCE
                            reason = plugin.BadBecauseClan;
                            break;
                        case ForbidBecause.None:
                        default:
                            reason = "(no reason)";
                            break;
                    }

                    if (expanded.Contains("%reason%")) expanded = expanded.Replace("%reason%", reason);
                }

                if (expanded.Contains("%name%")) expanded = expanded.Replace("%name%", Name);
                if (expanded.Contains("%tag%")) expanded = expanded.Replace("%tag%", Tag);
                if (expanded.Contains("%fromTeam%")) expanded = expanded.Replace("%fromTeam%", SourceName);
                if (expanded.Contains("%toTeam%")) expanded = expanded.Replace("%toTeam%", DestinationName);

                if (isYell)
                {
                    if (isBefore)
                    {
                        YellBefore = expanded;
                    }
                    else
                    {
                        YellAfter = expanded;
                    }
                }
                else
                {
                    if (isBefore)
                    {
                        ChatBefore = expanded;
                    }
                    else
                    {
                        ChatAfter = expanded;
                    }
                }
            }

            public override String ToString()
            {
                String s = "Move(";
                s += "[" + Tag + "]" + Name + ",";
                s += For + ",";
                s += Source + "(" + SourceName + "),";
                s += Destination + "(" + DestinationName + "),";
                s += "CB'" + ChatBefore + "',";
                s += "YB'" + YellBefore + "',";
                s += "CA'" + ChatAfter + "',";
                s += "YA'" + YellAfter + "')";
                return s;
            }
        } // end MoveInfo

        public class DelayedRequest
        {
            public String Name;
            public double MaxDelay; // in seconds
            public DateTime LastUpdate;
            public Action<DateTime> Request;

            public DelayedRequest()
            {
                MaxDelay = 0;
                LastUpdate = DateTime.MinValue;
                Request = null;
                Name = null;
            }

            public DelayedRequest(double delay, DateTime last)
            {
                MaxDelay = delay;
                LastUpdate = last;
                Request = null;
                Name = null;
            }
        } // end DelayedRequest

        public class PriorityQueue
        {
            /*
            This class models a prioritized single queue.
            Tag requests are given priority over stats requests.
            When the type of request doesn't matter, such as for Count, the unified value is used.
            When it does matter, such as distinguishing one request from another for Dequeue,
            direct access to the member variable is used.
            */
            public Queue<String> TagQueue; // of player names
            public Queue<String> StatsQueue; // of player names
            private MULTIbalancer fPlugin;

            public PriorityQueue()
            {
                TagQueue = new Queue<String>();
                StatsQueue = new Queue<String>();
                fPlugin = null;
            }

            public PriorityQueue(MULTIbalancer plugin) : this()
            {
                fPlugin = plugin;
            }

            public int Count
            {
                get { return (TagQueue.Count + StatsQueue.Count); }
            }

            public bool Contains(String name)
            {
                return (TagQueue.Contains(name) || StatsQueue.Contains(name));
            }

            public void Enqueue(String name)
            {
                if (!TagQueue.Contains(name)) TagQueue.Enqueue(name);
                if (fPlugin.WhichBattlelogStats != BattlelogStats.ClanTagOnly)
                {
                    if (!StatsQueue.Contains(name)) StatsQueue.Enqueue(name);
                }
            }

            public void Clear()
            {
                TagQueue.Clear();
                StatsQueue.Clear();
            }
        }

        public class Histogram
        {
            public const int BIN_SIZE = 100;
            public SortedDictionary<int, int> Bin;
            public int MaxBin;
            public int PeakBin;
            public int MaxFrequency;
            public int Total;

            public Histogram()
            {
                this.Bin = new SortedDictionary<int, int>();
                this.MaxBin = 0;
                this.PeakBin = 1;
                this.MaxFrequency = 0;
                this.Total = 0;
                this.Bin[PeakBin] = 0;
            }

            public void Clear()
            {
                Bin.Clear();
                MaxBin = 0;
                PeakBin = 1;
                MaxFrequency = 0;
                Total = 0;
                Bin[PeakBin] = 0;
            }

            public void Add(int sample)
            {
                if (sample < 100) return;
                int binNumber = sample / BIN_SIZE;
                // insure bin and all bins up to this bin are initialized
                if (!Bin.ContainsKey(binNumber))
                {
                    Bin[binNumber] = 1;
                    for (int i = 1; i < binNumber; ++i)
                    {
                        if (!Bin.ContainsKey(i)) Bin[i] = 0;
                    }
                }
                else
                {
                    Bin[binNumber] = Bin[binNumber] + 1;
                }
                MaxBin = Math.Max(MaxBin, binNumber);
                MaxFrequency = Math.Max(MaxFrequency, Bin[binNumber]);
                if (Bin[PeakBin] < Bin[binNumber]) PeakBin = binNumber;
                ++Total;
            }

            public List<String> Log(int maxLine)
            {
                List<String> log = new List<String>();
                // multiply normFactor into each frequency count to get a value less than or equal to maxLine
                double normFactor = Convert.ToDouble(maxLine) / Convert.ToDouble(MaxFrequency);
                log.Add(String.Format("Total ratios = {0}, bins = {1}, peak bin = {2}, peak count = {3}, scale factor = {4:F4}",
                    Total,
                    MaxBin,
                    PeakBin * BIN_SIZE,
                    MaxFrequency,
                    normFactor));

                foreach (int bin in Bin.Keys)
                {
                    if (bin == 0) continue;
                    StringBuilder buf = new StringBuilder(String.Format("{0,5}:", bin * BIN_SIZE));
                    int normFreq = (Bin[bin] == 0) ? 0 : Convert.ToInt32(Math.Ceiling(Bin[bin] * normFactor));
                    for (int i = 0; i < normFreq; ++i)
                    {
                        buf.Append("#");
                    }
                    log.Add(buf.ToString());
                }
                return log;
            }
        }

        #endregion

    } // end MULTIbalancer

} // end namespace PRoConEvents
