using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using BlazorApp3.Data;
using Microsoft.EntityFrameworkCore;
// alias the DTOs so we only refer to D.* throughout
using D = BlazorApp3.Data.Models;
// reference the API’s model for deserialization
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

        public async Task<List<D.PokemonCard>> GetAllCardsAsync() =>
            await _context.PokemonCards
                          .OrderBy(c => c.Pack)
                          .ThenBy(c => c.CardNumber)
                          .ToListAsync();

        public async Task<List<D.CardCollectionDto>> GetMyCollectionAsync(
            string userId, string? pack = null, string? rarity = null, bool? isCollected = null)
            => await _context.GetUserCollectionAsync(userId, pack, rarity, isCollected);

        public async Task<D.UserStatsDto> GetUserStatsAsync(string userId) =>
            await _context.GetUserStatsAsync(userId);

        public async Task SyncCardsFromApiAsync()
        {
            var client = _httpFactory.CreateClient();
            var resp = await client.GetAsync(
                "https://raw.githubusercontent.com/chase-manning/pokemon-tcg-pocket-cards/main/v4.json");
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync();
            var apiCards = JsonSerializer.Deserialize<List<ApiCard>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
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

                if (existing.TryGetValue(a.Id, out var card))
                {
                    // update
                    card.Name = a.Name;
                    card.Pack = a.Pack;
                    card.Rarity = rarity;
                    card.CardNumber = number;
                    card.ImageUrl = a.Image;
                    card.UpdatedAt = now;
                }
                else
                {
                    // create
                    _context.PokemonCards.Add(new D.PokemonCard
                    {
                        Id = a.Id,
                        Name = a.Name,
                        Pack = a.Pack,
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

        public async Task AddCardAsync(string userId, D.AddCardRequest req)
        {
            var exists = await _context.UserCards
                .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.CardId == req.CardId);

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
            var uc = await _context.UserCards
                .FirstOrDefaultAsync(x => x.UserId == userId && x.CardId == cardId);
            if (uc != null)
            {
                _context.UserCards.Remove(uc);
                await _context.SaveChangesAsync();
                await _context.UpdateUserStatsAsync(userId);
            }
        }

        public async Task ToggleTradeAsync(string userId, string cardId)
        {
            var uc = await _context.UserCards
                .FirstOrDefaultAsync(x => x.UserId == userId && x.CardId == cardId);
            if (uc != null)
            {
                uc.IsForTrade = !uc.IsForTrade;
                await _context.SaveChangesAsync();
            }
        }
    }
}
