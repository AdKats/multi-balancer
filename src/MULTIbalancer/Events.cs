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
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;

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
        /* ======================== OVERRIDES ============================= */

        public void OnPluginLoaded(String strHostName, String strPort, String strPRoConVersion)
        {
            fHost = strHostName;
            fPort = strPort;

            this.RegisterEvents(this.GetType().Name,
                "OnVersion",
                "OnServerInfo",
                "OnListPlayers",
                //"OnPlayerJoin",
                "OnPlayerLeft",
                "OnPlayerKilled",
                "OnPlayerSpawned",
                "OnPlayerTeamChange",
                "OnPlayerSquadChange",
                "OnGlobalChat",
                "OnTeamChat",
                "OnSquadChat",
                "OnRoundOverPlayers",
                "OnRoundOver",
                "OnRoundOverTeamScores",
                "OnLevelLoaded",
                "OnPlayerKilledByAdmin",
                "OnPlayerMovedByAdmin",
                "OnPlayerIsAlive",
                "OnReservedSlotsList",
                "OnEndRound",
                "OnRunNextLevel",
                "OnResponseError",
                "OnLogin",
                "OnTeamFactionOverride",
                "OnRoundTimeLimit"
            );
        }

        public void OnPluginEnable()
        {
            if (fFinalizerActive)
            {
                ConsoleWarn("Not done disabling, try again in 10 seconds!");
                return;
            }
            fIsEnabled = true;
            fPluginState = PluginState.JustEnabled;
            fGameState = GameState.Unknown;
            fEnabledTimestamp = DateTime.Now;
            fRoundOverTimestamp = DateTime.MinValue;
            fRoundStartTimestamp = DateTime.Now;

            // Determine BF3 vs. BF4
            fMaxSquadSize = (fGameVersion == GameVersion.BF3) ? 4 : 5;

            ConsoleWrite("^b^2Enabled!^0^n Version = " + GetPluginVersion(), 0);
            DebugWrite("^b^3State = " + fPluginState, 6);
            DebugWrite("^b^3Game state = " + fGameState, 6);

            GatherProconGoodies();

            StartThreads();

            ServerCommand("reservedSlotsList.list");
            ServerCommand("serverInfo");
            ServerCommand("admin.listPlayers", "all");
            UpdateRoundTimeLimit();
            if (fGameVersion == GameVersion.BF4) UpdateFactions();

            LaunchCheckForPluginUpdate();

            fIsCacheEnabled = IsCacheEnabled(true);
        }

        public void OnPluginDisable()
        {
            fIsEnabled = false;

            try
            {
                LaunchCheckForPluginUpdate();

                fEnabledTimestamp = DateTime.MinValue;

                ConsoleWrite("^bDisabling, stopping threads ...^n", 0);

                StopThreads();

                Reset();

                fPluginState = PluginState.Disabled;
                fGameState = GameState.Unknown;
                DebugWrite("^b^3State = " + fPluginState, 6);
                DebugWrite("^b^3Game state = " + fGameState, 6);
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
            ConsoleWrite("^1^bDisabled!", 0);
        }

        public override void OnVersion(String type, String ver)
        {
            if (!fIsEnabled) return;

            DebugWrite("Got ^bOnVersion^n: " + type + " " + ver, 7);
        }

        public override void OnLogin()
        {
            if (!fIsEnabled) return;

            DebugWrite("Got ^bOnLogin^n", 8);
            try
            {
                if (fPluginState != PluginState.Active) return;
                DebugWrite("^1^bRECONNECTING ...^n", 3);
                fGotLogin = true;
                ScheduleListPlayers(1);
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        //public override void OnPlayerJoin(String soldierName) { }

        public override void OnPlayerLeft(CPlayerInfo playerInfo)
        {
            if (!fIsEnabled) return;

            DebugWrite("^9^bGot OnPlayerLeft:^n " + playerInfo.SoldierName, 8);

            try
            {
                if (IsKnownPlayer(playerInfo.SoldierName))
                {
                    CheckRageQuit(playerInfo.SoldierName);
                    ValidateMove(playerInfo.SoldierName);
                    RemovePlayer(playerInfo.SoldierName);
                }

                DebugWrite("Player left: ^b" + playerInfo.SoldierName, 4);

                if (EnableAdminKillForFastBalance)
                {
                    FastBalance("Player left: ");
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        public override void OnPlayerSquadChange(String soldierName, Int32 teamId, Int32 squadId)
        {
            if (!fIsEnabled) return;

            if (fGameState == GameState.Playing && squadId == 0) return;

            DebugWrite("^9^bGot OnPlayerSquadChange^n: " + soldierName + " " + teamId + " " + squadId, 7);

            try
            {
                if (fNeedPlayerListUpdate)
                {
                    PerModeSettings perMode = GetPerModeSettings();
                    if (perMode != null && perMode.EnableScrambler && (KeepSquadsTogether || KeepClanTagsInSameTeam))
                    {
                        PlayerModel player = GetPlayer(soldierName);
                        if (player != null)
                        {
                            String msg = "Player ^b{0}^n did a squad change to " + GetTeamName(teamId) + "/" + GetSquadName(squadId) + " after the scrambler finished";
                            DebugScrambler(String.Format(msg, player.FullName));
                            lock (fExtrasLock)
                            {
                                fDebugScramblerSuspects[player.Name] = msg;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        public override void OnPlayerTeamChange(String soldierName, Int32 teamId, Int32 squadId)
        {
            if (!fIsEnabled) return;

            DebugWrite("^9^bGot OnPlayerTeamChange^n: " + soldierName + " " + teamId + " " + squadId, 6);

            if (fPluginState == PluginState.Disabled || fPluginState == PluginState.Error) return;

            try
            {
                // Only teamId is valid for BF3, squad change is sent on separate event

                // Handle team change event
                if (fReassigned.Contains(soldierName))
                {
                    // We reassigned this new player
                    fReassigned.Remove(soldierName);
                    IncrementTotal();
                    fReassignedRound = fReassignedRound + 1;
                    AddNewPlayer(soldierName, teamId);
                    UpdateTeams();
                    DebugWrite("^4New player^0: ^b" + soldierName + "^n, reassigned to " + GetTeamName(teamId) + " team by " + GetPluginName(), 4);
                }
                else if (!IsKnownPlayer(soldierName))
                {
                    Int32 diff = 0;
                    Boolean mustMove = false; // don't have a player model yet, can't determine if must move
                    Int32 reassignTo = ToTeam(soldierName, teamId, true, out diff, ref mustMove);
                    if (!ReassignNewPlayers)
                    {
                        DebugWrite("^4New player^0: ^b" + soldierName + "^n not reassigned, Reassign New Players set to False", 5);
                        reassignTo = 0;
                    }
                    if ((reassignTo == 0 || reassignTo == teamId) && !fWhileScrambling)
                    {
                        // New player was going to the right team anyway
                        IncrementTotal(); // no matching stat, reflects non-reassigment joins
                        AddNewPlayer(soldierName, teamId);
                        UpdateTeams();
                        DebugWrite("^4New player^0: ^b" + soldierName + "^n, assigned to " + GetTeamName(teamId) + " team by game server", 4);
                        if (EnableAdminKillForFastBalance)
                        {
                            FastBalance("New Player: ");
                        }
                    }
                    else
                    {
                        Reassign(soldierName, teamId, reassignTo, diff);
                    }
                }
                else if (fGameState == GameState.Playing)
                {

                    // If this was an MB move, finish it
                    Boolean wasPluginMove = FinishMove(soldierName, teamId);

                    // Handle remote disabling of unswitcher
                    Boolean dontDoubleCount = false;
                    if (fDisableUnswitcherByRemote)
                    {
                        DebugWrite("^nPlayer ^b" + soldierName + "^n moved to team " + teamId + ": ^8another plugin DISABLED the unswitcher!^0^n", 4);
                        PlayerModel lucky = GetPlayer(soldierName);
                        if (lucky != null)
                        {
                            lucky.MovesRound = lucky.MovesRound + 1;
                            UpdateMoveTime(soldierName);
                            UpdatePlayerTeam(soldierName, teamId);
                            UpdateTeams();
                            dontDoubleCount = true;
                            // Do not increment stats
                        }
                    }

                    /*
                     * We need to determine if this team change was instigated by a player or by an admin (plugin).
                     * We want to ignore moves by admin. This is tricky due to the events possibly being 
                     * in reverse order (team change first, then moved by admin). Use player.isAlive
                     * to force a round trip with the game server, to insure that we get the admin move
                     * event, if it exists.
                     */
                    if (fPendingTeamChange.ContainsKey(soldierName))
                    {
                        // This is an admin move in correct order, do not treat it as a team switch
                        fPendingTeamChange.Remove(soldierName);
                        DebugWrite("Moved by admin: ^b" + soldierName + "^n to team " + teamId, 6);
                        if (!wasPluginMove)
                        {
                            // Some other admin.movePlayer, so update to account for it
                            DebugWrite("^4^bADMIN^n moved player ^b" + soldierName + "^n, " + GetPluginName() + " will respect this move", 2);
                            if (dontDoubleCount)
                            {
                                ConditionalIncrementMoves(soldierName);
                                UpdatePlayerTeam(soldierName, teamId);
                                UpdateTeams();
                            }
                        } // MB moves incremented by FinishMove, so nothing to do here
                        return;
                    }

                    // Remember the pending move in a table
                    fPendingTeamChange[soldierName] = teamId;

                    // Admin move event may still be on its way, so do a round-trip to check
                    ServerCommand("player.isAlive", soldierName);
                }
                else if (fGameState == GameState.RoundStarting || fGameState == GameState.RoundEnding)
                {

                    UpdatePlayerTeam(soldierName, teamId);
                    UpdateTeams();
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        public override void OnPlayerIsAlive(String soldierName, Boolean isAlive)
        {
            if (!fIsEnabled) return;

            DebugWrite("^9^bGot OnPlayerIsAlive^n: " + soldierName + " " + isAlive, 7);

            try
            {
                if (fPluginState != PluginState.Active) return;
                /*
                This may be the return leg of the round-trip to insure that
                an admin move event, if any, has been processed. If the player's
                name is still in fPendingTeamChange, it's a real player instigated move
                */
                if (fPendingTeamChange.ContainsKey(soldierName))
                {
                    Int32 team = fPendingTeamChange[soldierName];
                    fPendingTeamChange.Remove(soldierName);

                    // Check if player is allowed to switch teams
                    // Unswitch is handled in CheckTeamSwitch
                    // Unswitch is skipped if disabled by remote
                    if (!fDisableUnswitcherByRemote)
                    {
                        if (CheckTeamSwitch(soldierName, team))
                        {
                            UpdatePlayerTeam(soldierName, team);
                            UpdateTeams();
                            IncrementTotal(); // No matching stat, reflects allowed team switches
                        }
                    }
                    else
                    {
                        DebugWrite("^nSkipped check for unswitch for ^b" + soldierName + "^n: ^8another plugin DISABLED the Unswitcher!", 4);
                    }
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        public override void OnPlayerMovedByAdmin(String soldierName, Int32 destinationTeamId, Int32 destinationSquadId, Boolean forceKilled)
        {
            if (!fIsEnabled) return;

            DebugWrite("^9^bGot OnPlayerMovedByAdmin^n: " + soldierName + " " + destinationTeamId + " " + destinationSquadId + " " + forceKilled, 7);

            try
            {
                if (fPluginState == PluginState.Active && fGameState == GameState.Playing)
                {
                    if (fDisableUnswitcherByRemote)
                    {
                        DebugWrite("^nADMIN MOVED ^b" + soldierName + "^n to team " + destinationTeamId + ": ^8another plugin DISABLED the Unswitcher!", 4);
                    }
                    if (fPendingTeamChange.ContainsKey(soldierName))
                    {
                        // this is an admin move in reversed order, clear from pending table
                        fPendingTeamChange.Remove(soldierName);
                        DebugWrite("(REVERSED) Moved by admin: ^b" + soldierName + "^n to team " + destinationTeamId, 6);
                        // If the move was not done by MB, update and count the move
                        PlayerModel player = GetPlayer(soldierName);
                        if (player == null // haven't seen this player before
                        || GetMovesThisRound(player) == 0 // never been moved before (MB FinishMove would have incremented this)
                        || player.Team != destinationTeamId // no update for teams has been done yet (MB FinishMove would have done this)
                        || player.LastMoveFrom != 0)
                        { // interrupted MB move, special case
                          // Do updates as needed
                            Boolean interruptedMBMove = (player != null && player.LastMoveFrom != 0);
                            if (!interruptedMBMove)
                            {
                                DebugWrite("^4^bADMIN^n moved player (REVERSED) ^b" + soldierName + "^n, " + GetPluginName() + " will respect this move", 4);
                            }
                            else
                            {
                                ConsoleDebug("Interrupted move (REVERSED) ^b" + soldierName + "^n, updating to correct");
                            }
                            UpdatePlayerTeam(soldierName, destinationTeamId);
                            if (!interruptedMBMove) ConditionalIncrementMoves(soldierName);
                            UpdateTeams();
                        }
                    }
                    else if (!fUnassigned.Contains(soldierName))
                    {
                        // this is an admin move in correct order, add to pending table and let OnPlayerTeamChange handle it
                        fPendingTeamChange[soldierName] = destinationTeamId;
                    }
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        /*
        public override void OnSquadListPlayers(int teamId, int squadId, int playerCount, List<String> playersInSquad) {
            if (!fIsEnabled) return;

            DebugWrite("Got ^bOnSquadListPlayers^n: " + teamId + "/" + squadId + " has " + playerCount, 7);

            try {
                if (playersInSquad == null || playersInSquad.Count == 0) return;

                // Logging
                if (DebugLevel >= 6) {
                    String ss = "Squad (";
                    int t = Math.Max(0, Math.Min(teamId, TEAM_NAMES.Length-1));
                    int s = Math.Max(0, Math.Min(squadId, SQUAD_NAMES.Length-1));

                    ss = ss + TEAM_NAMES[t] + "/" + SQUAD_NAMES[s] + "): ";

                    bool first = true;
                    foreach (String grunt in playersInSquad) {
                        if (first) {
                            ss = ss + grunt;
                            first = false;
                        } else {
                            ss = ss + ", " + grunt;
                        }
                    }

                    ConsoleWrite("^9" + ss);
                }
            } catch (Exception e) {
                ConsoleException(e);
            }
        }
        */

        public override void OnPlayerKilled(Kill kKillerVictimDetails)
        {
            if (!fIsEnabled) return;

            String killer = kKillerVictimDetails.Killer.SoldierName;
            String victim = kKillerVictimDetails.Victim.SoldierName;
            String weapon = kKillerVictimDetails.DamageType;

            Boolean isAdminKill = false;
            if (String.IsNullOrEmpty(killer))
            {
                killer = victim;
                isAdminKill = (weapon == "Death");
            }

            DebugWrite("^9^bGot OnPlayerKilled^n: " + killer + " -> " + victim + " (" + weapon + ")", 8);
            if (isAdminKill) DebugWrite("^9OnPlayerKilled: admin kill: ^b" + victim + "^n (" + weapon + ")", 7);

            try
            {

                if (fGameState == GameState.Unknown || fGameState == GameState.Warmup)
                {
                    Boolean wasUnknown = (fGameState == GameState.Unknown);
                    fGameState = (TotalPlayerCount() < 4) ? GameState.Warmup : GameState.Playing;
                    if (wasUnknown || fGameState == GameState.Playing) DebugWrite("OnPlayerKilled: ^b^3Game state = " + fGameState, 6);
                    if (wasUnknown && fGameVersion == GameVersion.BF4) UpdateFactions();
                    fNeedPlayerListUpdate = (fGameState == GameState.Playing);
                }

                if (!isAdminKill)
                {
                    KillUpdate(killer, victim);

                    if (fPluginState == PluginState.Active && fGameState == GameState.Playing)
                    {
                        if (!IsModelInSync())
                        {
                            if (fTimeOutOfJoint == 0)
                            {
                                // If a move or reassign takes too long, abort it, checked in OnListPlayers
                                fTimeOutOfJoint = GetTimeInRoundMinutes();
                            }
                        }
                        else
                        {
                            fTimeOutOfJoint = 0;
                            if (EnableAdminKillForFastBalance)
                            {
                                FastBalance("Kill: ");
                            }
                            // Ok to call normal balance after FastBalance, they exclude from each other
                            BalanceAndUnstack(victim);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        public override void OnListPlayers(List<CPlayerInfo> players, CPlayerSubset subset)
        {
            if (!fIsEnabled) return;

            DebugWrite("^9^bGot OnListPlayers^n", 8);

            try
            {
                if (subset.Subset != CPlayerSubset.PlayerSubsetType.All) return;

                lock (fListPlayersQ)
                {
                    fListPlayersTimestamp = DateTime.Now;
                    Monitor.Pulse(fListPlayersQ);
                }

                /*
                Check if server crashed or Blaze dumped players or model invalid for too long.
                Detected by: last recorded server uptime is greater than zero and less than new uptime,
                or a player model timed out while still being on the all players list,
                or got an OnLogin callback, which is used in connection initialization for Procon,
                or the refresh command was used,
                or the current list of players is more than CRASH_COUNT_HEURISTIC players less than the last
                recorded count, or the last known player count is greater than the maximum server size 
                (adjusted for BF4 and BFH, to allow for 2 commanders above max player count),
                or more than 3 minutes have elapsed since a move/reassign was started.
                Since these detections are not completely reliable, do a minimal  amount of recovery,
                don't do a full reset
                */
                Int32 adjMaxSize = (fGameVersion == GameVersion.BF3) ? MaximumServerSize : (MaximumServerSize + 2); // for commanders
                Int32 totalPlayers = TotalPlayerCount();
                if (fServerCrashed
                || fGotLogin
                || fRefreshCommand
                || (fServerCrashed = (totalPlayers >= 16
                    && totalPlayers > players.Count
                    && (totalPlayers - players.Count) >= Math.Min(CRASH_COUNT_HEURISTIC, totalPlayers)))
                || totalPlayers > adjMaxSize
                || (fTimeOutOfJoint > 0 && GetTimeInRoundMinutes() - fTimeOutOfJoint > 3.0))
                {
                    String revWhy = String.Empty;
                    if (fServerCrashed) revWhy += "Crash ";
                    if (fGotLogin) revWhy += "Login ";
                    if (fRefreshCommand) revWhy += "Refresh ";
                    if (totalPlayers > adjMaxSize) revWhy += "MaximumServerSize(" + totalPlayers + ">" + MaximumServerSize + ") ";
                    if (fTimeOutOfJoint > 0 && (GetTimeInRoundMinutes() - fTimeOutOfJoint) > 3.0) revWhy += "MoveTimeTooLong";
                    ValidateModel(players, revWhy);
                    fServerCrashed = false;
                    fGotLogin = false;
                    fRefreshCommand = false;
                    fTimeOutOfJoint = 0;
                }
                else
                {
                    fUnassigned.Clear();

                    foreach (CPlayerInfo p in players)
                    {
                        try
                        {
                            Int32 bf4Type = (fGameVersion != GameVersion.BF3) ? p.Type : ROLE_PLAYER;
                            UpdatePlayerModel(p.SoldierName, p.TeamID, p.SquadID, p.GUID, p.Score, p.Kills, p.Deaths, p.Rank, bf4Type);
                        }
                        catch (Exception e)
                        {
                            ConsoleException(e);
                            continue;
                        }
                    }
                }

                GarbageCollectKnownPlayers(); // also resets LastMoveTo

                UpdateTeams();

                fLastFastMoveTimestamp = DateTime.MinValue; // reset fast move gap timer

                LogStatus(false, DebugLevel);

                /* Special handling for JustEnabled state */
                if (fPluginState == PluginState.JustEnabled)
                {
                    fPluginState = PluginState.Active;
                    fRoundStartTimestamp = DateTime.Now;
                    DebugWrite("^b^3State = " + fPluginState, 6);
                }

                // Use updated player list, one-time updates
                if (fNeedPlayerListUpdate)
                {
                    try { AssignGroups(); } catch (Exception e) { ConsoleException(e); }
                    try { RememberTeams(); } catch (Exception e) { ConsoleException(e); }
                    fNeedPlayerListUpdate = false;
                }

                //CommandToLog("whitelist");
                foreach (CPlayerInfo p in players)
                {
                    try
                    {
                        PlayerModel player = GetPlayer(p.SoldierName);
                        if (player == null) continue;
                        String guid = (String.IsNullOrEmpty(player.EAGUID)) ? INVALID_NAME_TAG_GUID : player.EAGUID;
                        String xt = ExtractTag(player);
                        if (String.IsNullOrEmpty(xt)) xt = INVALID_NAME_TAG_GUID;
                        foreach (String item in fSettingWhitelist)
                        {
                            List<String> tokens = new List<String>(Regex.Split(item, @"\s+"));
                            if (tokens.Count < 1)
                            {
                                continue;
                            }
                            if (tokens[0] == player.Name || tokens[0] == xt || tokens[0] == guid)
                            {
                                if (player.Whitelist == 0)
                                {
                                    DebugWrite("^8^bWARNING^n^0: (^b" + player.Name + ", " + xt + ", ^n" + guid + ") matches (" + String.Join(", ", tokens.ToArray()) + ") ^8^bBUT NO WHITELIST FLAGS SET!", 7);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        ConsoleException(e);
                    }
                }

            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        public override void OnServerInfo(CServerInfo serverInfo)
        {
            if (!fIsEnabled || serverInfo == null) return;

            DebugWrite("^9^bGot OnServerInfo^n: Debug level = " + DebugLevel, 8);

            DateTime debugTime = DateTime.Now;

            try
            {
                Double elapsedTimeInSeconds = DateTime.Now.Subtract(fLastServerInfoTimestamp).TotalSeconds;
                fLastServerInfoTimestamp = DateTime.Now;
                if (fUpdateTicketsRequest != null) fUpdateTicketsRequest.LastUpdate = fLastServerInfoTimestamp;

                // Update game state if just enabled (as of R38, CTF TeamScores may be null, does not mean round end)
                if (fGameState == GameState.Unknown && serverInfo.GameMode != "CaptureTheFlag0")
                {
                    if (serverInfo.TeamScores == null || serverInfo.TeamScores.Count < 2)
                    {
                        if (fGameVersion == GameVersion.BFH && Regex.Match(serverInfo.GameMode, @"(Heist|Hotwire|Bloodmoney)", RegexOptions.IgnoreCase).Success)
                        {
                            // Special handling for BFH until bugs with TeamScores are fixed for these modes
                            DebugWrite("OnServerInfo: Ignoring null TeamScores for BFH mode: " + serverInfo.GameMode, 8);
                        }
                        else
                        {
                            fGameState = GameState.RoundEnding;
                            DebugWrite("OnServerInfo: ^b^3Game state = " + fGameState, 6);
                        }
                    }
                }

                // Show final status 
                if (fFinalStatus != null)
                {
                    try
                    {
                        DebugWrite("^bFINAL STATUS FOR PREVIOUS ROUND:^n", 2);
                        foreach (TeamScore ts in fFinalStatus)
                        {
                            if (ts.TeamID >= fTickets.Length) break;
                            fTickets[ts.TeamID] = (ts.Score == 1) ? 0 : ts.Score; // fix rounding
                        }
                        LogStatus(true, DebugLevel);
                        DebugWrite("+------------------------------------------------+", 2);
                        if (DebugLevel >= 3) CommandToLog("bad tags");
                    }
                    catch (Exception) { }
                    fFinalStatus = null;
                }

                if (fServerInfo == null || fServerInfo.GameMode != serverInfo.GameMode || fServerInfo.Map != serverInfo.Map)
                {
                    ConsoleDebug("ServerInfo update: " + serverInfo.Map + "/" + serverInfo.GameMode);
                }

                // Check for server crash
                if (fServerUptime > 0 && fServerUptime > serverInfo.ServerUptime + 2)
                { // +2 secs for rounding error in server!
                    fServerCrashed = true;
                    DebugWrite("^1^bDETECTED GAME SERVER CRASH^n (recorded uptime longer than latest serverInfo uptime)", 3);
                }
                fServerInfo = serverInfo;
                fServerUptime = serverInfo.ServerUptime;

                // Update max tickets
                Int32 totalPlayers = TotalPlayerCount();
                PerModeSettings perMode = GetPerModeSettings();
                Boolean isRush = IsRush();
                Double minTickets = Double.MaxValue;
                Double maxTickets = 0;
                Double attacker = 0;
                Double defender = 0;
                Double[] oldTickets = new Double[] { 0, fTickets[1], fTickets[2] };
                if (fServerInfo.TeamScores == null || fServerInfo.TeamScores.Count < 2) return;
                foreach (TeamScore ts in fServerInfo.TeamScores)
                {
                    if (ts.TeamID >= fTickets.Length) break;
                    fTickets[ts.TeamID] = ts.Score;
                    if (ts.Score > maxTickets) maxTickets = ts.Score;
                    if (ts.Score < minTickets) minTickets = ts.Score;
                }

                if (isRush)
                {
                    foreach (TeamScore ts in fServerInfo.TeamScores)
                    {
                        if (ts.TeamID == 1)
                        {
                            attacker = ts.Score;
                        }
                        else if (ts.TeamID == 2)
                        {
                            defender = ts.Score;
                        }
                    }
                    //attacker = fServerInfo.TeamScores[0].Score;
                    //defender = fServerInfo.TeamScores[1].Score;
                    if (fStageInProgress)
                    {
                        if (attacker < fRushPrevAttackerTickets && attacker > 0)
                        {
                            fRushAttackerStageLoss = fRushAttackerStageLoss + (fRushPrevAttackerTickets - attacker);
                            ++fRushAttackerStageSamples;
                        }
                    }
                    String avl = String.Empty;
                    if (fStageInProgress) avl = ", avg loss = " + RushAttackerAvgLoss().ToString("F1") + "/" + Math.Min(perMode.SecondsToCheckForNewStage, elapsedTimeInSeconds).ToString("F0") + " secs";
                    if (totalPlayers > 3) DebugWrite("^7serverInfo: Rush attacker = " + attacker + ", was = " + fMaxTickets + avl + ", defender = " + defender, 7);
                }

                if (fMaxTickets == -1)
                {
                    if (!isRush)
                    {
                        fMaxTickets = maxTickets;
                        ConsoleDebug("ServerInfo update: fMaxTickets = " + fMaxTickets.ToString("F0"));
                    }
                    else
                    {
                        fRushMaxTickets = defender;
                        fMaxTickets = attacker;
                        fRushStage = 1;
                        fRushPrevAttackerTickets = attacker;
                        fRushAttackerStageSamples = 0;
                        fRushAttackerStageLoss = 0;
                        fStageInProgress = false;
                        ConsoleDebug("ServerInfo update: fMaxTickets = " + fMaxTickets.ToString("F0") + ", fRushMaxTickets = " + fRushMaxTickets + ", fRushStage = " + fRushStage);
                    }
                }

                // Rush heuristic: if attacker tickets are higher than last check, new stage started
                if (isRush && fServerInfo != null && !String.IsNullOrEmpty(fServerInfo.Map))
                {
                    Int32 maxStages = GetRushMaxStages(fServerInfo.Map);
                    if (fRushStage == 0)
                    {
                        fRushMaxTickets = defender;
                        fMaxTickets = attacker;
                        fRushStage = 1;
                        fRushPrevAttackerTickets = attacker;
                        fRushAttackerStageSamples = 0;
                        fRushAttackerStageLoss = 0;
                    }
                    if (!fStageInProgress)
                    {
                        // hysteresis, wait for attacker tickets to go below threshold before stage is in progress for sure
                        fStageInProgress = ((attacker + (2 * perMode.SecondsToCheckForNewStage / 5)) < fMaxTickets);
                        if (fStageInProgress)
                        {
                            DebugWrite("^7serverInfo: stage " + fRushStage + " in progress!", 7);
                        }
                    }
                    else if (attacker > fRushPrevAttackerTickets
                    && (attacker - fRushPrevAttackerTickets) >= Math.Min(12, 2 * perMode.SecondsToCheckForNewStage / 5)
                    && AttackerTicketsWithinRangeOfMax(attacker)
                    && fRushStage < 5)
                    {
                        fStageInProgress = false;
                        fRushMaxTickets = defender;
                        fMaxTickets = attacker;
                        fRushPrevAttackerTickets = attacker;
                        fRushStage = fRushStage + 1;
                        fRushAttackerStageSamples = 0;
                        fRushAttackerStageLoss = 0;
                        DebugWrite(".................................... ^b^1New rush stage detected^0^n ....................................", 3);
                        DebugBalance("Rush Stage " + fRushStage + " of " + maxStages);
                    }
                    // update last known attacker ticket value
                    fRushPrevAttackerTickets = attacker;
                }

                // Ticket loss rate updates
                if ((EnableTicketLossRateLogging || perMode.EnableTicketLossRatio) && fGameState == GameState.Playing && totalPlayers >= 4)
                {
                    if (fUpdateTicketsRequest == null) SetupUpdateTicketsRequest();
                    AddTicketLossSample(1, oldTickets[1], fTickets[1], elapsedTimeInSeconds);
                    AddTicketLossSample(2, oldTickets[2], fTickets[2], elapsedTimeInSeconds);
                }
                else
                {
                    ResetAverageTicketLoss();
                }

                if (EnableTicketLossRateLogging && IsConquest())
                {
                    UpdateTicketLossRateLog(DateTime.Now, 0, 0);
                }

                if ((EnableTicketLossRateLogging || perMode.EnableTicketLossRatio) && fGameState == GameState.Playing && totalPlayers >= 4)
                {
                    try
                    {
                        Double a1 = GetAverageTicketLossRate(1, false);
                        Double a2 = GetAverageTicketLossRate(2, false);
                        Double ratio = (a1 > a2) ? (a1 / Math.Max(1, a2)) : (a2 / Math.Max(1, a1));
                        ratio = Math.Min(ratio, 50.0); // cap at 50x
                        ratio = ratio * 100.0;
                        fTicketLossHistogram.Add(Convert.ToInt32(Math.Round(ratio)));
                    }
                    catch (Exception e)
                    {
                        ConsoleException(e);
                    }
                }

                // Check for plugin updates periodically
                if (fLastVersionCheckTimestamp != DateTime.MinValue
                && DateTime.Now.Subtract(fLastVersionCheckTimestamp).TotalMinutes > CHECK_FOR_UPDATES_MINS)
                {
                    LaunchCheckForPluginUpdate();
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
            finally
            {
                Double elapsedTime = DateTime.Now.Subtract(debugTime).TotalMilliseconds;
                if (DebugLevel >= 8 || (DebugLevel >= 7 && elapsedTime > 100.0))
                {
                    DebugWrite("^8OnServerInfo took ^b" + elapsedTime.ToString("F0") + "^n ms", 1);
                }
            }
        }

        public override void OnGlobalChat(String speaker, String message)
        {
            if (!fIsEnabled) return;
            if (DebugLevel >= 8) ConsoleDebug("OnGlobalChat(" + speaker + ", '" + message + ")");

            try
            {
                if (Regex.Match(message, @"^\s*/?[!@#]mb", RegexOptions.IgnoreCase).Success)
                {
                    InGameCommand(message, ChatScope.Global, 0, 0, speaker);
                }
                else
                {
                    if (EnableAdminKillForFastBalance && speaker != "Server")
                    {
                        FastBalance("Chat: ");
                    }
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        public override void OnTeamChat(String speaker, String message, Int32 teamId)
        {
            if (!fIsEnabled) return;
            if (DebugLevel >= 8) ConsoleDebug("OnTeamChat(" + speaker + ", '" + message + "', " + teamId + ")");

            try
            {
                if (Regex.Match(message, @"^\s*/?[!@#]mb", RegexOptions.IgnoreCase).Success)
                {
                    InGameCommand(message, ChatScope.Team, teamId, 0, speaker);
                }
                else
                {
                    if (EnableAdminKillForFastBalance && speaker != "Server" && !message.StartsWith("ID_CHAT"))
                    {
                        FastBalance("Team Chat: ");
                    }
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        public override void OnSquadChat(String speaker, String message, Int32 teamId, Int32 squadId)
        {
            if (!fIsEnabled) return;

            try
            {
                if (Regex.Match(message, @"^\s*/?[!@#]mb", RegexOptions.IgnoreCase).Success)
                {
                    InGameCommand(message, ChatScope.Squad, teamId, squadId, speaker);
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        public override void OnRoundOverPlayers(List<CPlayerInfo> players)
        {
            if (!fIsEnabled) return;

            DebugWrite("^9^bGot OnRoundOverPlayers^n", 7);

            try
            {
                // TBD
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        public override void OnRoundOverTeamScores(List<TeamScore> teamScores)
        {
            if (!fIsEnabled) return;

            DebugWrite("^9^bGot OnRoundOverTeamScores^n", 7);

            try
            {
                fFinalStatus = teamScores;
                ServerCommand("serverInfo"); // get info for final status report
                Scrambler(teamScores);
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        public override void OnRoundOver(Int32 winningTeamId)
        {
            if (!fIsEnabled) return;

            DebugWrite("^9^bGot OnRoundOver^n: winner " + winningTeamId, 7);

            try
            {
                fWinner = winningTeamId;
                fRoundOverTimestamp = DateTime.Now;

                DebugWrite(":::::::::::::::::::::::::::::::::::: ^b^1Round over detected^0^n ::::::::::::::::::::::::::::::::::::", 3);

                if (fGameState == GameState.Playing || fGameState == GameState.Unknown)
                {
                    fGameState = GameState.RoundEnding;
                    DebugWrite("OnRoundOver: ^b^3Game state = " + fGameState, 6);
                }

                if (DebugLevel >= 3 && fTicketLossHistogram.Total > 10)
                {
                    CommandToLog("histogram");
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        public override void OnLevelLoaded(String mapFileName, String Gamemode, Int32 roundsPlayed, Int32 roundsTotal)
        {
            if (!fIsEnabled) return;

            DebugWrite("^9^bGot OnLevelLoaded^n: " + mapFileName + " " + Gamemode + " " + roundsPlayed + "/" + roundsTotal, 7);

            try
            {
                DebugWrite(":::::::::::::::::::::::::::::::::::: ^b^1Level loaded detected^0^n ::::::::::::::::::::::::::::::::::::", 3);

                if (fGameState == GameState.RoundEnding || (fGameState == GameState.Warmup && TotalPlayerCount() >= 4) || fGameState == GameState.Unknown)
                {
                    fGameState = GameState.RoundStarting;
                    DebugWrite("OnLevelLoaded: ^b^3Game state = " + fGameState, 6);

                    CheckRoundEndingDuration();
                }

                fMaxTickets = -1; // flag to pay attention to next serverInfo
                ServerCommand("serverInfo");

                UpdateRoundTimeLimit();
                if (fGameVersion == GameVersion.BF4) UpdateFactions();
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        public override void OnPlayerSpawned(String soldierName, Inventory spawnedInventory)
        {
            if (!fIsEnabled) return;

            DebugWrite("^9^bGot OnPlayerSpawned: ^n" + soldierName, 8);

            try
            {
                Int32 totalPlayers = TotalPlayerCount();
                if (fGameState == GameState.Unknown || fGameState == GameState.Warmup)
                {
                    Boolean wasUnknown = (fGameState == GameState.Unknown);
                    fGameState = (totalPlayers < 4) ? GameState.Warmup : GameState.Playing;
                    if (wasUnknown || fGameState == GameState.Playing) DebugWrite("OnPlayerSpawned: ^b^3Game state = " + fGameState, 6);
                    if (wasUnknown && fGameVersion == GameVersion.BF4) UpdateFactions();
                    fNeedPlayerListUpdate = (fGameState == GameState.Playing);
                    if (EnableAdminKillForFastBalance)
                    {
                        FastBalance("GameState changed to Playing: ");
                    }
                }
                else if (fGameState == GameState.RoundStarting)
                {
                    // First spawn after Level Loaded is the official start of a round
                    DebugWrite(":::::::::::::::::::::::::::::::::::: ^b^1First spawn detected^0^n ::::::::::::::::::::::::::::::::::::", 3);

                    fGameState = (totalPlayers < 4) ? GameState.Warmup : GameState.Playing;
                    DebugWrite("OnPlayerSpawned: ^b^3Game state = " + fGameState, 6);

                    ResetRound();
                    fIsFullRound = true;
                    ServerCommand("serverInfo");
                    UpdateRoundTimeLimit();
                    ScheduleListPlayers(2);
                    fNeedPlayerListUpdate = (fGameState == GameState.Playing);
                    if (fGameVersion == GameVersion.BF4) UpdateFactions();
                }

                if (fPluginState == PluginState.Active)
                {
                    ValidateMove(soldierName);
                    if (fGameState != GameState.RoundEnding)
                    {
                        SpawnUpdate(soldierName);
                        FireMessages(soldierName);
                        CheckDelayedMove(soldierName);
                        if (IsRush()) CheckServerInfoUpdate();
                    }
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        public override void OnPlayerKilledByAdmin(String soldierName)
        {
            if (!fIsEnabled) return;

            DebugWrite("^9^bGot OnPlayerKilledByAdmin^n: " + soldierName, 7);
        }

        public override void OnReservedSlotsList(List<String> lstSoldierNames)
        {
            // do this always

            DebugWrite("^9^bGot OnReservedSlotsList^n", 7);
            fReservedSlots = lstSoldierNames;

            if (EnableWhitelistingOfReservedSlotsList)
            {
                UpdateWhitelistModel();
            }
        }

        public override void OnEndRound(Int32 iWinningTeamID)
        {
            if (!fIsEnabled) return;

            DebugWrite("^9^bGot OnEndRound^n: " + iWinningTeamID, 7);
        }

        public override void OnRunNextLevel()
        {
            if (!fIsEnabled) return;

            DebugWrite("^9^bGot OnRunNextLevel^n", 7);
        }

        public override void OnTeamFactionOverride(Int32 teamId, Int32 faction)
        {
            if (!fIsEnabled) return;

            DebugWrite("^9^bGot OnTeamFactionOverride^n(" + teamId + ", " + faction + ")", 7);
            if (teamId >= 0 && teamId < fFactionByTeam.Length && faction >= 0)
            {
                fFactionByTeam[teamId] = faction;
            }
        }

        public override void OnRoundTimeLimit(Int32 limit)
        {
            if (!fIsEnabled) return;

            DebugWrite("^9^bGot OnRoundTimeLimit^n(" + limit + ")", 7);
            fRoundTimeLimit = limit / 100.0;
        }

        public override void OnResponseError(List<String> lstRequestWords, String strError)
        {
            if (!fIsEnabled) return;
            if (lstRequestWords == null || lstRequestWords.Count == 0) return;
            try
            {
                String msg = "Request(" + String.Join(", ", lstRequestWords.ToArray()) + "): ERROR = " + strError;

                Int32 level = 7;
                if (lstRequestWords[0] == "player.ping") level = 8;

                DebugWrite("^9^bGot OnResponseError, " + msg, level);

                Boolean isMove = false;
                if (lstRequestWords.Count > 2 && lstRequestWords[0] == "admin.movePlayer")
                {
                    DebugWrite("^1Move of ^b" + lstRequestWords[1] + "^n failed with error: " + strError, 4);
                    isMove = true;
                }

                // Record problems during a scramble
                if (isMove && (fGameState == GameState.RoundEnding || fGameState == GameState.RoundStarting))
                {
                    lock (fExtrasLock)
                    {
                        fDebugScramblerSuspects[lstRequestWords[1]] = "Move of ^b{0}^n during scramble got an error: " + strError;
                    }
                }
            }
            catch (Exception e)
            {
                ConsoleException(e);
            }
        }

        /* Not really an override, but a hook for other plugins to call */

        public void UpdatePluginData(params String[] parms)
        {
            /*
            parms[0]: Name of caller (plugin class)
            parms[1]: Name of the type of parm[3]: "bool", "double", "int", "string" (not possible to pass object type)
            parms[2]: Key or Data Field name
            parms[3]: Stringification of value
            */
            if (parms.Length != 4)
            {
                if (DebugLevel >= 5) ConsoleWarn("UpdatePluginData called with incorrect parameter count: " + parms.Length);
                return;
            }

            if (String.IsNullOrEmpty(parms[0]))
            {
                if (DebugLevel >= 5) ConsoleWarn("UpdatePluginData parms[0]: caller name is invalid!");
                return;
            }
            if (String.IsNullOrEmpty(parms[1]))
            {
                if (DebugLevel >= 5) ConsoleWarn("UpdatePluginData parms[1]: type is invalid!");
                return;
            }
            if (String.IsNullOrEmpty(parms[2]))
            {
                if (DebugLevel >= 5) ConsoleWarn("UpdatePluginData parms[2]: key is invalid!");
                return;
            }

            try
            {
                String calledFrom = parms[0];
                Type type = typeof(String);
                switch (parms[1])
                {
                    case "bool": type = typeof(Boolean); break;
                    case "double": type = typeof(Double); break;
                    case "int": type = typeof(Int32); break;
                    default: break;
                }
                String key = parms[2];
                Object value = parms[3];

                if (type == typeof(Boolean))
                {
                    Boolean v = false;
                    Boolean.TryParse(parms[3], out v);
                    value = (Boolean)v;
                }
                else if (type == typeof(Double))
                {
                    Double v = 0;
                    Double.TryParse(parms[3], out v);
                    value = (Double)v;
                }
                else if (type == typeof(Int32))
                {
                    Int32 v = 0;
                    Int32.TryParse(parms[3], out v);
                    value = (Int32)v;
                }

                switch (key)
                {
                    case "SetScrambleByCommand":
                        if (type != typeof(Boolean))
                        {
                            if (DebugLevel >= 5) ConsoleWarn("UpdatePluginData(" + calledFrom + ", " + key + ") expected bool, got " + parms[1]);
                            return;
                        }
                        else
                        {
                            fScrambleByCommand = (Boolean)value;
                            if (fScrambleByCommand)
                            {
                                DebugWrite("Plugin " + calledFrom + " turned team scrambling ON for this round!", 4);
                            }
                            else
                            {
                                DebugWrite("Plugin " + calledFrom + " turned team scrambling OFF for this round!", 4);
                            }
                        }
                        break;
                    case "DisableUnswitcher":
                        if (type != typeof(Boolean))
                        {
                            if (DebugLevel >= 5) ConsoleWarn("UpdatePluginData(" + calledFrom + ", " + key + ") expected bool, got " + parms[1]);
                            return;
                        }
                        else
                        {
                            fDisableUnswitcherByRemote = (Boolean)value;
                            if (fDisableUnswitcherByRemote)
                            {
                                DebugWrite("Plugin " + calledFrom + " turned unswitching OFF for this round!", 4);
                            }
                            else
                            {
                                DebugWrite("Plugin " + calledFrom + " turned unswitching ON for this round!", 4);
                            }
                        }
                        break;
                    default:
                        if (DebugLevel >= 5) ConsoleWarn("UpdatePluginData unknown key " + key + ", called from " + calledFrom);
                        return;
                }
                DebugWrite("Plugin ^b" + calledFrom + "^n, updated (" + parms[1] + ") " + key + " <- " + parms[3], 5);
            }
            catch (Exception e)
            {
                if (DebugLevel >= 5) ConsoleException(e);
            }

        }

        /* JSON parameters entry point support */

        public void UpdatePluginJSON(params String[] parms)
        {
            /*
            parms[0]: Name of caller plugin
            parms[1]: JSON with this format:
                {
                    "plugin":"string",
                    "type":"string",
                    "key":"string",
                    "value":"string"
                }
            */
            if (parms.Length != 2)
            {
                if (DebugLevel >= 5) ConsoleWarn("UpdatePluginJSON called with incorrect parameter count: " + parms.Length);
                return;
            }

            if (String.IsNullOrEmpty(parms[0]))
            {
                if (DebugLevel >= 5) ConsoleWarn("UpdatePluginJSON parms[0]: caller name is invalid!");
                return;
            }

            if (String.IsNullOrEmpty(parms[1]))
            {
                if (DebugLevel >= 5) ConsoleWarn("UpdatePluginJSON(" + parms[0] + ") parms[1]: JSON is invalid!");
                return;
            }

            try
            {

                Hashtable json = (Hashtable)JSON.JsonDecode(parms[1]);

                String plugin = null;
                String type = null;
                String key = null;
                String value = null;

                if (json == null)
                {
                    String tmp = parms[1].Replace('{', '(').Replace('}', ')');
                    if (DebugLevel >= 5) ConsoleWarn("UpdatePluginJSON(" + parms[0] + "): JSON is invalid (null): " + tmp);
                    return;
                }

                if (!json.ContainsKey("plugin"))
                {
                    if (DebugLevel >= 5) ConsoleWarn("UpdatePluginJSON(" + parms[0] + ") parms[1]: JSON does not contain 'plugin' key!");
                    return;
                }
                else
                {
                    plugin = (String)json["plugin"];
                }
                if (!json.ContainsKey("type"))
                {
                    if (DebugLevel >= 5) ConsoleWarn("UpdatePluginJSON(" + parms[0] + ") parms[1]: JSON does not contain 'type' key!");
                    return;
                }
                else
                {
                    type = (String)json["type"];
                }
                if (!json.ContainsKey("key"))
                {
                    if (DebugLevel >= 5) ConsoleWarn("UpdatePluginJSON(" + parms[0] + ") parms[1]: JSON does not contain 'key' key!");
                    return;
                }
                else
                {
                    key = (String)json["key"];
                }
                if (!json.ContainsKey("value"))
                {
                    if (DebugLevel >= 5) ConsoleWarn("UpdatePluginJSON(" + parms[0] + ") parms[1]: JSON does not contain 'value' key!");
                    return;
                }
                else
                {
                    value = (String)json["value"];
                }

                UpdatePluginData(plugin, type, key, value);
            }
            catch (Exception e)
            {
                if (DebugLevel >= 5) ConsoleException(e);
            }
        }

    } // end MULTIbalancer

} // end namespace PRoConEvents
