using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using TeeKoASPCore.Utility;

namespace TeeKoASPCore.Models
{

    /// <summary>
    /// Game model class that contains references to players, drawings, lines, compositions and scores. 
    /// Game state change is actually controlled by a game hub and an external singleton event handler.
    /// </summary>
    public class GameModel
    {
        /// <summary>
        /// Enum representing the game states.
        /// </summary>
        public enum GameState {
            waiting,
            drawing,
            writing,
            composing,
            voting
        }

        /// <summary>
        /// Ordered list representing game phases sequence.
        /// </summary>
        public static List<GameState> phaseSequence = new List<GameState>()
        { GameState.waiting,
          GameState.drawing,
          GameState.writing,
          GameState.composing,
          GameState.voting
        };

        /// <summary>
        /// Struct representing a drawing as seen in game context. Consists of an image data url and an author id.
        /// </summary>
        public struct Drawing {
            public string imgDataUrl;
            public string authorId;
        }

        /// <summary>
        /// Struct representing a line inputed during a game. Consists of a text and an aurhor id.
        /// </summary>
        public struct Line
        {

            public string text;
            public string authorId;
        }
        /// <summary>
        /// Struct representing a composition of a line and a drawing.
        /// </summary>
        public struct Composition
        {
            public Drawing drawing;
            public Line line;
            public string authorId;
        }

        /// <summary>
        /// Struct representing a player score entry. Consists of a score number and player name.
        /// Struct is meant to be sent to client.
        /// </summary>
        public struct PlayerPublicStats {
            public int score;
            public string name;
        }
        /// <summary>
        /// Game "Owner". Player who can start the game.
        /// </summary>
        public ClaimsPrincipal Owner { get; set; }
        public ObservableCollection<ClaimsPrincipal> Players { get; private set; }
        public Dictionary<ClaimsPrincipal, int> scores = new Dictionary<ClaimsPrincipal, int>();
       
        public string GameID { get; set; }
        public GameState _GameState = GameState.waiting;
        public List<Drawing> drawings = new List<Drawing>();
        public List<Line> lines = new List<Line>();
        public List<Composition> compositions = new List<Composition>();
        public List<ClaimsPrincipal> PlayersWhoVoted = new List<ClaimsPrincipal>();

        public int maxDrawingsPerPlayer = 3;
        public int maxLinesPerPlayer = 5;

        public int composingDrawingsDesired = 3;
        public int composingLinesDesired = 5;

        public int pointsForComposition = 2;
        public int pointsForLine = 1;
        public int pointsForDrawing = 2;

        public Dictionary<GameState, int> phaseDurations = new Dictionary<GameState, int>()
        {
            {GameState.waiting, 0},
            {GameState.drawing, 100},
            {GameState.writing, 40},
            {GameState.composing, 30},
            {GameState.voting, 30}
        };
        public bool phaseRotationInProgress = false;
        
        /// <summary>
        /// basic GameModel ctor which does not set a game owner.
        /// </summary>
        /// <returns></returns>
        private static GameModel Create()
        {
            var res = new GameModel() { };
            res.Players = new ObservableCollection<ClaimsPrincipal>();
            res.SetId();
            return res;
        }
        /// <summary>
        /// GameModel ctor which sets the game owner.
        /// </summary>
        /// <param name="owner">Game owner who may start the game.</param>
        /// <returns></returns>
        public static GameModel Create(ClaimsPrincipal owner) {
            var res = Create();
            res.Owner = owner;
            res.AddPlayer(owner);
            return res;
        }

        /// <summary>
        /// Set a unique game id.
        /// </summary>
        public void SetId() {
            GameID = HashUtility.GetShortHash(DateTime.Now.ToUniversalTime().ToString());
        }

        /// <summary>
        /// Add a player to the player list. Should probably be used only when handling game hub's OnConnected event.
        /// </summary>
        /// <param name="player">Player to be added to the game.</param>
        public void AddPlayer(ClaimsPrincipal player) {
            if (!Players.Any(p => p.FindFirst("id")?.Value == player.FindFirst("id")?.Value)) {
                Players.Add(player);
                scores.Add(player, 0);
            }
        }
        /// <summary>
        /// REmove a player from the game. Should probably be used only when handling game hub's onDisconnect event.
        /// </summary>
        /// <param name="player"></param>
        public void RemovePlayer(ClaimsPrincipal player)
        {
            //remove from player list
            var samePerson = Players.Where(p => p.IsIdentical(player));
            if (samePerson.Count() != 0)
            {
                Players.Remove(samePerson.First());

            }

            //remove from score list
            var pointsCountPrincipal = scores.Where(p => p.Key.IsIdentical(player));
            if (pointsCountPrincipal.Count() != 0)
            {

                scores.Remove(pointsCountPrincipal.Select(p => p.Key).First());

            }
        }


        /// <summary>
        /// Add a drawing to the game drawings pool.
        /// </summary>
        /// <param name="drawingDataUrl">Drawing data url</param>
        /// <param name="authorId">Author id</param>
        public void AddDrawing(string drawingDataUrl, string authorId)
        {
            var author = Players.Where(p => p.ReadFirstClaim("id") == authorId).First();

            if (Players.Where(p => p.ReadFirstClaim("id") == authorId).Count() == 0) return;
            if (drawings.Where(d => d.authorId == author.ReadFirstClaim("id")).Count() < maxDrawingsPerPlayer)
            {
                drawings.Add(new Drawing() { imgDataUrl = drawingDataUrl, authorId = author.ReadFirstClaim("id") });
            }
        }



        /// <summary>
        /// Add a line to game's line pool.
        /// </summary>
        /// <param name="text">Line text</param>
        /// <param name="authorId">Author id</param>
        public void AddLine(string text, string authorId)
        {
            if (Players.Where(p => p.ReadFirstClaim("id") == authorId).Count() == 0) return;
            if (lines.Where(d => d.authorId == authorId).Count() < maxLinesPerPlayer)
            {
                Regex reg = new Regex("[*'\",_&#^@]<>");
                text = reg.Replace(text, string.Empty);
                lines.Add(new Line() { text = text, authorId = authorId });
            }
        }

        /// <summary>
        /// Drawings to be distributed among players to make compositions. 
        /// This list copies the drawings list and is then gradually reduced to empty or near empty as
        /// players recieve drawings to choose from.
        /// </summary>
        List<Drawing> drawingsStaged = new List<Drawing>();
        /// <summary>
        /// Lines to be distributed among players to make compositions. 
        /// This list copies the lines list and is then gradually reduced to empty or near empty as
        /// players recieve lines to choose from.
        /// </summary>
        List<Line> linesStaged = new List<Line>();
        /// <summary>
        /// Get components for one player to make compositions from.
        /// </summary>
        /// <returns>A drawings list and a lines list.</returns>
        public Tuple<List<Drawing>, List<Line>> GetComposingElements() {
            var res = new Tuple<List<Drawing>, List<Line>>(new List<Drawing>(), new List<Line>());
            var composingDrawingsMaxPerPlayer = drawings.Count / Players.Count;
            var composingLinesMaxPerPlayer = lines.Count / Players.Count;
            //stage drawings if none are staged
            
             if (drawingsStaged.Count == 0)
                 drawingsStaged = drawings.ToList();
           
             if (linesStaged.Count == 0)
                 linesStaged = lines.ToList();

            //while drawings number threshold isn't reached yet, add drawings from staged drawings to the result to the result.
            for (int i = 0; i < composingDrawingsMaxPerPlayer; i++)
            {
                if (drawingsStaged.Count == 0) break;
                var drawing = (drawingsStaged.OrderBy(qu => Guid.NewGuid()).First());
                res.Item1.Add(drawing);
                lock (drawingsStaged)
                {
                    drawingsStaged.Remove(drawing);
                }
            }

            //while lines number threshold isn't reached yet, add lines from staged lines to the result
            for (int i = 0; i < composingLinesMaxPerPlayer; i++)
            {
                if (linesStaged.Count == 0) break;
                var line = (linesStaged.OrderBy(qu => Guid.NewGuid()).First());
                res.Item2.Add(line);
                lock (linesStaged)
                {
                    linesStaged.Remove(line);
                }
            }
            return res;

        }
        /// <summary>
        /// Add a composition to compositions pool.
        /// </summary>
        /// <param name="_img">Image data url.</param>
        /// <param name="imgAuthor">Image author id.</param>
        /// <param name="_line">Line text.</param>
        /// <param name="lineAuthor">Line author id.</param>
        /// <param name="compositionAuthor">Composition author id.</param>
        public void AddComposition(string _img, string imgAuthor,
            string _line, string lineAuthor,
            ClaimsPrincipal compositionAuthor) {

            var drawing = drawings.Where(d => d.imgDataUrl == _img).First();
            var line = lines.Where(l => l.text == _line).First();

            compositions.Add(
                new Composition() {
                    drawing = drawing,
                    line = line,
                    authorId = compositionAuthor.GetId()
                }
                );

        }

        /// <summary>
        /// Get game scores with no private information (e.g. player id)
        /// </summary>
        /// <returns>Return a list of stats.</returns>
        public List<PlayerPublicStats> GetPublicStats(){
            var res = new List<PlayerPublicStats>();
            foreach (var p in Players)
            {
                int score=0;
                //if no score records was found
                if (!scores.Where(pointScore => pointScore.Key.IsIdentical(p)).Any())
                {
                    score = -1;
                }
                else
                    score = scores.Where(pointScore => pointScore.Key.IsIdentical(p)).Select(pointScore => pointScore.Value).First();
                //display the game owner with a crown emoji
                if (Owner.IsIdentical(p)) {
                    res.Add(new PlayerPublicStats() { name = p.ReadFirstClaim("name")+ "&#x1F451", score = score });
                }
                else
                    res.Add(new PlayerPublicStats() { name = p.ReadFirstClaim("name"), score = score });

            }
            return res;
        }
        /// <summary>
        /// Take in a vote on the compositions from a player and distribute points among authors. 
        /// </summary>
        /// <param name="voter">User who voted</param>
        /// <param name="composition">Composition that was chosen</param>
        public void CastVote(ClaimsPrincipal voter, Composition composition) {
            //if vote was already recieved from this voter
            if (PlayersWhoVoted.Where(v=>v.IsIdentical(voter)).Any())
                return;
            //take a note that this player has voted
            PlayersWhoVoted.Add(voter);

            //give points
            var drawerId = drawings.Where(d => d.imgDataUrl == composition.drawing.imgDataUrl).Select(d => d.authorId).First();
            var drawer = Players.Where(p => p.GetId() == drawerId).First();

            var writerId = lines.Where(l => l.text == composition.line.text).Select(l => l.authorId).First();
            var writer = Players.Where(p => p.GetId() == writerId).First();

            var composer = Players.Where(p => p.GetId() == composition.authorId).First();

            scores[drawer] += pointsForDrawing;
            scores[writer] += pointsForLine;
            scores[composer] += pointsForComposition;

        }

        /// <summary>
        /// Clear drawings and lines.
        /// </summary>
        public void Reset()
        {
            drawingsStaged.Clear();
            linesStaged.Clear();
            drawings.Clear();
            lines.Clear();
        }
    }
}
