using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TeeKoASPCore.Models;
using TeeKoASPCore.Utility;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace TeeKoASPCore.Controllers
{
    [Authorize]
    public class GameController : Controller
    {
        public GamesList gameList;
        
        public GameController(GamesList games) {
            gameList = games;
        }

      

        [Route("game/StartLobby")]
        public IActionResult StartLobby()
        {
            var newGame = GameModel.Create(User);
            gameList.Add(newGame);
            return Redirect(String.Format("{0}",newGame.GameID.ToString()));
        }
        
        [Route("game/{id}")]
        public IActionResult ConnectToLobby( string id) {
            var game = gameList.Game(id);
            if (game == null)
            {
                return View("MissingGameError");
            }
            else
            {
                gameList.AddPlayer(User, game);
                return View("Index", game);
            }
        }

    }
}