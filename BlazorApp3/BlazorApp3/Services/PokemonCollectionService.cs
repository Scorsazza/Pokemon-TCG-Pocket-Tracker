// PokemonCollectionService.cs (COMPLETE)
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

        private static readonly Dictionary<string, string> PackToExpansion = new()
        {
            ["Charizard"] = "Genetic Apex",
            ["Mewtwo"] = "Genetic Apex",
            ["Pikachu"] = "Genetic Apex",
            ["Shared(Genetic Apex)"] = "Genetic Apex",
        };

        private string GetExpansionFromPack(string pack)
        {
            return PackToExpansion.TryGetValue(pack.Trim(), out var expansion) ? expansion : "Unknown";
        }

        public async Task SyncCardsFromApiAsync()
        {
            var client = _httpFactory.CreateClient();
            var resp = await client.GetAsync("https://raw.githubusercontent.com/chase-manning/pokemon-tcg-pocket-cards/main/v4.json");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            var apiCards = JsonSerializer.Deserialize<List<ApiCard>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                           ?? throw new Exception("Invalid JSON");

            var existing = await _context.PokemonCards.ToDictionaryAsync(c => c.Id);
            var now = DateTime.UtcNow;

            foreach (var a in apiCards)
            {
                if (string.IsNullOrWhiteSpace(a.Pack)) continue;

                var rarity = a.Rarity.Trim() switch
                {
                    "◊" => "Common",
                    "◊◊" => "Uncommon",
                    "◊◊◊" => "Rare",
                    "◊◊◊◊" => "Ultra Rare",
                    _ => "Unknown"
                };

                var number = a.Id.Split('-').ElementAtOrDefault(1) ?? "";
                var expansion = GetExpansionFromPack(a.Pack);

                if (existing.TryGetValue(a.Id, out var card))
                {
                    card.Name = a.Name;
                    card.Pack = a.Pack;
                    card.Expansion = expansion;
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
                        Pack = a.Pack,
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
            var uc = await _context.UserCards
                .FirstOrDefaultAsync(x => x.UserId == userId && x.CardId == cardId);

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

        public async Task<List<D.LeaderboardEntry>> GetLeaderboardAsync(int top = 10)
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

        public async Task ToggleTradeAsync(string userId, string cardId)
        {
            var uc = await _context.UserCards.FirstOrDefaultAsync(x => x.UserId == userId && x.CardId == cardId);
            if (uc != null)
            {
                uc.IsForTrade = !uc.IsForTrade;
                await _context.SaveChangesAsync();
            }
        }
    }
}