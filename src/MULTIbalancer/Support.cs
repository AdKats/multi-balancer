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

namespace PRoConEvents
{

    public partial class MULTIbalancer
    {
        /* ======================== SUPPORT FUNCTIONS ============================= */












        private String FormatMessage(String msg, MessageType type, int level)
        {
            String prefix = "[^b" + GetPluginName() + "^n]:" + level + " ";

            if (Thread.CurrentThread.Name != null) prefix += "Thread(^b^5" + Thread.CurrentThread.Name + "^0^n): ";

            if (type.Equals(MessageType.Warning))
                prefix += "^1^bWARNING^0^n: ";
            else if (type.Equals(MessageType.Error))
                prefix += "^1^bERROR^0^n: ";
            else if (type.Equals(MessageType.Exception))
                prefix += "^1^bEXCEPTION^0^n: ";
            else if (type.Equals(MessageType.Debug))
                prefix += "^9^bDEBUG^n: ";

            return prefix + msg.Replace('{', '(').Replace('}', ')') + "^n"; // close styling for every line with ^n
        }


        public void LogWrite(String msg)
        {
            if (fAborted) return;
            this.ExecuteCommand("procon.protected.pluginconsole.write", msg);
            if (EnableExternalLogging)
            {
                LogExternal(msg);
            }
        }

        public void ConsoleWrite(String msg, MessageType type, int level)
        {
            LogWrite(FormatMessage(msg, type, level));
        }

        public void ConsoleWrite(String msg, int level)
        {
            ConsoleWrite(msg, MessageType.Normal, level);
        }

        public void ConsoleWarn(String msg)
        {
            ConsoleWrite(msg, MessageType.Warning, 1);
        }

        public void ConsoleError(String msg)
        {
            ConsoleWrite(msg, MessageType.Error, 0);
        }

        public void ConsoleException(Exception e)
        {
            if (e.GetType() == typeof(ThreadAbortException)
              || e.GetType() == typeof(ThreadInterruptedException)
              || e.GetType() == typeof(CannotUnloadAppDomainException)
            )
                return;
            if (DebugLevel >= 3) ConsoleWrite(e.ToString(), MessageType.Exception, 3);
        }

        public void DebugWrite(String msg, int level)
        {
            if (DebugLevel >= level) ConsoleWrite(msg, MessageType.Normal, level);
        }

        public void ConsoleDebug(String msg)
        {
            if (DebugLevel >= 6) ConsoleWrite(msg, MessageType.Debug, 6);
        }

        public void ConsoleDump(String msg)
        {
            ConsoleWrite("^b[Show In Log]^n ^1" + msg, 0);
        }


        private void ServerCommand(params String[] args)
        {
            if (fAborted) return;
            List<String> list = new List<String>();
            list.Add("procon.protected.send");
            list.AddRange(args);
            this.ExecuteCommand(list.ToArray());
        }

        private void TaskbarNotify(String title, String msg)
        {
            if (fAborted) return;
            this.ExecuteCommand("procon.protected.notification.write", title, msg);
        }



        private List<String> GetSimplifiedModes()
        {
            List<String> r = new List<String>();

            if (fModeToSimple.Count < 1)
            {
                List<CMap> raw = this.GetMapDefines();
                foreach (CMap m in raw)
                {
                    String simple = null;
                    if (fGameVersion == GameVersion.BF3)
                    {
                        switch (m.GameMode)
                        {
                            case "Conquest Large":
                            case "Assault64":
                                simple = "Conquest Large";
                                break;
                            case "Conquest Small": // Fix for Issue #34
                            case "Assault":
                            case "Assault #2":
                            case "Conquest Domination":
                            case "Scavenger":
                                simple = "Conq Small, Dom, Scav";
                                break;
                            case "TDM":
                            case "TDM Close Quarters":
                                simple = "Team Deathmatch";
                                break;
                            case "Tank Superiority":
                            case "Air Superiority":
                                simple = "Superiority";
                                break;
                            case "Rush":
                            case "CTF":
                            case "Squad Deathmatch":
                            case "Gun Master":
                            case "Squad Rush":
                                simple = m.GameMode;
                                break;
                            default:
                                simple = "Unknown or New Mode";
                                break;
                        }
                    }
                    else if (fGameVersion == GameVersion.BF4)
                    {
                        switch (m.GameMode)
                        {
                            case "Conquest Large":
                            case "Conquest Small":
                            case "Domination":
                            case "Defuse":
                            case "Obliteration":
                            case "Squad Obliteration":
                            case "Rush":
                            case "Squad Deathmatch":
                            case "Team Deathmatch":
                            case "CTF":
                            case "Gun Master":
                                simple = m.GameMode;
                                break;
                            case "Air Superiority":
                                simple = "Superiority";
                                break;
                            case "Carrier Assault Large":
                                simple = "NS Carrier Large";
                                break;
                            case "Carrier Assault Small":
                                simple = "NS Carrier Small";
                                break;
                            case "Chain Link":
                                simple = "DT Chain Link";
                                break;
                            default:
                                simple = "Unknown or New Mode";
                                break;
                        }
                    }
                    else if (fGameVersion == GameVersion.BFH)
                    {
                        switch (m.GameMode)
                        {
                            case "Blood Money":
                            case "Conquest Large":
                            case "Conquest Small":
                            case "Crosshair":
                            case "Heist":
                            case "Hotwire":
                            case "Rescue":
                            case "Team Deathmatch":
                            case "Bounty Hunter":
                                simple = m.GameMode;
                                break;
                            default:
                                simple = "Unknown or New Mode";
                                break;
                        }
                    }
                    else
                    {
                        simple = "Unknown or New Mode";
                    }
                    if (fModeToSimple.ContainsKey(m.PlayList))
                    {
                        if (fModeToSimple[m.PlayList] != simple)
                        {
                            ConsoleWarn("For mode " + m.PlayList + " old value " + fModeToSimple[m.PlayList] + " != new value " + simple);
                        }
                    }
                    else
                    {
                        fModeToSimple[m.PlayList] = simple;
                    }
                }
            }

            bool last = false;
            foreach (KeyValuePair<String, String> p in fModeToSimple)
            {
                if (r.Contains(p.Value)) continue;
                if (p.Value == "Unknown or New Mode") { last = true; continue; }
                r.Add(p.Value); // collect up all the simple GameMode names
            }
            if (last) r.Add("Unknown or New Mode"); // make sure this is last

            return r;
        }

        public bool CheckForEquality(MULTIbalancer rhs)
        {
            return (this.OnWhitelist == rhs.OnWhitelist
             && this.OnFriendsList == rhs.OnFriendsList
             && this.ApplyFriendsListToTeam == rhs.ApplyFriendsListToTeam
             && this.TopScorers == rhs.TopScorers
             && this.SameClanTagsInSquad == rhs.SameClanTagsInSquad
             && this.SameClanTagsInTeam == rhs.SameClanTagsInTeam
             && this.SameClanTagsForRankDispersal == rhs.SameClanTagsForRankDispersal
             && this.LenientRankDispersal == rhs.LenientRankDispersal
             && this.MinutesAfterJoining == rhs.MinutesAfterJoining
             && this.JoinedEarlyPhase == rhs.JoinedEarlyPhase
             && this.JoinedMidPhase == rhs.JoinedMidPhase
             && this.JoinedLatePhase == rhs.JoinedLatePhase
             && MULTIbalancerUtils.EqualArrays(this.EarlyPhaseTicketPercentageToUnstack, rhs.EarlyPhaseTicketPercentageToUnstack)
             && MULTIbalancerUtils.EqualArrays(this.MidPhaseTicketPercentageToUnstack, rhs.MidPhaseTicketPercentageToUnstack)
             && MULTIbalancerUtils.EqualArrays(this.LatePhaseTicketPercentageToUnstack, rhs.LatePhaseTicketPercentageToUnstack)
             && MULTIbalancerUtils.EqualArrays(this.EarlyPhaseBalanceSpeed, rhs.EarlyPhaseBalanceSpeed)
             && MULTIbalancerUtils.EqualArrays(this.MidPhaseBalanceSpeed, rhs.MidPhaseBalanceSpeed)
             && MULTIbalancerUtils.EqualArrays(this.LatePhaseBalanceSpeed, rhs.LatePhaseBalanceSpeed)
             && this.ForbidSwitchingAfterAutobalance == rhs.ForbidSwitchingAfterAutobalance
             && this.ForbidSwitchingToWinningTeam == rhs.ForbidSwitchingToWinningTeam
             && this.ForbidSwitchingToBiggestTeam == rhs.ForbidSwitchingToBiggestTeam
             && this.ForbidSwitchingAfterDispersal == rhs.ForbidSwitchingAfterDispersal
             && this.EnableImmediateUnswitch == rhs.EnableImmediateUnswitch
            );
        }


        private void UpdatePresetValue()
        {
            Preset = PresetItems.None;  // backstop value

            try
            {

                // Check for Standard
                if (MULTIbalancerUtils.IsEqual(this, PresetItems.Standard))
                {
                    Preset = PresetItems.Standard;
                    return;
                }

                // Check for Aggressive
                if (MULTIbalancerUtils.IsEqual(this, PresetItems.Aggressive))
                {
                    Preset = PresetItems.Aggressive;
                    return;
                }

                // Check for Passive
                if (MULTIbalancerUtils.IsEqual(this, PresetItems.Passive))
                {
                    Preset = PresetItems.Passive;
                    return;
                }

                // Check for Intensify
                if (MULTIbalancerUtils.IsEqual(this, PresetItems.Intensify))
                {
                    Preset = PresetItems.Intensify;
                    return;
                }

                // Check for Retain
                if (MULTIbalancerUtils.IsEqual(this, PresetItems.Retain))
                {
                    Preset = PresetItems.Retain;
                    return;
                }

                // Check for BalanceOnly
                if (MULTIbalancerUtils.IsEqual(this, PresetItems.BalanceOnly))
                {
                    Preset = PresetItems.BalanceOnly;
                    return;
                }

                // Check for UnstackOnly
                if (MULTIbalancerUtils.IsEqual(this, PresetItems.UnstackOnly))
                {
                    Preset = PresetItems.UnstackOnly;
                    return;
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        private void Reset()
        {
            ResetRound();

            lock (fPriorityFetchQ)
            {
                fPriorityFetchQ.Clear();
                Monitor.Pulse(fPriorityFetchQ);
            }

            lock (fMoveQ)
            {
                fMoveQ.Clear();
                Monitor.Pulse(fMoveQ);
            }

            lock (fListPlayersQ)
            {
                fListPlayersQ.Clear();
                Monitor.Pulse(fListPlayersQ);
            }

            lock (fAllPlayers)
            {
                fAllPlayers.Clear();
            }

            lock (fMoving)
            {
                fMoving.Clear();
            }

            lock (fMoveStash)
            {
                fMoveStash.Clear();
            }

            lock (fExtrasLock)
            {
                fExtraNames.Clear();
                fDebugScramblerSuspects.Clear();
            }

            fReassigned.Clear();
            fPendingTeamChange.Clear();
            fUnassigned.Clear();

            /*
            fKnownPlayers is not cleared right away, since we want to retain stats from previous plugin sessions.
            It will be garbage collected after MODEL_MINUTES.
            */

            fServerInfo = null; // release Procon reference
            fListPlayersTimestamp = DateTime.MinValue;
            fRefreshCommand = false;
            fServerUptime = 0;
            fServerCrashed = false;
            fFinalStatus = null;
            fMaxTickets = -1;
            fBalanceIsActive = false;
            fIsFullRound = false;
            fLastMsg = null;
            fRoundsEnabled = 0;
            fGrandTotalQuits = 0;
            fGrandRageQuits = 0;
            fWhileScrambling = false;
            fUpdateTicketsRequest = null;
            fTotalRoundEndingRounds = 0;
            fTotalRoundEndingSeconds = 0;
            fLastAutoChatTimestamp = DateTime.MinValue;

            fDebugScramblerBefore[0].Clear();
            fDebugScramblerBefore[1].Clear();
            fDebugScramblerAfter[0].Clear();
            fDebugScramblerAfter[1].Clear();
            fDebugScramblerStartRound[0].Clear();
            fDebugScramblerStartRound[1].Clear();
        }

        private void ResetRound()
        {
            ClearTeams();

            for (int i = 0; i < fTickets.Length; i++)
            {
                fTickets[i] = 0;
            }

            fRoundStartTimestamp = DateTime.Now;
            fFullUnstackSwapTimestamp = DateTime.MinValue;

            lock (fAllPlayers)
            {
                foreach (String name in fAllPlayers)
                {
                    try
                    {
                        if (!fKnownPlayers.ContainsKey(name))
                        {
                            ConsoleDebug("ResetRound: " + name + " not in fKnownPlayers");
                            continue;
                        }
                        PlayerModel m = null;
                        lock (fKnownPlayers)
                        {
                            m = fKnownPlayers[name];
                        }

                        m.ResetRound();
                    }
                    catch (Exception e)
                    {
                        ConsoleException(e);
                    }
                }
            }

            fBalancedRound = 0;
            fUnstackedRound = 0;
            fUnswitchedRound = 0;
            fExcludedRound = 0;
            fExemptRound = 0;
            fFailedRound = 0;
            fTotalRound = 0;
            fReassignedRound = 0;
            fUnstackState = UnstackState.Off;
            fRushStage = 0;
            fRushPrevAttackerTickets = 0;
            fTimeOutOfJoint = 0;
            fRoundsEnabled = fRoundsEnabled + 1;
            fGrandTotalQuits = fGrandTotalQuits + fTotalQuits;
            fTotalQuits = 0;
            fGrandRageQuits = fGrandRageQuits + fRageQuits;
            fRageQuits = 0;
            fScrambleByCommand = false;
            fDisableUnswitcherByRemote = false;

            fLastBalancedTimestamp = DateTime.MinValue;

            ResetAverageTicketLoss();
            fTicketLossHistogram.Clear();
        }

        private bool IsSQDM()
        {
            if (fServerInfo == null) return false;
            return (fServerInfo.GameMode == "SquadDeathMatch0");
        }

        private bool IsRush()
        {
            if (fServerInfo == null) return false;
            return (fServerInfo.GameMode == "RushLarge0" || fServerInfo.GameMode == "SquadRush0");
        }

        private bool IsCTF()
        {
            if (fServerInfo == null) return false;
            return (fServerInfo.GameMode == "CaptureTheFlag0");
        }

        private bool IsConquest()
        {
            if (fServerInfo == null) return false;
            return Regex.Match(fServerInfo.GameMode, @"(Conquest|Domination|Scavenger|Chain|TurfWar|Heist|Hotwire|Bloodmoney)", RegexOptions.IgnoreCase).Success;
        }

        private bool IsDeathmatch()
        {
            if (fServerInfo == null) return false;
            return Regex.Match(fServerInfo.GameMode, @"(?:TeamDeathMatch|SquadDeathMatch|CashGrab)").Success;
        }

        private bool IsCarrierAssault()
        {
            if (fServerInfo == null) return false;
            return (fServerInfo.GameMode == "CarrierAssaultLarge0" || fServerInfo.GameMode == "CarrierAssaultSmall0");
        }

        private bool IsObliteration()
        {
            if (fServerInfo == null) return false;
            return (fServerInfo.GameMode == "SquadObliteration0" || fServerInfo.GameMode == "Obliteration");
        }

        private bool IsNonBalancingMode()
        {
            if (fServerInfo == null) return false;
            return (fGameVersion == GameVersion.BFH && (fServerInfo.GameMode == "Hit0" || fServerInfo.GameMode == "Hostage0"));
        }

        private bool IsCountUp()
        {
            if (fServerInfo == null) return false;
            return (
                IsDeathmatch()
                || (fGameVersion == GameVersion.BFH && fServerInfo.GameMode == "Heist0")
            );
        }

        private int MaxDiff()
        { // maximum difference that is still considered balanced, for normal balancing
            if (fServerInfo == null) return 2;
            PerModeSettings perMode = null;
            String simpleMode = String.Empty;

            if (IsSQDM())
            {
                return ((TotalPlayerCount() <= 32) ? 1 : 2);
            }

            perMode = GetPerModeSettings();
            if (!perMode.isDefault)
                return ((GetPopulation(perMode, false) == Population.High) ? 2 : 1);

            return 2;
        }

        private int MaxFastDiff()
        { // maximum difference that is still considered balanced, for fast balancing
            if (fTestFastBalance) return 1;
            if (fServerInfo == null) return 2;
            PerModeSettings perMode = null;
            String simpleMode = String.Empty;

            perMode = GetPerModeSettings();
            int lowFloor = (perMode.EnableLowPopulationAdjustments) ? 1 : 2;

            if (IsSQDM())
            {
                return ((TotalPlayerCount() <= 32) ? lowFloor : 3);
            }
            if (!perMode.isDefault)
                return ((GetPopulation(perMode, false) == Population.Low) ? lowFloor : 3);

            return 2;
        }

        private void UpdateTeams()
        {
            ClearTeams();

            List<String> names = new List<String>();

            lock (fAllPlayers)
            {
                foreach (String name in fAllPlayers)
                {
                    if (!fKnownPlayers.ContainsKey(name))
                    {
                        ConsoleDebug("UpdateTeams: " + name + " not in fKnownPlayers");
                        continue;
                    }
                    names.Add(name);
                }
            }
            lock (fKnownPlayers)
            {
                foreach (String dude in names)
                {
                    PlayerModel player = null;
                    if (fKnownPlayers.TryGetValue(dude, out player) && player != null)
                    {
                        if (fGameVersion != GameVersion.BF3 && player.Role != ROLE_PLAYER) continue; // account for role
                        List<PlayerModel> t = GetTeam(player.Team);
                        if (t != null) t.Add(player);
                        // Also update move timer
                        double mins = DateTime.Now.Subtract(player.MovedByMBTimestamp).TotalMinutes;
                        if (player.MovedByMBTimestamp != DateTime.MinValue && mins >= MinutesAfterBeingMoved)
                        {
                            player.MovedByMBTimestamp = DateTime.MinValue;
                        }
                    }
                }
            }
        }

        private List<PlayerModel> GetTeam(int team)
        {
            switch (team)
            {
                case 1: return (fTeam1);
                case 2: return (fTeam2);
                case 3: if (IsSQDM()) return (fTeam3); break;
                case 4: if (IsSQDM()) return (fTeam4); break;
                default: break;
            }
            return null;
        }

        private void ClearTeams()
        {
            fTeam1.Clear();
            fTeam2.Clear();
            fTeam3.Clear();
            fTeam4.Clear();
        }

        // Negative return value means toTeam is larger than fromTeam
        private int GetTeamDifference(ref int fromTeam, ref int toTeam)
        {
            // 0 vs 0 means assign the max team to fromTeam and min team to toTeam and return the difference
            if (fromTeam < 0 || fromTeam > 4) return 0;
            if (toTeam < 0 || toTeam > 4) return 0;
            if (fromTeam != 0 && toTeam != 0 && fromTeam == toTeam) return 0;

            if (fromTeam != 0 && toTeam != 0)
            {
                List<PlayerModel> from = null;
                List<PlayerModel> to = null;

                from = GetTeam(fromTeam);
                if (from == null) return 0;

                to = GetTeam(toTeam);
                if (to == null) return 0;

                return (from.Count - to.Count);
            }

            // otherwise find min and max

            List<TeamRoster> teams = new List<TeamRoster>();
            int big = 1;

            teams.Add(new TeamRoster(1, fTeam1));
            teams.Add(new TeamRoster(2, fTeam2));
            if (IsSQDM())
            {
                teams.Add(new TeamRoster(3, fTeam3));
                teams.Add(new TeamRoster(4, fTeam4));
                big = 3;
            }

            teams.Sort(delegate (TeamRoster lhs, TeamRoster rhs)
            {
                // Sort ascending order by count
                if (lhs == null || rhs == null) return 0;
                if (lhs.Roster.Count < rhs.Roster.Count) return -1;
                if (lhs.Roster.Count > rhs.Roster.Count) return 1;
                return 0;
            });

            TeamRoster minTeam = teams[0];
            TeamRoster maxTeam = teams[big];

            // assert(fromTeam == 0 && toTeam == 0)
            toTeam = minTeam.Team;
            fromTeam = maxTeam.Team;
            return (maxTeam.Roster.Count - minTeam.Roster.Count);
        }


        private void AnalyzeTeams(out int maxDiff, out int[] ascendingSize, out int[] descendingTickets, out int biggestTeam, out int smallestTeam, out int winningTeam, out int losingTeam)
        {

            biggestTeam = 0;
            smallestTeam = 0;
            winningTeam = 0;
            losingTeam = 0;
            maxDiff = 0;
            bool isSQDM = IsSQDM();

            ascendingSize = new int[4] { 0, 0, 0, 0 };
            descendingTickets = new int[4] { 0, 0, 0, 0 };

            if (fServerInfo == null) return;

            // special case, server is empty, always pick teamId 1
            if (TotalPlayerCount() == 0)
            {
                biggestTeam = 2;
                smallestTeam = 1;
                winningTeam = 2;
                losingTeam = 1;
                ascendingSize[0] = 1;
                ascendingSize[1] = 2;
                descendingTickets[0] = 2;
                descendingTickets[1] = 1;
                return;
            }

            List<TeamRoster> teams = new List<TeamRoster>();

            teams.Add(new TeamRoster(1, fTeam1));
            teams.Add(new TeamRoster(2, fTeam2));
            if (isSQDM)
            {
                teams.Add(new TeamRoster(3, fTeam3));
                teams.Add(new TeamRoster(4, fTeam4));
            }

            teams.Sort(delegate (TeamRoster lhs, TeamRoster rhs)
            {
                // Sort ascending order by count
                if (lhs == null || rhs == null) return 0;
                if (lhs.Roster.Count < rhs.Roster.Count) return -1;
                if (lhs.Roster.Count > rhs.Roster.Count) return 1;
                return 0;
            });

            for (int i = 0; i < ascendingSize.Length; ++i)
            {
                if (i < teams.Count)
                {
                    ascendingSize[i] = teams[i].Team;
                }
                else
                {
                    ascendingSize[i] = 0;
                }
            }

            TeamRoster small = teams[0];
            TeamRoster big = teams[teams.Count - 1];
            smallestTeam = small.Team;
            biggestTeam = big.Team;
            maxDiff = big.Roster.Count - small.Roster.Count;

            List<TeamScore> byScore = new List<TeamScore>();
            if (fServerInfo.TeamScores == null) return;
            bool isCTF = IsCTF();
            bool isCarrierAssault = IsCarrierAssault();
            bool isObliteration = IsObliteration();
            if (!isCTF && !isCarrierAssault && !isObliteration && fServerInfo.TeamScores.Count < 2) return;
            if (IsRush())
            {
                // Normalize scores
                TeamScore attackers = null;
                TeamScore defenders = null;
                foreach (TeamScore ts in fServerInfo.TeamScores)
                {
                    if (ts.TeamID == 1)
                    {
                        attackers = ts;
                    }
                    else if (ts.TeamID == 2)
                    {
                        defenders = ts;
                    }
                }
                //TeamScore attackers = fServerInfo.TeamScores[0];
                //TeamScore defenders = fServerInfo.TeamScores[1];
                double normalized = fMaxTickets - (fRushMaxTickets - defenders.Score);
                normalized = Math.Max(normalized, Convert.ToDouble(attackers.Score) / 2);
                byScore.Add(attackers); // attackers
                byScore.Add(new TeamScore(defenders.TeamID, Convert.ToInt32(normalized), defenders.WinningScore));
            }
            else if (isCTF || isCarrierAssault || isObliteration)
            {
                // Base sort on team points rather than tickets
                int usPoints = Convert.ToInt32(GetTeamPoints(1));
                int ruPoints = Convert.ToInt32(GetTeamPoints(2));
                DebugWrite("^9Score analysis: US/RU points = " + usPoints + "/" + ruPoints, 8);
                byScore.Add(new TeamScore(1, usPoints, 0));
                byScore.Add(new TeamScore(2, ruPoints, 0));
            }
            else
            {
                byScore.AddRange(fServerInfo.TeamScores);
            }

            byScore.Sort(delegate (TeamScore lhs, TeamScore rhs)
            {
                // Sort descending order by score
                if (lhs == null || rhs == null) return 0;
                if (lhs.Score < rhs.Score) return 1;
                if (lhs.Score > rhs.Score) return -1;
                return 0;
            });

            for (int i = 0; i < descendingTickets.Length; ++i)
            {
                if (isSQDM || i < 2)
                {
                    descendingTickets[i] = byScore[i].TeamID;
                }
                else
                {
                    descendingTickets[i] = 0;
                }
            }

            winningTeam = byScore[0].TeamID;
            int iloser = (isSQDM) ? 3 : 1;
            if (iloser >= byScore.Count) iloser = byScore.Count - 1;
            losingTeam = byScore[iloser].TeamID;
            DebugWrite("^9AnalyzeTeams: biggest/smallest/winning/losing = " + biggestTeam + "/" + smallestTeam + "/" + winningTeam + "/" + losingTeam, 8);
        }

        private int DifferenceFromSmallest(int fromTeam)
        {
            int biggestTeam = 0;
            int smallestTeam = 0;
            int winningTeam = 0;
            int losingTeam = 0;
            int diff = 0;
            int[] ascendingSize = null;
            int[] descendingTickets = null;

            List<TeamRoster> teams = new List<TeamRoster>();

            teams.Add(new TeamRoster(1, fTeam1));
            teams.Add(new TeamRoster(2, fTeam2));
            if (IsSQDM())
            {
                teams.Add(new TeamRoster(3, fTeam3));
                teams.Add(new TeamRoster(4, fTeam4));
            }

            if (!IsSQDM())
            {
                if (fromTeam < 1 || fromTeam > 2) return 0;
            }
            else
            {
                if (fromTeam < 1 || fromTeam > 4) return 0;
            }

            AnalyzeTeams(out diff, out ascendingSize, out descendingTickets, out biggestTeam, out smallestTeam, out winningTeam, out losingTeam);

            if (fromTeam == smallestTeam || smallestTeam < 1 || smallestTeam > teams.Count) return 0;

            return (teams[fromTeam - 1].Roster.Count - teams[smallestTeam - 1].Roster.Count);
        }


        private int ToTeam(String name, int fromTeam, bool isReassign, out int diff, ref bool mustMove)
        {
            diff = 0;
            if (fromTeam < 1 || fromTeam > 4) return 0;

            List<PlayerModel>[] byId = new List<PlayerModel>[5] { null, fTeam1, fTeam2, fTeam3, fTeam4 };

            int biggestTeam = 0;
            int smallestTeam = 0;
            int winningTeam = 0;
            int losingTeam = 0;
            int[] ascendingSize = null;
            int[] descendingTickets = null;

            AnalyzeTeams(out diff, out ascendingSize, out descendingTickets, out biggestTeam, out smallestTeam, out winningTeam, out losingTeam);

            // diff already set by AnalyzeTeams
            if (mustMove)
            {
                int disTeam = ToTeamByDispersal(name, fromTeam, byId);

                if (disTeam == -1)
                {
                    // this player moved more than other dispersals, skip
                    DebugBalance("Exempting dispersal player ^b" + name + "^n, moved more than others");
                    // leave mustMove set to true so that caller does the right thing
                    return 0;
                }

                if (disTeam != 0)
                {
                    DebugWrite("^9(DEBUG) ToTeam for ^b" + name + "^n: dispersal returned team " + disTeam, 6);
                    return disTeam;
                }
                // fall thru if dispersal doesn't find a suitable team
                mustMove = false;
            }

            DebugWrite("^9(DEBUG) ToTeam for ^b" + name + "^n: winning/losing = " + winningTeam + "/" + losingTeam, 8);
            if (DebugLevel >= 8 && descendingTickets != null)
            {
                String ds = "^9(DEBUG) ToTeam for ^b" + name + "^n: descendingTickets = [";
                for (int k = 0; k < descendingTickets.Length; ++k)
                {
                    ds = ds + descendingTickets[k] + " ";
                }
                ds = ds + "]";
                DebugWrite(ds, 8);
            }

            // diff is maximum difference between any two teams
            if (!isReassign && diff <= MaxDiff()) return 0;
            int superDiff = diff;

            int targetTeam = smallestTeam;

            // if teams are same size, send to losing team
            if (biggestTeam != smallestTeam && byId[biggestTeam].Count == byId[smallestTeam].Count && losingTeam != fromTeam)
            {
                DebugWrite("^9(DEBUG) ToTeam for ^b" + name + "^n: teams same size, so send to losing team: " + losingTeam, 8);
                targetTeam = losingTeam;
            }

            if (targetTeam == fromTeam) return 0;

            // Special handling for SQDM
            bool isSQDM = IsSQDM();
            if (isSQDM)
            {
                int orig = targetTeam;
                int i = 0;

                // Don't send to the winning team, even if it is the smallest, unless reassigning
                if (!isReassign && targetTeam == winningTeam)
                {
                    while (i < ascendingSize.Length)
                    {
                        int aTeam = ascendingSize[i];
                        ++i;
                        if (aTeam == orig || aTeam == winningTeam || aTeam == fromTeam) continue;
                        targetTeam = aTeam;
                        break;
                    }
                }

                if (targetTeam != orig)
                {
                    String szs = "(";
                    for (i = 1; i < byId.Length; ++i)
                    {
                        szs = szs + byId[i].Count.ToString();
                        if (i == 4)
                        {
                            szs = szs + ")";
                        }
                        else
                        {
                            szs = szs + ", ";
                        }
                    }
                    DebugBalance("ToTeam  for ^b" + name + "^n: SQDM adjusted target from " + GetTeamName(orig) + " team to " + GetTeamName(targetTeam) + " team: " + szs);
                }
            }

            // recompute diff to be difference between fromTeam and target team
            diff = GetTeamDifference(ref fromTeam, ref targetTeam);
            if (diff < 0)
            {
                ConsoleDebug("ToTeam for ^b" + name + "^n: GetTeamDifference returned negative diff = " + diff);
                diff = Math.Abs(diff);
            }

            // Fake out difference due to adjustment
            if (isSQDM && diff < MaxDiff() && diff != 0)
            {
                DebugBalance("ToTeam  for ^b" + name + "^n: SQDM fake out diff due to adjustment, was " + diff + ", will be reported as " + superDiff);
                diff = superDiff;
            }

            String tm = "(";
            for (int j = 1; j <= 4; ++j)
            {
                if (j == winningTeam) tm = tm + "+";
                if (j == losingTeam) tm = tm + "-";
                tm = tm + byId[j].Count;
                if (j != 4) tm = tm + "/";
            }
            tm = tm + ")";
            DebugWrite("^9(DEBUG) ToTeam for ^b" + name + "^n: analyze returned " + tm + ", " + fromTeam + " ==> " + targetTeam, 5);

            return targetTeam;
        }

        private int ToTeamByDispersal(String name, int fromTeam, List<PlayerModel>[] teamListsById)
        {
            int targetTeam = 0;
            bool allEqual = false;
            int grandTotal = 0;

            if (teamListsById == null) return 0;

            /*
            Select a team that would disperse this player evenly with similar players,
            regardless of balance or stacking. Dispersal list takes priority over
            other dispersal types.
            */

            PlayerModel player = GetPlayer(name);
            if (player == null) return 0;

            PerModeSettings perMode = GetPerModeSettings();
            if (perMode.isDefault) return 0;

            bool isSQDM = IsSQDM();
            bool mostMoves = true;

            bool isDispersalByRank = IsRankDispersal(player);
            bool isDispersalByList = IsInDispersalList(player, false);
            /* DCE */
            bool isDispersalByClanPop = false;
            if (!isDispersalByList) isDispersalByClanPop = IsClanDispersal(player, false);

            /* By Dispersal List */

            if (isDispersalByList)
            {
                int[] usualSuspects = new int[5] { 0, 0, 0, 0, 0 };

                if (player.DispersalGroup >= 1 && player.DispersalGroup <= 4)
                {
                    // Disperse by group
                    if (!isSQDM && player.DispersalGroup > 2)
                    {
                        if (DebugLevel >= 7) ConsoleDebug("ToTeamByDispersal ignoring Group " + player.DispersalGroup + " for ^b" + player.FullName + "^n, not SQDM");
                        // fall thru
                    }
                    else
                    {
                        if (perMode.EnableStrictDispersal) return fGroupAssignments[player.DispersalGroup];
                        // Otherwise, don't allow server to become wildly unbalanced
                        targetTeam = fGroupAssignments[player.DispersalGroup];
                        int nTarget = GetTeam(targetTeam).Count;
                        int nFrom = GetTeam(fromTeam).Count;
                        // Always ok if target team is smaller than current team
                        if (nTarget < nFrom) return targetTeam;
                        // Might be okay if target team is within MaxDiff
                        if ((nTarget - nFrom) <= MaxDiff()) return targetTeam;
                        // Target team too big, don't move this player
                        if (DebugLevel >= 7) ConsoleDebug("ToTeamByDispersal lenient mode, target team " + GetTeamName(targetTeam) + " has too many players " + nTarget + "/" + nFrom + ", skipping dispersal by group of ^b" + player.FullName);
                        targetTeam = 0;
                        goto clan;
                    }
                }
                // Otherwise normal list dispersal
                mostMoves = true;

                for (int teamId = 1; teamId < teamListsById.Length; ++teamId)
                {
                    foreach (PlayerModel p in teamListsById[teamId])
                    {
                        if (p.Name == player.Name) continue; // don't count this player

                        if (IsInDispersalList(p, true))
                        {
                            usualSuspects[teamId] = usualSuspects[teamId] + 1;
                            grandTotal = grandTotal + 1;

                            // Make sure this player hasn't been moved more than any other dispersal player
                            if (GetMovesThisRound(p) >= GetMovesThisRound(player))
                            {
                                mostMoves = false;
                            }
                        }
                    }
                }

                if (mostMoves && GetMovesThisRound(player) > 0)
                {
                    ConsoleDebug("^9ToTeamByDispersal List: ^b" + player.Name + "^n moved more than other dispersals (" + GetMovesThisRound(player) + " times), skipping!");
                    targetTeam = -1;
                    goto clan;
                }

                String an = usualSuspects[1] + "/" + usualSuspects[2];
                if (isSQDM) an = an + "/" + usualSuspects[3] + "/" + usualSuspects[4];
                DebugWrite("^9(DEBUG) ToTeamByDispersal: analysis of ^b" + player.FullName + "^n dispersal by list: " + an, 5);

                // Pick smallest one
                targetTeam = 0;
                allEqual = true;
                int minSuspects = 70;
                for (int i = 1; i < usualSuspects.Length; ++i)
                {
                    if (!isSQDM && i > 2) continue;
                    if (allEqual && usualSuspects[i] == minSuspects)
                    {
                        allEqual = true;
                    }
                    else if (usualSuspects[i] < minSuspects)
                    {
                        minSuspects = usualSuspects[i];
                        targetTeam = i;
                        if (i != 1) allEqual = false;
                    }
                    else
                    {
                        if (i != 1) allEqual = false;
                    }
                }

                if (grandTotal > 1 && !allEqual && targetTeam != 0 && targetTeam != fromTeam)
                {
                    if (perMode.EnableStrictDispersal) return targetTeam;
                    // Otherwise, don't allow server to become wildly unbalanced
                    int nTarget = GetTeam(targetTeam).Count;
                    int nFrom = GetTeam(fromTeam).Count;
                    // Always ok if target team is smaller than current team
                    if (nTarget < nFrom) return targetTeam;
                    // Might be okay if target team is within MaxDiff
                    if ((nTarget - nFrom) <= MaxDiff()) return targetTeam;
                    // Target team too big, don't move this player
                    if (DebugLevel >= 7) ConsoleDebug("ToTeamByDispersal lenient mode, target team " + GetTeamName(targetTeam) + " has too many players " + nTarget + "/" + nFrom + ", skipping dispersal by list of ^b" + player.FullName);
                    targetTeam = 0;
                    goto clan;
                }

                if (allEqual) DebugWrite("^9(DEBUG) ToTeamByDispersal: all equal list, skipping", 5);
                // otherwise fall through and try clan
            }

        clan:
            if (isDispersalByClanPop)
            {
                String tag = ExtractTag(player);
                int[] pops = new int[5] { 0, 0, 0, 0, 0 };
                grandTotal = 0;
                mostMoves = false;

                int n = GetClanPopulation(player, 1);
                pops[1] = n;
                grandTotal = grandTotal + n;
                n = GetClanPopulation(player, 2);
                pops[2] = n;
                grandTotal = grandTotal + n;
                if (isSQDM)
                {
                    n = GetClanPopulation(player, 3);
                    pops[3] = n;
                    grandTotal = grandTotal + n;
                    n = GetClanPopulation(player, 4);
                    pops[4] = n;
                    grandTotal = grandTotal + n;
                }

                if (grandTotal >= perMode.DisperseEvenlyByClanPlayers)
                {
                    if (GetMovesThisRound(player) > 0 && player.Team >= 1 && player.Team < teamListsById.Length)
                    {
                        mostMoves = true;
                        foreach (PlayerModel p in teamListsById[player.Team])
                        {
                            if (p.Name == player.Name) continue; // don't count this player
                                                                 // Make sure this player hasn't been moved more than any other dispersal player
                            if (GetMovesThisRound(p) >= GetMovesThisRound(player))
                            {
                                mostMoves = false;
                                break;
                            }
                        }
                    }
                    if (mostMoves)
                    {
                        ConsoleDebug("^9ToTeamByDispersal Clan: ^b" + player.FullName + "^n moved more than other dispersals (" + GetMovesThisRound(player) + " times), skipping!");
                        targetTeam = -1;
                        goto rank;
                    }

                    String a = pops[1] + "/" + pops[2];
                    if (isSQDM) a = a + "/" + pops[3] + "/" + pops[4];
                    DebugWrite("^9(DEBUG) ToTeamByDispersal: analysis of ^b" + player.FullName + "^n dispersal of clan population >= " + perMode.DisperseEvenlyByClanPlayers + ": " + grandTotal + " = " + a, 5);

                    // Pick largest and smallest
                    targetTeam = 0;
                    int bigTeam = 0;
                    allEqual = true;
                    int minPop = 40;
                    int maxPop = 0;
                    for (int i = 1; i < pops.Length; ++i)
                    {
                        if (!isSQDM && i > 2) continue;
                        if (allEqual && pops[i] == minPop)
                        {
                            allEqual = true;
                        }
                        else if (pops[i] < minPop)
                        {
                            minPop = pops[i];
                            targetTeam = i;
                            if (i != 1) allEqual = false;
                        }
                        else
                        {
                            if (i != 1) allEqual = false;
                        }
                        if (pops[i] > maxPop)
                        {
                            maxPop = pops[i];
                            bigTeam = i;
                        }
                    }

                    if (allEqual)
                    {
                        DebugWrite("^9(DEBUG) ToTeamByDispersal: all equal by clan population, skipping", 5);
                        targetTeam = 0; // don't disperse
                        goto rank;
                    }
                    else if (Math.Abs(maxPop - minPop) < 2 || targetTeam == bigTeam)
                    {
                        DebugWrite("^9(DEBUG) ToTeamByDispersal: [" + tag + "] clan populations " + maxPop + "/" + minPop + " balanced or targetTeam same as bigTeam", 5);
                        targetTeam = 0;
                        goto rank;
                    }
                    else
                    {
                        return targetTeam;
                    }
                }
                // fall through
            }

        /* By Rank? */
        rank:
            if (isDispersalByRank)
            {
                int[] rankers = new int[5] { 0, 0, 0, 0, 0 };
                grandTotal = 0;
                mostMoves = true;

                for (int i = 1; i < teamListsById.Length; ++i)
                {
                    foreach (PlayerModel p in teamListsById[i])
                    {
                        if (p.Name == player.Name) continue; // don't count this player
                        if (p.Rank >= perMode.DisperseEvenlyByRank)
                        {
                            rankers[i] = rankers[i] + 1;
                            grandTotal = grandTotal + 1;

                            // Make sure this player hasn't been moved more than any other dispersal player
                            if (GetMovesThisRound(p) >= GetMovesThisRound(player))
                            {
                                mostMoves = false;
                            }
                        }
                    }
                }

                if (mostMoves && GetMovesThisRound(player) > 0)
                {
                    ConsoleDebug("^9ToTeamByDispersal Rank: ^b" + player.Name + "^n moved more than other dispersals (" + GetMovesThisRound(player) + " times), skipping!");
                    return -1;
                }

                String a = rankers[1] + "/" + rankers[2];
                if (isSQDM) a = a + "/" + rankers[3] + "/" + rankers[4];
                DebugWrite("^9(DEBUG) ToTeamByDispersal: analysis of ^b" + name + "^n dispersal of rank >= " + perMode.DisperseEvenlyByRank + ": " + a, 5);

                // Pick smallest one
                targetTeam = 0;
                allEqual = true;
                int minRanks = 70;
                for (int i = 1; i < rankers.Length; ++i)
                {
                    if (!isSQDM && i > 2) continue;
                    if (allEqual && rankers[i] == minRanks)
                    {
                        allEqual = true;
                    }
                    else if (rankers[i] < minRanks)
                    {
                        minRanks = rankers[i];
                        targetTeam = i;
                        if (i != 1) allEqual = false;
                    }
                    else
                    {
                        if (i != 1) allEqual = false;
                    }
                }

                if (allEqual || grandTotal < 2)
                {
                    DebugWrite("^9(DEBUG) ToTeamByDispersal: all equal by rank, skipping", 5);
                    return 0; // don't disperse
                }
                // fall through
            }

            return targetTeam; // ok if 0 or same as fromTeam, caller checks
        }

        private int ToSquad(String name, int team)
        {
            int ret = 0;
            try
            {
                List<PlayerModel> teamList = null;

                if (IsSQDM()) return 1; // SQDM, squad is always 1

                teamList = GetTeam(team);
                if (teamList == null) return 0;

                int[] squads = new int[SQUAD_NAMES.Length];

                // Build table of squad counts
                int i = 0;
                for (i = 0; i < squads.Length; ++i)
                {
                    squads[i] = 0;
                }
                foreach (PlayerModel p in teamList)
                {
                    i = p.Squad;
                    if (i < 0 || i >= squads.Length) continue;
                    squads[i] = squads[i] + 1;
                }

                // Find the biggest squad less than fMaxSquadSize (that isn't locked -- TODO)
                int squad = 0;
                int best = 0;
                int atZero = 0;
                int highOccupied = 0; // for scrambling time
                for (int squadNum = 1; squadNum < squads.Length; ++squadNum)
                {
                    int n = squads[squadNum];
                    if (n == 0)
                    {
                        if (atZero == 0) atZero = squadNum;
                        continue;
                    }
                    highOccupied = squadNum;
                    if (n >= fMaxSquadSize) continue;
                    if (n > best)
                    {
                        squad = squadNum;
                        best = n;
                    }
                }
                // if no best squad, use empty squad with lowest slot number
                if (squad == 0 && atZero != 0)
                {
                    ret = atZero;
                }
                else
                {
                    // otherwise return the best squad
                    ret = squad;
                }
                // While scrambling, find the highest empty squad by three
                if (fWhileScrambling)
                {
                    if (highOccupied > 0)
                    {
                        i = highOccupied + 3;
                        while (i < squads.Length && squads[i] != 0) i = i + 1;
                        if (i < squads.Length)
                        {
                            ret = i;
                        }
                        else
                        {
                            // Use the existing selected empty squad
                            ret = atZero;
                        }
                    }
                    else
                    {
                        // We just moved all the players out of squads!
                        ret = 0;
                    }
                }
                if (DebugLevel >= 6)
                {
                    String ss = "selected " + ret + " out of ";
                    for (int k = 1; k < squads.Length; ++k)
                    {
                        if (squads[k] == 0) continue;
                        ss = ss + k + ":" + squads[k] + "/";
                    }
                    ss = ss + "-";
                    if (!fWhileScrambling)
                    {
                        ConsoleDebug("ToSquad ^b" + name + "^n: " + ss);
                    }
                    else
                    {
                        ConsoleDebug("While scrambling, ToSquad ^b" + name + "^n: " + ss);
                    }
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
            return ret;
        }

        private void StartThreads()
        {
            fMoveThread = new Thread(new ThreadStart(MoveLoop));
            fMoveThread.IsBackground = true;
            fMoveThread.Name = "mover";
            fMoveThread.Start();

            fListPlayersThread = new Thread(new ThreadStart(ListPlayersLoop));
            fListPlayersThread.IsBackground = true;
            fListPlayersThread.Name = "lister";
            fListPlayersThread.Start();

            fFetchThread = new Thread(new ThreadStart(FetchLoop));
            fFetchThread.IsBackground = true;
            fFetchThread.Name = "fetcher";
            fFetchThread.Start();

            fScramblerThread = new Thread(new ThreadStart(ScramblerLoop));
            fScramblerThread.IsBackground = true;
            fScramblerThread.Name = "scrambler";
            fScramblerThread.Start();

            DebugWrite("Starting timer loop", 3);
            fTimerThread = new Thread(new ThreadStart(TimerLoop));
            fTimerThread.IsBackground = true;
            fTimerThread.Name = "timer";
            fTimerThread.Start();
        }

        private void JoinWith(Thread thread, int secs)
        {
            if (thread == null || !thread.IsAlive || fAborted)
                return;

            ConsoleWrite("Waiting for ^b" + thread.Name + "^n to finish", 0);
            thread.Join(secs * 1000);
        }

        private void StopThreads()
        {
            if (fAborted) return;
            try
            {
                Thread stopper = new Thread(new ThreadStart(delegate ()
                    {
                        fFinalizerActive = true;

                        Thread.Sleep(100);

                        try
                        {
                            lock (fMoveQ)
                            {
                                Monitor.Pulse(fMoveQ);
                            }
                            JoinWith(fMoveThread, 1);
                            fMoveThread = null;
                            JoinWith(fListPlayersThread, 1);
                            fListPlayersThread = null;
                            lock (fPriorityFetchQ)
                            {
                                Monitor.Pulse(fPriorityFetchQ);
                            }
                            JoinWith(fFetchThread, 1);
                            fFetchThread = null;
                            lock (fScramblerLock)
                            {
                                fScramblerLock.MaxDelay = 0;
                                Monitor.Pulse(fScramblerLock);
                            }
                            JoinWith(fScramblerThread, 1);
                            fScramblerThread = null;
                            lock (fTimerRequestList)
                            {
                                Monitor.Pulse(fTimerRequestList);
                            }
                            JoinWith(fTimerThread, 1); // checks for null
                            fTimerThread = null;
                        }
                        catch (Exception e)
                        {
                            ConsoleException(e);
                        }

                        fFinalizerActive = false;
                        ConsoleWrite("^1^bFinished disabling threads, ready to be enabled again!", 0);
                    }));

                stopper.Name = "stopper";
                stopper.IsBackground = true;
                stopper.Start();

            }
            catch (Exception e)
            {
                if (!fAborted) ConsoleException(e);
            }
        }

        private void UpdateMoveTime(String name)
        {
            PlayerModel player = GetPlayer(name);
            if (player == null) return;
            player.MovedTimestamp = DateTime.Now;
        }

        private void IncrementMoves(String name)
        {
            if (!IsKnownPlayer(name)) return;
            lock (fKnownPlayers)
            {
                PlayerModel m = fKnownPlayers[name];
                m.MovesByMBRound = m.MovesByMBRound + 1;
                DateTime now = DateTime.Now;
                lock (m.MovedByMBHistory)
                {
                    m.MovedByMBHistory.Add(now);
                }
                m.MovedByMBTimestamp = now;
            }
            UpdateMoveTime(name);
        }

        private void ConditionalIncrementMoves(String name)
        {
            /*
            If some other plugin did an admin move on this player, increment
            the non-MB move counter so that this player will be exempted from balancing and unstacking
            for the rest of this round, but don't set the flag or the timer, since MB didn't move this player.
            */
            if (!IsKnownPlayer(name)) return;
            lock (fKnownPlayers)
            {
                PlayerModel m = fKnownPlayers[name];
                m.MovesRound = m.MovesRound + 1;
            }
            IncrementTotal(); // no matching stat, reflects handling of non-MB admin move
        }


        private int GetMovesThisRound(PlayerModel player)
        {
            if (player == null) return 0;
            return (player.MovesRound + player.MovesByMBRound);
        }


        private void IncrementTotal()
        {
            if (fPluginState == PluginState.Active) fTotalRound = fTotalRound + 1;
        }

        public String GetTeamName(int teamId)
        {
            if (teamId <= 0) return "Neutral";

            String ret = "#" + teamId;
            if (IsSQDM())
            {
                ret = GetSquadName(teamId);
            }
            else if (IsRush() && teamId < RUSH_NAMES.Length)
            {
                ret = RUSH_NAMES[teamId];
            }
            else
            {
                if (fGameVersion == GameVersion.BF4)
                {
                    if (teamId < fFactionByTeam.Length)
                    {
                        int faction = fFactionByTeam[teamId];
                        if (faction < 0)
                        {
                            return "T" + teamId;
                        }
                        else if (faction >= BF4_TEAM_NAMES.Length)
                        {
                            return "f" + faction + "." + teamId;
                        }
                        ret = BF4_TEAM_NAMES[faction];
                    }
                }
                else if (fGameVersion == GameVersion.BFH)
                {
                    switch (teamId)
                    {
                        case 1:
                            ret = "LE";
                            break;
                        case 2:
                            ret = "CR";
                            break;
                        default:
                            ret = "None";
                            break;
                    }
                }
                else if (teamId < TEAM_NAMES.Length)
                {
                    ret = TEAM_NAMES[teamId];
                }
            }
            return ret;
        }

        public String GetSquadName(int squadId)
        {
            if (squadId < 0) return "-None";
            String ret = "$" + squadId;
            if (squadId < SQUAD_NAMES.Length)
            {
                ret = SQUAD_NAMES[squadId];
            }
            return ret;
        }

        private void ListPlayersLoop()
        {
            /*
            Strategy: Control the rate of listPlayers commands by keeping track of the
            timestamp of the last event. Only issue a new command if no new event occurs within
            the required time.

            TBD: This ought to be retired in favor of a TimerLoop request
            */
            try
            {
                while (fIsEnabled)
                {
                    DelayedRequest request = null;
                    lock (fListPlayersQ)
                    {
                        while (fListPlayersQ.Count == 0)
                        {
                            Monitor.Wait(fListPlayersQ);
                            if (!fIsEnabled) return;
                        }

                        request = fListPlayersQ.Dequeue();

                        // Wait until event handler updates fListPlayersTimestamp or MaxDelay has elapsed
                        while (request.LastUpdate == fListPlayersTimestamp
                          && DateTime.Now.Subtract(request.LastUpdate).TotalSeconds < request.MaxDelay)
                        {
                            Monitor.Wait(fListPlayersQ, 1000);
                            if (!fIsEnabled) return;
                        }
                    }

                    // If there has been no event, ask for one
                    if (request.LastUpdate == fListPlayersTimestamp) ServerCommand("admin.listPlayers", "all");
                }
            }
            catch (ThreadAbortException)
            {
                fAborted = true;
                return;
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
            finally
            {
                if (!fAborted) ConsoleWrite("^bListPlayersLoop^n thread stopped", 0);
            }
        }

        private void ScheduleListPlayers(double delay)
        {
            DelayedRequest r = new DelayedRequest(delay, fListPlayersTimestamp);
            DebugWrite("^9Scheduling listPlayers no sooner than " + r.MaxDelay + " seconds from " + r.LastUpdate.ToString("HH:mm:ss"), 7);
            lock (fListPlayersQ)
            {
                fListPlayersQ.Enqueue(r);
                Monitor.Pulse(fListPlayersQ);
            }
        }

        private String ExtractTag(PlayerModel m)
        {
            if (m == null) return String.Empty;

            String tag = m.Tag;
            if (String.IsNullOrEmpty(tag))
            {
                // Maybe they are using [_-=]XXX[=-_]PlayerName[_-=]XXX[=-_] format
                Match tm = Regex.Match(m.Name, @"^[=_\-]*([^=_\-]{2,4})[=_\-]");
                if (tm.Success)
                {
                    tag = tm.Groups[1].Value;
                }
                else
                {
                    tm = Regex.Match(m.Name, @"[^=_\-][=_\-]([^=_\-]{2,4})[=_\-]*$");
                    if (tm.Success)
                    {
                        tag = tm.Groups[1].Value;
                    }
                    else
                    {
                        tag = String.Empty;
                    }
                }
            }
            return tag;
        }

        // Sort delegate
        public static int DescendingRoundScore(PlayerModel lhs, PlayerModel rhs)
        {
            if (lhs == null)
            {
                return ((rhs == null) ? 0 : -1);
            }
            else if (rhs == null)
            {
                return ((lhs == null) ? 0 : 1);
            }

            if (lhs.ScoreRound < rhs.ScoreRound) return 1;
            if (lhs.ScoreRound > rhs.ScoreRound) return -1;
            return 0;
        }


        // Sort delegate
        public static int DescendingRoundSPM(PlayerModel lhs, PlayerModel rhs)
        {
            if (lhs == null)
            {
                return ((rhs == null) ? 0 : -1);
            }
            else if (rhs == null)
            {
                return ((lhs == null) ? 0 : 1);
            }
            if (lhs.SPMRound < rhs.SPMRound) return 1;
            if (lhs.SPMRound > rhs.SPMRound) return -1;
            return 0;
        }


        // Sort delegate
        public static int DescendingRoundKills(PlayerModel lhs, PlayerModel rhs)
        {
            if (lhs == null)
            {
                return ((rhs == null) ? 0 : -1);
            }
            else if (rhs == null)
            {
                return ((lhs == null) ? 0 : 1);
            }

            if (lhs.KillsRound < rhs.KillsRound) return 1;
            if (lhs.KillsRound > rhs.KillsRound) return -1;
            return 0;
        }


        // Sort delegate
        public static int DescendingRoundKDR(PlayerModel lhs, PlayerModel rhs)
        {
            if (lhs == null)
            {
                return ((rhs == null) ? 0 : -1);
            }
            else if (rhs == null)
            {
                return ((lhs == null) ? 0 : 1);
            }

            if (lhs.KDRRound < rhs.KDRRound) return 1;
            if (lhs.KDRRound > rhs.KDRRound) return -1;
            return 0;
        }


        // Sort delegate
        public static int DescendingPlayerRank(PlayerModel lhs, PlayerModel rhs)
        {
            if (lhs == null)
            {
                return ((rhs == null) ? 0 : -1);
            }
            else if (rhs == null)
            {
                return ((lhs == null) ? 0 : 1);
            }

            if (lhs.Rank < rhs.Rank) return 1;
            if (lhs.Rank > rhs.Rank) return -1;
            return 0;
        }

        // Sort delegate
        public static int DescendingRoundKPM(PlayerModel lhs, PlayerModel rhs)
        {
            if (lhs == null)
            {
                return ((rhs == null) ? 0 : -1);
            }
            else if (rhs == null)
            {
                return ((lhs == null) ? 0 : 1);
            }
            if (lhs.KPMRound < rhs.KPMRound) return 1;
            if (lhs.KPMRound > rhs.KPMRound) return -1;
            return 0;
        }

        // Sort delegate
        public static int DescendingMetricSquad(SquadRoster lhs, SquadRoster rhs)
        {
            if (lhs == null)
            {
                return ((rhs == null) ? 0 : -1);
            }
            else if (rhs == null)
            {
                return ((lhs == null) ? 0 : 1);
            }

            // Dividing by Clan Tag takes precedence, only when both are zero is the metric used
            if (lhs.ClanTagCount > 0 || rhs.ClanTagCount > 0)
            {
                if (lhs.ClanTagCount < rhs.ClanTagCount) { return 1; }
                if (lhs.ClanTagCount > rhs.ClanTagCount) { return -1; }
                return 0;
            }

            if (lhs.Metric < rhs.Metric) return 1;
            if (lhs.Metric > rhs.Metric) return -1;
            return 0;
        }


        // Sort delegate
        public static int DescendingSPM(PlayerModel lhs, PlayerModel rhs)
        {
            if (lhs == null)
            {
                return ((rhs == null) ? 0 : -1);
            }
            else if (rhs == null)
            {
                return ((lhs == null) ? 0 : 1);
            }
            double lSPM = (lhs.StatsVerified) ? lhs.SPM : lhs.SPMRound;
            double rSPM = (rhs.StatsVerified) ? rhs.SPM : rhs.SPMRound;
            if (lSPM < rSPM) return 1;
            if (lSPM > rSPM) return -1;
            return 0;
        }


        // Sort delegate
        public static int DescendingKDR(PlayerModel lhs, PlayerModel rhs)
        {
            if (lhs == null)
            {
                return ((rhs == null) ? 0 : -1);
            }
            else if (rhs == null)
            {
                return ((lhs == null) ? 0 : 1);
            }
            double lKDR = (lhs.StatsVerified) ? lhs.KDR : lhs.KDRRound;
            double rKDR = (rhs.StatsVerified) ? rhs.KDR : rhs.KDRRound;
            if (lKDR < rKDR) return 1;
            if (lKDR > rKDR) return -1;
            return 0;
        }


        // Sort delegate
        public static int DescendingKPM(PlayerModel lhs, PlayerModel rhs)
        {
            if (lhs == null)
            {
                return ((rhs == null) ? 0 : -1);
            }
            else if (rhs == null)
            {
                return ((lhs == null) ? 0 : 1);
            }
            double lKPM = (lhs.StatsVerified) ? lhs.KPM : lhs.KPMRound;
            double rKPM = (rhs.StatsVerified) ? rhs.KPM : rhs.KPMRound;
            if (lKPM < rKPM) return 1;
            if (lKPM > rKPM) return -1;
            return 0;
        }


        private void GatherProconGoodies()
        {
            fFriendlyMaps.Clear();
            fFriendlyModes.Clear();
            List<CMap> bf3_defs = this.GetMapDefines();
            foreach (CMap m in bf3_defs)
            {
                if (!fFriendlyMaps.ContainsKey(m.FileName)) fFriendlyMaps[m.FileName] = m.PublicLevelName;
                if (!fFriendlyModes.ContainsKey(m.PlayList)) fFriendlyModes[m.PlayList] = m.GameMode;
            }
            if (DebugLevel >= 8)
            {
                foreach (KeyValuePair<String, String> pair in fFriendlyMaps)
                {
                    DebugWrite("friendlyMaps[" + pair.Key + "] = " + pair.Value, 8);
                }
                foreach (KeyValuePair<String, String> pair in fFriendlyModes)
                {
                    DebugWrite("friendlyModes[" + pair.Key + "] = " + pair.Value, 8);
                }
            }
            DebugWrite("Friendly names loaded", 6);
        }


        private PlayerModel GetPlayer(String name)
        {
            if (String.IsNullOrEmpty(name)) return null;
            PlayerModel p = null;
            lock (fKnownPlayers)
            {
                if (!fKnownPlayers.TryGetValue(name, out p))
                {
                    p = null;
                }
            }
            if (p == null && DebugLevel >= 8) ConsoleDebug("GetPlayer unknown player ^b" + name);
            return p;
        }

        private double RemainingTicketPercent(double tickets, double goal)
        {
            if (goal == 0)
            {
                if (IsRush() && tickets > fMaxTickets && tickets < fRushMaxTickets)
                {
                    double normalized = Math.Max(0, fMaxTickets - (fRushMaxTickets - tickets));
                    return ((normalized / fMaxTickets) * 100.0);
                }
                return ((tickets / fMaxTickets) * 100.0);
            }
            return (((goal - tickets) / goal) * 100.0);
        }

        private double RemainingTickets()
        {
            double ret = 0;
            if (fServerInfo == null || fServerInfo.TeamScores.Count < 2) return 0;

            if (IsConquest() || IsRush())
            {
                // Pick lowest ticket count of all teams
                ret = Double.MaxValue;
                foreach (TeamScore ts in fServerInfo.TeamScores)
                {
                    if (ts.Score < ret) ret = ts.Score;
                }
            }
            else
            {
                // Picket highest ticket count of all teams
                ret = 0;
                double tmax = 0;
                foreach (TeamScore ts in fServerInfo.TeamScores)
                {
                    if (ts.Score > ret) ret = ts.Score;
                    if (ts.WinningScore > tmax) tmax = ts.WinningScore;
                }
                ret = tmax - ret;
            }

            return ret;
        }

        private TimeSpan GetPlayerJoinedTimeSpan(PlayerModel player)
        {
            if (player != null && player.FirstSeenTimestamp != DateTime.MinValue)
            {
                return (DateTime.Now.Subtract(player.FirstSeenTimestamp));
            }
            return TimeSpan.FromMinutes(0);
        }

        private void DebugBalance(String msg)
        {
            // Filter out repeat messages
            int level = 5;
            if (fLastMsg != null)
            {
                if (msg.Equals(fLastMsg))
                {
                    level = 8;
                }
                else
                {
                    String[] mWords = msg.Split(new Char[] { ' ' });
                    String[] lWords = fLastMsg.Split(new Char[] { ' ' });

                    int n = Math.Min(mWords.Length, lWords.Length);
                    int i = 0;
                    for (i = 0; i < n; ++i)
                    {
                        if (!mWords[i].Equals(lWords[i])) break;
                    }
                    if ((i + 1) >= 5) level = 8;
                }
            }
            DebugWrite("^5(AUTO)^9 " + msg, level);
            fLastMsg = msg;
        }

        private void DebugFast(String msg)
        {
            DebugWrite("^5(FAST)^9 " + msg, 5);
        }


        private void DebugUnswitch(String msg)
        {
            String prefix = String.Empty;
            if (Thread.CurrentThread.Name == null || Thread.CurrentThread.Name != "unswitcher") prefix = "^5(SWITCH)";
            DebugWrite(prefix + " ^6" + msg, 5);
        }


        private void DebugFetch(String msg)
        {
            DebugFetch(msg, 7);
        }

        private void DebugFetch(String msg, int level)
        {
            String prefix = String.Empty;
            if (Thread.CurrentThread.Name == null || (Thread.CurrentThread.Name != "fetcher" && Thread.CurrentThread.Name != "ResponseLoop")) prefix = "^5(FETCH)";
            DebugWrite(prefix + " ^9" + msg, level);
        }

        private void DebugScrambler(String msg)
        {
            String prefix = String.Empty;
            if (Thread.CurrentThread.Name == null || Thread.CurrentThread.Name != "scrambler") prefix = "^5(SCRAMBLER)";
            DebugWrite(prefix + " ^9" + msg, 6);
        }


        private double NextSwapGroupInSeconds(PerModeSettings perMode)
        {
            if (fFullUnstackSwapTimestamp == DateTime.MinValue) return 0;
            if (fUnstackGroupCount > 0 && fUnstackGroupCount <= perMode.NumberOfSwapsPerGroup) return 0;
            double since = DateTime.Now.Subtract(fFullUnstackSwapTimestamp).TotalSeconds;
            if (since > perMode.DelaySecondsBetweenSwapGroups) return 0;
            return (perMode.DelaySecondsBetweenSwapGroups - since);
        }


        private String GetPlayerStatsString(String name)
        {
            DateTime now = DateTime.Now;
            double score = -1;
            double kills = -1;
            double deaths = -1;
            double kdr = -1;
            double spm = -1;
            double kpm = -1;
            int team = -1;
            bool ok = false;
            TimeSpan tir = TimeSpan.FromSeconds(0); // Time In Round
            PlayerModel m = null;

            lock (fKnownPlayers)
            {
                if (fKnownPlayers.TryGetValue(name, out m))
                {
                    ok = true;
                    m.LastSeenTimestamp = now;
                    m.IsDeployed = false;
                    score = m.ScoreRound;
                    kills = m.KillsRound;
                    deaths = m.DeathsRound;
                    kdr = m.KDRRound;
                    spm = m.SPMRound;
                    kpm = m.KPMRound;
                    tir = now.Subtract((m.FirstSpawnTimestamp != DateTime.MinValue) ? m.FirstSpawnTimestamp : now);
                    team = m.Team;
                }
            }

            if (!ok) return ("NO STATS FOR: " + name);

            String type = "ROUND";
            if (WhichBattlelogStats != BattlelogStats.ClanTagOnly && m.StatsVerified)
            {
                type = (WhichBattlelogStats == BattlelogStats.AllTime) ? "ALL-TIME" : "RESET";
                kdr = m.KDR;
                spm = m.SPM;
                kpm = m.KPM;
            }

            Match rm = Regex.Match(tir.ToString(), @"([0-9]+:[0-9]+:[0-9]+)");
            String sTIR = (rm.Success) ? rm.Groups[1].Value : "?";
            String vn = m.FullName;

            return ("^0" + type + " STATS: ^b" + vn + "^n [T:" + team + ", S:" + score + ", K:" + kills + ", D:" + deaths + ", KDR:" + kdr.ToString("F2") + ", SPM:" + spm.ToString("F0") + ", KPM:" + kpm.ToString("F1") + ", TIR: " + sTIR + "]");
        }

        private double GetPlayerStat(PlayerModel player, DefineStrong which)
        {
            double stat = 0;
            switch (which)
            {
                case DefineStrong.RoundScore:
                    stat = player.ScoreRound;
                    break;
                case DefineStrong.RoundSPM:
                    stat = player.SPMRound;
                    break;
                case DefineStrong.RoundKills:
                    stat = player.KillsRound;
                    break;
                case DefineStrong.RoundKDR:
                    stat = player.KDRRound;
                    break;
                case DefineStrong.PlayerRank:
                    stat = player.Rank;
                    break;
                case DefineStrong.RoundKPM:
                    stat = player.KPMRound;
                    break;
                case DefineStrong.BattlelogSPM:
                    stat = ((player.StatsVerified) ? player.SPM : player.SPMRound);
                    break;
                case DefineStrong.BattlelogKDR:
                    stat = ((player.StatsVerified) ? player.KDR : player.KDRRound);
                    break;
                case DefineStrong.BattlelogKPM:
                    stat = ((player.StatsVerified) ? player.KPM : player.KPMRound);
                    break;
                default:
                    break;
            }
            return stat;
        }


        private double GetTimeInRoundMinutes()
        {
            DateTime rst = (fRoundStartTimestamp == DateTime.MinValue) ? DateTime.Now : fRoundStartTimestamp;
            return (DateTime.Now.Subtract(fRoundStartTimestamp).TotalMinutes);
        }

        private String GetTimeInRoundString()
        {
            DateTime rst = (fRoundStartTimestamp == DateTime.MinValue) ? DateTime.Now : fRoundStartTimestamp;
            Match rm = Regex.Match(DateTime.Now.Subtract(fRoundStartTimestamp).ToString(), @"([0-9]+:[0-9]+:[0-9]+)");
            return ((rm.Success) ? rm.Groups[1].Value : "?");
        }

        private void SetSpawnMessages(String name, String chat, String yell, bool quiet)
        {
            PlayerModel player = null;

            player = GetPlayer(name);
            if (player == null) return;

            if (!String.IsNullOrEmpty(player.SpawnChatMessage))
            {
                DebugWrite("^9Overwriting previous chat message for ^b" + name + "^n: " + player.SpawnChatMessage, 7);
            }
            player.SpawnChatMessage = chat;
            player.SpawnYellMessage = yell;
            player.QuietMessage = quiet;
        }

        private void FireMessages(String name)
        {
            PlayerModel player = GetPlayer(name);
            if (player == null) return;

            if (!String.IsNullOrEmpty(player.SpawnChatMessage) || !String.IsNullOrEmpty(player.SpawnYellMessage))
            {
                DebugWrite("^5(SPAWN)^9 firing messages delayed until spawn for ^b" + name, 5);
            }
            if (!String.IsNullOrEmpty(player.SpawnChatMessage)) Chat(name, player.SpawnChatMessage, player.QuietMessage);
            if (!String.IsNullOrEmpty(player.SpawnYellMessage)) Yell(name, player.SpawnYellMessage);
            player.SpawnChatMessage = String.Empty;
            player.SpawnYellMessage = String.Empty;
            player.QuietMessage = false;
        }

        private void CheckDelayedMove(String name)
        {
            PlayerModel player = GetPlayer(name);
            if (player == null) return;

            if (player.DelayedMove != null)
            {
                MoveInfo dm = player.DelayedMove;
                player.DelayedMove = null;

                DebugWrite("^5(SPAWN)^9 executing delayed move of ^b" + name, 5);
                DebugUnswitch("FORBIDDEN: Detected bad team switch, scheduling admin kill and move for ^b: " + name);
                String log = "^4^bUNSWITCHING^n^0 ^b" + player.FullName + "^n from " + dm.SourceName + " back to " + dm.DestinationName;
                log = (EnableLoggingOnlyMode) ? "^9(SIMULATING)^0 " + log : log;
                DebugWrite(log, 3);
                KillAndMoveAsync(dm);
            }
        }

        private double GetTeamPoints(int team)
        {
            double total = 0;
            List<String> dup = new List<String>();
            // copy player name list
            lock (fAllPlayers)
            {
                dup.AddRange(fAllPlayers);
            }
            // sum up player points for specified team
            foreach (String name in dup)
            {
                PlayerModel player = GetPlayer(name);
                if (player.Team != team) continue;
                total = total + player.ScoreRound;
            }
            return total;
        }


        private PerModeSettings GetPerModeSettings()
        {
            PerModeSettings perMode = null;
            if (fModeToSimple == null || fServerInfo == null) return new PerModeSettings();
            String simpleMode = String.Empty;
            if (fModeToSimple.TryGetValue(fServerInfo.GameMode, out simpleMode)
            && !String.IsNullOrEmpty(simpleMode)
            && fPerMode.TryGetValue(simpleMode, out perMode)
            && perMode != null)
            {
                return perMode;
            }
            ConsoleDebug("GetPerModeSettings: using default settings for " + fServerInfo.GameMode + " => " + simpleMode);
            return new PerModeSettings();
        }

        private void CheckDeativateBalancer(String reason)
        {
            if (fBalanceIsActive)
            {
                fBalanceIsActive = false;
                double dur = DateTime.Now.Subtract(fLastBalancedTimestamp).TotalSeconds;
                if (fLastBalancedTimestamp == DateTime.MinValue) dur = 0;
                if (dur > 0)
                {
                    if (DebugLevel >= 6) DebugBalance("^2^b" + reason + "^n: Was active for " + dur.ToString("F0") + " seconds!");
                }
            }
        }

        /* DCE - Disperse Clan Evenly */
        private bool IsClanDispersal(PlayerModel player, bool ignoreWhitelist)
        {
            if (player == null) return false;
            PerModeSettings perMode = GetPerModeSettings();
            if (perMode.DisperseEvenlyByClanPlayers == 0) return false;
            if (OnWhitelist && !ignoreWhitelist && CheckWhitelist(player, WL_DISPERSE)) return false;
            bool disperse = false;
            String extractedTag = ExtractTag(player);
            if (!String.IsNullOrEmpty(extractedTag) && GetClanPopulation(player, 0) >= perMode.DisperseEvenlyByClanPlayers)
            { // 0 means all teams
                disperse = true;
            }
            return disperse;
        }

        /* DCE */
        private int GetClanPopulation(PlayerModel player, int teamId)
        {
            if (player == null) return 0;
            Scope scope = Scope.Total;
            switch (teamId)
            {
                case 1:
                    scope = Scope.TeamOne;
                    break;
                case 2:
                    scope = Scope.TeamTwo;
                    break;
                case 3:
                    scope = Scope.TeamThree;
                    break;
                case 4:
                    scope = Scope.TeamFour;
                    break;
                default:
                    break;
            }
            return CountMatchingTags(player, scope);
        }


        private bool IsInDispersalList(PlayerModel player, bool ignoreWhitelist)
        {
            if (player == null) return false;
            if (player.Role != ROLE_PLAYER) return false;
            player.DispersalGroup = 0;
            PerModeSettings perMode = GetPerModeSettings();
            if (!perMode.EnableDisperseEvenlyList) return false;
            bool isDispersalByList = false;
            String extractedTag = ExtractTag(player);
            if (String.IsNullOrEmpty(extractedTag))
            {
                extractedTag = INVALID_NAME_TAG_GUID;
            }
            String guid = (String.IsNullOrEmpty(player.EAGUID)) ? INVALID_NAME_TAG_GUID : player.EAGUID;
            if (fSettingDisperseEvenlyList.Count > 0)
            {
                if (fSettingDisperseEvenlyList.Contains(player.Name)
                || fSettingDisperseEvenlyList.Contains(guid)
                || fSettingDisperseEvenlyList.Contains(extractedTag))
                {
                    // Special case for whitelist options: clan tag on dispersal list, but Whitelist option enabled
                    if (fSettingDisperseEvenlyList.Contains(extractedTag)
                        && !fSettingDisperseEvenlyList.Contains(player.Name)
                        && !fSettingDisperseEvenlyList.Contains(guid)
                        && OnWhitelist
                        && !ignoreWhitelist
                        && CheckWhitelist(player, WL_DISPERSE))
                    {
                        isDispersalByList = false;
                    }
                    else
                    {
                        isDispersalByList = true;
                    }
                }
            }
            for (int i = 1; i <= 4; ++i)
            { // Up to 4 groups
                if (!isDispersalByList && fDispersalGroups[i].Count > 0)
                {
                    fDispersalGroups[i] = new List<String>(fDispersalGroups[i]);
                    if (fDispersalGroups[i].Contains(player.Name)
                    || fDispersalGroups[i].Contains(guid)
                    || fDispersalGroups[i].Contains(extractedTag))
                    {
                        // Special case for whitelist options: clan tag on dispersal list, but Whitelist option enabled
                        if (fDispersalGroups[i].Contains(extractedTag)
                            && !fDispersalGroups[i].Contains(player.Name)
                            && !fDispersalGroups[i].Contains(guid)
                            && OnWhitelist
                            && !ignoreWhitelist
                            && CheckWhitelist(player, WL_DISPERSE))
                        {
                            isDispersalByList = false;
                        }
                        else
                        {
                            isDispersalByList = true;
                            player.DispersalGroup = i;
                        }
                        break;
                    }
                }
            }
            return (isDispersalByList);
        }

        private bool IsRankDispersal(PlayerModel player)
        {
            if (player == null) return false;
            if (player.Role != ROLE_PLAYER) return false;
            PerModeSettings perMode = GetPerModeSettings();
            if (perMode.DisperseEvenlyByRank == 0) return false;
            if (SameClanTagsForRankDispersal && CountMatchingTags(player, Scope.SameTeam) >= 2)
            {
                if (player.Rank >= perMode.DisperseEvenlyByRank) DebugWrite("^9Exempting player from rank dispersal, due to SameClanTagsForRankDispersal: ^b" + "^b" + player.FullName + "^n", 6);
                return false;
            }
            if (OnWhitelist && CheckWhitelist(player, WL_RANK)) return false; // special case for whitelist options
            return (player.Rank >= perMode.DisperseEvenlyByRank);
        }

        private void FinishedFullSwap(String name, PerModeSettings perMode)
        {
            fUnstackGroupCount = fUnstackGroupCount + 1;
            if (fUnstackGroupCount >= perMode.NumberOfSwapsPerGroup)
            {
                fFullUnstackSwapTimestamp = DateTime.Now; // start the timer
                DebugBalance("For ^b" + name + "^n, finished group of " + perMode.NumberOfSwapsPerGroup + ", delay timer set");
                fUnstackGroupCount = 0;
            }
            else
            {
                DebugBalance("For ^b" + name + "^n, did swap " + fUnstackGroupCount + " of " + perMode.NumberOfSwapsPerGroup);
                fFullUnstackSwapTimestamp = DateTime.MinValue;
            }
        }

        private void ValidateInt(ref int val, String propName, int def)
        {
            if (val < 0)
            {
                ConsoleError("^b" + propName + "^n must be greater than or equal to 0, was set to " + val + ", corrected to " + def);
                val = def;
                return;
            }
        }


        private void ValidateIntRange(ref int val, String propName, int min, int max, int def, bool zeroOK)
        {
            if (zeroOK && val == 0) return;
            if (val < min || val > max)
            {
                String zero = (zeroOK) ? " or equal to 0" : String.Empty;
                ConsoleError("^b" + propName + "^n must be greater than or equal to " + min + " and less than or equal to " + max + zero + ", was set to " + val + ", corrected to " + def);
                val = def;
            }
        }


        private void ValidateDouble(ref double val, String propName, double def)
        {
            if (val < 0)
            {
                ConsoleError("^b" + propName + "^n must be greater than or equal to 0, was set to " + val + ", corrected to " + def);
                val = def;
                return;
            }
        }


        private void ValidateDoubleRange(ref double val, String propName, double min, double max, double def, bool zeroOK)
        {
            if (zeroOK && val == 0.0) return;
            if (val < min || val > max)
            {
                String zero = (zeroOK) ? " or equal to 0" : String.Empty;
                ConsoleError("^b" + propName + "^n must be greater than or equal to " + min + " and less than or equal to " + max + zero + ", was set to " + val + ", corrected to " + def);
                val = def;
                return;
            }
        }


        private void CheckRageQuit(String name)
        {
            /*
            Heuristic: if player leaves server within 1 minute of being moved, treat as a rage quit
            due to actions of this plugin.
            */
            PlayerModel player = GetPlayer(name);
            if (player == null) return;

            ++fTotalQuits;

            if (player.MovedTimestamp != DateTime.MinValue && DateTime.Now.Subtract(player.MovedTimestamp).TotalSeconds <= 60)
            {
                ++fRageQuits;
                DebugWrite("Looks like ^b" + name + "^n rage quit: " + fRageQuits + " so far this round, out of " + fTotalQuits, 4);
            }
        }


        private int CountMatchingTags(PlayerModel player, Scope scope)
        {
            if (player == null) return 0;
            if (player.Team == 0 || player.Squad == 0) return 0;
            int team = player.Team;
            int squad = player.Squad;

            if (scope == Scope.TeamOne) { team = 1; }
            else if (scope == Scope.TeamTwo) { team = 2; }
            else if (scope == Scope.TeamThree) { team = 3; }
            else if (scope == Scope.TeamFour) { team = 4; }

            List<PlayerModel> teamList = GetTeam(team);
            if (teamList == null) return 0;

            String tag = ExtractTag(player);
            if (String.IsNullOrEmpty(tag)) return 0;
            int same = 0;
            int verified = 0;
            int total = 0;

            foreach (PlayerModel mate in teamList)
            {
                if (scope == Scope.SameSquad && mate.Squad != squad) continue;
                ++total;
                if (mate.TagVerified) ++verified;
                if (fTestClanDispersal)
                {
                    // Treat tags of same length as equal
                    if (ExtractTag(mate).Length == tag.Length)
                    {
                        ++same;
                        continue;
                    }
                }
                if (ExtractTag(mate) == tag) ++same;
            }

            String sname = GetSquadName(squad);

            String loc = sname;
            if (scope == Scope.SameTeam || (scope >= Scope.TeamOne && scope <= Scope.TeamFour)) loc = GetTeamName(team);
            else if (scope == Scope.Total) loc = "server";

            if (verified < 2)
            {
                if (DebugLevel >= 7) DebugBalance("Count for matching tags for player ^b" + player.Name + "^n in " + loc + ", not enough verified tags to find matches");
                return 0;
            }
            else
            {
                if (DebugLevel >= 7 && same > 1) DebugBalance("Count for matching tags for player ^b" + player.Name + "^n in " + loc + ", found " + same + " matching tags [" + tag + "]");
            }
            return same;
        }


        private void ListSideBySide(List<PlayerModel> us, List<PlayerModel> ru, bool useScrambledSquad, bool useSquadSort)
        {
            int max = Math.Max(us.Count, ru.Count);

            // Sort lists by specified metric, which might have changed by now, oh well
            List<PlayerModel> all = new List<PlayerModel>();
            PlayerModel player = null;
            double usTotal = 0;
            foreach (PlayerModel u in us)
            {
                String en = u.Name;
                player = u;
                if (player == null) continue;
                all.Add(player);
                double stat = GetPlayerStat(player, ScrambleBy);
                /*
                switch (ScrambleBy) {
                    case DefineStrong.RoundScore:
                        stat = player.ScoreRound;
                        break;
                    case DefineStrong.RoundSPM:
                        stat = player.SPMRound;
                        break;
                    case DefineStrong.RoundKills:
                        stat = player.KillsRound;
                        break;
                    case DefineStrong.RoundKDR:
                        stat = player.KDRRound;
                        break;
                    case DefineStrong.PlayerRank:
                        stat = player.Rank;
                        break;
                    case DefineStrong.RoundKPM:
                        stat = player.KPMRound;
                        break;
                    case DefineStrong.BattlelogSPM:
                        stat = ((player.StatsVerified) ? player.SPM :player.SPMRound);
                        break;
                    case DefineStrong.BattlelogKDR:
                        stat = ((player.StatsVerified) ? player.KDR :player.KDRRound);
                        break;
                    case DefineStrong.BattlelogKPM:
                        stat = ((player.StatsVerified) ? player.KPM :player.KPMRound);
                        break;
                    default:
                        break;
                }
                */
                usTotal = usTotal + stat;
            }
            double usAvg = usTotal / Math.Max(1, us.Count);

            double ruTotal = 0;
            foreach (PlayerModel r in ru)
            {
                String en = r.Name;
                player = r;
                if (player == null) continue;
                all.Add(player);
                double stat = GetPlayerStat(player, ScrambleBy);
                /*
                switch (ScrambleBy) {
                    case DefineStrong.RoundScore:
                        stat = player.ScoreRound;
                        break;
                    case DefineStrong.RoundSPM:
                        stat = player.SPMRound;
                        break;
                    case DefineStrong.RoundKills:
                        stat = player.KillsRound;
                        break;
                    case DefineStrong.RoundKDR:
                        stat = player.KDRRound;
                        break;
                    case DefineStrong.PlayerRank:
                        stat = player.Rank;
                        break;
                    case DefineStrong.RoundKPM:
                        stat = player.KPMRound;
                        break;
                    case DefineStrong.BattlelogSPM:
                        stat = ((player.StatsVerified) ? player.SPM :player.SPMRound);
                        break;
                    case DefineStrong.BattlelogKDR:
                        stat = ((player.StatsVerified) ? player.KDR :player.KDRRound);
                        break;
                    case DefineStrong.BattlelogKPM:
                        stat = ((player.StatsVerified) ? player.KPM :player.KPMRound);
                        break;
                    default:
                        break;
                }
                */
                ruTotal = ruTotal + stat;
            }
            double ruAvg = ruTotal / Math.Max(1, ru.Count);

            String kstat = "?";
            switch (ScrambleBy)
            {
                case DefineStrong.RoundScore:
                    all.Sort(DescendingRoundScore);
                    kstat = "S";
                    break;
                case DefineStrong.RoundSPM:
                    all.Sort(DescendingRoundSPM);
                    kstat = "SPM";
                    break;
                case DefineStrong.RoundKills:
                    all.Sort(DescendingRoundKills);
                    kstat = "K";
                    break;
                case DefineStrong.RoundKDR:
                    all.Sort(DescendingRoundKDR);
                    kstat = "KDR";
                    break;
                case DefineStrong.PlayerRank:
                    all.Sort(DescendingPlayerRank);
                    kstat = "R";
                    break;
                case DefineStrong.RoundKPM:
                    all.Sort(DescendingRoundKPM);
                    kstat = "KPM";
                    break;
                case DefineStrong.BattlelogSPM:
                    all.Sort(DescendingSPM);
                    kstat = "bSPM";
                    break;
                case DefineStrong.BattlelogKDR:
                    all.Sort(DescendingKDR);
                    kstat = "bKDR";
                    break;
                case DefineStrong.BattlelogKPM:
                    all.Sort(DescendingKPM);
                    kstat = "bKPM";
                    break;
                default:
                    all.Sort(DescendingRoundScore);
                    break;
            }
            List<String> allNames = new List<String>();
            foreach (PlayerModel p in all)
            {
                allNames.Add(p.Name); // sorted name list
            }

            // Sort teams

            if (useSquadSort)
            {
                us.Sort(delegate (PlayerModel lhs, PlayerModel rhs)
                { // ascending squad id
                    if (lhs == null && rhs == null) return 0;
                    if (lhs == null) return -1;
                    if (rhs == null) return 1;

                    int l = (useScrambledSquad) ? lhs.ScrambledSquad : lhs.Squad;
                    int r = (useScrambledSquad) ? rhs.ScrambledSquad : rhs.Squad;
                    if (l == 0 && r == 0) return 0;
                    if (l == 0) l = 999; // 0 sorts to end
                    if (r == 0) r = 999;
                    if (l < r) return -1;
                    if (l > r) return 1;
                    return 0;
                });
                ru.Sort(delegate (PlayerModel lhs, PlayerModel rhs)
                { // ascending squad id
                    if (lhs == null && rhs == null) return 0;
                    if (lhs == null) return -1;
                    if (rhs == null) return 1;

                    int l = (useScrambledSquad) ? lhs.ScrambledSquad : lhs.Squad;
                    int r = (useScrambledSquad) ? rhs.ScrambledSquad : rhs.Squad;
                    if (l == 0 && r == 0) return 0;
                    if (l == 0) l = 999; // 0 sorts to end
                    if (r == 0) r = 999;
                    if (l < r) return -1;
                    if (l > r) return 1;
                    return 0;
                });
            }
            else
            {
                us.Sort(delegate (PlayerModel lhs, PlayerModel rhs)
                { // descending position in allNames
                    if (lhs == null && rhs == null) return 0;
                    if (lhs == null) return -1;
                    if (rhs == null) return 1;

                    int l = allNames.IndexOf(lhs.Name) + 1;
                    int r = allNames.IndexOf(rhs.Name) + 1;
                    if (l == 0 && r == 0) return 0;
                    if (l == 0) return 1; // 0 sorts to end
                    if (r == 0) return 1;
                    if (l < r) return -1;
                    if (l > r) return 1;
                    return 0;
                });
                ru.Sort(delegate (PlayerModel lhs, PlayerModel rhs)
                { // descending position in allNames
                    if (lhs == null && rhs == null) return 0;
                    if (lhs == null) return -1;
                    if (rhs == null) return 1;

                    int l = allNames.IndexOf(lhs.Name) + 1;
                    int r = allNames.IndexOf(rhs.Name) + 1;
                    if (l == 0 && r == 0) return 0;
                    if (l == 0) return 1; // 0 sorts to end
                    if (r == 0) return 1;
                    if (l < r) return -1;
                    if (l > r) return 1;
                    return 0;
                });
            }

            for (int i = 0; i < max; ++i)
            {
                String u = " ";
                String r = " ";
                String xt = "";
                int sq = 0;
                if (i < us.Count)
                {
                    try
                    {
                        player = us[i];
                        xt = ExtractTag(player);
                        if (!String.IsNullOrEmpty(xt))
                        {
                            xt = "[" + xt + "]" + player.Name;
                        }
                        else
                        {
                            xt = player.Name;
                        }
                        sq = Math.Max(0, Math.Min(((useScrambledSquad) ? player.ScrambledSquad : player.Squad), SQUAD_NAMES.Length - 1));
                    }
                    catch (Exception e) { ConsoleException(e); }
                    //u = xt + " (" + SQUAD_NAMES[sq] + ", " + kstat + ":#" + (allNames.IndexOf(player.Name)+1) + ")";
                    u = "(" + GetSquadName(sq) + ", " + kstat + ":#" + (allNames.IndexOf(player.Name) + 1) + ") " + xt;
                }
                if (i < ru.Count)
                {
                    try
                    {
                        player = ru[i];
                        xt = ExtractTag(player);
                        if (!String.IsNullOrEmpty(xt))
                        {
                            xt = "[" + xt + "]" + player.Name;
                        }
                        else
                        {
                            xt = player.Name;
                        }
                        sq = Math.Max(0, Math.Min(((useScrambledSquad) ? player.ScrambledSquad : player.Squad), SQUAD_NAMES.Length - 1));
                    }
                    catch (Exception e) { ConsoleException(e); }
                    r = xt + " (" + GetSquadName(sq) + ", " + kstat + ":#" + (allNames.IndexOf(player.Name) + 1) + ")";
                }
                ConsoleDump(String.Format("{0,-40} - {1,40}", u, r));
            }
            String divider = "----------------------------------------";
            ConsoleDump(String.Format("{0,-40} - {1,40}", divider, divider));
            if (usAvg != 0 && ruAvg != 0) ConsoleDump(String.Format("{0,-40} - {1,40}",
                "US AVG " + kstat + ":" + usAvg.ToString("F2"),
                "RU AVG " + kstat + ":" + ruAvg.ToString("F2")
            ));

        }

        private String ExtractName(String fullName)
        {
            String ret = fullName;
            Match m = Regex.Match(fullName, @"\[\w+\](\w+)");
            if (m.Success)
            {
                ret = m.Groups[1].Value;
            }
            return ret;
        }

        private void CheckServerInfoUpdate()
        {
            // Already checked IsRush
            PerModeSettings perMode = GetPerModeSettings();
            if (DateTime.Now.Subtract(fLastServerInfoTimestamp).TotalSeconds >= perMode.SecondsToCheckForNewStage)
            {
                ServerCommand("serverInfo");
                //fLastServerInfoTimestamp = DateTime.Now;
            }
        }

        private bool AttackerTicketsWithinRangeOfMax(double attacker)
        {
            if (attacker >= fMaxTickets) return true;
            PerModeSettings perMode = GetPerModeSettings();
            return (attacker + Math.Min(12, 2 * perMode.SecondsToCheckForNewStage / 5) >= fMaxTickets);
        }


        private double RushAttackerAvgLoss()
        {
            if (fRushAttackerStageSamples == 0) return fRushAttackerStageLoss;
            return (fRushAttackerStageLoss / fRushAttackerStageSamples);
        }

        private bool AdjustForMetro(PerModeSettings perMode)
        {
            if (perMode == null) return false;
            if (!perMode.EnableMetroAdjustments) return false;
            if (perMode.EnableTicketLossRatio) return false;
            if (fServerInfo == null) return false;
            return (fServerInfo.Map == "MP_Subway" || fServerInfo.Map == "XP0_Metro");
        }

        private void LogExternal(String msg)
        {
            if (msg == null || ExternalLogSuffix == null) return;
            String entry = "[" + DateTime.Now.ToString("HH:mm:ss") + "] ";
            entry = entry + msg;
            entry = Regex.Replace(entry, @"\^[bni\d]", String.Empty);
            entry = entry.Replace(" [" + GetPluginName() + "]", String.Empty);
            String date = DateTime.Now.ToString("yyyyMMdd");
            String suffix = (String.IsNullOrEmpty(ExternalLogSuffix)) ? "_mb.log" : ExternalLogSuffix;
            String path = Path.Combine(Path.Combine("Logs", fHost + "_" + fPort), date + suffix);

            try
            {
                if (!Path.IsPathRooted(path)) path = Path.Combine(Directory.GetParent(Application.ExecutablePath).FullName, path);

                // Add newline
                entry = entry + "\n";

                lock (ExternalLogSuffix)
                { // mutex access to log file
                    using (FileStream fs = File.Open(path, FileMode.Append))
                    {
                        Byte[] info = new UTF8Encoding(true).GetBytes(entry);
                        fs.Write(info, 0, info.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleError("Unable to append to log file: " + path);
                ConsoleError(ex.ToString());
            }
        }

        void ApplyWizardSettings()
        {
            ConsoleWrite("Applying Wizard settings ...", 0);

            // Validate the numbers
            ValidateIntRange(ref MaximumPlayersForMode, "Maximum Players For Mode", 8, 70, 64, false);
            ValidateIntRange(ref LowestMaximumTicketsForMode, "Lowest Maximum Tickets For Mode", 20, 10000, 300, false);
            ValidateIntRange(ref HighestMaximumTicketsForMode, "Highest Maximum Tickets For Mode", 20, 10000, 400, false);
            if (HighestMaximumTicketsForMode < LowestMaximumTicketsForMode)
            {
                ConsoleError("^b" + "Highest Maximum Tickets For Mode" + "^n must be greater than ^b" + "Lowest Maximum Tickets For Mode" + "^n, corrected");
                int tmp = HighestMaximumTicketsForMode;
                HighestMaximumTicketsForMode = LowestMaximumTicketsForMode;
                LowestMaximumTicketsForMode = tmp;
            }

            try
            {
                String modeName = WhichMode;
                if (modeName == "Conq Small or Dom or Scav") modeName = "Conq Small, Dom, Scav"; // settings don't like commas in enum
                ConsoleWrite("For mode: ^b" + modeName, 0);
                PerModeSettings perMode = null;
                if (fPerMode == null)
                {
                    ConsoleWarn("Settings Wizard failed due to being disabled, please enable the plugin!");
                    return;
                }
                if (fPerMode.TryGetValue(modeName, out perMode) && perMode != null)
                {
                    bool isCTF = (modeName == "CTF");
                    bool isCarrierAssault = modeName.Contains("Carrier");
                    bool isObliteration = modeName.Contains("Obliteration");

                    // Set the per mode Max Players
                    perMode.MaxPlayers = MaximumPlayersForMode;
                    ConsoleWrite("Set ^bMax Players^n to " + perMode.MaxPlayers, 0);

                    // Set the Population ranges
                    if (MaximumPlayersForMode >= 64)
                    {
                        perMode.DefinitionOfHighPopulationForPlayers = 48;
                        perMode.DefinitionOfLowPopulationForPlayers = 16;
                    }
                    else if (MaximumPlayersForMode >= 56)
                    {
                        perMode.DefinitionOfHighPopulationForPlayers = 40;
                        perMode.DefinitionOfLowPopulationForPlayers = 16;
                    }
                    else if (MaximumPlayersForMode >= 48)
                    {
                        perMode.DefinitionOfHighPopulationForPlayers = 32;
                        perMode.DefinitionOfLowPopulationForPlayers = 16;
                    }
                    else if (MaximumPlayersForMode >= 40)
                    {
                        perMode.DefinitionOfHighPopulationForPlayers = 28;
                        perMode.DefinitionOfLowPopulationForPlayers = 12;
                    }
                    else if (MaximumPlayersForMode >= 32)
                    {
                        perMode.DefinitionOfHighPopulationForPlayers = 24;
                        perMode.DefinitionOfLowPopulationForPlayers = 8;
                    }
                    else if (MaximumPlayersForMode >= 24)
                    {
                        perMode.DefinitionOfHighPopulationForPlayers = 16;
                        perMode.DefinitionOfLowPopulationForPlayers = 8;
                    }
                    else if (MaximumPlayersForMode >= 16)
                    {
                        perMode.DefinitionOfHighPopulationForPlayers = 12;
                        perMode.DefinitionOfLowPopulationForPlayers = 4;
                    }
                    else
                    {
                        perMode.DefinitionOfHighPopulationForPlayers = 6;
                        perMode.DefinitionOfLowPopulationForPlayers = 4;
                    }
                    ConsoleWrite("Set ^bDefinition Of High Population For Players^n to " + perMode.DefinitionOfHighPopulationForPlayers, 0);
                    ConsoleWrite("Set ^bDefinition Of Low Population For Players^n to " + perMode.DefinitionOfLowPopulationForPlayers, 0);

                    // Set the Phase ranges
                    if (!isCTF && !isCarrierAssault && !isObliteration)
                    {
                        double high = HighestMaximumTicketsForMode;
                        double low = LowestMaximumTicketsForMode;
                        double late = low / 4.0; // late always 25% of low
                                                 // Try 33% of high first
                        double delta = high / 3.0;
                        if ((low - delta - late) < Math.Min(50.0, low / 2))
                        {
                            // Try 25% of high
                            delta = high / 4.0;
                            if ((low - delta - late) < Math.Min(50.0, low / 2))
                            {
                                // Use 33% of low
                                delta = low / 3.0;
                            }
                        }
                        perMode.DefinitionOfEarlyPhaseFromStart = Math.Min(300, Convert.ToInt32(delta)); // adaptive early
                        perMode.DefinitionOfLatePhaseFromEnd = Math.Min(300, Convert.ToInt32(late));
                        ConsoleWrite("Set ^bDefinition Of Early Phase As Tickets From Start^n to " + perMode.DefinitionOfEarlyPhaseFromStart, 0);
                        ConsoleWrite("Set ^bDefinition Of Late Phase As Tickets From End^n to " + perMode.DefinitionOfLatePhaseFromEnd, 0);
                    }
                    else if (isCTF)
                    {
                        ConsoleWrite("CTF Phase definitions cannot be set with the wizard, skipping.", 0);
                    }
                    else if (isCarrierAssault)
                    {
                        ConsoleWrite("Carrier Assault Phase definitions cannot be set with the wizard, skipping.", 0);
                    }
                    else if (isObliteration)
                    {
                        ConsoleWrite("Obliteration Phase definitions cannot be set with the wizard, skipping.", 0);
                    }

                    if (MetroIsInMapRotation && modeName.Contains("Conq"))
                    {
                        // Use half of low
                        perMode.MetroAdjustedDefinitionOfLatePhase = LowestMaximumTicketsForMode / 2;
                        ConsoleWrite("Set ^bMetro Adjusted Defintion Of Late Phase^n to " + perMode.MetroAdjustedDefinitionOfLatePhase, 0);
                    }

                    switch (PreferredStyleOfBalancing)
                    {
                        case PresetItems.Standard:

                            EarlyPhaseBalanceSpeed = new Speed[3] { Speed.Fast, Speed.Adaptive, Speed.Adaptive };
                            MidPhaseBalanceSpeed = new Speed[3] { Speed.Fast, Speed.Adaptive, Speed.Adaptive };
                            LatePhaseBalanceSpeed = new Speed[3] { Speed.Stop, Speed.Stop, Speed.Stop };
                            break;

                        case PresetItems.Aggressive:

                            EarlyPhaseBalanceSpeed = new Speed[3] { Speed.Fast, Speed.Fast, Speed.Fast };
                            MidPhaseBalanceSpeed = new Speed[3] { Speed.Fast, Speed.Fast, Speed.Fast };
                            LatePhaseBalanceSpeed = new Speed[3] { Speed.Fast, Speed.Fast, Speed.Fast };

                            break;

                        case PresetItems.Passive:

                            EarlyPhaseBalanceSpeed = new Speed[3] { Speed.Slow, Speed.Slow, Speed.Slow };
                            MidPhaseBalanceSpeed = new Speed[3] { Speed.Slow, Speed.Slow, Speed.Slow };
                            LatePhaseBalanceSpeed = new Speed[3] { Speed.Stop, Speed.Stop, Speed.Stop };

                            break;

                        case PresetItems.Intensify:

                            // TBD: Needs Speed.OverBalance (similar to Fast, but puts more players on losing team)
                            EarlyPhaseBalanceSpeed = new Speed[3] { Speed.Adaptive, Speed.Adaptive, Speed.Adaptive };
                            MidPhaseBalanceSpeed = new Speed[3] { Speed.Adaptive, Speed.Adaptive, Speed.Adaptive };
                            LatePhaseBalanceSpeed = new Speed[3] { Speed.Stop, Speed.Stop, Speed.Stop };

                            break;

                        case PresetItems.Retain:

                            EarlyPhaseBalanceSpeed = new Speed[3] { Speed.Slow, Speed.Adaptive, Speed.Slow };
                            MidPhaseBalanceSpeed = new Speed[3] { Speed.Slow, Speed.Adaptive, Speed.Slow };
                            LatePhaseBalanceSpeed = new Speed[3] { Speed.Stop, Speed.Stop, Speed.Stop };

                            break;

                        case PresetItems.BalanceOnly:

                            EarlyPhaseBalanceSpeed = new Speed[3] { Speed.Adaptive, Speed.Adaptive, Speed.Adaptive };
                            MidPhaseBalanceSpeed = new Speed[3] { Speed.Adaptive, Speed.Adaptive, Speed.Adaptive };
                            LatePhaseBalanceSpeed = new Speed[3] { Speed.Stop, Speed.Stop, Speed.Stop };

                            break;

                        case PresetItems.UnstackOnly:

                            EarlyPhaseBalanceSpeed = new Speed[3] { Speed.Unstack, Speed.Unstack, Speed.Unstack };
                            MidPhaseBalanceSpeed = new Speed[3] { Speed.Unstack, Speed.Unstack, Speed.Unstack };
                            LatePhaseBalanceSpeed = new Speed[3] { Speed.Unstack, Speed.Unstack, Speed.Unstack };

                            break;

                        case PresetItems.None:
                            break;
                        default:
                            break;
                    }

                    if (MetroIsInMapRotation && modeName.Contains("Conq"))
                    {
                        // In sure that Metro adjustment results in a Stop speed
                        LatePhaseBalanceSpeed = new Speed[3] { Speed.Stop, Speed.Stop, Speed.Stop };
                    }

                    // Set unstacking maximum ticket gap
                    if (!isCTF && !isCarrierAssault && !isObliteration)
                    {
                        perMode.MaxUnstackingTicketDifference = (HighestMaximumTicketsForMode / 2); // 50% of max
                        ConsoleWrite("Set ^bMax Unstacking Ticket Difference^n to " + perMode.MaxUnstackingTicketDifference, 0);
                    }

                    ConsoleWrite("Please review your Section 3 Early, Mid and Late Balance Speeds set to style " + PreferredStyleOfBalancing, 0);

                    ConsoleWrite("COMPLETED application of Wizard settings! Please review your Section 8 settings for ^b" + modeName, 0);
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        private void UpgradePreV1Settings()
        {
            /* ===== SECTION 6 - Unswitcher ===== */
            ForbidSwitchingAfterAutobalance = (ForbidSwitchAfterAutobalance) ? UnswitchChoice.Always : UnswitchChoice.Never;
            ForbidSwitchingAfterDispersal = (ForbidSwitchAfterDispersal) ? UnswitchChoice.Always : UnswitchChoice.Never;
            ForbidSwitchingToBiggestTeam = (ForbidSwitchToBiggestTeam) ? UnswitchChoice.Always : UnswitchChoice.Never;
            ForbidSwitchingToWinningTeam = (ForbidSwitchToWinningTeam) ? UnswitchChoice.Always : UnswitchChoice.Never;

            if (!EnableUnstacking)
            { // Assume settings were customized and should be left unchanged if True
                /* ===== SECTION 8 - Per-Mode Settings ===== */
                List<String> simpleModes = GetSimplifiedModes();

                foreach (String sm in simpleModes)
                {
                    PerModeSettings oneSet = null;
                    if (fPerMode.TryGetValue(sm, out oneSet) && oneSet != null)
                    {
                        PerModeSettings def = new PerModeSettings(sm, fGameVersion);
                        oneSet.DelaySecondsBetweenSwapGroups = def.DelaySecondsBetweenSwapGroups;
                        oneSet.MaxUnstackingSwapsPerRound = def.MaxUnstackingSwapsPerRound;
                        oneSet.NumberOfSwapsPerGroup = def.NumberOfSwapsPerGroup;
                    }
                }
            }
        }

        private bool Forbid(PerModeSettings perMode, UnswitchChoice choice)
        {
            if (choice == UnswitchChoice.Always) return true;
            if (choice == UnswitchChoice.Never) return false;

            bool ret = false;
            if (choice == UnswitchChoice.LatePhaseOnly)
            {
                if (perMode == null) return false;
                ret = (GetPhase(perMode, false) == Phase.Late);
            }
            return ret;
        }

        private void MergeWithFile(String[] var, List<String> list)
        {
            if (var == null || list == null) return;
            list.Clear();
            int n = 0;
            foreach (String s in var)
            {
                if (n == 0 && Regex.Match(s, @"^\s*<").Success)
                {
                    String fileName = s.Replace("<", String.Empty);
                    String path = Path.Combine("Configs", fileName);

                    try
                    {
                        if (!Path.IsPathRooted(path)) path = Path.Combine(Directory.GetParent(Application.ExecutablePath).FullName, path);
                        Byte[] buffer = new Byte[128]; // 64k buffer
                        int got = 0;
                        UTF8Encoding utf = new UTF8Encoding(false, true);
                        StringBuilder sb = new StringBuilder();

                        using (FileStream fs = File.Open(path, FileMode.Open))
                        {
                            while ((got = fs.Read(buffer, 0, buffer.Length - 1)) > 0)
                            {
                                String tmp = utf.GetString(buffer, 0, got);
                                foreach (Char c in tmp)
                                {
                                    if (c == '\n')
                                    {
                                        list.Add(sb.ToString());
                                        sb = new StringBuilder();
                                    }
                                    else if (c == '\r')
                                    {
                                        continue;
                                    }
                                    else
                                    {
                                        sb.Append(c);
                                    }
                                }
                            }
                            if (sb.Length > 0)
                            {
                                list.Add(sb.ToString());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        ConsoleError("Unable to merge file: " + fileName);
                        ConsoleError(ex.GetType().ToString() + ": " + ex.Message);
                    }
                    if (list.Count > 0)
                    {
                        ConsoleDebug("MergeWithFile ^b" + fileName + "^n contained:");
                        foreach (String mf in list)
                        {
                            ConsoleDebug(mf);
                        }
                        ConsoleDebug("MergeWithFile, end of ^b" + fileName + "^n");
                    }
                }
                else
                {
                    list.Add(s);
                }
                n = n + 1;
            }
        }


        private void SetDispersalListGroups()
        {
            /*
            This function scans the Disperse Evenly List for lines that specify
            a group. A group starts with a single digit, 1 thru 4, followed by
            whitespace, followed by a whitespace separated list of name/tag/guid items
            that should be assigned to the specified group list.
            */
            if (fSettingDisperseEvenlyList.Count == 0) return;
            if (fSettingDisperseEvenlyList.Count == 1 && fSettingDisperseEvenlyList[0] == DEFAULT_LIST_ITEM) return;
            foreach (List<String> gl in fDispersalGroups)
            {
                if (gl == null) continue;
                gl.Clear();
            }
            List<String> copy = new List<String>(fSettingDisperseEvenlyList);
            foreach (String line in copy)
            {
                try
                {
                    if (String.IsNullOrEmpty(line)) continue;
                    String[] tokens = Regex.Split(line, @"\s+");
                    if (tokens != null && tokens.Length == 1 && !String.IsNullOrEmpty(tokens[0]))
                    {
                        // Not a group, so retain
                        continue;
                    }
                    // Otherwise, check for a group specifier
                    bool first = true;
                    bool remove = false;
                    List<String> group = null;
                    bool bad = false;
                    int groupId = 0;
                    // Scan one line
                    foreach (String token in tokens)
                    {
                        if (String.IsNullOrEmpty(token)) continue;
                        // First token might be group specifier, if so move remaining tokens to group list
                        if (first)
                        {
                            if (Regex.Match(token, @"^[1234]").Success)
                            {
                                // It's a group
                                if (Int32.TryParse(token, out groupId))
                                {
                                    if (groupId >= 1 && groupId <= 4)
                                    {
                                        group = fDispersalGroups[groupId];
                                        remove = true;
                                    }
                                    else bad = true;
                                }
                            }
                            first = false;
                            if (group != null) continue; // skip group id
                        }
                        if (group == null)
                        {
                            break; // not a group specification, get out of this token parsing loop
                        }
                        else if (group.Contains(token))
                        {
                            ConsoleWarn("In Disperse Evenly List in Group " + groupId + ", ^b" + token + "^n is duplicated, please remove all duplicates");
                        }
                        else
                        {
                            // Add the rest of the tokens to the group
                            group.Add(token);
                        }
                    }
                    if (bad)
                    {
                        // Warn, leave line in original as is
                        ConsoleWarn("In Disperse Evenly List, unrecognized grouping, possible typo? " + line);
                    }
                    else if (remove)
                    {
                        // Remove lines that define groups from the normal list
                        fSettingDisperseEvenlyList.Remove(line);
                    }
                }
                catch (Exception e)
                {
                    ConsoleWarn("In Disperse Evenly List, skipping bad line: " + line);
                    ConsoleWarn(e.Message);
                }
            }
            // Check for uniqueness
            List<String> uniq = new List<String>();
            for (int i = 1; i <= 4; ++i)
            {
                copy = new List<String>(fDispersalGroups[i]);
                foreach (String s in copy)
                {
                    if (uniq.Contains(s))
                    {
                        ConsoleWarn("In Disperse Evenly List in Group " + i + ", ^b" + s + "^n is duplicated, please remove all duplicates");
                        fDispersalGroups[i].Remove(s);
                    }
                    else
                    {
                        uniq.Add(s);
                    }
                }
            }
            copy = new List<String>(fSettingDisperseEvenlyList);
            foreach (String s in copy)
            {
                if (uniq.Contains(s))
                {
                    ConsoleWarn("In Disperse Evenly List, ^b" + s + "^n is duplicated, please remove all duplicates");
                    fSettingDisperseEvenlyList.Remove(s);
                }
                else
                {
                    uniq.Add(s);
                }
            }
            // debugging
            if (DebugLevel >= 6)
            {
                String g1 = "Group 1: ";
                String g2 = "Group 2: ";
                String g3 = "Group 3: ";
                String g4 = "Group 4: ";
                if (fDispersalGroups[1].Count > 0)
                {
                    g1 = g1 + String.Join(", ", fDispersalGroups[1].ToArray());
                    ConsoleDebug("SetDispersalListGroups " + g1);
                }
                if (fDispersalGroups[2].Count > 0)
                {
                    g2 = g2 + String.Join(", ", fDispersalGroups[2].ToArray());
                    ConsoleDebug("SetDispersalListGroups " + g2);
                }
                if (fDispersalGroups[3].Count > 0)
                {
                    g3 = g3 + String.Join(", ", fDispersalGroups[3].ToArray());
                    ConsoleDebug("SetDispersalListGroups " + g3);
                }
                if (fDispersalGroups[4].Count > 0)
                {
                    g4 = g4 + String.Join(", ", fDispersalGroups[4].ToArray());
                    ConsoleDebug("SetDispersalListGroups " + g4);
                }
                ConsoleDebug("SetDispersalListGroups remaining list: " + String.Join(", ", fSettingDisperseEvenlyList.ToArray()));
            }
        }

        private void AssignGroups()
        {
            int grandTotal = 0;
            List<int> availableTeamIds = new List<int>(new int[4] { 1, 2, 3, 4 });

            try
            {
                // Insure that dispersal groups have been assigned
                List<PlayerModel> all = new List<PlayerModel>();
                all.AddRange(fTeam1);
                all.AddRange(fTeam2);
                all.AddRange(fTeam3);
                all.AddRange(fTeam4);
                foreach (PlayerModel p in all)
                {
                    if (IsInDispersalList(p, true))
                    {
                        if (DebugLevel >= 6) ConsoleDebug("AssignGroups assigned ^b" + p.FullName + "^n to Group " + p.DispersalGroup);
                    }
                }

                // Clear
                for (int groupId = 1; groupId <= 4; ++groupId)
                {
                    fGroupAssignments[groupId] = 0;
                }

                // Compute distribution of groups
                int[,] count = new int[5, 5]{ // group,team
            {0,0,0,0,0},
            {0,0,0,0,0},
            {0,0,0,0,0},
            {0,0,0,0,0},
            {0,0,0,0,0}
        };

                foreach (PlayerModel p in all)
                {
                    if (p.DispersalGroup == 0) continue;
                    ++count[p.DispersalGroup, p.Team];
                    ++grandTotal;
                }

                if (grandTotal == 0)
                {
                    ConsoleDebug("AssignGroups: No players or no groups, defaulting to 1,2,3,4");
                    for (int i = 1; i <= 4; ++i)
                    {
                        fGroupAssignments[i] = i;
                    }
                    return;
                }

                // Assign team to group that has the most players in that team
                for (int groupId = 1; groupId <= 4; ++groupId)
                {
                    // Find the max team count for this group
                    int most = 0;
                    int num = 0;
                    foreach (int teamId in availableTeamIds)
                    {
                        if (count[groupId, teamId] > num)
                        {
                            most = teamId;
                            num = count[groupId, teamId];
                        }
                    }
                    if (most != 0)
                    {
                        if (!availableTeamIds.Contains(most))
                        {
                            throw new Exception("team " + most + " already allocated!");
                        }
                        fGroupAssignments[groupId] = most;
                        availableTeamIds.Remove(most);
                    }
                }

                // Assign unallocated teams
                for (int groupId = 1; groupId <= 4; ++groupId)
                {
                    if (fGroupAssignments[groupId] == 0)
                    {
                        if (availableTeamIds.Count == 0)
                        {
                            throw new Exception("Ran out of team IDs!");
                        }
                        int ti = availableTeamIds[0];
                        fGroupAssignments[groupId] = ti;
                        availableTeamIds.Remove(ti);
                    }
                }

                // Sanity check
                availableTeamIds.Clear();
                for (int groupId = 1; groupId <= 4; ++groupId)
                {
                    if (availableTeamIds.Contains(fGroupAssignments[groupId]))
                    {
                        throw new Exception("Duplicate assignment!");
                    }
                    else
                    {
                        availableTeamIds.Add(fGroupAssignments[groupId]);
                    }
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
                ConsoleDebug("AssignGroups: Defaulting to 1,2,3,4");
                for (int i = 1; i <= 4; ++i)
                {
                    fGroupAssignments[i] = i;
                }
            }
            finally
            {
                if (DebugLevel >= 6)
                {
                    String msg = "Group assignments: ";
                    for (int i = 1; i <= 4; ++i)
                    {
                        msg = msg + fGroupAssignments[i];
                        if (i < 4) msg = msg + "/";
                    }
                    ConsoleWrite(msg, 6);
                }
            }
        }


        private void SetFriends()
        {

            if (fSettingFriendsList.Count == 0) return;
            if (fSettingFriendsList.Count == 1 && fSettingFriendsList[0] == DEFAULT_LIST_ITEM) return;

            fFriends.Clear();
            int key = 1;

            foreach (String line in fSettingFriendsList)
            {
                try
                {
                    if (String.IsNullOrEmpty(line)) continue;
                    if (line == DEFAULT_LIST_ITEM) continue;
                    String[] tokens = Regex.Split(line, @"\s+");
                    if (tokens != null && tokens.Length == 1 && !String.IsNullOrEmpty(tokens[0]))
                    {
                        throw new Exception("Line contains only one name");
                    }
                    // Otherwise, store the sub-list of friends
                    List<String> subList = new List<String>();
                    foreach (String token in tokens)
                    {
                        if (String.IsNullOrEmpty(token)) continue;
                        subList.Add(token);
                    }
                    fFriends[key] = subList;
                    ++key;
                }
                catch (Exception e)
                {
                    ConsoleWarn("In Friends List, skipping bad line: " + line);
                    ConsoleWarn(e.Message);
                }
            }
            // Check uniqueness
            fAllFriends.Clear();
            foreach (int k in fFriends.Keys)
            {
                List<String> copy = new List<String>(fFriends[k]);
                foreach (String name in copy)
                {
                    if (fAllFriends.Contains(name))
                    {
                        ConsoleWarn("In Friends List, ^b" + name + "^n is duplicated on one line, please change the line");
                        fFriends[k].Remove(name);
                    }
                    else
                    {
                        fAllFriends.Add(name);
                    }
                }
            }
            // Update player model
            UpdateFriends();
            // debugging
            if (DebugLevel >= 6)
            {
                ConsoleDebug("SetFriends list of friends: ");
                foreach (int k in fFriends.Keys)
                {
                    ConsoleDebug(k.ToString() + ": " + String.Join(", ", fFriends[k].ToArray()));
                }
            }
        }

        private void UpdateFriends()
        {
            // short-circuit
            if (fSettingFriendsList.Count == 0) return;
            if (fSettingFriendsList.Count == 1 && fSettingFriendsList[0] == DEFAULT_LIST_ITEM) return;

            lock (fAllPlayers)
            {
                foreach (String name in fAllPlayers)
                {
                    PlayerModel friend = null;
                    lock (fKnownPlayers)
                    {
                        if (!fKnownPlayers.TryGetValue(name, out friend) || friend == null) continue;
                    }
                    UpdatePlayerFriends(friend);
                }
            }
        }

        private void UpdatePlayerFriends(PlayerModel friend)
        {
            if (friend == null) return;
            friend.Friendex = -1;

            String guid = (String.IsNullOrEmpty(friend.EAGUID)) ? INVALID_NAME_TAG_GUID : friend.EAGUID;
            String tag = ExtractTag(friend);
            if (String.IsNullOrEmpty(tag)) tag = INVALID_NAME_TAG_GUID;

            foreach (int key in fFriends.Keys)
            {
                try
                {
                    List<String> subList = fFriends[key];
                    if (subList.Contains(friend.Name)
                    || subList.Contains(tag)
                    || subList.Contains(guid))
                    {
                        friend.Friendex = key;
                        if (DebugLevel >= 8) ConsoleDebug("UpdatePlayerFriends: (^b" + friend.Name + ", " + tag + ", ^n" + guid + ") in " + key + ": " + String.Join(", ", subList.ToArray()));
                        break;
                    }
                }
                catch (Exception e)
                {
                    if (DebugLevel >= 7) ConsoleException(e);
                }
            }
        }


        private int CountMatchingFriends(PlayerModel player, Scope scope)
        {
            if (player == null) return 0;
            if (player.Friendex == -1) return 0;
            if (player.Team == 0 || player.Squad == 0) return 0;
            int team = player.Team;
            int squad = player.Squad;

            List<PlayerModel> teamList = GetTeam(team);
            if (teamList == null) return 0;

            int same = 0;

            foreach (PlayerModel mate in teamList)
            {
                if (scope == Scope.SameSquad && mate.Squad != squad) continue;
                if (mate.Friendex == player.Friendex) ++same;
            }

            String sname = GetSquadName(squad) + " squad";

            String where = sname;
            if (scope == Scope.SameTeam)
            {
                where = GetTeamName(team) + " team";
            }

            if (DebugLevel >= 6 && same > 1) DebugBalance("Count of matching friends for player ^b" + player.Name + "^n in " + where + ", found " + same + " matching friends (friendex = " + player.Friendex + ")");

            return same;
        }

        private void InGameCommand(String msg, ChatScope scope, int team, int squad, String name)
        {
            if (EnableLoggingOnlyMode && !fTestMBCommand)
            {
                ConsoleDebug("EnableLoggingOnlyMode enabled, commands disabled");
                return;
            }
            if (!EnableInGameCommands)
            {
                ConsoleDebug("EnableInGameCommands is False, commands disabled");
                return;
            }
            CPrivileges p = this.GetAccountPrivileges(name);
            if (!fTestMBCommand && (p == null || !p.CanMovePlayers))
            {
                List<String> m = new List<String>();
                m.Add("You are not authorized to use @mb commands! Check your Procon account settings.");
                SayLines(m, name);
                return;
            }

            Match mbCmd = Regex.Match(msg, @"^\s*/?[@!#]mb\s+([\w]+)\s+(.*)$", RegexOptions.IgnoreCase);
            Match mbSubCmd = Regex.Match(msg, @"^\s*/?[@!#]mb\s+(sub|unsub)", RegexOptions.IgnoreCase);
            Match mbHelp = Regex.Match(msg, @"^\s*/?[@!#]mb\s+help\s*$", RegexOptions.IgnoreCase);
            Match mbHelpCmd = Regex.Match(msg, @"^\s*/?[@!#]mb\s+help\s+(add|del|list|new|sub|unsub|scramble)", RegexOptions.IgnoreCase);
            Match mbScrambleCmd = Regex.Match(msg, @"^\s*/?[@!#]mb\s+scramble\s+(on|off|true|false|yes|no|enable|disable)", RegexOptions.IgnoreCase);
            Match mbScramInfoCmd = Regex.Match(msg, @"^\s*/?[@!#]mb\s+scramble\s*$", RegexOptions.IgnoreCase);

            List<String> lines = null;
            PlayerModel player = null;
            int dispersalGroup = 0;
            String nameMatch = String.Empty;
            List<String> list = null;

            if (mbHelp.Success)
            {
                lines = new List<String>();
                lines.Add("Type '@mb help' and one of the following:");
                lines.Add("add, delete, list, new, subscribe, unsubscribe, scramble");
                SayLines(lines, name);
                return;
            }

            if (mbHelpCmd.Success)
            {
                lines = new List<String>();
                String which = mbHelpCmd.Groups[1].Value.ToLower();
                switch (which.ToLower())
                {
                    case "add":
                        lines.Add("Add names to a matching name in the disperse, friends, or white list");
                        lines.Add("Example: @mb add friends Match Adam");
                        break;
                    case "del":
                        lines.Add("Delete player names from the disperse, friends, or white list");
                        lines.Add("Example: @mb del friends Adam Eve");
                        break;
                    case "list":
                        lines.Add("List the disperse, friends or white list");
                        lines.Add("Example: @mb list friends");
                        break;
                    case "new":
                        lines.Add("Create a new entry in the disperse, friends, or white list");
                        lines.Add("Example: @mb new disperse 2 Name1 Name2 Name3");
                        break;
                    case "sub":
                        lines.Add("Subscribe to all balancer chat messages");
                        break;
                    case "unsub":
                        lines.Add("Unsubscribe from all balancer chat messages");
                        break;
                    case "scramble":
                        lines.Add("Check if teams will be scrambled");
                        lines.Add("To scramble teams at end of round, use: @mb scramble on");
                        lines.Add("To not scramble teams at end of round, use: @mb scramble off");
                        break;
                    default:
                        break;
                }
                SayLines(lines, name);
                return;
            }

            if (mbSubCmd.Success)
            {
                lines = new List<String>();
                String which = mbSubCmd.Groups[1].Value.ToLower();
                switch (which.ToLower())
                {
                    case "sub":
                        player = GetPlayer(name);
                        if (player != null)
                        {
                            player.Subscribed = true;
                            lines.Add("You will see all balancer chat messages");
                        }
                        break;
                    case "unsub":
                        player = GetPlayer(name);
                        if (player != null)
                        {
                            player.Subscribed = false;
                            lines.Add("You will no longer see all balancer chat messages");
                        }
                        break;
                    default:
                        break;
                }
                SayLines(lines, name);
                return;
            }

            if (mbScramInfoCmd.Success)
            {
                lines = new List<String>();
                if (OnlyByCommand)
                {
                    if (fScrambleByCommand)
                    {
                        lines.Add("Scrambler is ON: Only By Command required and '@mb scramble on' command given");
                    }
                    else
                    {
                        lines.Add("Scrambler is OFF: Only By Command required and '@mb scramble on' command not given");
                    }
                }
                else
                {
                    if (fScrambleByCommand)
                    {
                        lines.Add("Teams WILL be scrambled by command at end of round");
                    }
                    else
                    {
                        lines.Add("No command used so far, scramble will be by plugin settings");
                    }
                }
                SayLines(lines, name);
                return;
            }

            if (mbScrambleCmd.Success)
            {
                lines = new List<String>();
                /*
                if (!OnlyByCommand) {
                    lines.Add("Only By Command setting is False, in-game admin command is disabled");
                    return;
                }
                */
                String which = mbScrambleCmd.Groups[1].Value.ToLower();
                switch (which.ToLower())
                {
                    case "on":
                    case "yes":
                    case "true":
                    case "enable":
                        fScrambleByCommand = true;
                        lines.Add("Teams WILL be scrambled at end of round");
                        break;
                    case "off":
                    case "no":
                    case "false":
                    case "disable":
                        fScrambleByCommand = false;
                        lines.Add("No scrambling of teams at end of round");
                        break;
                    default:
                        break;
                }
                SayLines(lines, name);
                return;
            }

            if (mbCmd.Success)
            {
                lines = new List<String>();
                String which = mbCmd.Groups[1].Value;
                String tmp = mbCmd.Groups[2].Value;
                IGCommand cmd = IGCommand.None;

                if (Regex.Match(which, @"^add", RegexOptions.IgnoreCase).Success)
                {
                    cmd = IGCommand.Add;
                }
                else if (Regex.Match(which, @"^del", RegexOptions.IgnoreCase).Success)
                {
                    cmd = IGCommand.Delete;
                }
                else if (Regex.Match(which, @"^list", RegexOptions.IgnoreCase).Success)
                {
                    cmd = IGCommand.List;
                }
                else if (Regex.Match(which, @"^new", RegexOptions.IgnoreCase).Success)
                {
                    cmd = IGCommand.New;
                }
                else
                {
                    lines.Add("Unknown command: " + which + ", try @mb help");
                    SayLines(lines, name);
                    return;
                }

                String[] args = Regex.Split(tmp, @"\s+");

                if (args.Length == 0)
                {
                    lines.Add("No list (disperse, friends) specified, try @mb help");
                    SayLines(lines, name);
                    return;
                }
                else if (cmd != IGCommand.List && args.Length < 2)
                {
                    lines.Add("The command is incomplete: " + msg + ", try @mb help");
                    SayLines(lines, name);
                }

                // args[0] should be the name of the list
                String listName = String.Empty;
                if (Regex.Match(args[0], @"^di?s?p?e?r?s?e?", RegexOptions.IgnoreCase).Success)
                {
                    listName = "Dispersal";
                }
                else if (Regex.Match(args[0], @"^fr?i?e?n?d?s?", RegexOptions.IgnoreCase).Success)
                {
                    listName = "Friends";
                }
                else if (Regex.Match(args[0], @"^wh?i?t?e?l?i?s?t?", RegexOptions.IgnoreCase).Success)
                {
                    listName = "Whitelist";
                }
                else
                {
                    lines.Add("Unknown list name: " + args[0] + ", try @mb help");
                    SayLines(lines, name);
                    return;
                }

                int i = 1;

                if (listName == "Dispersal" && args.Length >= 3)
                {
                    // args[1] may be a dispersal group
                    if (args[1] == "1")
                    {
                        dispersalGroup = 1;
                        ++i;
                    }
                    else if (args[1] == "2")
                    {
                        dispersalGroup = 2;
                        ++i;
                    }
                }

                // Next arg may be the match string for add
                if (cmd == IGCommand.Add && listName == "Friends" && i < args.Length)
                {
                    nameMatch = args[i];
                    ++i;
                }

                // The rest of the args are the name operands
                List<String> names = new List<String>();
                while (i < args.Length)
                {
                    names.Add(args[i]);
                    ++i;
                }

                // Execute the command
                switch (cmd)
                {
                    case IGCommand.Add:
                        if (listName == "Dispersal")
                        {
                            if (dispersalGroup != 0)
                            {
                                bool found = false;
                                int groupId = 0;
                                String[] copy = (String[])DisperseEvenlyList.Clone();
                                list = new List<String>();
                                list.AddRange(DisperseEvenlyList);
                                for (int n = 0; n < copy.Length; ++n)
                                {
                                    if (Regex.Match(copy[n], @"^[1234]\s+").Success)
                                    {
                                        // It's a group
                                        List<String> tokens = new List<String>(Regex.Split(copy[n], @"\s+"));
                                        if (tokens.Count > 0 && Int32.TryParse(tokens[0], out groupId) && groupId == dispersalGroup)
                                        {
                                            found = true;
                                            foreach (String nm in names)
                                            {
                                                copy[n] = copy[n] + " " + nm;
                                            }
                                            break;
                                        }
                                    }
                                }
                                if (found)
                                {
                                    list.Clear();
                                    list.AddRange(copy);
                                    lines.Add("Added " + names.Count + " names to Dispersal Group " + groupId);
                                }
                                else
                                {
                                    lines.Add("Can't find Dispersal Group " + groupId + ", add failed!");
                                    SayLines(lines, name);
                                    return;
                                }
                            }
                            else
                            {
                                foreach (String nm in names)
                                {
                                    player = GetPlayer(nm);
                                    if (player != null && IsInDispersalList(player, true))
                                    {
                                        lines.Add("Duplicate name ^b" + nm + "^n, add failed!");
                                        SayLines(lines, name);
                                        return;
                                    }
                                    list.Add(nm);
                                }
                                lines.Add("Added " + names.Count + " names to Disperse Evenly List");
                            }
                            ForceSetPluginVariable("1 - Settings|Disperse Evenly List", list.ToArray());
                        }
                        else if (listName == "Friends")
                        {
                            bool found = false;
                            String[] copy = fSettingFriendsList.ToArray();
                            list = new List<String>();
                            list.AddRange(fSettingFriendsList);
                            for (int n = 0; n < copy.Length; ++n)
                            {
                                // Find a line in the list that contains the nameMatch string
                                List<String> tokens = new List<String>(Regex.Split(copy[n], @"\s+"));
                                if (tokens.Contains(nameMatch))
                                {
                                    found = true;
                                    foreach (String nm in names)
                                    {
                                        copy[n] = copy[n] + " " + nm;
                                    }
                                    break;
                                }
                            }
                            if (found)
                            {
                                list.Clear();
                                list.AddRange(copy);
                                lines.Add("Added " + names.Count + " names to Friends List");
                            }
                            else
                            {
                                lines.Add("Can't find friend " + nameMatch + " in Friends List, add failed!");
                                SayLines(lines, name);
                                return;
                            }
                            ForceSetPluginVariable("1 - Settings|Friends List", list.ToArray());
                        }
                        else if (listName == "Whitelist")
                        {
                            String[] copy = fSettingWhitelist.ToArray();
                            list = new List<String>();
                            list.AddRange(fSettingWhitelist);
                            // Check for duplication
                            foreach (String nm in names)
                            {
                                for (int n = 0; n < copy.Length; ++n)
                                {
                                    // Find matches
                                    List<String> tokens = new List<String>(Regex.Split(copy[n], @"\s+"));
                                    if (tokens.Contains(nm))
                                    {
                                        lines.Add("Duplicate name ^b" + nm + "^n, add failed!");
                                        SayLines(lines, name);
                                        return;
                                    }
                                }
                                // ok to add
                                list.Add(nm);
                            }
                            lines.Add("Added " + names.Count + " names to Whitelist");
                            ForceSetPluginVariable("1 - Settings|Whitelist", list.ToArray());
                        }
                        break;
                    case IGCommand.Delete:
                        if (listName == "Dispersal")
                        {
                            if (dispersalGroup != 0)
                            {
                                bool found = false;
                                String remove = String.Empty;
                                int groupId = 0;
                                String[] copy = (String[])DisperseEvenlyList.Clone();
                                list = new List<String>();
                                list.AddRange(DisperseEvenlyList);
                                for (int n = 0; n < copy.Length; ++n)
                                {
                                    if (Regex.Match(copy[n], @"^[1234]\s+").Success)
                                    {
                                        // It's a group
                                        List<String> tokens = new List<String>(Regex.Split(copy[n], @"\s+"));
                                        if (tokens.Count > 0 && Int32.TryParse(tokens[0], out groupId) && groupId == dispersalGroup)
                                        {
                                            found = true;
                                            foreach (String nm in names)
                                            {
                                                if (tokens.Contains(nm))
                                                {
                                                    tokens.Remove(nm);
                                                }
                                            }
                                            if (tokens.Count > 1)
                                            {
                                                copy[n] = String.Join(" ", tokens.ToArray());
                                            }
                                            else
                                            {
                                                // Remove the whole item
                                                remove = copy[n];
                                            }
                                            break;
                                        }
                                    }
                                }
                                if (found)
                                {
                                    list.Clear();
                                    list.AddRange(copy);
                                    if (!String.IsNullOrEmpty(remove))
                                    {
                                        list.Remove(remove);
                                    }
                                    lines.Add("Deleted " + names.Count + " names from Dispersal Group " + groupId);
                                }
                                else
                                {
                                    lines.Add("Can't find Dispersal Group " + groupId + ", delete failed!");
                                    SayLines(lines, name);
                                    return;
                                }
                            }
                            else
                            {
                                foreach (String nm in names)
                                {
                                    player = GetPlayer(nm);
                                    if (player != null && !IsInDispersalList(player, true))
                                    {
                                        lines.Add("Can't find name ^b" + nm + "^n, delete failed!");
                                        SayLines(lines, name);
                                        return;
                                    }
                                    list.Remove(nm);
                                }
                                lines.Add("Deleted " + names.Count + " names from Disperse Evenly List");
                            }
                            ForceSetPluginVariable("1 - Settings|Disperse Evenly List", list.ToArray());
                        }
                        else if (listName == "Friends")
                        {
                            bool found = false;
                            String remove = String.Empty;
                            String[] copy = fSettingFriendsList.ToArray();
                            list = new List<String>();
                            list.AddRange(fSettingFriendsList);
                            for (int n = 0; n < copy.Length; ++n)
                            {
                                // Find a token in the line that contains a match
                                List<String> tokens = new List<String>(Regex.Split(copy[n], @"\s+"));
                                foreach (String nm in names)
                                {
                                    if (tokens.Contains(nm))
                                    {
                                        found = true;
                                        tokens.Remove(nm);
                                    }
                                }
                                if (tokens.Count > 1)
                                {
                                    copy[n] = String.Join(" ", tokens.ToArray());
                                }
                                else
                                {
                                    // Remove the whole item
                                    remove = copy[n];
                                }
                            }
                            if (found)
                            {
                                list.Clear();
                                list.AddRange(copy);
                                if (!String.IsNullOrEmpty(remove)) list.Remove(remove);
                                lines.Add("Deleted " + names.Count + " names from Friends List");
                            }
                            else
                            {
                                lines.Add("Can't find any matching friends in Friends List, delete failed!");
                                SayLines(lines, name);
                                return;
                            }
                            ForceSetPluginVariable("1 - Settings|Friends List", list.ToArray());
                        }
                        else if (listName == "Whitelist")
                        {
                            bool found = false;
                            String[] copy = fSettingWhitelist.ToArray();
                            list = new List<String>();
                            list.AddRange(fSettingWhitelist);
                            // Check for match
                            foreach (String nm in names)
                            {
                                for (int n = 0; n < copy.Length; ++n)
                                {
                                    // Find matches
                                    List<String> tokens = new List<String>(Regex.Split(copy[n], @"\s+"));
                                    if (tokens.Contains(nm))
                                    {
                                        list.Remove(copy[n]);
                                        found = true;
                                    }
                                }
                            }
                            if (found)
                            {
                                lines.Add("Deleted " + names.Count + " names from Whitelist");
                            }
                            else
                            {
                                lines.Add("Can't find any matching names in Whitelist, delete failed!");
                            }
                            ForceSetPluginVariable("1 - Settings|Whitelist", list.ToArray());
                        }
                        break;
                    case IGCommand.List:
                        String buffer = String.Empty;
                        bool first = true;
                        if (listName == "Dispersal")
                        {
                            for (int j = 1; j <= 4; ++j)
                            {
                                if (fDispersalGroups[j].Count > 0)
                                {
                                    String dg = j.ToString() + " " + String.Join(" ", fDispersalGroups[j].ToArray());
                                    if (first)
                                    {
                                        buffer = dg;
                                        first = false;
                                    }
                                    else
                                    {
                                        buffer = buffer + "; " + dg;
                                    }
                                }
                            }
                            foreach (String item in fSettingDisperseEvenlyList)
                            {
                                if (first)
                                {
                                    buffer = item;
                                    first = false;
                                }
                                else
                                {
                                    buffer = buffer + "; " + item;
                                }
                            }
                            lines.Add(buffer);
                        }
                        else if (listName == "Friends")
                        {
                            foreach (String item in fSettingFriendsList)
                            {
                                if (first)
                                {
                                    buffer = item;
                                    first = false;
                                }
                                else
                                {
                                    buffer = buffer + "; " + item;
                                }
                            }
                            lines.Add(buffer);
                        }
                        else if (listName == "Whitelist")
                        {
                            foreach (String item in fSettingWhitelist)
                            {
                                if (first)
                                {
                                    buffer = item;
                                    first = false;
                                }
                                else
                                {
                                    buffer = buffer + "; " + item;
                                }
                            }
                            lines.Add(buffer);
                        }
                        break;
                    case IGCommand.New:
                        if (listName == "Dispersal")
                        {
                            if (dispersalGroup != 0)
                            {
                                int groupId = 0;
                                String[] copy = (String[])DisperseEvenlyList.Clone();
                                list = new List<String>();
                                list.AddRange(DisperseEvenlyList);
                                for (int n = 0; n < copy.Length; ++n)
                                {
                                    if (Regex.Match(copy[n], @"^[1234]\s+").Success)
                                    {
                                        // It's a group
                                        List<String> tokens = new List<String>(Regex.Split(copy[n], @"\s+"));
                                        if (tokens.Count > 0 && Int32.TryParse(tokens[0], out groupId) && groupId == dispersalGroup)
                                        {
                                            lines.Add("Dispersal Group " + groupId + " already exists, new failed!");
                                            SayLines(lines, name);
                                            return;
                                        }
                                    }
                                }
                                list.Add(groupId + " " + String.Join(" ", names.ToArray()));
                                lines.Add("Created Dispersal Group " + groupId + " with " + names.Count + " names in Disperse Evenly List");
                            }
                            else
                            {
                                foreach (String nm in names)
                                {
                                    player = GetPlayer(nm);
                                    if (player != null && IsInDispersalList(player, true))
                                    {
                                        lines.Add("Duplicate name ^b" + nm + "^n, new failed!");
                                        SayLines(lines, name);
                                        return;
                                    }
                                    list.Add(nm);
                                }
                                lines.Add("Created " + names.Count + " new names in Disperse Evenly List");
                            }
                            ForceSetPluginVariable("1 - Settings|Disperse Evenly List", list.ToArray());
                        }
                        else if (listName == "Friends")
                        {
                            if (names.Count < 2)
                            {
                                lines.Add("New friends must have at least 2 names, new failed!");
                                SayLines(lines, name);
                                return;
                            }
                            bool found = false;
                            String[] copy = fSettingFriendsList.ToArray();
                            list = new List<String>();
                            list.AddRange(fSettingFriendsList);
                            for (int n = 0; n < copy.Length; ++n)
                            {
                                // Find a line in the list that contains the nameMatch string
                                List<String> tokens = new List<String>(Regex.Split(copy[n], @"\s+"));
                                foreach (String nm in names)
                                {
                                    if (tokens.Contains(nm))
                                    {
                                        found = true;
                                        break;
                                    }
                                }
                                if (found) break;
                            }
                            if (!found)
                            {
                                list.Add(String.Join(" ", names.ToArray()));
                                lines.Add("Created " + names.Count + " new names in Friends List");
                            }
                            else
                            {
                                lines.Add("Duplicate names in Friends List, new failed!");
                                SayLines(lines, name);
                                return;
                            }
                            ForceSetPluginVariable("1 - Settings|Friends List", list.ToArray());
                        }
                        else if (listName == "Whitelist")
                        {
                            String[] copy = fSettingWhitelist.ToArray();
                            list = new List<String>();
                            list.AddRange(fSettingWhitelist);
                            // Check for duplication
                            foreach (String nm in names)
                            {
                                for (int n = 0; n < copy.Length; ++n)
                                {
                                    // Find matches
                                    List<String> tokens = new List<String>(Regex.Split(copy[n], @"\s+"));
                                    if (tokens.Contains(nm))
                                    {
                                        lines.Add("Duplicate name ^b" + nm + "^n, new failed!");
                                        SayLines(lines, name);
                                        return;
                                    }
                                }
                                // ok to add
                                list.Add(nm);
                            }
                            lines.Add("Created " + names.Count + " new names in Whitelist");
                            ForceSetPluginVariable("1 - Settings|Whitelist", list.ToArray());
                        }
                        break;
                    default:
                        break;
                }

                // Send the results
                SayLines(lines, name);
                return;
            }

            // Unknown command
            lines = new List<String>();
            lines.Add("Unknown command: " + msg);
            SayLines(lines, name);
        }

        private List<String> Chunker(String msg, int maxLen)
        {
            List<String> ret = new List<String>();
            String sub = msg;
            while (sub.Length > maxLen)
            {
                ret.Add(sub.Substring(0, maxLen));
                sub = "... " + sub.Substring(maxLen);
            }
            ret.Add(sub);
            return ret;
        }

        private void SayLines(List<String> lines, String name)
        {
            foreach (String line in lines)
            {
                List<String> chunks = Chunker(line, 123);
                if (String.IsNullOrEmpty(name))
                {
                    foreach (String chunk in chunks)
                    {
                        ServerCommand("admin.say", chunk);
                        if (fTestMBCommand)
                        {
                            ProconChat(chunk);
                            ConsoleDump("  " + chunk);
                        }
                    }
                }
                else
                {
                    foreach (String chunk in chunks)
                    {
                        ServerCommand("admin.say", chunk, "player", name);
                        if (fTestMBCommand)
                            ProconChatPlayer(name, chunk);
                        ConsoleDump("  " + name + "> " + chunk);
                    }
                }
            }
        }

        private void UpdateFromWhitelist(PlayerModel player)
        {
            if (player == null) return;
            foreach (String item in fSettingWhitelist)
            {
                try
                {
                    // Example item: name B U S D R
                    List<String> tokens = new List<String>(Regex.Split(item, @"\s+"));
                    if (tokens.Count < 1) continue;
                    String guid = (String.IsNullOrEmpty(player.EAGUID)) ? INVALID_NAME_TAG_GUID : player.EAGUID;
                    String xt = ExtractTag(player);
                    if (String.IsNullOrEmpty(xt)) xt = INVALID_NAME_TAG_GUID;
                    // If nothing matches, keep looking
                    if (!(tokens[0] == player.Name || tokens[0] == xt || tokens[0] == guid)) continue;
                    // Reset
                    player.Whitelist = 0;
                    // Set new flags
                    if (tokens.Count == 1)
                    { // no option codes means set all of them
                        player.Whitelist = WL_ALL;
                        DebugWrite("^9DEBUG: UpdateFromWhitelist(^b" + player.FullName + "^n) set ALL flags!", 7);
                    }
                    else
                    {
                        for (int i = 1; i < tokens.Count; ++i)
                        {
                            switch (tokens[i])
                            {
                                case "B":
                                    player.Whitelist |= WL_BALANCE;
                                    DebugWrite("^9DEBUG: UpdateFromWhitelist(^b" + player.FullName + "^n) set WL_BALANCE flag!", 7);
                                    break;
                                case "U":
                                    player.Whitelist |= WL_UNSTACK;
                                    DebugWrite("^9DEBUG: UpdateFromWhitelist(^b" + player.FullName + "^n) set WL_UNSTACK flag!", 7);
                                    break;
                                case "S":
                                    player.Whitelist |= WL_SWITCH;
                                    DebugWrite("^9DEBUG: UpdateFromWhitelist(^b" + player.FullName + "^n) set WL_SWITCH flag!", 7);
                                    break;
                                case "D":
                                    player.Whitelist |= WL_DISPERSE;
                                    DebugWrite("^9DEBUG: UpdateFromWhitelist(^b" + player.FullName + "^n) set WL_DISPERSE flag!", 7);
                                    break;
                                case "R":
                                    player.Whitelist |= WL_RANK;
                                    DebugWrite("^9DEBUG: UpdateFromWhitelist(^b" + player.FullName + "^n) set WL_RANK flag!", 7);
                                    break;
                                default:
                                    ConsoleWarn("Skipping unknown Whitelist code " + tokens[i] + ", in item: " + item);
                                    break;
                            }
                        }
                    }
                    return;
                }
                catch (Exception e)
                {
                    ConsoleException(e);
                }
            }
        }

        private void UpdateAllFromWhitelist()
        {
            lock (fKnownPlayers)
            {
                foreach (String name in fKnownPlayers.Keys)
                {
                    try
                    {
                        PlayerModel player = fKnownPlayers[name];
                        if (player == null) continue;
                        UpdateFromWhitelist(player);
                    }
                    catch (Exception e)
                    {
                        ConsoleException(e);
                    }
                }
            }
        }

        private void MergeWhitelistWithReservedSlots()
        {
            List<String> vip = new List<String>(fSettingWhitelist);
            foreach (String reserved in fReservedSlots)
            {
                bool dupe = false;
                // Check for duplicates
                foreach (String item in fSettingWhitelist)
                {
                    List<String> tokens = new List<String>(Regex.Split(item, @"\s+"));
                    if (tokens[0] == reserved)
                    {
                        if (DebugLevel >= 6) ConsoleDebug("Reserved slots list duplicates Whitelist name ^b" + reserved);
                        dupe = true;
                        break;
                    }
                }
                if (dupe) continue;
                // Otherwise, add it
                vip.Add(reserved);
            }
            fSettingWhitelist.Clear();
            // clean up the list
            foreach (String v in vip)
            {
                if (String.IsNullOrEmpty(v)) continue;
                if (v == INVALID_NAME_TAG_GUID) continue;
                if (v.Contains("[")) continue;
                fSettingWhitelist.Add(v);
            }
        }

        private bool CheckWhitelist(PlayerModel player, uint flags)
        {
            if (player == null) return false;
            String guid = (String.IsNullOrEmpty(player.EAGUID)) ? INVALID_NAME_TAG_GUID : player.EAGUID;
            String xt = ExtractTag(player);
            if (String.IsNullOrEmpty(xt)) xt = INVALID_NAME_TAG_GUID;
            foreach (String item in fSettingWhitelist)
            {
                List<String> tokens = new List<String>(Regex.Split(item, @"\s+"));
                if (tokens.Count < 1)
                {
                    ConsoleError("tokens.Count < 1!");
                    continue;
                }
                if (tokens[0] == player.Name || tokens[0] == xt || tokens[0] == guid)
                {
                    if (DebugLevel >= 7)
                    {
                        DebugWrite("^b" + player.Name + ", " + xt + ", ^n" + player.Whitelist.ToString("X") + ", " + guid, 7);
                        DebugWrite("WL: " + String.Join(", ", tokens.ToArray()), 7);
                        String fs = String.Empty;
                        if ((player.Whitelist & WL_BALANCE) == WL_BALANCE) fs = fs + "B ";
                        if ((player.Whitelist & WL_UNSTACK) == WL_UNSTACK) fs = fs + "U ";
                        if ((player.Whitelist & WL_SWITCH) == WL_SWITCH) fs = fs + "S ";
                        if ((player.Whitelist & WL_RANK) == WL_RANK) fs = fs + "R ";
                        if ((player.Whitelist & WL_DISPERSE) == WL_DISPERSE) fs = fs + "D ";
                        if (fs == String.Empty) fs = "(none)";
                        DebugWrite("CheckWhitelist ^b" + player.FullName + "^n " + fs, 7);
                    }
                    return ((player.Whitelist & flags) == flags);
                }
            }
            return false;
        }

        private void UpdateWhitelistModel()
        {
            try
            {
                DebugWrite("^9Updating Whitelist data model", 7);
                MergeWithFile(Whitelist, fSettingWhitelist); // clears fSettingWhitelist
                if (EnableWhitelistingOfReservedSlotsList) MergeWhitelistWithReservedSlots();
                UpdateAllFromWhitelist();
                if (DebugLevel >= 7)
                {
                    String l = "Whitelist: ";
                    l = l + String.Join(", ", fSettingWhitelist.ToArray());
                    ConsoleDebug(l);
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        private void TimerLoop()
        {
            /*
            Strategy: Every 1/2 second, check the list of timers to see if any
            actions need to be fired.
            */
            try
            {
                while (fIsEnabled)
                {
                    lock (fTimerRequestList)
                    {
                        Monitor.Wait(fTimerRequestList, 500); // 1/2 second max heartbeat
                        if (!fIsEnabled)
                        {
                            fTimerRequestList.Clear();
                            return;
                        }

                        // Time to check all requests
                        DebugWrite("Checking " + fTimerRequestList.Count + " requests", 9);
                        DateTime now = DateTime.Now;
                        foreach (DelayedRequest request in fTimerRequestList)
                        {
                            DebugWrite("Request: " + request.Name + ", " + now.Subtract(request.LastUpdate).TotalSeconds.ToString("F1") + " of " + request.MaxDelay + " seconds", 9);
                            if (now.Subtract(request.LastUpdate).TotalSeconds >= request.MaxDelay)
                            {
                                try
                                {
                                    if (request.Request != null)
                                    {
                                        if (DebugLevel >= 8) ConsoleDebug("Executing request: " + request.Name);
                                        request.Request(now);
                                    }
                                }
                                catch (Exception e)
                                {
                                    if (DebugLevel >= 9) ConsoleException(e);
                                }
                                request.LastUpdate = now;
                            }
                        }
                    }
                }
            }
            catch (ThreadAbortException)
            {
                fAborted = true;
                return;
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
            finally
            {
                if (!fAborted) ConsoleWrite("^bTimerLoop^n thread stopped", 0);
            }
        }


        private DelayedRequest AddTimedRequest(String name, double maxDelay, Action<DateTime> request)
        {
            DelayedRequest r = null;
            lock (fTimerRequestList)
            {
                foreach (DelayedRequest old in fTimerRequestList)
                {
                    if (!String.IsNullOrEmpty(old.Name) && !String.IsNullOrEmpty(name) && old.Name == name)
                    {
                        ConsoleDebug("ASSERT AddTimedRequest: request with name ^b" + name + "^n already exists, skipping!");
                        return null;
                    }
                }
                r = new DelayedRequest();
                r.Name = name;
                r.MaxDelay = maxDelay;
                r.LastUpdate = DateTime.MinValue;
                r.Request = request;
                ConsoleDebug("Added: " + name);
                fTimerRequestList.Add(r);
            }
            return r;
        }

        private void UpdateTicketLossRateLog(DateTime now, int strong, int weak)
        {
            /*
            Log will be log rolled at midnight, so date is built into the log name
            Log will be log rolled by round-map-mode
            Sequence number follows date to disambiguate round-map-mode
            Log name template: YYYYMMDD_Seq_Round-Map-ModeCode_tlr.csv
            Example: 20130713_09_2-Caspian_Border-CL0_tlr.csv
            Time: HH:MM:SS
            Round: Number
            Map: Text
            Mode: Text
            Max Players: Number
            US Players: Number
            RU Players: Number
            US Tickets: Number
            RU Tickets: Number
            Samples: Number
            US Average Ticket Loss: Number (looking backward for Samples, normalized to a positive value)
            RU Average Ticket Loss: Number (looking backward for Samples, normalized to a positive value)
            Ratio%: Number (as a percentage)
            Strong unstacked to: Number (0 means no unstack this entry, 1 means to US team, 2 means to RU team)
            Weak unstacked to: Number (0 means no unstack this entry, 1 means to US team, 2 means to RU team)
            */

            if (fServerInfo == null || TotalPlayerCount() < 4 || fGameState != GameState.Playing) return;

            String path = String.Empty;

            try
            {
                String date = now.ToString("yyyyMMdd");
                String suffix = "tlr.csv";
                String map = GetRoundMapMode();
                String log = String.Join("_", new String[] { date, String.Format("{0:D3}", fRoundsEnabled), map, suffix });
                path = Path.Combine(Path.Combine("Logs", fHost + "_" + fPort), log);
                DebugWrite("^9^bDEBUG^n: UpdateTicketLossRateLog " + path + " at " + now, 8);

                PerModeSettings perMode = GetPerModeSettings();
                String[] row = new String[18]; // index of array is column number
                row[0] = now.ToString("HH:mm:ss");
                row[1] = (fServerInfo.CurrentRound + 1).ToString();
                row[2] = FriendlyMap;
                row[3] = FriendlyMode;
                row[4] = fServerInfo.MaxPlayerCount.ToString();
                row[5] = fTeam1.Count.ToString();
                row[6] = fTeam2.Count.ToString();
                row[7] = fTickets[1].ToString();
                row[8] = ((IsRush()) ? Convert.ToInt32(Math.Max(fTickets[1] / 2, fMaxTickets - (fRushMaxTickets - fTickets[2]))) : fTickets[2]).ToString();
                row[9] = perMode.TicketLossSampleCount.ToString();
                double a1 = GetAverageTicketLossRate(1, true);
                row[10] = a1.ToString("F3");
                double a2 = GetAverageTicketLossRate(2, true);
                row[11] = a2.ToString("F3");
                double ratio = (a1 > a2) ? (a1 / Math.Max(1, a2)) : (a2 / Math.Max(1, a1));
                ratio = Math.Min(ratio, 50.0); // cap at 50x
                ratio = ratio * 100.0;
                row[12] = ratio.ToString("F0");
                row[13] = strong.ToString();
                row[14] = weak.ToString();
                // Spares for future expansion
                row[15] = String.Empty;
                row[16] = String.Empty;
                row[17] = String.Empty;

                if (!Path.IsPathRooted(path)) path = Path.Combine(Directory.GetParent(Application.ExecutablePath).FullName, path);

                // Add newline
                String entry = String.Join(",", row) + "\n";

                lock (fAverageTicketLoss)
                { // mutex access to log file
                    using (FileStream fs = File.Open(path, FileMode.Append))
                    {
                        Byte[] info = new UTF8Encoding(true).GetBytes(entry);
                        fs.Write(info, 0, info.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleError("Unable to append to log file: " + path);
                ConsoleError(ex.ToString());
            }

        }

        private String GetRoundMapMode()
        {
            String map = Regex.Replace(FriendlyMap, @"[\s]+", "_");
            String mode = Regex.Replace(fServerInfo.GameMode, @"[a-z]+", String.Empty);
            String round = (fServerInfo.CurrentRound + 1).ToString();
            return String.Join("-", new String[] { round, map, mode });
        }

        private double GetAverageTicketLossRate(int team, bool verbose)
        {
            if (team < 1 || team > 2) return 0;
            double rate = 0;
            try
            {
                PerModeSettings perMode = GetPerModeSettings();
                if (perMode.TicketLossSampleCount < MIN_SAMPLE_COUNT) return 0;
                List<double> copy = null;
                lock (fAverageTicketLoss)
                {
                    while (fAverageTicketLoss[team].Count > perMode.TicketLossSampleCount)
                    {
                        fAverageTicketLoss[team].Dequeue();
                    }
                    copy = new List<double>(fAverageTicketLoss[team].ToArray());
                }
                // If not enough samples, force average to 0
                if (copy.Count < perMode.TicketLossSampleCount) return 0;
                String debug = null;
                foreach (double sample in copy)
                {
                    rate = rate + sample;
                    if (verbose)
                    {
                        if (debug == null)
                        {
                            debug = "[" + sample.ToString("F2");
                        }
                        else
                        {
                            debug = debug + "," + sample.ToString("F2");
                        }
                    }
                }
                double actual = Math.Max(1.0, copy.Count);
                rate = (rate / actual) * 60.0; // loss per minute
                if (verbose)
                {
                    if (debug != null) DebugWrite("^7" + GetTeamName(team) + " (" + copy.Count + ") = " + debug + "]", 8);
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
            return rate;
        }

        private void AddTicketLossSample(int team, double oldTickets, double newTickets, double seconds)
        {
            // Ticket changes are normalized to a positive value
            if (seconds < 1) seconds = 1;
            PerModeSettings perMode = GetPerModeSettings();

            try
            {
                lock (fAverageTicketLoss)
                {
                    double normalizedSample = Math.Abs(oldTickets - newTickets) / seconds;
                    int secs = Convert.ToInt32(Math.Round(seconds));
                    for (int i = 0; i < secs; ++i)
                    {
                        fAverageTicketLoss[team].Enqueue(normalizedSample);
                    }
                    while (fAverageTicketLoss[team].Count > perMode.TicketLossSampleCount)
                    {
                        fAverageTicketLoss[team].Dequeue();
                    }
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        private void ResetAverageTicketLoss()
        {
            lock (fAverageTicketLoss)
            {
                fAverageTicketLoss[1].Clear();
                fAverageTicketLoss[2].Clear();
            }
        }

        private void SetupUpdateTicketsRequest()
        {
            if (fUpdateTicketsRequest != null) return;
            fUpdateTicketsRequest = AddTimedRequest("Update serverInfo every 5 seconds", 5.0, delegate (DateTime now)
            {
                try
                {
                    if (fGameState == GameState.Playing && TotalPlayerCount() >= 4) ServerCommand("serverInfo");
                }
                catch (Exception) { }
            });
        }

        private void CheckRoundEndingDuration()
        {
            if (fRoundOverTimestamp == DateTime.MinValue) return;
            double secs = DateTime.Now.Subtract(fRoundOverTimestamp).TotalSeconds;
            if (secs < 30)
            {
                DebugWrite("Between round seconds less than 30 seconds (" + secs.ToString("F0") + "), skipping", 3);
                return;
            }
            else if (secs > 180)
            { // 3 mins
                DebugWrite("Between round seconds greater than 180 seconds (" + secs.ToString("F0") + "), skipping", 3);
                return;
            }
            // Sum up for average
            fTotalRoundEndingSeconds += secs;
            fTotalRoundEndingRounds += 1;
            DebugWrite("Between round seconds = " + secs.ToString("F0") + ", average of " + fTotalRoundEndingRounds + " rounds = " + (fTotalRoundEndingSeconds / fTotalRoundEndingRounds).ToString("F1"), 3);
        }


        private void UpdateFactions()
        {
            ServerCommand("vars.teamFactionOverride");
        }


        private void UpdateRoundTimeLimit()
        {
            ServerCommand("vars.roundTimeLimit");
        }


        private int PriorityQueueCount()
        {
            int c = 0;
            lock (fPriorityFetchQ)
            {
                c = fPriorityFetchQ.Count;
            }
            return c;
        }


        public int TotalPlayerCount()
        {
            fPlayerCount = 0;
            if (fGameVersion != GameVersion.BF3)
            { // BF4 and BFH
                fBF4CommanderCount = 0;
                fBF4SpectatorCount = 0;

                lock (fAllPlayers)
                {
                    foreach (String name in fAllPlayers)
                    {
                        PlayerModel p = GetPlayer(name);
                        if (p == null) continue;
                        if (p.Role == ROLE_PLAYER)
                        {
                            ++fPlayerCount;
                        }
                        else if (p.Role == ROLE_COMMANDER_PC || p.Role == ROLE_COMMANDER_MOBILE)
                        {
                            ++fBF4CommanderCount;
                        }
                        else if (p.Role == ROLE_SPECTATOR)
                        {
                            ++fBF4SpectatorCount;
                        }
                    }
                }
            }
            else
            {
                lock (fAllPlayers) { fPlayerCount = fAllPlayers.Count; }
            }
            return fPlayerCount;
        }



        private double ComputeTicketRatio(double a, double b, double goal, bool countDown, out String msg)
        {
            if (IsRush() && fMaxTickets != -1)
            {
                // normalize Rush ticket ratio
                b = fMaxTickets - (fRushMaxTickets - b);
                b = Math.Max(b, 1);
            }

            double ratio = 0;
            if (countDown)
            {
                // ratio of difference from max
                if (a < b)
                {
                    ratio = (goal - a) / Math.Max(1, (goal - b));
                    msg = "Ratio T1/T2: " + a + " vs " + b + " <- [" + goal + "]: " + (goal - a) + "/" + Math.Max(1, (goal - b)) + " = " + ratio.ToString("F2");
                }
                else
                {
                    ratio = (goal - b) / Math.Max(1, (goal - a));
                    msg = "Ratio T2/T1: " + a + " vs " + b + " <- [" + goal + "]: " + (goal - b) + "/" + Math.Max(1, (goal - a)) + " = " + ratio.ToString("F2");
                }
            }
            else
            {
                // direct ratio
                if (a > b)
                {
                    ratio = a / Math.Max(1, b);
                    msg = "Ratio T1/T2: " + a + " vs " + b + " -> [" + goal + "]: " + a + "/" + Math.Max(1, b) + " = " + ratio.ToString("F2");
                }
                else
                {
                    ratio = b / Math.Max(1, a);
                    msg = "Ratio T2/T2: " + a + " vs " + b + " -> [" + goal + "]: " + b + "/" + Math.Max(1, a) + " = " + ratio.ToString("F2");
                }
            }
            return ratio;
        }


        int GetRushMaxStages(String mapName)
        {
            int maxStages = 4;
            if (!String.IsNullOrEmpty(mapName))
            {
                if (fRushMap3Stages.Contains(mapName))
                {
                    // Need to deal with BF3 and BF4 both having map codes that start XP1_
                    bool isXP1 = mapName.StartsWith("XP1_");
                    if (!isXP1 || (isXP1 && fGameVersion == GameVersion.BF4))
                    {
                        maxStages = 3;
                    }
                }
                else if (fRushMap5Stages.Contains(mapName))
                {
                    maxStages = 5;
                }
            }
            return maxStages;
        }

        double GetAveragePlayerStats(int teamId, DefineStrong stat)
        {
            double avg = 0;
            List<PlayerModel> team = GetTeam(teamId);
            if (team.Count < 1) return 0;
            double n = Convert.ToDouble(team.Count);
            switch (stat)
            {
                case DefineStrong.BattlelogKDR:
                    foreach (PlayerModel player in team)
                    {
                        avg = avg + player.KDR;
                    }
                    break;
                case DefineStrong.BattlelogKPM:
                    foreach (PlayerModel player in team)
                    {
                        avg = avg + player.KPM;
                    }
                    break;
                case DefineStrong.BattlelogSPM:
                    foreach (PlayerModel player in team)
                    {
                        avg = avg + player.SPM;
                    }
                    break;
                case DefineStrong.PlayerRank:
                    foreach (PlayerModel player in team)
                    {
                        avg = avg + player.Rank;
                    }
                    break;
                case DefineStrong.RoundKDR:
                    foreach (PlayerModel player in team)
                    {
                        avg = avg + player.KDRRound;
                    }
                    break;
                case DefineStrong.RoundKills:
                    foreach (PlayerModel player in team)
                    {
                        avg = avg + player.KillsRound;
                    }
                    break;
                case DefineStrong.RoundKPM:
                    foreach (PlayerModel player in team)
                    {
                        avg = avg + player.KPMRound;
                    }
                    break;
                case DefineStrong.RoundScore:
                    foreach (PlayerModel player in team)
                    {
                        avg = avg + player.ScoreRound;
                    }
                    break;
                case DefineStrong.RoundSPM:
                    foreach (PlayerModel player in team)
                    {
                        avg = avg + player.SPMRound;
                    }
                    break;
                default: return 0;
            }
            return (avg / n);
        }

        /* === NEW_NEW_NEW === */





        public void LaunchCheckForPluginUpdate()
        {
            try
            {
                double alive = 0;
                DateTime since = DateTime.MinValue;
                lock (fUpdateThreadLock)
                {
                    alive = fUpdateThreadLock.MaxDelay; // repurpose MaxDelay to be a thread counter
                    since = fUpdateThreadLock.LastUpdate;
                }
                if (alive > 0)
                {
                    DebugWrite("Unable to check for updates, " + alive + " threads active for " + DateTime.Now.Subtract(since).TotalSeconds.ToString("F1") + " seconds!", 3);
                    return;
                }

                Thread t = new Thread(new ThreadStart(CheckForPluginUpdate));
                t.IsBackground = true;
                t.Name = "updater";
                DebugWrite("Starting updater thread ...", 3);
                t.Start();
                Thread.Sleep(2);
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }


        public void CheckForPluginUpdate()
        { // runs in one-shot thread
            try
            {
                lock (fUpdateThreadLock)
                {
                    fUpdateThreadLock.MaxDelay = fUpdateThreadLock.MaxDelay + 1;
                    fUpdateThreadLock.LastUpdate = DateTime.Now;
                }
                XmlDocument xml = new XmlDocument();
                try
                {
                    xml.Load("https://myrcon.com/procon/plugins/report/format/xml/plugin/MULTIbalancer");
                }
                catch (System.Security.SecurityException e)
                {
                    if (DebugLevel >= 8) ConsoleException(e);
                    ConsoleWrite(" ", 0);
                    ConsoleWrite("^8^bNOTICE! Unable to check for plugin update!", 0);
                    ConsoleWrite("Tools => Options... => Plugins tab: ^bPlugin security^n is set to ^bRun plugins in a sandbox^n.", 0);
                    //ConsoleWrite("Please add ^bmyrcon.com^n to your trusted ^bOutgoing connections^n");
                    ConsoleWrite("Consider changing to ^bRun plugins with no restrictions.^n", 0);
                    ConsoleWrite("Alternatively, check the ^bPlugins^n forum for an update to this plugin.", 0);
                    ConsoleWrite(" ", 0);
                    return;
                }
                if (DebugLevel >= 8) ConsoleDebug("CheckForPluginUpdate: Got " + xml.BaseURI);

                /*
                Example:
                <report>
                    <id>5132671</id>
                    <plugin>
                        <id>478344</id>
                        <uid>MultiBalancer</uid>
                        <name>MULTI-balancer</name>
                    </plugin>
                    <version>
                        <id>965536</id>
                        <major>1</major>
                        <minor>0</minor>
                        <maintenance>0</maintenance>
                        <build>1</build>
                    </version>
                    <sum_in_use>22</sum_in_use>
                    <avg_in_use>22.0000</avg_in_use>
                    <max_in_use>22</max_in_use>
                    <min_in_use>22</min_in_use>
                    <stamp>2013-05-10 10:00:04</stamp>
                </report>
                */

                XmlNodeList rows = xml.SelectNodes("//report");
                if (DebugLevel >= 8) ConsoleDebug("CheckForPluginUpdate: # rows = " + rows.Count);
                if (rows.Count == 0) return;
                Dictionary<String, Int32> versions = new Dictionary<String, Int32>();
                foreach (XmlNode tr in rows)
                {
                    XmlNode ver = tr.SelectSingleNode("version");
                    //XmlNode count = tr.SelectSingleNode("sum_in_use");
                    XmlNode count = tr.SelectSingleNode("max_in_use");
                    if (DebugLevel >= 8) ConsoleDebug("CheckForPluginUpdate: using max_in_use");
                    if (ver != null && count != null)
                    {
                        int test = 0;
                        XmlNode major = ver.SelectSingleNode("major");
                        if (major != null && !Int32.TryParse(major.InnerText, out test)) continue;
                        XmlNode minor = ver.SelectSingleNode("minor");
                        if (minor != null && !Int32.TryParse(minor.InnerText, out test)) continue;
                        XmlNode maint = ver.SelectSingleNode("maintenance");
                        if (maint != null && !Int32.TryParse(maint.InnerText, out test)) continue;
                        XmlNode build = ver.SelectSingleNode("build");
                        if (build != null && !Int32.TryParse(build.InnerText, out test)) continue;
                        String vt = major.InnerText + "." + minor.InnerText + "." + maint.InnerText + "." + build.InnerText;
                        if (DebugLevel >= 8) ConsoleDebug("CheckForPluginUpdate: Version: " + vt + ", Count: " + count.InnerText);
                        int n = 0;
                        if (!Int32.TryParse(count.InnerText, out n)) continue;
                        versions[vt] = n;
                    }
                }

                // Select current version and any "later" versions
                int usage = 0;
                String myVersion = GetPluginVersion();
                if (!versions.TryGetValue(myVersion, out usage))
                {
                    DebugWrite("CheckForPluginUpdate: " + myVersion + " not found!", 8);
                    return;
                }

                // numeric sort
                List<String> byNumeric = new List<String>();
                byNumeric.AddRange(versions.Keys);
                // Sort numerically descending
                byNumeric.Sort(delegate (String lhs, String rhs)
                {
                    if (lhs == rhs) return 0;
                    if (String.IsNullOrEmpty(lhs)) return 1;
                    if (String.IsNullOrEmpty(rhs)) return -1;
                    uint l = VersionToNumeric(lhs);
                    uint r = VersionToNumeric(rhs);
                    if (l < r) return 1;
                    if (l > r) return -1;
                    return 0;
                });
                DebugWrite("CheckForPluginUpdate: sorted version list:", 7);
                foreach (String u in byNumeric)
                {
                    DebugWrite(u + " (" + String.Format("{0:X8}", VersionToNumeric(u)) + "), count = " + versions[u], 7);
                }

                int position = byNumeric.IndexOf(myVersion);

                DebugWrite("CheckForPluginUpdate: found " + position + " newer versions", 5);

                if (position != 0)
                {
                    // Newer versions found
                    // Find the newest version with the largest number of usages
                    int hasMost = -1;
                    int most = 0;
                    for (int i = position - 1; i >= 0; --i)
                    {
                        int newerVersionCount = versions[byNumeric[i]];
                        if (hasMost == -1 || most < newerVersionCount)
                        {
                            // Skip newer versions that don't have enough usage yet
                            if (most > 0 && newerVersionCount < MIN_UPDATE_USAGE_COUNT) continue;
                            hasMost = i;
                            most = versions[byNumeric[i]];
                        }
                    }

                    if (hasMost != -1 && hasMost < byNumeric.Count && most >= MIN_UPDATE_USAGE_COUNT)
                    {
                        String newVersion = byNumeric[hasMost];
                        ConsoleWrite("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!", 0);
                        ConsoleWrite(" ", 0);
                        ConsoleWrite("^8^bA NEW VERSION OF THIS PLUGIN IS AVAILABLE!", 0);
                        ConsoleWrite(" ", 0);
                        ConsoleWrite("^8^bPLEASE UPDATE TO VERSION: ^0" + newVersion, 0);
                        ConsoleWrite(" ", 0);
                        ConsoleWrite("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!", 0);

                        TaskbarNotify(GetPluginName() + ": new version available!", "Please download and install " + newVersion);
                    }
                }
            }
            catch (ThreadAbortException)
            {
                fAborted = true;
                return;
            }
            catch (Exception e)
            {
                if (!fAborted) ConsoleException(e);
            }
            finally
            {
                if (!fAborted)
                {
                    // Update check time
                    fLastVersionCheckTimestamp = DateTime.Now;
                    // Update traffic control
                    lock (fUpdateThreadLock)
                    {
                        fUpdateThreadLock.MaxDelay = fUpdateThreadLock.MaxDelay - 1;
                        fUpdateThreadLock.LastUpdate = DateTime.MinValue;
                    }
                    DebugWrite("Updater thread finished!", 3);
                }
            }
        }

        private uint VersionToNumeric(String ver)
        {
            uint numeric = 0;
            byte part = 0;
            Match m = Regex.Match(ver, @"^\s*([0-9]+)\.([0-9]+)\.([0-9]+)\.([0-9]+)(\w*)\s*$");
            if (m.Success)
            {
                for (int i = 1; i < 5; ++i)
                {
                    if (!Byte.TryParse(m.Groups[i].Value, out part))
                    {
                        part = 0;
                    }
                    numeric = (numeric << 8) | part;
                }
            }
            return numeric;
        }


        private void LogStatus(bool isFinal, int level)
        {
            try
            {
                String tmsg = null;
                // If server is empty, log status only every 60 minutes
                int totalPlayers = TotalPlayerCount();
                if (!isFinal && level < 9 && totalPlayers == 0)
                {
                    if (fRoundStartTimestamp != DateTime.MinValue && DateTime.Now.Subtract(fRoundStartTimestamp).TotalMinutes <= 60)
                    {
                        return;
                    }
                    else
                    {
                        fRoundStartTimestamp = DateTime.Now;
                    }
                }

                if (!isFinal && (level == 4)) ConsoleWrite("+------------------------------------------------+", 0);

                if (isFinal && fWinner != 0)
                {
                    tmsg = "^1Winner was team " + fWinner + " (" + GetTeamName(fWinner) + ")^0";
                    DebugWrite("^bStatus^n: " + tmsg, 2);
                    ProconChat(tmsg);
                }

                Speed balanceSpeed = Speed.Adaptive;

                String tm = fTickets[1] + "/" + fTickets[2];
                if (IsSQDM()) tm = tm + "/" + fTickets[3] + "/" + fTickets[4];
                if (IsRush()) tm = tm + "(" + Math.Max(fTickets[1] / 2, fMaxTickets - (fRushMaxTickets - fTickets[2])) + ")";
                bool isCTF = IsCTF();
                bool isCarrierAssault = IsCarrierAssault();
                bool isObliteration = IsObliteration();
                if (isCTF || isCarrierAssault || isObliteration) tm = GetTeamPoints(1) + "/" + GetTeamPoints(2);

                double goal = 0;
                bool countDown = true;
                if (IsCountUp())
                {
                    countDown = false;
                    if (fServerInfo.TeamScores != null && fServerInfo.TeamScores.Count > 1)
                    {
                        foreach (TeamScore ts in fServerInfo.TeamScores)
                        {
                            if (ts.TeamID == 1)
                            {
                                goal = ts.WinningScore;
                                break;
                            }
                        }
                    }
                }

                if (goal == 0)
                {
                    if (fMaxTickets != -1)
                    {
                        tm = tm + " <- [" + fMaxTickets.ToString("F0") + "]";
                        goal = fMaxTickets;
                    }
                }
                else
                {
                    tm = tm + " -> [" + goal.ToString("F0") + "]";
                }

                String rt = GetTimeInRoundString();

                PerModeSettings perMode = GetPerModeSettings();

                String metroAdj = (perMode.EnableMetroAdjustments) ? ", Metro Adjustments Enabled" : String.Empty;
                String unstackDisabled = (!EnableUnstacking) ? ", Unstacking Disabled" : String.Empty;
                String logOnly = (EnableLoggingOnlyMode) ? ", Logging Only Mode Enabled" : String.Empty;
                String weakOnly = (perMode.OnlyMoveWeakPlayers) ? ", Only Move Weak Players" : String.Empty;
                String fastBalance = (EnableAdminKillForFastBalance) ? ", Admin Kill Enabled" : String.Empty;

                if (level >= 6) DebugWrite("^bStatus^n: Plugin state = " + fPluginState + ", game state = " + fGameState + fastBalance + weakOnly + metroAdj + unstackDisabled + logOnly, 0);
                int useLevel = (isFinal) ? 2 : 4;
                if (IsRush())
                {
                    tmsg = "Map = " + this.FriendlyMap + ", mode = " + this.FriendlyMode + ", stage = " + fRushStage + ", time in round = " + rt + ", tickets = " + tm;
                }
                else if (isCTF || isCarrierAssault || isObliteration)
                {
                    tmsg = "Map = " + this.FriendlyMap + ", mode = " + this.FriendlyMode + ", time in round = " + rt + ", score = " + tm;
                }
                else
                {
                    tmsg = "Map = " + this.FriendlyMap + ", mode = " + this.FriendlyMode + ", time in round = " + rt + ", tickets = " + tm;
                }
                if (level >= useLevel)
                    DebugWrite("^bStatus^n: " + tmsg, 0);
                if (isFinal)
                    ProconChat(tmsg);

                int ticketGap = Math.Abs(fTickets[1] - fTickets[2]);
                if (IsRush()) ticketGap = Convert.ToInt32(Math.Abs(fTickets[1] - Math.Max(fTickets[1] / 2, fMaxTickets - (fRushMaxTickets - fTickets[2]))));
                if (perMode.EnableTicketLossRatio && false)
                { // disable for this release
                    double a1 = GetAverageTicketLossRate(1, !EnableTicketLossRateLogging);
                    double a2 = GetAverageTicketLossRate(2, !EnableTicketLossRateLogging);
                    double rat = (a1 > a2) ? (a1 / Math.Max(1, a2)) : (a2 / Math.Max(1, a1));
                    rat = Math.Min(rat, 50.0); // cap at 50x
                    rat = rat * 100.0;
                    if (level >= useLevel) DebugWrite("^bStatus^n: Ticket difference = " + ticketGap + ", average ticket loss = " + a1.ToString("F2") + "(US) vs " + a2.ToString("F2") + " (RU)" + " for " + perMode.TicketLossSampleCount + " samples, ratio is " + rat.ToString("F0") + "%", 0);
                }
                else if (!IsSQDM() && fServerInfo.GameMode != "GunMaster0")
                {
                    bool privIsRush = IsRush();
                    double a1 = fTickets[1];
                    double a2 = (privIsRush) ? (Math.Max(fTickets[1] / 2, fMaxTickets - (fRushMaxTickets - fTickets[2]))) : fTickets[2];
                    double rat = (a1 > a2) ? (a1 / Math.Max(1, a2)) : (a2 / Math.Max(1, a1));
                    // For end of round, use standard function for ratio
                    if (fTickets[1] < 1 || fTickets[2] < 1)
                    {
                        String cmsg = String.Empty;
                        a1 = fTickets[1];
                        a2 = fTickets[2];
                        rat = ComputeTicketRatio(a1, a2, goal, countDown, out cmsg);
                        DebugWrite("^9DEBUG: " + cmsg, 7);
                    }
                    rat = Math.Min(rat, 50.0); // cap at 50x
                    rat = rat * 100.0;
                    String extra = ", score " + GetTeamPoints(1) + "/" + GetTeamPoints(2);
                    if (perMode.EnableUnstackingByPlayerStats)
                    {
                        a1 = GetAveragePlayerStats(1, perMode.DetermineStrongPlayersBy);
                        a2 = GetAveragePlayerStats(2, perMode.DetermineStrongPlayersBy);
                        double ratio = (a1 > a2) ? (a1 / Math.Max(0.01, a2)) : (a2 / Math.Max(0.01, a1));
                        ratio = Math.Min(ratio, 50.0); // cap at 50x

                        String cmp = (a1 > a2) ? (a1.ToString("F1") + "/" + a2.ToString("F1")) : (a2.ToString("F1") + "/" + a1.ToString("F1"));
                        extra = ", average " + perMode.DetermineStrongPlayersBy + " stats ratio = " + (ratio * 100.0).ToString("F0") + "% (" + cmp + ")";
                    }
                    else if ((privIsRush && perMode.EnableAdvancedRushUnstacking) || isCTF || isCarrierAssault || isObliteration)
                    {
                        // Check team points as well as tickets
                        double usPoints = GetTeamPoints(1);
                        double ruPoints = GetTeamPoints(2);
                        if (usPoints <= 0) usPoints = 1;
                        if (ruPoints <= 0) ruPoints = 1;
                        double sratio = (usPoints > ruPoints) ? (usPoints / ruPoints) : (ruPoints / usPoints);
                        String cr = (usPoints > ruPoints) ? (usPoints.ToString("F0") + "/" + ruPoints.ToString("F0")) : (ruPoints.ToString("F0") + "/" + usPoints.ToString("F0"));
                        extra = ", score ratio = " + (sratio * 100).ToString("F0") + "% (" + cr + ")";
                    }
                    if (level >= useLevel) DebugWrite("^bStatus^n: Ticket difference = " + ticketGap + ", ticket ratio percentage is " + rat.ToString("F0") + "%" + extra, 0);
                }

                if (fPluginState == PluginState.Active)
                {
                    double secs = DateTime.Now.Subtract(fLastBalancedTimestamp).TotalSeconds;
                    if (!fBalanceIsActive || fLastBalancedTimestamp == DateTime.MinValue) secs = 0;
                    /*
                    PerModeSettings perMode = null;
                    String simpleMode = String.Empty;
                    if (fModeToSimple.TryGetValue(fServerInfo.GameMode, out simpleMode) 
                      && fPerMode.TryGetValue(simpleMode, out perMode) && perMode != null) {
                    */
                    if (perMode != null)
                    {
                        balanceSpeed = GetBalanceSpeed(perMode);
                        double unstackRatio = GetUnstackTicketRatio(perMode);
                        String activeTime = (secs > 0) ? "^1active (" + secs.ToString("F0") + " secs)^0" : "not active";
                        if (level >= 4) DebugWrite("^bStatus^n: Autobalance is " + activeTime + ", phase = " + GetPhase(perMode, false) + ", population = " + GetPopulation(perMode, false) + ", speed = " + balanceSpeed + ", unstack when ratio >= " + (unstackRatio * 100).ToString("F0") + "%", 0);
                    }
                }
                if (!IsModelInSync())
                {
                    double toj = (fTimeOutOfJoint == 0) ? 0 : GetTimeInRoundMinutes() - fTimeOutOfJoint;
                    if (level >= 6) DebugWrite("^bStatus^n: Model not in sync for " + toj.ToString("F1") + " mins: fMoving = " + fMoving.Count + ", fReassigned = " + fReassigned.Count, 0);
                }

                String raged = fRageQuits.ToString() + "/" + fTotalQuits + " raged, ";
                useLevel = (isFinal) ? 2 : 5;
                if (level >= useLevel) DebugWrite("^bStatus^n: " + raged + fReassignedRound + " reassigned, " + fBalancedRound + " balanced, " + fUnstackedRound + " unstacked, " + fUnswitchedRound + " unswitched, " + fExcludedRound + " excluded, " + fExemptRound + " exempted, " + fFailedRound + " failed; of " + fTotalRound + " TOTAL", 0);

                useLevel = (isFinal) ? 2 : 4;
                String bf4Extras = (fGameVersion != GameVersion.BF3) ? ", " + fBF4CommanderCount + " commanders, " + fBF4SpectatorCount + " spectators" : String.Empty;
                if (IsSQDM())
                {
                    if (level >= useLevel) DebugWrite("^bStatus^n: Team counts [" + totalPlayers + "] = " + fTeam1.Count + "(A) vs " + fTeam2.Count + "(B) vs " + fTeam3.Count + "(C) vs " + fTeam4.Count + "(D), with " + fUnassigned.Count + " unassigned" + bf4Extras, 0);
                }
                else
                {
                    if (level >= useLevel) DebugWrite("^bStatus^n: Team counts [" + totalPlayers + "] = " + fTeam1.Count + "(" + GetTeamName(1) + ") vs " + fTeam2.Count + "(" + GetTeamName(2) + "), with " + fUnassigned.Count + " unassigned" + bf4Extras, 0);
                }

                List<int> counts = new List<int>();
                counts.Add(fTeam1.Count);
                counts.Add(fTeam2.Count);
                if (IsSQDM())
                {
                    counts.Add(fTeam3.Count);
                    counts.Add(fTeam4.Count);
                }

                // Announce autobalancing status

                counts.Sort();
                int diff = Math.Abs(counts[0] - counts[counts.Count - 1]);
                String next = "^n";
                String annType = null;

                if (EnableAdminKillForFastBalance && diff > MaxFastDiff())
                {
                    next = "^n^0 ... fast balance with admin kills in progress!";
                    annType = "USING ADMIN KILL";
                }
                else if ((totalPlayers >= 6 && diff > MaxDiff() && fGameState == GameState.Playing && balanceSpeed != Speed.Stop && !fBalanceIsActive))
                {
                    next = "^n^0 ... autobalance will activate as soon as possible!";

                    if (fUnassigned.Count >= (diff - MaxDiff()))
                    {
                        annType = "WAITING FOR " + fUnassigned.Count + " PLAYERS TO JOIN";
                    }
                    else
                    {
                        annType = "MOVE ON DEATH";
                    }
                }

                // Team difference

                if (level >= 4)
                {
                    String md = ((diff > MaxDiff()) ? "^8^b" : "^b") + diff + ((diff > MaxFastDiff() && EnableAdminKillForFastBalance) ? " (FAST)" : String.Empty);
                    DebugWrite("^bStatus^n: Team difference = " + md + next, 0);
                }

                // chats and yells
                if (fLastAutoChatTimestamp == DateTime.MinValue || DateTime.Now.Subtract(fLastAutoChatTimestamp).TotalSeconds > (YellDurationSeconds + 2.0))
                {
                    String cab = ChatAutobalancing;
                    String yab = YellAutobalancing;
                    if (!String.IsNullOrEmpty(cab) && cab.Contains("%technicalDetails%"))
                        cab = cab.Replace("%technicalDetails%", annType);
                    if (!String.IsNullOrEmpty(yab) && yab.Contains("%technicalDetails%"))
                        yab = yab.Replace("%technicalDetails%", annType);

                    if (annType != null && !String.IsNullOrEmpty(cab))
                    {
                        fLastAutoChatTimestamp = DateTime.Now;
                        Chat("all", cab);
                    }
                    if (annType != null && !String.IsNullOrEmpty(yab))
                    {
                        fLastAutoChatTimestamp = DateTime.Now;
                        Yell("all", yab);
                    }
                }

            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        public void OnPluginLoadingEnv(List<String> lstPluginEnv)
        {
            foreach (String env in lstPluginEnv)
            {
                DebugWrite("^9OnPluginLoadingEnv: " + env, 8);
            }
            switch (lstPluginEnv[1])
            {
                case "BF3": fGameVersion = GameVersion.BF3; break;
                case "BF4": fGameVersion = GameVersion.BF4; break;
                case "BFHL": fGameVersion = GameVersion.BFH; break;
                default: break;
            }
            ConsoleWrite("^2Game Version = " + lstPluginEnv[1], 0);
            /*
            Version PRoConVersion = new Version(lstPluginEnv[0]);
            this.m_strPRoConVersion = PRoConVersion.ToString();
            this.m_strServerGameType = lstPluginEnv[1].ToLower();
            this.m_strGameMod = lstPluginEnv[2];
            this.m_strServerVersion = lstPluginEnv[3];
            this.m_strSandboxEnabled = lstPluginEnv[4];

            if (this.m_strServerGameType == "bf3") {
                this.m_iTimeDivider = 1000;
            }
            */
        }

    } // end MULTIbalancer

} // end MULTIbalancer

} // end namespace PRoConEvents
