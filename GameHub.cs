using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using TeeKoASPCore.Models;
using TeeKoASPCore.Utility;

namespace TeeKoASPCore
{
    /// <summary>
    /// SignalR hub responsible for game interactions. Uses GameModel class as the underlying model.
    /// </summary>
    public class GameHub : Hub
    {
        /// <summary>
        /// List of ongoing games. A singletor recieved by DI.
        /// </summary>
        GamesList gamesList;
        ILogger logger;

        /// <summary>
        /// External singleton event handler which is used to redirect long term events from the transient GameHub.
        /// </summary>
        GameHubEventHandler eventHandler;

        /// <summary>
        /// Basic ctor with parameters for DI
        /// </summary>
        public GameHub(GamesList _gameList, ILogger<GameHub> _logger, GameHubEventHandler _eventHandler)
        {
            gamesList = _gameList;
            logger = _logger;
            eventHandler = _eventHandler;
        }

        public override Task OnDisconnectedAsync(Exception exception)
        {
            gamesList.Disconnect(Context.User);
            return base.OnDisconnectedAsync(exception);
        }

        public override Task OnConnectedAsync()
        {
            return base.OnConnectedAsync();
        }


        public async Task Connected(string gameId)
        {
            var game = gamesList.Game(gameId);

            if (game == null) return;

            //Add the connected user
            gamesList.AddPlayer(Context.User, gameId);
            //Inform users about gamestate change
            InformPlayersChanged(game);
            //If someone joined late
            if (game._GameState != GameModel.GameState.waiting) {
                await InformPhaseChanged(game, game._GameState);
            }
            if (game.Owner.IsIdentical(Context.User)) {
                await StartLobby(gameId);
            }
            InformPlayersChanged(game);
            InformBasicInfo(game);
        }


        public async Task RequestBasicInfo(string gameId) {
            var game = gamesList.Game(gameId);
            InformBasicInfo(game);
        }

        public Task StartLobby(string gameId) {
            var game = gamesList.Game(gameId);
            if (game == null) return Task.CompletedTask;

            if (Context.User.IsIdentical(game.Owner))
            {
                //subscribe to events representing playerbase change and drawingbase change
                game.Players.CollectionChanged += async (sender, e) => await eventHandler.InformPlayersChanged(sender, e, game);
            }
            
            return Task.CompletedTask;
            
        }

        public async Task StartGame(string gameId)
        {
            var game = gamesList.Game(gameId);
            if (game == null) return;
            //if caller is owner then proceed
            if (game.Owner.IsIdentical(Context.User) && game._GameState == GameModel.GameState.waiting)
            {
                //Handles the game phase rotation process
                eventHandler.StartPhaseChanging(game);
                //Reset collected pieces
                game.drawings.Clear();
                game.lines.Clear();
                
            }
            
            logger.Log(LogLevel.Debug, "Starting a game.");
        }

        /// <summary>
        /// Inform the clints about game phase change. 
        /// This should only be used to change the state immediatly. 
        /// For everything else see GameHubEventHandler.
        /// </summary>
        public async Task InformPhaseChanged(GameModel game, GameModel.GameState phase) {
            foreach (var player in game.Players)
            {
                await Clients.User(player.ReadFirstClaim("id")).SendAsync("PhaseChange", phase.ToString());
            }
        }

        //Reminder: do not use methods in the hub as event handlers

        /// <summary>
        ///Give the clients an update on the player list
        /// </summary>
        /// <param name="game">Game that should have it's clients informed.</param>
        private void InformPlayersChanged(GameModel game)
        {
            foreach (var player in game.Players)
            {
                var publicStats = game.GetPublicStats();
                Clients.User(player.ReadFirstClaim("id")).SendAsync("PlayersChanged", publicStats);
            }
        }

        private void InformBasicInfo(GameModel game) {
            Clients.User(Context.User.GetId()).SendAsync("BasicInfo", game.maxDrawingsPerPlayer, game.maxLinesPerPlayer);
        }


        /// <summary>
        /// Obtain a drawing and inform the client that a drawing was recieved from it
        /// </summary>
        /// <param name="drawingDataURL">Drawing data</param>
        /// <param name="gameId">Game id.</param>
        public async Task SubmitDrawing(string drawingDataURL, string gameId)
        {
            var game = gamesList.Game(gameId);
            if (game == null) return;
            if (game._GameState == GameModel.GameState.drawing)   //if state is right) 
            {
                await Clients.Caller.SendAsync("DrawingRecieved");
                gamesList.Game(gameId).AddDrawing(drawingDataURL, Context.User.ReadFirstClaim("id"));
                //if it's the last drawing the user can submit then inform the client to hide canvas
                if (game.drawings.Where(d => d.authorId == Context.User.ReadFirstClaim("id")).Count() >= game.maxDrawingsPerPlayer) {
                    await Clients.Caller.SendAsync("LastDrawingRecieved");
                }
            }
        }

        /// <summary>
        /// A request from client side to provide elements for compositions is handled here.
        /// </summary>
        public async Task RequestComposingElements(string gameId)
        {
            var game = gamesList.Game(gameId);
            if (game == null) return;
            if (game._GameState == GameModel.GameState.composing)   //if state is right) 
            {
                // get random pictures and lines and send them to client
                var elements = game.GetComposingElements(Context.User);
                await Clients.Caller.SendAsync("ComposingElementsProvided", elements.Item1, elements.Item2);
                
            }
        }

        /// <summary>
        /// A request from client side to provide compositions for voting is handled here.
        /// </summary>
        public async Task RequestVotingCandidates(string gameId)
        {
            var game = gamesList.Game(gameId);
            if (game == null) return;
            if (game._GameState == GameModel.GameState.voting)   //if state is right) 
            {
                // get compositions and send them to client
                var compositions = game.compositions.Where(c=>c.authorId!=Context.User.GetId());
                await Clients.Caller.SendAsync("VotingCandidatesProvided", compositions);

            }
        }

        /// <summary>
        /// Obtain a line and inform the client that a line was recieved from it
        /// </summary>
        /// <param name="text">Line text</param>
        /// <param name="gameId">Game id</param>
        public async Task SubmitLine(string text, string gameId)
        {
            var game = gamesList.Game(gameId);
            if (game == null) return;
            var userId = Context.User.ReadFirstClaim("id");

            //remove special characters
            Regex reg = new Regex("[*'\",_&#^@]<>");
            text = reg.Replace(text, string.Empty);

            //if text is empty
            if (text.Length == 0) return;

            if (game._GameState == GameModel.GameState.writing)   //if state is right) 
            {
                await Clients.Caller.SendAsync("LineRecieved");
                gamesList.Game(gameId).AddLine(text, userId);
                //if it's the last drawing the user can submit then inform the client to hide canvas
                if (game.lines.Where(d => d.authorId == Context.User.GetId()).Count() >= game.maxLinesPerPlayer)
                {
                    await Clients.Caller.SendAsync("LastLineRecieved");
                }
            }
        }

        /// <summary>
        /// Obtain a composing and inform the client that a composing was recieved from it
        /// </summary>
        public async Task SubmitComposing(string selectedImage, string selectedLine, string gameId)
        {
            var game = gamesList.Game(gameId);
            if (game == null) return;
            var userId = Context.User.ReadFirstClaim("id");

            

            

            if ( game._GameState == GameModel.GameState.composing //if state is right
                && game.drawings.Where(d => d.imgDataUrl == selectedImage).Count()!=0  //if drawing exists
                && game.lines.Where(l => l.text == selectedLine).Count() != 0 //if line author exists
               ) 
            {

                string selectedImageAuthor = game.drawings.Where(d => d.imgDataUrl == selectedImage).Select(d => d.authorId).First();
                string selectedLineAuthor = game.lines.Where(l => l.text == selectedLine).Select(l => l.authorId).First(); ;

                //if some of input is empty don't do anything
                if (selectedImage.Length == 0
                || selectedImageAuthor.Length == 0
                || selectedLine.Length == 0
                || selectedLine.Length == 0
                    ) return;

                game.AddComposition(selectedImage, selectedImageAuthor,
                selectedLine,  selectedLineAuthor, Context.User);
                await Clients.Caller.SendAsync("CompositionRecieved");
            }
        }
        
        /// <summary>
        /// Submit a vote on the composition.
        /// </summary>
        public async Task SubmitVote(string selectedImage, string selectedLine, string gameId)
        {
            var game = gamesList.Game(gameId);
            if (game == null) return;
            var userId = Context.User.ReadFirstClaim("id");

            if ( game._GameState == GameModel.GameState.voting //if state is right
                && game.drawings.Where(d => d.imgDataUrl == selectedImage).Count()!=0  //if drawing exists
                && game.lines.Where(l => l.text == selectedLine).Count() != 0 //if line author exists
               ) 
            {
                //if some of input is empty don't do anything
                if (selectedImage.Length == 0
                || selectedLine.Length == 0
                ) return;


                var composition = game.compositions.Where(c =>
                c.drawing.imgDataUrl == selectedImage && c.line.text == selectedLine).First();

                string compositionAuthor = composition.authorId;
                string selectedImageAuthor = game.drawings.Where(d => d.imgDataUrl == selectedImage).Select(d => d.authorId).First();
                string selectedLineAuthor = game.lines.Where(l => l.text == selectedLine).Select(l => l.authorId).First(); ;


                game.CastVote(Context.User, composition);
                InformPlayersChanged(game);
                await Clients.Caller.SendAsync("VoteRecieved");
            }
        }


    }
}
