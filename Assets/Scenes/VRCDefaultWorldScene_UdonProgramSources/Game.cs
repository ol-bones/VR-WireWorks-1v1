
using System;
using System.Collections.Generic;
using Miner28.UdonUtils.Network;
using UdonSharp;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;

public enum GameStatus
{
    Waiting,
    Ready,
    InProgress,
    Finished
}

public class Game : NetworkInterface
{
    public GameObject m_BombPrefab;
    public Bomb m_Bomb;
    public Bomb[] m_BombsOnBoard;
    public int m_GameId;

    public VRCPlayerApi m_GameMaster;

    public int m_MaximumScrewDriverUsage;
    public int[] m_ScrewDriverUsageCount;
    public int[] m_Scores;
    public VRCPlayerApi[] m_Players;
    public int m_GridSize;

    public int m_SeatTurn;
    public bool m_HasBonusTurn;

    public GameStatus m_GameStatus;

    public int m_PlayerWithBomb;
    public int m_Seat;

    public int m_MaximumBoxes;
    public int m_BoxCount;
    public Box[] m_Boxes;

    void Start()
    {
        this.m_Players = new VRCPlayerApi[2];
        this.m_MaximumBoxes = ((m_GridSize-1) * (m_GridSize-1)) * 2;
        this.m_Boxes = new Box[this.m_MaximumBoxes];
        this.m_Scores = new int[2];
        this.m_MaximumScrewDriverUsage = 1;
        this.m_ScrewDriverUsageCount = new int[2];
        this.ResetScores();
        this.m_SeatTurn = 0;
        this.m_HasBonusTurn = false;
        this.m_GameStatus = GameStatus.Waiting;
        this.m_PlayerWithBomb = -1;
        this.m_Bomb = null;
        this.m_BombsOnBoard = new Bomb[2];
    }

    public void AddBox(Box box, int seat)
    {
        m_Boxes[m_BoxCount++] = box;
    }

    public void DeleteAllBoxes()
    {
        foreach (Box box in m_Boxes)
        {
            if (box != null)
            {
                box.Delete();
            }
        }

        for(int i = 0; i < m_Boxes.Length; i++)
        {
            m_Boxes[i] = null;
        }

        m_BoxCount = 0;
    }

    public void Join(VRCPlayerApi player, int seat, bool master)
    {
        if(player == null) { Debug.LogError("Join: Player is null"); return; }
        if (!(seat == 0 || seat == 1)) { Debug.LogError("Join: Invalid seatid"); return; }
        if (this.m_Players[seat] != null) { Debug.LogError("Join: Seat is already taken"); return; }
        if (this.m_GameStatus != GameStatus.Waiting) { Debug.LogError("Join: Game is not in waiting state"); return; }

        Debug.Log("Player " + player.displayName + " joined  game" + this.m_GameId.ToString() + " seat" + seat.ToString());

        this.m_Players[seat] = player;
        if (master)
        {
            Debug.Log("Player " + player.displayName + " is the game master");
            this.m_GameMaster = player;
        }

        if(Networking.LocalPlayer == player)
        {
            this.m_Seat = seat;
        }

        if(this.m_Players[0] != null && this.m_Players[1] != null)
        {
            this.On_GameReady();
        }
    }

    public void On_GameReady()
    {
        Debug.Log("Game.cs: On_GameReady: Both players have joined. Ready...");
        this.m_GameStatus = GameStatus.Ready;
        if(this.IsLocalPlayerMaster())
        {
            StartButton startButton = this.FindStartButtonForSeat(this.m_Seat);
            if(startButton == null) { Debug.LogError("Join: StartButton not found"); return; }

            startButton.gameObject.SetActive(true);
        }
    }

    public void On_TryStartGame(VRCPlayerApi requestingPlayer)
    {
        if (requestingPlayer == null) return;
        if (!IsPlayerInGame(requestingPlayer)) { Debug.LogError("On_StartGameRequest: Player not in game"); return; }
        if (!IsPlayerMaster(requestingPlayer)) { Debug.LogError("On_StartGameRequest: Player not master"); return; }

        Debug.Log("On_StartGameRequest called");

        Notify_GameStart();
    }

    public bool m_IsRequestingGameStart = false;
    public bool m_IsRequestingGameStartTimerStarted = false;
    public bool m_IsRequestingGameStartTimerExpired = false;
    public void Notify_GameStart()
    {
        if (m_IsRequestingGameStart) return;
        if (m_IsRequestingGameStartTimerStarted) return;

        if (IsLocalPlayerMaster())
        {
            m_IsRequestingGameStart = true;
            m_IsRequestingGameStartTimerStarted = true;
            m_IsRequestingGameStartTimerExpired = false;

            SendCustomEventDelayedSeconds(nameof(this.On_RequestGameStartTimerExpired), 5);

            SendMethodNetworked(
                nameof(this.On_RequestGameStart),
                SyncTarget.All,
                new DataToken(Networking.LocalPlayer),
                new DataToken(this.m_GameId)
            );
        }
    }

    public void On_RequestGameStartTimerExpired()
    {
        this.m_IsRequestingGameStart = false;
        this.m_IsRequestingGameStartTimerStarted = false;
        this.m_IsRequestingGameStartTimerExpired = true;

        if (this.m_GameStatus == GameStatus.Waiting || this.m_GameStatus == GameStatus.Ready)
        {
            StartButton startButton = this.FindStartButtonForSeat(this.m_Seat);
            if(startButton == null) { Debug.LogError("Join: StartButton not found"); return; }

            startButton.gameObject.SetActive(true);
        }
    }

    [NetworkedMethod]
    public void On_RequestGameStart(VRCPlayerApi requestingPlayer, int gameId)
    {
        if (this.m_GameId != gameId) return;
        if (requestingPlayer == null) return;
        if(requestingPlayer.playerId == Networking.LocalPlayer.playerId && !this.BothLocalPlayers()) return;
        if (!IsPlayerInGame(requestingPlayer)) { Debug.LogError("RequestGameStart: Player not in game"); return; }
        if (!IsPlayerInGame(Networking.LocalPlayer)) { Debug.LogError("RequestGameStart: Local player not in game"); return; }
        Debug.Log("RequestGameStart called");

        SendMethodNetworked(
            nameof(this.On_GameStartRequest_Accepted),
            SyncTarget.All,
            new DataToken(Networking.LocalPlayer),
            new DataToken(this.m_GameId)
        );
    }

    [NetworkedMethod]
    public void On_GameStartRequest_Accepted(VRCPlayerApi respondingPlayer, int gameId)
    {
        if (this.m_GameId != gameId) return;
        if (respondingPlayer == null) return;
        if (!IsPlayerInGame(respondingPlayer)) { Debug.LogError("On_GameStartRequest_Accepted: Player not in game"); return; }
        if (!IsLocalPlayerMaster()) { Debug.LogError("On_GameStartRequest_Accepted: Local player not master"); return; }
        if (respondingPlayer.playerId == Networking.LocalPlayer.playerId && !this.BothLocalPlayers()) return;

        Debug.Log("On_GameStartRequest_Accepted called");

        SendMethodNetworked(
            nameof(this.GameStart),
            SyncTarget.All,
            new DataToken(Networking.LocalPlayer),
            new DataToken(this.m_GameId)
        );
    }

    [NetworkedMethod]
    public void GameStart(VRCPlayerApi requestingPlayer, int gameId)
    {
        if (this.m_GameId != gameId) return;
        if (requestingPlayer == null) return;
        if (!IsPlayerInGame(requestingPlayer)) { Debug.LogError("Game.cs: GameStart: Player not in game"); return; }
        if (this.m_GameStatus != GameStatus.Ready) return;
        if(IsLocalPlayerMaster() && requestingPlayer.playerId != Networking.LocalPlayer.playerId)
        {
            // Somehow, two players became game masters.
            // Other player started the game first, so that player is the game master now.
            Debug.Log("Game.cs: GameStart: 2 game masters? setting game master to other player.");
            this.m_GameMaster = this.PlayerInGameById(requestingPlayer.playerId);
        }
        else if(!IsPlayerInGame(Networking.LocalPlayer))
        {
            // Somehow, the local player is not set for spectators. Oh well, just set it here.
            Debug.Log("Game.cs: GameStart: Local player not set. Setting local player to requesting player.");
            this.m_GameMaster = requestingPlayer;
        }

        Debug.Log("GameStart called");
        //this.GivePlayerBomb(0);

        StartButton startButton0 = this.FindStartButtonForSeat(0);
        if(startButton0 == null) { Debug.LogError("Join: StartButton not found"); return; }

        startButton0.gameObject.SetActive(false);

        StartButton startButton1 = this.FindStartButtonForSeat(1);
        if(startButton1 == null) { Debug.LogError("Join: StartButton not found"); return; }

        startButton1.gameObject.SetActive(false);

        this.m_SeatTurn = 0;
        this.m_GameStatus = GameStatus.InProgress;

        GameCanvas gameCanvas0 = this.GetGameCanvasBySeatId(0);
        GameCanvas gameCanvas1 = this.GetGameCanvasBySeatId(1);

        if (gameCanvas0 == null || gameCanvas1 == null)
        {
            Debug.LogError("GameStart: GameCanvas not found");
            return;
        }

        gameCanvas0.ShowAllDots();
        gameCanvas1.ShowAllDots();
    }

    public void Request_BoxSelect(Box box)
    {
        Debug.Log("Request_BoxSelect called");
        if(!this.IsPlayersTurn(Networking.LocalPlayer.playerId)) { Debug.LogError($"Game.cs Request_BoxSelect: Not LocalPlayers' turn - turn={this.m_SeatTurn} seat={this.m_Seat}"); return; }
        if(this.m_GameStatus != GameStatus.InProgress) { Debug.LogError($"Game.cs Request_BoxSelect: Game not in progress (state={this.m_GameStatus})"); return; }

        if(this.m_Bomb != null && this.m_PlayerWithBomb == this.m_Seat)
        {
            Debug.Log("Requesting bomb place");
            SendMethodNetworked(
                nameof(this.On_RequestPlaceBomb),
                SyncTarget.All,
                new DataToken(Networking.LocalPlayer),
                new DataToken(this.m_GameId),
                new DataToken(this.m_Seat),
                new DataToken(box.m_ID)
            );
        }
        else
        {
            Debug.LogError($"Game.cs Request_BoxSelect: Player {this.m_Seat} doesn't have bomb (playerWithBomb={this.m_PlayerWithBomb}) or bomb is null");
        }
    }

    [NetworkedMethod]
    public void On_RequestPlaceBomb(VRCPlayerApi requestingPlayer, int gameId, int seat, string boxId)
    {
        Debug.Log("On_RequestPlaceBomb called (prechecks)");
        if (requestingPlayer == null) { Debug.LogError("On_RequestPlaceBomb: Player not found"); return; }
        if (this.m_GameId != gameId) { Debug.LogError("On_RequestPlaceBomb: GameId mismatch"); return; }
        if (!IsLocalPlayerMaster()) { Debug.LogError("On_RequestPlaceBomb: Only GameMaster can auth this"); return; }
        if (!IsPlayerInGame(requestingPlayer)) { Debug.LogError("On_RequestPlaceBomb: Player not in game"); return; }
        if (!IsPlayerInSeat(requestingPlayer, seat)) { Debug.LogError("On_RequestPlaceBomb: Player not in seat"); return; }
        if (!IsPlayersTurn(requestingPlayer.playerId)) { Debug.LogError("On_RequestPlaceBomb: Not player's turn"); return; }
        if (this.m_GameStatus != GameStatus.InProgress) { Debug.LogError("On_RequestPlaceBomb: Game not in progress"); return; }
        if (this.m_PlayerWithBomb != this.m_SeatTurn) { Debug.LogError("On_RequestPlaceBomb: Player doesn't have bomb"); return; }
        if (this.m_Bomb == null) { Debug.LogError("On_RequestPlaceBomb: Bomb not found"); return; }

        Debug.Log("On_RequestPlaceBomb called (postchecks)");

        SendMethodNetworked(
            nameof(this.On_RequestPlaceBomb_Success),
            SyncTarget.All,
            new DataToken(Networking.LocalPlayer),
            new DataToken(requestingPlayer),
            new DataToken(this.m_GameId),
            new DataToken(seat),
            new DataToken(boxId)
        );
    }

    [NetworkedMethod]
    public void On_RequestPlaceBomb_Success(VRCPlayerApi master, VRCPlayerApi requestingPlayer, int gameId, int seat, string boxId)
    {
        Debug.Log("On_RequestPlaceBomb_Success called");
        if (requestingPlayer == null) return;
        if (master == null) return;
        if (this.m_GameId != gameId) return;
        if (!IsPlayerMaster(master)) return;
        if (!IsPlayerInGame(requestingPlayer)) return;
        if (!IsPlayerInSeat(requestingPlayer, seat)) return;
        if (!IsPlayersTurn(requestingPlayer.playerId)) return;
        if (this.m_GameStatus != GameStatus.InProgress) return;
        if (this.m_PlayerWithBomb != this.m_SeatTurn) return;
        if (this.m_Bomb == null) { Debug.LogError("On_RequestPlaceBomb: Bomb not found"); return; }

        Debug.Log("putting bmb on box");

        int i = 0;
        //each(Box box in this.m_Boxes)
        for(int bombIndex = 0; bombIndex < this.m_Boxes.Length; bombIndex++)
        {
            Box box = this.m_Boxes[bombIndex];
            if(box == null) continue;
            if(box.m_CanvasSeatId == -1) { Debug.LogError("Game.cs: On_RequestPlaceBomb_Success: Box canvas seat id is -1...?"); continue; }
            if(box.m_ID == boxId)
            {
                GameObject bomb = UnityEngine.Object.Instantiate(this.m_BombPrefab);
                if (bomb == null) { Debug.LogError("Game.cs: On_RequestPlaceBomb_Success: Bomb failed to instantiate"); return; }
                
                Bomb bombComponent = bomb.GetComponent<Bomb>();
                if (bombComponent == null) { Debug.LogError("Game.cs: On_RequestPlaceBomb_Success: Bomb component not found"); return; }

                VRCPlayerApi player = this.m_Players[seat];
                if (player == null) { Debug.LogError("Game.cs: On_RequestPlaceBomb_Success: Player not found"); return; }

                Debug.Log($"Game.cs: On_RequestPlaceBomb_Success: Player {requestingPlayer.displayName} placed bomb on box {box.m_ID}/{boxId} at seat {seat}/{box.m_CanvasSeatId}");
                bombComponent.PutBombOnBox(this.m_Boxes[bombIndex]);
                bombComponent.BeginExplosion();

                this.m_BombsOnBoard[i++] = bombComponent;
            }
        }

        this.m_Bomb.gameObject.SetActive(false);
        UnityEngine.Object.Destroy(this.m_Bomb.gameObject);
        this.m_Bomb = null;
        this.m_PlayerWithBomb = -1;
        this.m_BombsPlaced = true;

        if(IsLocalPlayerMaster() && !this.m_BombTimerStarted)
        {
            this.m_BombTimerStarted = true;
            this.m_BombTimerExpired = false;
            SendCustomEventDelayedSeconds(nameof(this.On_BombTimerExpired), 3);
        }
    }

    public bool m_BombsPlaced = false;
    public bool m_BombTimerStarted = false;
    public bool m_BombTimerExpired = false;

    public void On_BombTimerExpired()
    {
        this.m_BombTimerStarted = false;
        this.m_BombTimerExpired = true;

        if(this.m_BombsPlaced)
        {
            SendMethodNetworked(
                nameof(this.On_BombsExplode),
                SyncTarget.All,
                new DataToken(Networking.LocalPlayer),
                new DataToken(this.m_GameId)
            );
        }
    }

    [NetworkedMethod]
    public void On_BombsExplode(VRCPlayerApi master, int gameId)
    {
        if (master == null) return;
        if (this.m_GameId != gameId) return;
        if (!IsPlayerMaster(master)) return;
        if (this.m_GameStatus != GameStatus.InProgress) return;
        if (!this.m_BombsPlaced) return;

        Debug.Log("On_BombsExplode called");

        foreach(Bomb bomb in this.m_BombsOnBoard)
        {
            if(bomb == null) continue;
            bomb.Explode();
        }

        //       also, make the bomb placable on dots as well as boxes
        this.m_BombsOnBoard = new Bomb[2];
        this.m_BombsPlaced = false;

        if(IsLocalPlayerMaster())
        {
            if(this.IsGameEnd())
            {
                Notify_GameEnd();
                return;
            }
        }
    }

    public bool m_IsRequestingDotSelect = false;
    public bool m_IsRequestingDotSelectTimerStarted = false;
    public bool m_IsRequestingDotSelectTimerExpired = false;
    public Dot m_PreselectedDot = null;
    public void Request_DotSelect(Dot dot)
    {
        if(!this.IsPlayersTurn(Networking.LocalPlayer.playerId)) { Debug.LogError($"Game.cs Request_DotSelect: Not LocalPlayers' turn - turn={this.m_SeatTurn} seat={this.m_Seat}"); return; }
        if(this.m_GameStatus != GameStatus.InProgress) { Debug.LogError($"Game.cs Request_DotSelect: Game not in progress (state={this.m_GameStatus})"); return; }
        
        if(!this.BothLocalPlayers())
        {
            if(!this.CanSelectDot(this.m_Seat, dot)) { Debug.LogError($"Game.cs Request_DotSelect: Can't select dot"); return; }
            if(this.m_SeatTurn != this.m_Seat) { Debug.LogError($"Game.cs Request_DotSelect: Not player's turn"); return; }
            if(dot.m_SeatId != this.m_Seat) { Debug.LogError($"Game.cs Request_DotSelect: Dot not in player's seat"); return; }
        }

        GameCanvas gameCanvas = this.GetGameCanvasBySeatId(this.m_Seat);
        if (gameCanvas == null) { Debug.LogError("Game.cs: On_RequestDotSelectTimerExpired: GameCanvas not found"); return; }

        if(m_IsRequestingDotSelect && this.m_PreselectedDot == null && gameCanvas.m_SelectedDot_A == null && !m_IsRequestingDotSelectTimerStarted)
        {
            this.m_PreselectedDot = dot;
            dot.Highlight(this.SeatColor(this.m_Seat));
            return;
        }
        if(this.m_PreselectedDot != null)
        {
            return;
        }

        dot.Highlight(this.SeatColor(this.m_Seat));

        m_IsRequestingDotSelect = true;
        m_IsRequestingDotSelectTimerStarted = true;
        m_IsRequestingDotSelectTimerExpired = false;
        SendCustomEventDelayedSeconds(nameof(this.On_RequestDotSelectTimerExpired), 5);

        Debug.Log($"Request_DotSelect: {dot.m_GridPosition.ToString()} seat: {this.m_Seat} dotSeat: {dot.m_SeatId}");
        SendMethodNetworked(
            nameof(this.On_RequestDotSelect),
            SyncTarget.All,
            new DataToken(Networking.LocalPlayer),
            new DataToken(this.m_GameId),
            new DataToken(dot.m_GridPosition),
            new DataToken(this.m_Seat)
        );
    }

    public void On_RequestDotSelectTimerExpired()
    {
        m_IsRequestingDotSelect = false;
        m_IsRequestingDotSelectTimerStarted = false;
        m_IsRequestingDotSelectTimerExpired = true;
        
        if(this.m_PreselectedDot != null)
        {
            GameCanvas gameCanvas = this.GetGameCanvasBySeatId(this.m_Seat);
            if (gameCanvas == null) { Debug.LogError("Game.cs: On_RequestDotSelectTimerExpired: GameCanvas not found"); return; }

            if(gameCanvas.m_SelectedDot_A != null && gameCanvas.m_SelectedDot_B == null)
            {
                Dot dotCopy = this.m_PreselectedDot;
                this.m_PreselectedDot = null;
                Request_DotSelect(dotCopy);
            } 
            else
            {
                this.m_PreselectedDot.Unhighlight();
                this.m_PreselectedDot = null;
            }
        }
    }

    [NetworkedMethod]
    public void On_RequestDotSelect(VRCPlayerApi requestingPlayer, int gameId, Vector2Int gridPosition, int seatid)
    {
        Debug.Log($"On_RequestDotSelect: name: {requestingPlayer.displayName} gameId: {gameId} gridPosition: {gridPosition} seatid: {seatid}");
        if (requestingPlayer == null) { Debug.LogError("On_RequestDotSelect: Player not found"); return; }
        if (!IsLocalPlayerMaster()) return;
        if (this.m_GameId != gameId) return;
        if (!IsPlayerInGame(requestingPlayer)) { Debug.LogError("On_RequestDotSelect: Player not in game"); return; }
        if (!IsPlayerInSeat(requestingPlayer, seatid)) { Debug.LogError("On_RequestDotSelect: Player not in seat"); return; }
        else { Debug.Log($"On_RequestDotSelect: {requestingPlayer.playerId} is in seat {this.SeatForPlayer(requestingPlayer)} and requesting dotselect for seat {seatid}"); }
        if (!IsPlayersTurn(requestingPlayer.playerId)) { Debug.LogError("On_RequestDotSelect: Not player's turn"); return; }
        if(this.m_GameStatus != GameStatus.InProgress) { Debug.LogError("On_RequestDotSelect: Game not in progress"); return; }

        GameCanvas playerGameCanvas = this.GetGameCanvasBySeatId(seatid);
        if (playerGameCanvas == null)
        {
            Debug.LogError("On_RequestDotSelect: GameCanvas not found");
            return;
        }

        Dot dot = playerGameCanvas.GetDotAtPosition(gridPosition);
        if (dot == null)
        {
            Debug.LogError("On_RequestDotSelect: Dot not found");
            return;
        }

        if (CanSelectDot(seatid, dot))
        {
            SendMethodNetworked(
                nameof(this.On_RequestDotSelect_Success),
                SyncTarget.All,
                new DataToken(requestingPlayer),
                new DataToken(gameId),
                new DataToken(dot.m_GridPosition),
                new DataToken(dot.m_SeatId)
            );
        }
        else
        {
            SendMethodNetworked(
                nameof(this.On_RequestDotSelect_Failed),
                SyncTarget.All,
                new DataToken(requestingPlayer),
                new DataToken(gameId),
                new DataToken(dot.m_GridPosition),
                new DataToken(dot.m_SeatId)
            );
        }
    }

    [NetworkedMethod]
    public void On_RequestDotSelect_Success(VRCPlayerApi requestingPlayer, int gameId, Vector2Int gridPosition, int seatid)
    {
        Debug.Log($"On_RequestDotSelect_Success: name: {requestingPlayer.displayName} gameId: {gameId} gridPosition: {gridPosition} seatid: {seatid}");
        if (requestingPlayer == null) return;
        if (this.m_GameId != gameId) return;
        if (!IsPlayerInGame(requestingPlayer)) { Debug.LogError("On_RequestDotSelect_Success: Player not in game"); return; }
        if (!IsPlayerInSeat(requestingPlayer, seatid)) { Debug.LogError("On_RequestDotSelect_Success: Player not in seat"); return; }
        if (!IsPlayersTurn(requestingPlayer.playerId)) { Debug.LogError("On_RequestDotSelect_Success: Not player's turn"); return; }
        if(this.m_GameStatus != GameStatus.InProgress) { Debug.LogError("On_RequestDotSelect_Success: Game not in progress"); return; }

        if(requestingPlayer.playerId == Networking.LocalPlayer.playerId)
        {
            m_IsRequestingDotSelect = false;
            m_IsRequestingDotSelectTimerStarted = false;
            m_IsRequestingDotSelectTimerExpired = false;
        }

        GameCanvas playerGameCanvas = this.GetGameCanvasBySeatId(seatid);
        if (playerGameCanvas == null)
        {
            Debug.LogError("On_RequestDotSelect_Success: GameCanvas not found");
            return;
        }

        Dot dot = playerGameCanvas.GetDotAtPosition(gridPosition);
        if (dot == null)
        {
            Debug.LogError("On_RequestDotSelect_Success: Dot not found");
            return;
        }

        int resultA = playerGameCanvas.On_DotSelected(this, dot, this.SeatColor(seatid));
        dot.Highlight(this.SeatColor(seatid));

        GameCanvas otherGameCanvas = this.GetOtherGameCanvasBySeatId(seatid);
        if (otherGameCanvas == null)
        {
            Debug.LogError("On_RequestDotSelect_Success: Other GameCanvas not found");
            return;
        }

        Dot otherDot = otherGameCanvas.GetDotAtPosition(gridPosition);
        if (otherDot == null)
        {
            Debug.LogError("On_RequestDotSelect_Success: Other Dot not found");
            return;
        }

        int resultB = otherGameCanvas.On_DotSelected(this, otherDot,this.SeatColor(seatid));
        otherDot.Highlight(this.SeatColor(seatid));

        if (resultA == resultB)
        {
            if(resultA < 0 || resultA > 0)
            {
                playerGameCanvas.UnhighlightAllDots();
                otherGameCanvas.UnhighlightAllDots();
            }

            if(this.IsGameEnd())
            {
                Notify_GameEnd();
                return;
            }

            // dots formed a square (on both canvases) so the player gets an extra turn
            if (resultA == 2)
            {
                this.m_Scores[seatid]++;

                Notify_PlayerExtraTurn();
                return;
            }
            // dots formed a link (on both canvases) so its the end of a players turn
            if (resultA == 1)
            {
                Notify_PlayerTurnChange();
                return;
              }

            if(requestingPlayer.playerId == Networking.LocalPlayer.playerId)
            {
                if(this.m_PreselectedDot != null)
                {
                    Dot dotCopy = this.m_PreselectedDot;
                    this.m_PreselectedDot = null;
                    Request_DotSelect(dotCopy);
                }
            }

        }
        else
        {
            // error, result should be same on both canvases
            Debug.LogError("On_RequestDotSelect_Success: resultA != resultB");
        }
    }

    public bool IsGameEnd()
    {
        GameCanvas gameCanvas0 = this.GetGameCanvasBySeatId(0);
        if (gameCanvas0 == null) { Debug.LogError("Game.cs: IsGameEnd: GameCanvas not found"); return false; }

        bool result = gameCanvas0.RemainingDotsUnlinkable();
        Debug.Log("Game.cs: IsGameEnd: " + result);

        return result; 
    }

    public void Notify_PlayerTurnChange()
    {
        if (IsLocalPlayerMaster())
        {
            if(this.IsGameEnd())
            {
                Notify_GameEnd();
                return;
            }

            if(this.m_HasBonusTurn)
            {
                this.m_HasBonusTurn = false;
                Notify_PlayerExtraTurn();

                return;
            }

            this.NextTurn();
        
            SendMethodNetworked(
                nameof(this.On_PlayerTurnChange),
                SyncTarget.All,
                new DataToken(Networking.LocalPlayer),
                new DataToken(this.m_GameId),
                new DataToken(this.m_SeatTurn)
            );
        }
    }

    public void Notify_PlayerExtraTurn()
    {
        if (IsLocalPlayerMaster())
        {
            SendMethodNetworked(
                nameof(this.On_PlayerExtraTurn),
                SyncTarget.All,
                new DataToken(Networking.LocalPlayer),
                new DataToken(this.m_GameId),
                new DataToken(this.m_SeatTurn)
            );
        }
    }

    public void Notify_GameEnd()
    {
        if (IsLocalPlayerMaster())
        {
            SendMethodNetworked(
                nameof(this.On_GameEnd),
                SyncTarget.All,
                new DataToken(Networking.LocalPlayer),
                new DataToken(this.m_GameId)
            );
        }
    }

    [NetworkedMethod]
    public void On_GameEnd(VRCPlayerApi requestingPlayer, int gameId)
    {
        if (this.m_GameId != gameId) return;
        if (requestingPlayer == null) return;
        if (!IsPlayerMaster(requestingPlayer)) return;

        Debug.Log("On_GameEnd called");
        this.m_GameStatus = GameStatus.Finished;

        Player player0 = this.GetPlayerTextObjectForSeat(0);
        if (player0 == null) { Debug.LogError("Game.cs: On_GameEnd: Player0 not found"); return; }
        Player player1 = this.GetPlayerTextObjectForSeat(1);
        if (player1 == null) { Debug.LogError("Game.cs: On_GameEnd: Player1 not found"); return; }

        int winner = 0;
        if (this.m_Scores[1] > this.m_Scores[0])
        {
            winner = 1;
        } else if (this.m_Scores[0] > this.m_Scores[1])
        {
            winner = 0;
        } else
        {
            winner = -1;
        }

        if (winner == 0)
        {
            player0.SetCanvasText("Winner!");
            player1.SetCanvasText("Loser!");
        }
        else if (winner == 1)
        {
            player0.SetCanvasText("Loser!");
            player1.SetCanvasText("Winner!");
        } else
        {
            player0.SetCanvasText("Draw!");
            player1.SetCanvasText("Draw!");
        }
    }

    [NetworkedMethod]
    public void On_PlayerTurnChange(VRCPlayerApi requestingPlayer, int gameId, int seat)
    {
        if (this.m_GameId != gameId) { Debug.LogError($"Game.cs: On_PlayerTurnChange: gameId mismatch: {this.m_GameId} != {gameId}"); return; }
        if (requestingPlayer == null) { Debug.LogError("Game.cs: On_PlayerTurnChange: requestingPlayer is null"); return; }
        if(!IsPlayerMaster(requestingPlayer)) { Debug.LogError("Game.cs: On_PlayerTurnChange: requestingPlayer is not master"); return; }

        Debug.Log($"On_PlayerTurnChange: {this.m_SeatTurn} -> {seat}");
        this.m_SeatTurn = seat;
    }

    [NetworkedMethod]
    public void On_PlayerExtraTurn(VRCPlayerApi requestingPlayer, int gameId, int seat)
    {
        if (this.m_GameId != gameId) return;
        if (requestingPlayer == null) return;
        if(!IsPlayerMaster(requestingPlayer)) return;

        Debug.Log($"On_PlayerExtraTurn: {this.m_SeatTurn} -> {seat}");
        this.m_SeatTurn = seat;
    }

    [NetworkedMethod]
    public void On_RequestDotSelect_Failed(VRCPlayerApi requestingPlayer, int gameId, Vector2Int gridPosition, int seatid)
    {
        Debug.Log($"On_RequestDotSelect_Failed: name: {requestingPlayer.displayName} gameId: {gameId} gridPosition: {gridPosition} seatid: {seatid}");
        
        if(requestingPlayer.playerId == Networking.LocalPlayer.playerId)
        {
            m_IsRequestingDotSelect = false;
            m_IsRequestingDotSelectTimerStarted = false;
            m_IsRequestingDotSelectTimerExpired = false;

            GameCanvas gameCanvas = this.GetGameCanvasForPlayer(requestingPlayer);
            if (gameCanvas == null) { Debug.LogError("Game.cs: On_RequestDotSelect_Failed: GameCanvas not found"); return; }
            gameCanvas.UnhighlightAllDots();
        }
    }

    public VRCPlayerApi GetPlayerByName(string name)
    {
        foreach (VRCPlayerApi player in this.m_Players)
        {
            if (player != null && player.displayName == name)
            {
                return player;
            }
        }

        return null;
    }


    // Game
    //    - GameInterface
    //        - Canvas
    //            - Player_Name_Text
    //        - GameCanvas
    //            - Dot
    //    - GameInterface
    //        - Canvas
    //            - Player_Name_Text
    //        - GameCanvas
    //            - Dot
    public GameCanvas GetGameCanvasForPlayer(VRCPlayerApi player)
    {
        // Get all children of the Game object
        foreach (Transform child in transform)
        {
            // Check if the child is a GameInterface
            if (child.name.Contains("GameInterface"))
            {
                // Get the Player_Name_Text object in the Canvas
                Player playerText = child.transform
                    .Find("Canvas/Player_Name_Text")
                    .GetComponent<Player>();

                // Check if the player's name matches m_CurrentPlayerName
                if (playerText.m_CurrentPlayerId == player.playerId)
                {
                    // If it matches, return the GameCanvas in this hierarchy
                    return child.transform
                        .Find("GameCanvas")
                        .GetComponent<GameCanvas>();
                }
            }
        }

        // If no matching GameCanvas is found, return null
        return null;
    }

    public GameCanvas GetGameCanvasBySeatId(int seatid)
    {
        // Get all children of the Game object
        foreach (Transform child in transform)
        {
            // Check if the child is a GameInterface
            if (child.name.Contains("GameInterface"))
            {
                foreach (Transform subchild in child)
                {
                    if (subchild.name.Contains("GameCanvas"))
                    {
                        GameCanvas gameCanvas = subchild.GetComponent<GameCanvas>();
                        if (gameCanvas.m_SeatId == seatid)
                        {
                            return gameCanvas;
                        }
                    }
                }
            }
        }

        // If no matching GameCanvas is found, return null
        return null;
    }

    public GameCanvas GetOtherGameCanvasBySeatId(int seatid)
    {
        // Get all children of the Game object
        foreach (Transform child in transform)
        {
            // Check if the child is a GameInterface
            if (child.name.Contains("GameInterface"))
            {
                foreach (Transform subchild in child)
                {
                    if (subchild.name.Contains("GameCanvas"))
                    {
                        GameCanvas gameCanvas = subchild.GetComponent<GameCanvas>();
                        if (gameCanvas.m_SeatId != seatid)
                        {
                            return gameCanvas;
                        }
                    }
                }
            }
        }

        // If no matching GameCanvas is found, return null
        return null;
    }

    public GameCanvas[] GetAllGameCanvases()
    {
        // First, count the number of GameCanvas components
        int count = 0;
        foreach (Transform child in transform)
        {
            if (child.name.Contains("GameInterface"))
            {
                foreach (Transform subchild in child)
                {
                    if (subchild.name.Contains("GameCanvas"))
                    {
                        count++;
                    }
                }
            }
        }

        // Then, create an array of the correct size and fill it with the GameCanvas components
        GameCanvas[] gameCanvases = new GameCanvas[count];
        int index = 0;
        foreach (Transform child in transform)
        {
            if (child.name.Contains("GameInterface"))
            {
                foreach (Transform subchild in child)
                {
                    if (subchild.name.Contains("GameCanvas"))
                    {
                        GameCanvas gameCanvas = subchild.GetComponent<GameCanvas>();
                        gameCanvases[index] = gameCanvas;
                        index++;
                    }
                }
            }
        }

        return gameCanvases;
    }

    public bool CanSelectDot(int seat, Dot dot)
    {
        if(dot.m_SeatId != seat)
        {
            return false;
        }

        return true;
    }

    public bool IsLocalPlayerMaster()
    {
        if (this.m_GameMaster == null) return false;
        if (VRC.SDKBase.Networking.LocalPlayer == null) return false;

        return this.m_GameMaster.playerId == VRC.SDKBase.Networking.LocalPlayer.playerId;
    }

    public bool IsPlayerMaster(VRCPlayerApi player)
    {
        if (this.m_GameMaster == null) return false;
        if (player == null) return false;

        return this.m_GameMaster.playerId == player.playerId;
    }

    public bool IsPlayerInGame(VRCPlayerApi player)
    {
        foreach (VRCPlayerApi p in this.m_Players)
        {
            if (p != null && p.playerId == player.playerId)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsPlayerInSeat(VRCPlayerApi player, int seat)
    {
        if (seat != 0 && seat != 1) return false;
        if (this.m_Players[seat] == null) return false;

        return this.m_Players[seat].playerId == player.playerId;
    }

    public bool m_RequestGameResetTimerStarted;
    public bool m_RequestGameResetTimerExpired;
    public bool m_RequestGameResetRejected;
    public void Request_ResetGame()
    {
        Debug.Log("Request_ResetGame called");
        if (this.IsPlayerInGame(Networking.LocalPlayer))
        {
            Debug.Log("Player is in game. Sending On_RequestResetGame_Success");
            SendMethodNetworked(
                nameof(this.On_RequestResetGame_Success),
                SyncTarget.All,
                new DataToken(Networking.LocalPlayer),
                new DataToken(this.m_GameId)
            );
        }
        else
        {
            if (this.m_RequestGameResetTimerStarted) return;

            Debug.Log("Starting game reset timer");
            this.m_RequestGameResetTimerStarted = true;
            this.m_RequestGameResetTimerExpired = false;
            this.m_RequestGameResetRejected = false;

            SendCustomEventDelayedSeconds(nameof(this.RequestResetGameTimerExpired), 5);

            SendMethodNetworked(
                nameof(this.On_RequestResetGame),
                SyncTarget.All,
                new DataToken(Networking.LocalPlayer),
                new DataToken(this.m_GameId)
            );
        }
    }

    public void RequestResetGameTimerExpired()
    {
        Debug.Log("RequestResetGameTimerExpired called");
        if (this.m_RequestGameResetTimerStarted && !this.m_RequestGameResetTimerExpired)
        {
            Debug.Log("Game reset timer expired");
            this.m_RequestGameResetTimerExpired = true;
            this.m_RequestGameResetTimerStarted = false;
            if (this.m_RequestGameResetRejected == true) return;

            SendMethodNetworked(
                nameof(this.On_RequestResetGame_Success),
                SyncTarget.All,
                new DataToken(Networking.LocalPlayer),
                new DataToken(this.m_GameId)
            );
        }
    }

    [NetworkedMethod]
    public void On_RequestResetGame(VRCPlayerApi requestingPlayer, int gameId)
    {
        Debug.Log("On_RequestResetGame called");
        if (this.m_GameId != gameId) return;
        if (IsPlayerInGame(Networking.LocalPlayer))
        {
            Debug.Log("Player is in game. Sending On_RequestResetGame_Rejected");
            SendMethodNetworked(
                nameof(this.On_RequestResetGame_Rejected),
                SyncTarget.All,
                new DataToken(requestingPlayer),
                new DataToken(gameId)
            );
        }
    }

    [NetworkedMethod]
    public void On_RequestResetGame_Rejected(VRCPlayerApi requestingPlayer, int gameId)
    {
        Debug.Log("On_RequestResetGame_Rejected called");
        if (this.m_GameId != gameId) return;
        if (requestingPlayer.playerId != Networking.LocalPlayer.playerId) return;

        if (this.m_RequestGameResetTimerStarted)
        {
            Debug.Log("Game reset request rejected");
            this.m_RequestGameResetRejected = true;
        }
    }

    public void ResetScores()
    {
        if (this.m_Scores == null) return;
        if (this.m_Scores.Length != 2) return;

        this.m_Scores[0] = 0;
        this.m_Scores[1] = 0;
    }

    public void ResetScrewDriverUsageCount()
    {
        if (this.m_ScrewDriverUsageCount == null) return;
        if (this.m_ScrewDriverUsageCount.Length != 2) return;

        this.m_ScrewDriverUsageCount[0] = 0;
        this.m_ScrewDriverUsageCount[1] = 0;
    }

    [NetworkedMethod]
    public void On_RequestResetGame_Success(VRCPlayerApi requestingPlayer, int gameId)
    {
        Debug.Log($"On_RequestResetGame_Success: name: {requestingPlayer.displayName} gameId: {gameId}");
        if (requestingPlayer == null) return;
        if (this.m_GameId != gameId) return;

        GameCanvas gameCanvas0 = this.GetGameCanvasBySeatId(0);
        GameCanvas gameCanvas1 = this.GetGameCanvasBySeatId(1);

        if (gameCanvas0 == null || gameCanvas1 == null)
        {
            Debug.LogError("On_RequestResetGame_Success: GameCanvas not found");
            return;
        }

        Debug.Log("On_RequestResetGame_Success: Resetting game canvases");

        gameCanvas0.ClearLines();
        gameCanvas1.ClearLines();

        Debug.Log("On_RequestResetGame_Success: Resetting game canvases");

        gameCanvas0.ResetSelections();
        gameCanvas1.ResetSelections();

        gameCanvas0.ResetEverything();
        gameCanvas1.ResetEverything();

        Player playerTextObject0 = GetPlayerTextObjectForSeat(0);
        Player playerTextObject1 = GetPlayerTextObjectForSeat(1);

        if (playerTextObject0 == null || playerTextObject1 == null)
        {
            Debug.LogError("On_RequestResetGame_Success: PlayerTextObject not found");
            return;
        }

        Debug.Log("On_RequestResetGame_Success: Resetting player text objects");
        playerTextObject0.Reset();
        playerTextObject1.Reset();

        StartButton startButton0 = this.FindStartButtonForSeat(0);
        if(startButton0 == null) { Debug.LogError("Join: StartButton not found"); return; }

        startButton0.gameObject.SetActive(false);

        StartButton startButton1 = this.FindStartButtonForSeat(1);
        if(startButton1 == null) { Debug.LogError("Join: StartButton not found"); return; }

        startButton1.gameObject.SetActive(false);
        
        if(this.m_Bomb != null && this.m_Bomb.gameObject != null)
        {
            this.m_Bomb.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(this.m_Bomb.gameObject);
            this.m_Bomb = null;
            this.m_PlayerWithBomb = -1;
        }

        this.m_Players[0] = null;
        this.m_Players[1] = null;

        this.m_PreselectedDot = null;
        this.m_IsRequestingDotSelect = false;
        this.m_IsRequestingDotSelectTimerStarted = false;
        this.m_IsRequestingDotSelectTimerExpired = false;

        this.m_RequestGameResetTimerStarted = false;
        this.m_RequestGameResetTimerExpired = false;
        this.m_RequestGameResetRejected = false;

        this.m_IsRequestingGameStart = false;
        this.m_IsRequestingGameStartTimerStarted = false;
        this.m_IsRequestingGameStartTimerExpired = false;

        this.m_GameMaster = null;
        this.m_Players = new VRCPlayerApi[2];
        this.m_SeatTurn = 0;
        this.m_HasBonusTurn = false;
        this.m_GameStatus = GameStatus.Waiting;

        this.ResetScores();
        this.ResetScrewDriverUsageCount();
        this.DeleteAllBoxes();
    }

    public Player GetPlayerTextObjectForSeat(int seat)
    {
        foreach (Transform child in transform)
        {
            if (child.name.Contains("GameInterface"))
            {
                Player playerTextObject = child.transform
                    .Find("Canvas/Player_Name_Text")
                    .GetComponent<Player>();

                if (playerTextObject.m_GameSeatIdInt == seat)
                {
                    return playerTextObject;
                }
            }
        }

        return null;
    }

    public StartButton FindStartButtonForSeat(int seat)
    {
        Player playerTextObject = GetPlayerTextObjectForSeat(seat);
        if (playerTextObject == null) return null;

        return playerTextObject.FindStartButton();
    }

    public bool IsPlayerConnected(VRCPlayerApi player)
    {
        VRCPlayerApi[] allPlayers = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
        allPlayers = VRCPlayerApi.GetPlayers(allPlayers);
        foreach (VRCPlayerApi p in allPlayers)
        {
            if (p.playerId == player.playerId)
            {
                return true;
            }
        }
        return false;
    }

    public VRCPlayerApi PlayerInGameById(int playerId)
    {
        foreach (VRCPlayerApi player in this.m_Players)
        {
            if (player != null && player.playerId == playerId)
            {
                return player;
            }
        }

        return null;
    }

    public VRCPlayerApi OtherPlayer(int playerId)
    {
        foreach (VRCPlayerApi player in this.m_Players)
        {
            if (player != null && player.playerId != playerId)
            {
                return player;
            }
        }

        return null;
    }

    public int SeatForPlayer(VRCPlayerApi player)
    {
        for (int i = 0; i < this.m_Players.Length; i++)
        {
            if (this.m_Players[i] != null && this.m_Players[i].playerId == player.playerId)
            {
                return i;
            }
        }

        return -1;
    }

    public void NextTurn()
    {
        this.m_SeatTurn = (++this.m_SeatTurn) % 2;
        Debug.Log($"NextTurn: {this.m_SeatTurn}");
    }

    public bool IsPlayersTurn(int playerId)
    {
        if(this.m_Players[this.m_SeatTurn] == null) return false;
        
        return this.m_Players[this.m_SeatTurn].playerId == playerId;
    }

    public Color SeatColor(int seat)
    {
        if (seat == 0)
        {
            return Color.blue;
        }
        else if (seat == 1)
        {
            return Color.red;
        }
        else
        {
            return Color.white;
        }
    }

    public bool BothLocalPlayers()
    {
        foreach (VRCPlayerApi player in this.m_Players)
        {
            if (player == null) return false;
            if (player.playerId != Networking.LocalPlayer.playerId) return false;
        }

        return true;
    }

    public void Request_ScrewDriverADot(Dot dot)
    {
        if(!this.IsPlayersTurn(Networking.LocalPlayer.playerId)) { Debug.LogError($"Game.cs Request_ScrewDriverADot: Not LocalPlayers' turn - turn={this.m_SeatTurn} seat={this.m_Seat}"); return; }
        if(this.m_GameStatus != GameStatus.InProgress) { Debug.LogError($"Game.cs Request_ScrewDriverADot: Game not in progress (state={this.m_GameStatus})"); return; }
        
        SendMethodNetworked(
            nameof(this.On_RequestScrewDriverADot),
            SyncTarget.All,
            new DataToken(Networking.LocalPlayer),
            new DataToken(this.m_GameId),
            new DataToken(dot.m_GridPosition),
            new DataToken(this.m_Seat)
        );
    }

    [NetworkedMethod]
    public void On_RequestScrewDriverADot(VRCPlayerApi requestingPlayer, int gameId, Vector2Int gridPosition, int seatid)
    {
        Debug.Log($"On_RequestScrewDriverADot: name: {requestingPlayer.displayName} gameId: {gameId} gridPosition: {gridPosition} seatid: {seatid}");
        if (requestingPlayer == null) { Debug.LogError("On_RequestScrewDriverADot: Player not found"); return; }
        if (!IsLocalPlayerMaster()) return;
        if (this.m_GameId != gameId) return;
        if (!IsPlayerInGame(requestingPlayer)) { Debug.LogError("On_RequestScrewDriverADot: Player not in game"); return; }
        if (!IsPlayerInSeat(requestingPlayer, seatid)) { Debug.LogError("On_RequestScrewDriverADot: Player not in seat"); return; }
        if (!IsPlayersTurn(requestingPlayer.playerId)) { Debug.LogError("On_RequestScrewDriverADot: Not player's turn"); return; }
        if(this.m_GameStatus != GameStatus.InProgress) { Debug.LogError("On_RequestScrewDriverADot: Game not in progress"); return; }

        GameCanvas playerGameCanvas = this.GetGameCanvasBySeatId(seatid);
        if (playerGameCanvas == null)
        {
            Debug.LogError("On_RequestScrewDriverADot: GameCanvas not found");
            return;
        }

        Dot dot = playerGameCanvas.GetDotAtPosition(gridPosition);
        if (dot == null)
        {
            Debug.LogError("On_RequestScrewDriverADot: Dot not found");
            return;
        }

        if (CanSelectDot(seatid, dot) && this.m_ScrewDriverUsageCount[seatid] < this.m_MaximumScrewDriverUsage)
        {
            // random int between 1 and 3
            int powerup = UnityEngine.Random.Range(1, 100);

            SendMethodNetworked(
                nameof(this.On_RequestScrewDriverADot_Success),
                SyncTarget.All,
                new DataToken(requestingPlayer),
                new DataToken(gameId),
                new DataToken(dot.m_GridPosition),
                new DataToken(dot.m_SeatId),
                new DataToken(powerup)
            );
        }
        else
        {
            SendMethodNetworked(
                nameof(this.On_RequestScrewDriverADot_Failed),
                SyncTarget.All,
                new DataToken(requestingPlayer),
                new DataToken(gameId),
                new DataToken(dot.m_GridPosition),
                new DataToken(dot.m_SeatId)
            );
        }
    }

    [NetworkedMethod]
    public void On_RequestScrewDriverADot_Success(VRCPlayerApi requestingPlayer, int gameId, Vector2Int gridPosition, int seatid, int powerup)
    {
        Debug.Log($"On_RequestScrewDriverADot_Success: name: {requestingPlayer.displayName} gameId: {gameId} gridPosition: {gridPosition} seatid: {seatid}");
        if (requestingPlayer == null) return;
        if (this.m_GameId != gameId) return;
        if (!IsPlayerInGame(requestingPlayer)) { Debug.LogError("On_RequestScrewDriverADot_Success: Player not in game"); return; }
        if (!IsPlayerInSeat(requestingPlayer, seatid)) { Debug.LogError("On_RequestScrewDriverADot_Success: Player not in seat"); return; }
        if (!IsPlayersTurn(requestingPlayer.playerId)) { Debug.LogError("On_RequestScrewDriverADot_Success: Not player's turn"); return; }
        if(this.m_GameStatus != GameStatus.InProgress) { Debug.LogError("On_RequestScrewDriverADot_Success: Game not in progress"); return; }

       
        GameCanvas playerGameCanvas = this.GetGameCanvasBySeatId(seatid);
        if (playerGameCanvas == null)
        {
            Debug.LogError("On_RequestDotSelect_Success: GameCanvas not found");
            return;
        }

        Dot dot = playerGameCanvas.GetDotAtPosition(gridPosition);
        if (dot == null)
        {
            Debug.LogError("On_RequestDotSelect_Success: Dot not found");
            return;
        }

        GameCanvas otherGameCanvas = this.GetOtherGameCanvasBySeatId(seatid);
        if (otherGameCanvas == null)
        {
            Debug.LogError("On_RequestDotSelect_Success: Other GameCanvas not found");
            return;
        }

        Dot otherDot = otherGameCanvas.GetDotAtPosition(gridPosition);
        if (otherDot == null)
        {
            Debug.LogError("On_RequestDotSelect_Success: Other Dot not found");
            return;
        }

        dot.Hide();
        otherDot.Hide();
        this.m_ScrewDriverUsageCount[seatid]++;
        
        if (powerup >= 1 && powerup <= 5) // Chance: 5%
        {
            this.GivePlayerBomb(seatid);
            Debug.Log("On_RequestScrewDriverADot_Success: bomb");
        }
        else if (powerup >= 6 && powerup <= 25) // Chance: 20%
        {
            this.m_HasBonusTurn = true;
            Debug.Log("On_RequestScrewDriverADot_Success: bonus turn");
        }
        else if (powerup >= 26 && powerup <= 50) // Chance: 25%
        {
            this.Notify_PlayerTurnChange();
            Debug.Log("On_RequestScrewDriverADot_Success: skip turn");
            return;
        }
        else // Chance: 50%
        {
            Debug.LogError("On_RequestScrewDriverADot_Success: literally nothing");
        }

         if(IsLocalPlayerMaster())
        {
            if(this.IsGameEnd())
            {
                Notify_GameEnd();
                return;
            }
        }
    }

    [NetworkedMethod]
    public void On_RequestScrewDriverADot_Failed(VRCPlayerApi requestingPlayer, int gameId, Vector2Int gridPosition, int seatid)
    {
        Debug.Log($"On_RequestScrewDriverADot_Failed: name: {requestingPlayer.displayName} gameId: {gameId} gridPosition: {gridPosition} seatid: {seatid}");
    }

    public void GivePlayerBomb(int seat)
    {
        GameCanvas gameCanvas = this.GetGameCanvasBySeatId(seat);
        if (gameCanvas == null) { Debug.LogError("Game.cs: GivePlayerBomb: GameCanvas not found"); return; }

        GameObject bomb = UnityEngine.Object.Instantiate(this.m_BombPrefab);
        if (bomb == null) { Debug.LogError("Game.cs: GivePlayerBomb: Bomb failed to instantiate"); return; }

        Bomb bombComponent = bomb.GetComponent<Bomb>();
        if (bombComponent == null) { Debug.LogError("Game.cs: GivePlayerBomb: Bomb component not found"); return; }

        VRCPlayerApi player = this.m_Players[seat];
        if (player == null) { Debug.LogError("Game.cs: GivePlayerBomb: Player not found"); return; }

        bombComponent.Give(player, seat);
        
        this.m_PlayerWithBomb = seat;
        this.m_Bomb = bombComponent;
    }

    public void DeleteBoxesByIDs(string[] boxIds)
    {
        foreach (string boxId in boxIds)
        {
            foreach (Box box in this.m_Boxes)
            {
                if (box == null) continue;
                if (box.m_ID == boxId)
                {
                    // should setactive instead
                    box.Delete();
                }
            }
        }
    }

    public Box GetBoxByIDs(string id, int seat)
    {
        foreach (Box box in this.m_Boxes)
        {
            if (box == null) continue;
            if (box.m_ID == id && box.m_CanvasSeatId == seat)
            {
                return box;
            }
        }

        return null;
    }
}
