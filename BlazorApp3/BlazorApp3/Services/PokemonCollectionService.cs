using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using BlazorApp3.Data;
using Microsoft.EntityFrameworkCore;
using D = BlazorApp3.Data.Models;
using ApiCard = BlazorApp3.Controllers.ApiPokemonCard;

namespace BlazorApp3.Services
{
    public class PokemonCollectionService : IPokemonCollectionService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpClientFactory _httpFactory;

        public PokemonCollectionService(ApplicationDbContext context, IHttpClientFactory httpFactory)
        {
            _context = context;
            _httpFactory = httpFactory;
        }

        private static readonly Dictionary<string, string> PromoToPack = new()
        {
            ["Promo V1"] = "Promo Cards",
            ["Promo V2"] = "Promo Cards",
            ["Promo V3"] = "Promo Cards",
            ["Promo V4"] = "Promo Cards",
            ["Promo V5"] = "Promo Cards",
            ["Promo V6"] = "Promo Cards",
            ["Promo V7"] = "Promo Cards",
            ["Promo V8"] = "Promo Cards",
            ["Promo V9"] = "Promo Cards",
            ["Promo V10"] = "Promo Cards",
            ["Charizard"] = "Charizard",
            ["Mewtwo"] = "Mewtwo",
            ["Pikachu"] = "Pikachu",
            ["Shared(Genetic Apex)"] = "Shared(Genetic Apex)",
            ["Mythical Island"] = "Mythical Island",
            ["Dialga"] = "Dialga",
            ["Palkia"] = "Palkia",
            ["Shared(Space-Time Smackdown)"] = "Shared(Space-Time Smackdown)",
            ["Triumphant Light"] = "Triumphant Light",
            ["Shining Revelry"] = "Shining Revelry",
            ["Solgaleo"] = "Solgaleo",
            ["Lunala"] = "Lunala",
            ["Shared(Celestial Guardians)"] = "Shared(Celestial Guardians)",
            ["Extradimensional Crisis"] = "Extradimensional Crisis",
            ["Eevee Grove"] = "Eevee Grove",
            ["Shop"] = "Shop",
            ["Shop Bundle"] = "Shop Bundle",
            ["Premium Missions"] = "Premium Missions",
            ["Missions"] = "Missions",
            ["Campaign"] = "Campaign",
            ["Wonder Pick"] = "Wonder Pick"
        };

        private string GetPackFromPromo(string promo)
        {
            return PromoToPack.TryGetValue(promo.Trim(), out var pack) ? pack : "Unknown";
        }

        private static readonly Dictionary<string, string> PackToExpansion = new()
        {
            ["Charizard"] = "Genetic Apex",
            ["Mewtwo"] = "Genetic Apex",
            ["Pikachu"] = "Genetic Apex",
            ["Shared(Genetic Apex)"] = "Genetic Apex",
            ["Mythical Island"] = "Mythical Island",
            ["Dialga"] = "Space-Time Smackdown",
            ["Palkia"] = "Space-Time Smackdown",
            ["Shared(Space-Time Smackdown)"] = "Space-Time Smackdown",
            ["Triumphant Light"] = "Triumphant Light",
            ["Shining Revelry"] = "Shining Revelry",
            ["Solgaleo"] = "Celestial Guardians",
            ["Lunala"] = "Celestial Guardians",
            ["Shared(Celestial Guardians)"] = "Celestial Guardians",
            ["Extradimensional Crisis"] = "Extradimensional Crisis",
            ["Eevee Grove"] = "Eevee Grove",
            ["Promo V1"] = "Promo Cards",
            ["Promo V2"] = "Promo Cards",
            ["Promo V3"] = "Promo Cards",
            ["Promo V4"] = "Promo Cards",
            ["Promo V5"] = "Promo Cards",
            ["Promo V6"] = "Promo Cards",
            ["Promo V7"] = "Promo Cards",
            ["Promo V8"] = "Promo Cards",
            ["Promo V9"] = "Promo Cards",
            ["Promo V10"] = "Promo Cards",
            ["Shop"] = "Promo Cards",
            ["Shop Bundle"] = "Promo Cards",
            ["Premium Missions"] = "Promo Cards",
            ["Missions"] = "Promo Cards",
            ["Campaign"] = "Promo Cards",
            ["Wonder Pick"] = "Promo Cards",
            ["Unknown"] = "Promo Cards",
            ["Error"] = "Promo Cards"
        };

        private string GetExpansionFromPack(string pack)
        {
            return PackToExpansion.TryGetValue(pack.Trim(), out var expansion) ? expansion : "Unknown";
        }

        private string GetRarityClass(string rarity) => rarity switch
        {
            "Common" => "rarity-common",
            "Uncommon" => "rarity-uncommon",
            "Rare" => "rarity-rare",
            "EX Cards" => "rarity-ex-cards",
            "Full Art" => "rarity-full-art",
            "Enhanced Full Art" => "rarity-enhanced-full-art",
            "Immersive" => "rarity-immersive",
            "Gold" => "rarity-gold",
            "Promo" => "rarity-promo",
            _ => "rarity-other"
        };

        public async Task SyncCardsFromApiAsync()
        {
            var client = _httpFactory.CreateClient();
            var resp = await client.GetAsync("https://raw.githubusercontent.com/chase-manning/pokemon-tcg-pocket-cards/main/v4.json");
            resp.EnsureSuccessStatusCode();
            var json = await resp.Content.ReadAsStringAsync();
            var apiCards = JsonSerializer.Deserialize<List<ApiCard>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new Exception("Invalid JSON");
            var existing = await _context.PokemonCards.ToDictionaryAsync(c => c.Id);
            var now = DateTime.UtcNow;

            foreach (var a in apiCards)
            {
                if (string.IsNullOrWhiteSpace(a.Pack)) continue;
                if (a.Pack == "Error")
                {
                    a.Pack = "Shop";
                }
                var rarity = a.Rarity.Trim() switch
                {
                    "◊" => "Common",
                    "◊◊" => "Uncommon",
                    "◊◊◊" => "Rare",
                    "◊◊◊◊" => "EX Cards",
                    "☆" => "Full Art",
                    "☆☆" => "Enhanced Full Art",
                    "☆☆☆" => "Immersive",
                    "♕" => "Gold",
                    "Promo" => "Promo",
                    "Shop" => "Shop Exclusive",
                    _ => "Other"
                };
                var number = a.Id.Split('-').ElementAtOrDefault(1) ?? "";
                var expansion = GetExpansionFromPack(a.Pack);
                var pack = GetPackFromPromo(a.Pack) ?? a.Pack.Trim();

                if (existing.TryGetValue(a.Id, out var card))
                {
                    card.Name = a.Name;
                    card.Pack = pack;
                    card.Expansion = expansion;
                    card.Type = a.Type;
                    card.Rarity = rarity;
                    card.CardNumber = number;
                    card.ImageUrl = a.Image;
                    card.UpdatedAt = now;
                }
                else
                {
                    _context.PokemonCards.Add(new D.PokemonCard
                    {
                        Id = a.Id,
                        Name = a.Name,
                        Pack = pack,
                        Type = a.Type,
                        Expansion = expansion,
                        Rarity = rarity,
                        CardNumber = number,
                        ImageUrl = a.Image,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
            }
            await _context.SaveChangesAsync();
        }

        public async Task SetQuantityAsync(string userId, string cardId, int quantity)
        {
            var uc = await _context.UserCards.FirstOrDefaultAsync(x => x.UserId == userId && x.CardId == cardId);
            if (quantity <= 0)
            {
                if (uc != null)
                    _context.UserCards.Remove(uc);
            }
            else
            {
                if (uc != null)
                    uc.Quantity = quantity;
                else
                    _context.UserCards.Add(new D.UserCard
                    {
                        UserId = userId,
                        CardId = cardId,
                        Quantity = quantity,
                        CollectedAt = DateTime.UtcNow
                    });
            }
            await _context.SaveChangesAsync();
            await _context.UpdateUserStatsAsync(userId);
        }

        public async Task<List<D.PokemonCard>> GetAllCardsAsync()
        {
            return await _context.PokemonCards
                .OrderBy(c => c.Pack)
                .ThenBy(c => c.CardNumber)
                .ToListAsync();
        }

        public async Task<List<D.CardCollectionDto>> GetMyCollectionAsync(string userId, string? pack = null, string? rarity = null, bool? isCollected = null)
        {
            return await _context.GetUserCollectionAsync(userId, pack, rarity, isCollected);
        }

        public async Task<D.UserStatsDto> GetUserStatsAsync(string userId)
        {
            return await _context.GetUserStatsAsync(userId);
        }

        public async Task AddCardAsync(string userId, D.AddCardRequest req)
        {
            var exists = await _context.UserCards.FirstOrDefaultAsync(uc => uc.UserId == userId && uc.CardId == req.CardId);
            if (exists != null)
            {
                exists.Quantity += req.Quantity;
                exists.IsForTrade = req.IsForTrade;
            }
            else
            {
                _context.UserCards.Add(new D.UserCard
                {
                    UserId = userId,
                    CardId = req.CardId,
                    Quantity = req.Quantity,
                    IsForTrade = req.IsForTrade,
                    CollectedAt = DateTime.UtcNow
                });
            }
            await _context.SaveChangesAsync();
            await _context.UpdateUserStatsAsync(userId);
        }

        public async Task RemoveCardAsync(string userId, string cardId)
        {
            var uc = await _context.UserCards.FirstOrDefaultAsync(x => x.UserId == userId && x.CardId == cardId);
            if (uc != null)
            {
                _context.UserCards.Remove(uc);
                await _context.SaveChangesAsync();
                await _context.UpdateUserStatsAsync(userId);
            }
        }

        public async Task<List<D.LeaderboardEntry>> GetLeaderboardAsync(int top = 10, string? packFilter = null)
        {
            if (string.IsNullOrEmpty(packFilter) || packFilter == "All")
            {
                var totalCardsCount = await _context.PokemonCards.CountAsync();
                var topUsers = await _context.UserStats
                    .OrderByDescending(u => u.UniqueCards)
                    .Take(top)
                    .Join(_context.Users,
                          stats => stats.UserId,
                          user => user.Id,
                          (stats, user) => new D.LeaderboardEntry
                          {
                              UserName = user.UserName ?? "Unknown",
                              CompletionPercentage = totalCardsCount > 0
                                  ? Math.Round((decimal)stats.UniqueCards / totalCardsCount * 100, 2)
                                  : 0,
                              UniqueCards = stats.UniqueCards,
                              TotalCards = stats.TotalCards,
                              BountiesCompleted = stats.BountiesCompleted,
                              TradesCompleted = stats.TradesCompleted
                          })
                    .ToListAsync();
                return topUsers;
            }
            var cardsInPack = await _context.PokemonCards
                .Where(c => c.Pack == packFilter)
                .Select(c => c.Id)
                .ToListAsync();
            var totalPackCount = cardsInPack.Count;
            var topUsersPack = await _context.UserCards
                .Where(uc => cardsInPack.Contains(uc.CardId))
                .GroupBy(uc => uc.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    UniqueCount = g.Select(x => x.CardId).Distinct().Count(),
                    TotalCount = g.Sum(x => x.Quantity)
                })
                .OrderByDescending(x => x.UniqueCount)
                .Take(top)
                .ToListAsync();
            var result = new List<D.LeaderboardEntry>();
            foreach (var u in topUsersPack)
            {
                var userEntity = await _context.Users.FindAsync(u.UserId);
                var pct = totalPackCount > 0
                    ? Math.Round((decimal)u.UniqueCount / totalPackCount * 100, 2)
                    : 0;
                result.Add(new D.LeaderboardEntry
                {
                    UserName = userEntity?.UserName ?? "Unknown",
                    CompletionPercentage = pct,
                    UniqueCards = u.UniqueCount,
                    TotalCards = u.TotalCount,
                    BountiesCompleted = (await _context.UserStats.FindAsync(u.UserId))?.BountiesCompleted ?? 0,
                    TradesCompleted = (await _context.UserStats.FindAsync(u.UserId))?.TradesCompleted ?? 0
                });
            }
            return result;
        }

        public async Task ToggleTradeAsync(string userId, string cardId)
        {
            var uc = await _context.UserCards.FirstOrDefaultAsync(x => x.UserId == userId && x.CardId == cardId);
            if (uc != null)
            {
                uc.IsForTrade = !uc.IsForTrade;
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<string>> GetAllExpansionsAsync()
        {
            return await _context.PokemonCards
                .Select(c => c.Expansion)
                .Distinct()
                .OrderBy(e => e)
                .ToListAsync();
        }
    }
}
