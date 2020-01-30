using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TeeKoASPCore.Models;

namespace TeeKoASPCore.Utility
{
    public class GamesList : List<GameModel>
    {
        public GameModel Game(string id) {
            var games = this.Where(g => g.GameID == id);
            if (games.Count() == 0)
            {
                return null;
            }
            else return games.First();
        }

        public Dictionary<ClaimsPrincipal, GameModel> PlayersInGames = new Dictionary<ClaimsPrincipal, GameModel>();

        public void AddPlayer(ClaimsPrincipal user, string gameId) {
            var game = this.Where(g => g.GameID == gameId).FirstOrDefault();
            AddPlayer(user, game);
        }

        public void AddPlayer(ClaimsPrincipal user, GameModel game)
        {
            game.AddPlayer(user);
            PlayersInGames.Add(user, game);
        }

        public void Disconnect(ClaimsPrincipal user) {
            var games = PlayersInGames.Where(pair => pair.Key.IsIdentical(user)).Select(pair => pair.Value);
            foreach (var g in games) {
                g.RemovePlayer(user);
                if (g.Players.Count == 0) {
                    this.Remove(g);
                }
                else
                if (g.Owner.IsIdentical(user)) {
                    g.Owner = g.Players.First();
                }
            }
            
        }

        
    }
}
