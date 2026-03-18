using Microsoft.AspNetCore.Mvc;
using api.Services;

namespace api.Controllers;

[ApiController]
[Route("api/games")]
public class GamesController : ControllerBase
{
    private readonly IgdbService _igdbService;

    public GamesController(IgdbService igdbService)
    {
        _igdbService = igdbService;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Ok(Array.Empty<object>());
        }

        try
        {
            var games = await _igdbService.SearchGamesAsync(query);
            return Ok(games.Select(game => new
            {
                id = game.Id,
                name = game.Name,
                cover = game.Cover == null
                    ? null
                    : new
                    {
                        image_id = game.Cover.ImageId
                    }
            }));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Unable to load games right now.",
                error = ex.Message
            });
        }
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id)
    {
        try
        {
            var game = await _igdbService.GetGameByIdAsync(id);
            if (game == null)
            {
                return NotFound(new { message = "Game not found." });
            }

            return Ok(new
            {
                id = game.Id,
                name = game.Name,
                coverUrl = game.CoverUrl
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Unable to load the game right now.",
                error = ex.Message
            });
        }
    }
}
