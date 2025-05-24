using BlazorApp3.Data;
using BlazorApp3.Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace BlazorApp3.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PokemonCollectionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PokemonCollectionController> _logger;

        public PokemonCollectionController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IHttpClientFactory httpClientFactory,
            ILogger<PokemonCollectionController> logger)
        {
            _context = context;
            _userManager = userManager;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        // POST: api/PokemonCollection/sync-cards
        [HttpPost("sync-cards")]
        public async Task<ActionResult> SyncCardsFromApi()
        {
            try
            {
                var httpClient = _httpClientFactory.CreateClient();
                var url = "https://raw.githubusercontent.com/chase-manning/pokemon-tcg-pocket-cards/main/v4.json";
                var response = await httpClient.GetAsync(url);
                var jsonString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return BadRequest("Failed to fetch card data from API.");

                if (!jsonString.TrimStart().StartsWith("{") && !jsonString.TrimStart().StartsWith("["))
                    return BadRequest("Remote data was not JSON.");

                var apiCards = JsonSerializer.Deserialize<List<ApiPokemonCard>>(jsonString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiCards == null)
                    return BadRequest("Invalid card data format.");

                var existingCards = await _context.PokemonCards.ToDictionaryAsync(c => c.Id, c => c);
                var newCards = new List<PokemonCard>();
                var updatedCards = new List<PokemonCard>();

                foreach (var apiCard in apiCards)
                {
                    // Skip cards with blank Pack
                    if (string.IsNullOrWhiteSpace(apiCard.Pack))
                        continue;

                    var convertedRarity = ConvertRarity(apiCard.Rarity);
                    var cardNumber = GetCardNumber(apiCard.Id);

                    if (existingCards.TryGetValue(apiCard.Id, out var existingCard))
                    {
                        existingCard.Name = apiCard.Name;
                        existingCard.Pack = apiCard.Pack;
                        existingCard.Rarity = convertedRarity;
                        existingCard.CardNumber = cardNumber;
                        existingCard.ImageUrl = apiCard.Image;
                        existingCard.Type = ""; // Not present in API
                        existingCard.UpdatedAt = DateTime.UtcNow;
                        updatedCards.Add(existingCard);
                    }
                    else
                    {
                        var newCard = new PokemonCard
                        {
                            Id = apiCard.Id,
                            Name = apiCard.Name,
                            Pack = apiCard.Pack,
                            Rarity = convertedRarity,
                            CardNumber = cardNumber,
                            ImageUrl = apiCard.Image,
                            Type = "",
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        newCards.Add(newCard);
                    }
                }

                if (newCards.Any())
                    _context.PokemonCards.AddRange(newCards);

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    Message = "Cards synchronized successfully",
                    NewCards = newCards.Count,
                    UpdatedCards = updatedCards.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing cards from API");
                return StatusCode(500, $"An error occurred while syncing cards: {ex.Message}");
            }
        }

        // Helper: Converts rarity symbols to readable names
        private string ConvertRarity(string raritySymbols)
        {
            return raritySymbols?.Trim() switch
            {
                "◊" => "Common",
                "◊◊" => "Uncommon",
                "◊◊◊" => "Rare",
                "◊◊◊◊" => "Ultra Rare",
                _ => "Unknown",
            };
        }

        // Helper: Extracts card number from id (e.g., "a1-055" => "055")
        private string GetCardNumber(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return "";
            var parts = id.Split('-');
            return parts.Length > 1 ? parts[1] : "";
        }

        // GET: api/PokemonCollection/my-collection
        // Now allows anonymous users and returns an empty list if not signed in
        [HttpGet("my-collection")]
        [AllowAnonymous]
        public async Task<ActionResult<List<CardCollectionDto>>> GetMyCollection(
            [FromQuery] string? pack = null,
            [FromQuery] string? rarity = null,
            [FromQuery] bool? isCollected = null)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                // Not authenticated → treat as “no cards owned”
                return Ok(new List<CardCollectionDto>());
            }

            var collection = await _context.GetUserCollectionAsync(userId, pack, rarity, isCollected);
            return Ok(collection);
        }

        [HttpPost("add-card")]
        public async Task<ActionResult> AddCardToCollection([FromBody] AddCardRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var card = await _context.PokemonCards.FindAsync(request.CardId);
            if (card == null) return NotFound("Card not found");

            var existingUserCard = await _context.UserCards
                .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.CardId == request.CardId);

            if (existingUserCard != null)
            {
                existingUserCard.Quantity += request.Quantity;
                existingUserCard.IsForTrade = request.IsForTrade;
            }
            else
            {
                var userCard = new UserCard
                {
                    UserId = userId,
                    CardId = request.CardId,
                    Quantity = request.Quantity,
                    CollectedAt = DateTime.UtcNow,
                    IsForTrade = request.IsForTrade
                };
                _context.UserCards.Add(userCard);
            }

            await _context.SaveChangesAsync();
            await _context.UpdateUserStatsAsync(userId);

            return Ok(new { Message = "Card added to collection" });
        }

        [HttpDelete("remove-card/{cardId}")]
        public async Task<ActionResult> RemoveCardFromCollection(string cardId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var userCard = await _context.UserCards
                .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.CardId == cardId);

            if (userCard == null) return NotFound("Card not in collection");

            _context.UserCards.Remove(userCard);
            await _context.SaveChangesAsync();
            await _context.UpdateUserStatsAsync(userId);

            return Ok(new { Message = "Card removed from collection" });
        }

        [HttpGet("stats")]
        public async Task<ActionResult<UserStatsDto>> GetMyStats()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            var stats = await _context.GetUserStatsAsync(userId);
            return Ok(stats);
        }

        [HttpGet("leaderboard")]
        public async Task<ActionResult<List<LeaderboardEntry>>> GetLeaderboard([FromQuery] int limit = 10)
        {
            var leaderboard = await _context.UserStats
                .Include(us => us.User)
                .OrderByDescending(us => us.UniqueCards)
                .ThenByDescending(us => us.TotalCards)
                .Take(limit)
                .Select(us => new LeaderboardEntry
                {
                    UserName = us.User.UserName ?? "Unknown",
                    UniqueCards = us.UniqueCards,
                    TotalCards = us.TotalCards,
                    CompletionPercentage = us.UniqueCards > 0
                        ? Math.Round((decimal)us.UniqueCards / _context.PokemonCards.Count() * 100, 2)
                        : 0,
                    BountiesCompleted = us.BountiesCompleted,
                    TradesCompleted = us.TradesCompleted
                })
                .ToListAsync();

            return Ok(leaderboard);
        }

        [HttpGet("cards")]
        [AllowAnonymous]
        public async Task<ActionResult<List<PokemonCard>>> GetAllCards()
        {
            var cards = await _context.PokemonCards
                .OrderBy(c => c.Pack)
                .ThenBy(c => c.CardNumber)
                .ToListAsync();

            return Ok(cards);
        }

        [HttpGet("packs")]
        [AllowAnonymous]
        public async Task<ActionResult<List<string>>> GetAvailablePacks()
        {
            var packs = await _context.PokemonCards
                .Select(c => c.Pack)
                .Distinct()
                .OrderBy(p => p)
                .ToListAsync();

            return Ok(packs);
        }

        [HttpGet("rarities")]
        [AllowAnonymous]
        public async Task<ActionResult<List<string>>> GetAvailableRarities()
        {
            var rarities = await _context.PokemonCards
                .Select(c => c.Rarity)
                .Distinct()
                .OrderBy(r => r)
                .ToListAsync();

            return Ok(rarities);
        }

        [HttpPut("toggle-trade/{cardId}")]
        public async Task<ActionResult> ToggleCardForTrade(string cardId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var userCard = await _context.UserCards
                .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.CardId == cardId);

            if (userCard == null) return NotFound("Card not in collection");

            userCard.IsForTrade = !userCard.IsForTrade;
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Trade status updated", IsForTrade = userCard.IsForTrade });
        }
    }

    // -- API MODEL FOR REMOTE JSON DATA --
    public class ApiPokemonCard
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Rarity { get; set; } = "";
        public string Pack { get; set; } = "";
        public string Health { get; set; } = "";
        public string Image { get; set; } = "";
        public string FullArt { get; set; } = "";
        public string Ex { get; set; } = "";
        public string Artist { get; set; } = "";
    }
    // -- DTOs for API responses and requests --
    public class CardCollectionDto
    {
        public string CardId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Pack { get; set; } = "";
        public string Rarity { get; set; } = "";
        public string CardNumber { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public string Type { get; set; } = "";
        public int Quantity { get; set; }
        public bool IsCollected { get; set; }
        public bool IsForTrade { get; set; }
        public DateTime? CollectedAt { get; set; }
    }

    public class LeaderboardEntry
    {
        public string UserName { get; set; } = "";
        public int UniqueCards { get; set; }
        public int TotalCards { get; set; }
        public decimal CompletionPercentage { get; set; }
        public int BountiesCompleted { get; set; }
        public int TradesCompleted { get; set; }
    }

    public class AddCardRequest
    {
        public string CardId { get; set; } = "";
        public int Quantity { get; set; }
        public bool IsForTrade { get; set; }
    }

    public class UserStatsDto
    {
        public int TotalCards { get; set; }
        public int UniqueCards { get; set; }
        public int CompletedSets { get; set; }
        public decimal CompletionPercentage { get; set; }
        public int BountiesCompleted { get; set; }
        public int BountiesPosted { get; set; }
        public int TradesCompleted { get; set; }
        public int Rank { get; set; }
    }
}
