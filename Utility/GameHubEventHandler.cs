using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using TeeKoASPCore.Models;

namespace TeeKoASPCore.Utility
{
    public class GameHubEventHandler
    {
        private readonly IHubContext<GameHub> _hubContext;
        public GameHubEventHandler(IHubContext<GameHub> hubContext)
        {
            _hubContext = hubContext;
        }
        public async Task InformPlayersChanged(object sender, NotifyCollectionChangedEventArgs e, GameModel game)
        {
                foreach (var player in game.Players)
            {
                var stats = game.GetPublicStats();
                await _hubContext.Clients.User(player.ReadFirstClaim("id")).SendAsync("PlayersChanged", stats);
            }
        }

        public void StartPhaseChanging(GameModel game)
        {
            /**Three consequental timers to cycle through game phases. 
            If you want to implement more phases then I'd suggest
            rewriting this to use a list of phases rather then
            a hardcoded sequence.
            **/
            Timer drawingTimer = new Timer(game.phaseDurations[GameModel.GameState.drawing] * 1000d);
            drawingTimer.AutoReset = false;
            Timer writingTimer = new Timer(game.phaseDurations[GameModel.GameState.writing] * 1000d);
            writingTimer.AutoReset = false;
            Timer composingTimer = new Timer(game.phaseDurations[GameModel.GameState.composing] * 1000d);
            composingTimer.AutoReset = false;
            Timer votingTimer = new Timer(game.phaseDurations[GameModel.GameState.voting] * 1000d);
            votingTimer.AutoReset = false;

            //if timers are already set
            if (game.phaseRotationInProgress)
            {
                return;
            }
            else {
                game.phaseRotationInProgress = true;
            }

            drawingTimer.Elapsed += (_sender, _e) =>
            {
                ChangePhase( game, GameModel.GameState.writing, game.phaseDurations[GameModel.GameState.writing]);
                writingTimer.Start();
            };

            writingTimer.Elapsed += (_sender, _e) =>
            {
                ChangePhase(game, GameModel.GameState.composing, game.phaseDurations[GameModel.GameState.composing]);
                composingTimer.Start();
            };

            composingTimer.Elapsed += (_sender, _e) =>
            {
                ChangePhase( game, GameModel.GameState.voting, game.phaseDurations[GameModel.GameState.voting]);
                votingTimer.Start();
            };

            votingTimer.Elapsed += (_sender, _e) =>
            {
                ChangePhase( game, GameModel.GameState.waiting, game.phaseDurations[GameModel.GameState.waiting]);
                game.phaseRotationInProgress = false;
                game.Reset();
            };

            //start the first timer and start the first stage
            ChangePhase(game, GameModel.GameState.drawing, game.phaseDurations[GameModel.GameState.drawing]);
            drawingTimer.Start();
        }

        private void ChangePhase(GameModel game, GameModel.GameState nextState, int phaseDuration)
        {
            game._GameState = nextState;
            foreach (var player in game.Players)
            {
                _hubContext.Clients.User(player.ReadFirstClaim("id")).SendAsync("PhaseChange", nextState.ToString(), phaseDuration);
            }
        }
    }
}
