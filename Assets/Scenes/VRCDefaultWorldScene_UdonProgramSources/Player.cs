
using System;
using System.Collections.Generic;
using Midi;
using Miner28.UdonUtils.Network;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;

public class Player : NetworkInterface
{
    TextMeshProUGUI textCanvas;

    public string m_GameSeatId;
    public int m_GameSeatIdInt;
    public string m_GameId;
    public string m_CurrentPlayerName;
    public int m_CurrentPlayerId;
    public bool m_SeatOccupied;

    void Start()
    {
        this.textCanvas = this.GetComponent<TextMeshProUGUI>();

        string initialisationText = this.textCanvas.text;
        string[] parsed = this.ParseGameIDString(initialisationText);

        if (parsed.Length == 2)
        {
            this.m_GameId = parsed[0];
            this.m_GameSeatId = parsed[1];
            Game game = FindGameObject();
            if(game != null)
            {
                if (int.TryParse(this.m_GameId, out int gameid))
                {
                    game.m_GameId = gameid;
                }
                else
                {
                    Debug.LogError("Start: Failed to parse gameid");
                }
                
                if (int.TryParse(this.m_GameSeatId, out int seat))
                {
                    GameCanvas gameCanvas = this.GetGameCanvas();
                    if(gameCanvas != null)
                    {
                        this.m_GameSeatIdInt = seat;
                        gameCanvas.UpdateSeatId(seat);
                    }
                    else
                    {
                        Debug.LogError("Start: Failed to find game canvas");
                    }
                }
                else
                {
                    Debug.LogError("Start: Failed to parse seat");
                }
            }
            else
            {
                Debug.LogError("Start: Failed to find game gameobject");
            }
        } 
        else
        {
            Debug.LogError("Start: Failed to get id parts");
        }

        this.SetCanvasText("Seat Open");
    }

    public string[] ParseGameIDString(string data)
    {
        string[] parsed = {"", ""};

        string[] split = data.Split("_");
        if(split.Length == 2)
        {
            parsed[0] = (split[0]);
            parsed[1] = (split[1]);
        }
        else
        {
            Debug.LogError("ParseGameIDString: Failed to parse game id string data");
        }

        return parsed;
    }

    /// <summary>
    /// Any player tried to join the seat, either accept or reject the join
    /// </summary>
    [NetworkedMethod]
    public void On_SeatJoinRequest(VRCPlayerApi player, string seatId, string gameId)
    {
        Debug.Log("On_SeatJoinRequest");
        bool isThisSeat = this.m_GameId == gameId && this.m_GameSeatId == seatId;

        if(isThisSeat)
        {
            this.TryJoinSeat(player);
        }
    
    }

    /// <summary>
    /// Handle another player trying to join the seat
    /// </summary>
    public void TryJoinSeat(VRCPlayerApi requestingPlayer)
    {
        if(Networking.LocalPlayer.playerId == requestingPlayer.playerId)
        {
            this.m_SeatJoinTimerStarted = false;
            this.m_RequestGameMasterTimerStarted = false;
            this.SetCanvasText("Joining..." + $" {requestingPlayer.playerId.ToString()}");
        }

        if(this.m_SeatOccupied == true)
        {
            this.SeatJoinFailed(requestingPlayer);
        }
        else
        {
            this.SeatJoinSuccess(requestingPlayer);
        }
    }

    // A player tried to join the seat but this instance knows it's taken...
    // tell the player who tried to join its taken
    public void SeatJoinFailed(VRCPlayerApi requestingPlayer)
    {
        Debug.Log("SeatJoinFailed");
        SendMethodNetworked(
            nameof(this.On_SeatJoinRequestFailed),
            SyncTarget.All,
            new DataToken(requestingPlayer),
            new DataToken(this.m_CurrentPlayerName),
            new DataToken(this.m_GameSeatId),
            new DataToken(this.m_GameId)
        );
    }

    /// <summary>
    /// LocalPlayer tried to join the seat but another client thinks the seat is taken
    /// </summary>
    [NetworkedMethod]
    public void On_SeatJoinRequestFailed(VRCPlayerApi requestingPlayer, string occupyingPlayer, string seatId, string gameId)
    {
        Debug.Log("On_SeatJoinRequestFailed");

        // only need to deal with it once but all the other clients will spam this message
        if(this.m_SeatOccupied == false) return;

        if(Networking.LocalPlayer.playerId == requestingPlayer.playerId)
        {
            bool isThisSeat = this.m_GameId == gameId && this.m_GameSeatId == seatId;
            if(isThisSeat)
            {
                this.m_SeatOccupied = true;
                this.m_CurrentPlayerName = occupyingPlayer;
                this.m_CurrentPlayerId = requestingPlayer.playerId;
                this.SetCanvasText(this.m_CurrentPlayerName + $" {requestingPlayer.playerId.ToString()}");
            }
        }
    }


    public bool m_SeatJoinTimerStarted;
    public bool m_SeatJoinTimerExpired;

    /// <summary>
    /// A player successfully joined this seat
    /// </summary>
    public void SeatJoinSuccess(VRCPlayerApi requestingPlayer)
    {
        Debug.Log("SeatJoinSuccess");
        
        if(Networking.LocalPlayer.playerId == requestingPlayer.playerId)
        {
            if(!this.m_SeatJoinTimerStarted)
            {
                this.m_SeatJoinTimerExpired = false;
                this.m_SeatJoinTimerStarted = true;
                SendCustomEventDelayedSeconds("SeatJoinSuccessTimerExpired", 3);
            }
        }
        else
        {
            this.m_SeatOccupied = true;
            this.m_CurrentPlayerName = requestingPlayer.displayName;
            this.m_CurrentPlayerId = requestingPlayer.playerId;
            this.SetCanvasText(this.m_CurrentPlayerName + $" {requestingPlayer.playerId.ToString()}");
            JoinButton joinButton = this.FindJoinButtonObject();
            if(joinButton != null)
            {
                joinButton.gameObject.SetActive(false);
            }

            Game game = FindGameObject();
            if(game != null)
            {
                game.Join(requestingPlayer, this.m_GameSeatIdInt, false);
                GameCanvas gameCanvas = this.GetGameCanvas();
                if(gameCanvas != null)
                {
                    gameCanvas.UpdateSeatId(this.m_GameSeatIdInt);
                }
                else
                {
                    Debug.LogError("JoinGame: Failed to find game canvas");
                }
            }
        }

    }

    public void SeatJoinSuccessTimerExpired()
    {
        Debug.Log("Player.cs: SeatJoinSuccessTimerExpired");
        if(this.m_SeatOccupied) { Debug.LogError("Player.cs: SeatJoinSuccessTimerExpired: Seat is already occupied"); return; }
        if(this.m_SeatJoinTimerExpired) { Debug.LogError("Player.cs: SeatJoinSuccessTimerExpired: Timer already expired"); return; }
        if(!this.m_SeatJoinTimerStarted) { Debug.LogError("Player.cs: SeatJoinSuccessTimerExpired: Timer not started"); return; }

        this.m_SeatJoinTimerExpired = true;
        
        this.JoinGame(Networking.LocalPlayer);
    }

    /// <summary>
    /// LocalPlayer joins the game
    /// </summary>
    public void JoinGame(VRCPlayerApi player)
    {
        Debug.Log("Player.cs: JoinGame");
        Game game = FindGameObject();
        if(game != null)
        {
            if (int.TryParse(this.m_GameSeatId, out int seat))
            {
                Debug.Log("Player.cs: JoinGame: m_GameSeatId int conversion success");
                if(game.m_GameMaster == null)
                {
                    Debug.Log("Player.cs: JoinGame: GameMaster is null");
                    if(!this.m_RequestGameMasterTimerStarted)
                    {
                        Debug.Log("Player.cs: JoinGame: RequestGameMasterTimer starting");
                        this.m_RequestGameMasterTimerExpired = false;
                        this.m_RequestGameMasterTimerStarted = true;
                        this.m_RequestGameMasterFailed = false;
                        SendCustomEventDelayedSeconds(nameof(this.RequestGameMasterTimerExpired), 3);
                    }

                    Debug.Log("Player.cs: JoinGame: Requesting GameMaster");
                    SendMethodNetworked(
                        nameof(this.On_RequestGameMaster),
                        SyncTarget.All,
                        new DataToken(player),
                        new DataToken(this.m_GameSeatId),
                        new DataToken(this.m_GameId)
                    );
                }
                else
                {
                    Debug.Log("Player.cs: JoinGame: GameMaster is not null, joining game as pleb");
                    this.m_SeatOccupied = true;
                    this.m_CurrentPlayerName = player.displayName;
                    this.m_CurrentPlayerId = player.playerId;
                    this.SetCanvasText(this.m_CurrentPlayerName + $" {player.playerId.ToString()}");

                    game.Join(player, seat, false);
                    GameCanvas gameCanvas = this.GetGameCanvas();
                    if(gameCanvas != null)
                    {
                        gameCanvas.UpdateSeatId(this.m_GameSeatIdInt);
                    }
                    else
                    {
                        Debug.LogError("JoinGame: Failed to find game canvas");
                    }
                }
            }
            else
            {
                Debug.LogError($"JoinGame: m_GameSeatId int conversion failed ({this.m_GameSeatId})");
            }
        }
        else
        {
            Debug.LogError("JoinGame: Game GameObject not found under parent.");
        }
    }

    [NetworkedMethod]
    public void On_RequestGameMaster(VRCPlayerApi requestingPlayer, string seatId, string gameId)
    {
        Debug.Log($"Player.cs: On_RequestGameMaster Received from {requestingPlayer.displayName} {requestingPlayer.playerId} for seat {seatId} in game {gameId}");
        Game game = FindGameObject();
        if(game != null)
        {
            if(game.m_GameMaster != null)
            {
                Debug.Log("Player.cs: On_RequestGameMaster: GameMaster already exists, rejecting request");
                SendMethodNetworked(
                    nameof(this.On_RequestGameMasterFailed),
                    SyncTarget.All,
                    new DataToken(requestingPlayer),
                    new DataToken(this.m_GameSeatId),
                    new DataToken(this.m_GameId),
                    new DataToken(game.m_GameMaster)
                );
            }
        }
    }

    public bool m_RequestGameMasterTimerStarted;
    public bool m_RequestGameMasterTimerExpired;
    public bool m_RequestGameMasterFailed;
    
    [NetworkedMethod]
    public void On_RequestGameMasterFailed(VRCPlayerApi requestingPlayer, string seatId, string gameId, VRCPlayerApi gameMaster)
    {
        Debug.Log($"Player.cs: On_RequestGameMasterFailed Received from {requestingPlayer.displayName} {requestingPlayer.playerId} for seat {seatId} in game {gameId}");
        if(Networking.LocalPlayer.playerId == requestingPlayer.playerId && this.m_RequestGameMasterTimerStarted && !this.m_RequestGameMasterTimerExpired)
        {
            bool isThisSeat = this.m_GameId == gameId && this.m_GameSeatId == seatId;
            if(!isThisSeat) return;

            this.m_RequestGameMasterFailed = true;

            Game game = FindGameObject();
            if(game != null)
            {
                game.m_GameMaster = gameMaster;
            }
            else
            {
                Debug.LogError("On_RequestGameMasterFailed: Game GameObject not found under parent.");
            }
        }
    }

    public void RequestGameMasterTimerExpired()
    {
        Debug.Log("Player.cs: RequestGameMasterTimerExpired");
        if(this.m_RequestGameMasterTimerExpired) { Debug.LogError("Player.cs: RequestGameMasterTimerExpired: Timer already expired"); return;}
        if(!this.m_RequestGameMasterTimerStarted) { Debug.LogError("Player.cs: RequestGameMasterTimerExpired: Timer not started"); return; }

        this.m_RequestGameMasterTimerStarted = false;
        this.m_RequestGameMasterTimerExpired = true;

        Game game = FindGameObject();
        if (game != null)
        {
            if (int.TryParse(this.m_GameSeatId, out int seat))
            {
                Debug.Log($"Player.cs: RequestGameMasterTimerExpired: Joining game (master={!this.m_RequestGameMasterFailed})");
                this.m_SeatOccupied = true;
                this.m_CurrentPlayerName = Networking.LocalPlayer.displayName;
                this.m_CurrentPlayerId = Networking.LocalPlayer.playerId;
                this.SetCanvasText(this.m_CurrentPlayerName + $" {Networking.LocalPlayer.playerId.ToString()}");
                game.Join(Networking.LocalPlayer, seat, !this.m_RequestGameMasterFailed);

                GameCanvas gameCanvas = this.GetGameCanvas();
                if (gameCanvas != null)
                {
                    gameCanvas.UpdateSeatId(this.m_GameSeatIdInt);
                }
                else
                {
                    Debug.LogError("RequestGameMasterTimerExpired: Failed to find game canvas");
                }
                
                SendMethodNetworked(
                    nameof(this.On_InformGameJoin),
                    SyncTarget.All,
                    new DataToken(Networking.LocalPlayer),
                    new DataToken(this.m_GameSeatId),
                    new DataToken(this.m_GameId),
                    new DataToken(!this.m_RequestGameMasterFailed)
                );
            }
            else
            {
                Debug.LogError("RequestGameMasterTimerExpired: m_GameSeatId int conversion failed.");
            }
        }
        else
        {
            Debug.LogError("RequestGameMasterTimerExpired: Game GameObject not found under parent.");
        }
    }

    [NetworkedMethod]
    public void On_InformGameJoin(VRCPlayerApi requestingPlayer, string seatId, string gameId, bool master)
    {
        if(Networking.LocalPlayer.playerId == requestingPlayer.playerId) return;

        bool isThisSeat = this.m_GameId == gameId && this.m_GameSeatId == seatId;
        if(!isThisSeat) return;

        Game game = FindGameObject();
        if(game != null)
        {
            if (int.TryParse(this.m_GameSeatId, out int seat))
            {
                this.m_SeatOccupied = true;
                this.m_CurrentPlayerName = requestingPlayer.displayName;
                this.m_CurrentPlayerId = requestingPlayer.playerId;
                this.SetCanvasText(this.m_CurrentPlayerName + $" {requestingPlayer.playerId.ToString()}");
                game.Join(requestingPlayer, seat, master);
                GameCanvas gameCanvas = this.GetGameCanvas();
                if(gameCanvas != null)
                {
                    gameCanvas.UpdateSeatId(this.m_GameSeatIdInt);
                }
                else
                {
                    Debug.LogError("On_InformGameJoin: Failed to find game canvas");
                }
            }
            else
            {
                Debug.LogError("On_InformGameJoin: m_GameSeatId int conversion failed.");
            }
        }
        else
        {
            Debug.LogError("On_InformGameJoin: Game GameObject not found under parent.");
        }
    }

    private JoinButton FindJoinButtonObject()
    {
        GameObject currentObject = this.gameObject;
        Transform canvasTransform = currentObject.transform.parent;
        if (canvasTransform != null)
        {
            Transform gameTransform = canvasTransform.transform.parent;
            if (gameTransform != null)
            {
                Transform playerNameTextTransform = gameTransform.Find("Join Button");
                if (playerNameTextTransform != null)
                {
                    GameObject playerGameObject = playerNameTextTransform.gameObject;
                    Debug.Log("FindJoinButtonObject: Found Button GameObject: " + playerGameObject.name);
                    JoinButton joinButton = playerGameObject.GetComponent<JoinButton>();
                    if (joinButton != null)
                    {
                        return joinButton;
                    }
                }
                else
                {
                    Debug.LogError("FindJoinButtonObject: Button GameObject not found under Canvas.");
                }
            }
            else
            {
                Debug.LogError("FindJoinButtonObject: GameInterface GameObject not found under Canvas.");
            }
        }
        else
        {
            Debug.LogError("FindJoinButtonObject: Canvas GameObject not found under parent.");
        }

        return null;
    }

    public StartButton FindStartButton()
    {
        GameObject currentObject = this.gameObject;
        Transform canvasTransform = currentObject.transform.parent;
        if (canvasTransform != null)
        {
            Transform gameTransform = canvasTransform.transform.parent;
            if (gameTransform != null)
            {
                Transform playerNameTextTransform = gameTransform.Find("Start Button");
                if (playerNameTextTransform != null)
                {
                    GameObject playerGameObject = playerNameTextTransform.gameObject;
                    Debug.Log("FindStartButton: Found Button GameObject: " + playerGameObject.name);
                    StartButton startButton = playerGameObject.GetComponent<StartButton>();
                    if (startButton != null)
                    {
                        return startButton;
                    }
                }
                else
                {
                    Debug.LogError("FindStartButton: Button GameObject not found under Canvas.");
                }
            }
            else
            {
                Debug.LogError("FindStartButton: GameInterface GameObject not found under Canvas.");
            }
        }
        else
        {
            Debug.LogError("FindStartButton: Canvas GameObject not found under parent.");
        }

        return null;
    }

    public Game FindGameObject()
    {
        GameObject currentObject = this.gameObject; 

        // Get the parent of the current object
        Transform parentTransform = currentObject.transform.parent;
        if (parentTransform != null)
        {
            // Get the parent of the parent (grandparent)
            parentTransform = parentTransform.parent;
            if (parentTransform != null)
            {
                // Get the parent of the grandparent (great-grandparent)
                parentTransform = parentTransform.parent;
                if (parentTransform != null)
                {
                    // Get the Game component from the great-grandparent (Game object)
                    Game game = parentTransform.GetComponent<Game>();
                    if (game != null)
                    {
                        // Return the Game component if found
                        return game;
                    }
                    else
                    {
                        // Log an error if the Game component is not found
                        Debug.LogError("FindGameObject: Game component not found on the Game object.");
                    }
                }
                else
                {
                    // Log an error if the Game object is not found in the hierarchy
                    Debug.LogError("FindGameObject: Game object not found in the hierarchy.");
                }
            }
            else
            {
                // Log an error if the grandparent is null
                Debug.LogError("FindGameObject: Grandparent of current object is null.");
            }
        }
        else
        {
            // Log an error if the parent is null
            Debug.LogError("FindGameObject: Parent of current object is null.");
        }

        return null; // Return null if the Game component is not found
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
    public GameCanvas GetGameCanvas()
    {
        GameObject currentObject = this.gameObject;
        Transform textCanvasTransform = currentObject.transform.parent;
        if(textCanvasTransform == null)
        {
            Debug.LogError("GetGameCanvas: TextCanvasTransform not found");
            return null;
        }

        Transform gameInterfaceTransform = textCanvasTransform.parent;
        if(gameInterfaceTransform == null)
        {
            Debug.LogError("GetGameCanvas: GameInterfaceTransform not found");
            return null;
        }

        Transform gameCanvasTransform = gameInterfaceTransform.Find("GameCanvas");
        if(gameCanvasTransform == null)
        {
            Debug.LogError("GetGameCanvas: CanvasTransform not found");
            return null;
        }

        GameObject canvasGameObject = gameCanvasTransform.gameObject;
        Debug.Log("GetGameCanvas: Found Canvas GameObject: " + canvasGameObject.name);
        GameCanvas gameCanvas = canvasGameObject.GetComponent<GameCanvas>();
        if (gameCanvas != null)
        {
            return gameCanvas;
        }

        return null;
    }

    public void SetCanvasText(string text)
    {
        textCanvas.text = text;
    }

    public void Reset()
    {
        this.m_SeatJoinTimerStarted = false;
        this.m_SeatJoinTimerExpired = false;
        this.m_RequestGameMasterTimerStarted = false;
        this.m_RequestGameMasterTimerExpired = false;
        this.m_RequestGameMasterFailed = false;

        this.m_SeatOccupied = false;
        this.m_CurrentPlayerName = "";
        this.m_CurrentPlayerId = -1;
        this.SetCanvasText("Seat Open");
        JoinButton joinButton = this.FindJoinButtonObject();
        if(joinButton != null)
        {
            joinButton.gameObject.SetActive(true);
        }
    }
}
