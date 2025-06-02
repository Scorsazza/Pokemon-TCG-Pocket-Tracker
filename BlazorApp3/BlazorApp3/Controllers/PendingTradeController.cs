// Controllers/PendingTradesController.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using BlazorApp3.Data;
using BlazorApp3.Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BlazorApp3.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PendingTradesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PendingTradesController> _logger;

        public PendingTradesController(
            ApplicationDbContext context,
            ILogger<PendingTradesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<List<PendingTradeDto>>> GetPendingTrades()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("You must be logged in to view pending trades.");

            var trades = await _context.BountyOffers
                .Include(o => o.BountyRequest)
                    .ThenInclude(br => br.User)
                .Include(o => o.BountyRequest)
                    .ThenInclude(br => br.Card)
                .Include(o => o.Offerer)
                .Include(o => o.Card)
                .Where(o => o.Status == OfferStatus.Accepted &&
                       o.BountyRequest.Status == BountyStatus.Completed &&
                       (o.BountyRequest.UserId == userId || o.OffererId == userId))
                .Select(o => new PendingTradeDto
                {
                    Id = o.Id,

                    BountyOwnerName = o.BountyRequest.User.UserName ?? "Unknown",
                    BountyOwnerFriendCode = o.BountyRequest.OfferedItem,

                    OffererName = o.Offerer.UserName ?? "Unknown",
                    OffererFriendCode = o.FriendCode,

                    RequestedCardName = o.BountyRequest.Card.Name,
                    RequestedCardImageUrl = o.BountyRequest.Card.ImageUrl,

                    OfferedCardName = o.Card.Name,
                    OfferedCardImageUrl = o.Card.ImageUrl,

                    CreatedAt = o.CreatedAt
                })
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            return Ok(trades);
        }

        [HttpPost("complete/{tradeId}")]
        public async Task<ActionResult> CompleteTrade(int tradeId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("You must be logged in to complete trades.");

            var offer = await _context.BountyOffers
                .Include(o => o.BountyRequest)
                .FirstOrDefaultAsync(o => o.Id == tradeId && o.Status == OfferStatus.Accepted);

            if (offer == null)
                return NotFound("Trade not found or not in accepted status.");

            if (offer.BountyRequest.UserId != userId && offer.OffererId != userId)
                return Forbid("You are not authorized to complete this trade.");

            // No extra flag needed—once accepted, it's “completed” for UI purposes
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Trade marked as completed successfully." });
        }

        [HttpPost("cancel/{tradeId}")]
        public async Task<ActionResult> CancelTrade(int tradeId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("You must be logged in to cancel trades.");

            var offer = await _context.BountyOffers
                .Include(o => o.BountyRequest)
                .FirstOrDefaultAsync(o => o.Id == tradeId && o.Status == OfferStatus.Accepted);

            if (offer == null)
                return NotFound("Trade not found or not in accepted status.");

            if (offer.BountyRequest.UserId != userId && offer.OffererId != userId)
                return Forbid("You are not authorized to cancel this trade.");

            // Reopen the bounty for new offers
            offer.BountyRequest.Status = BountyStatus.Active;
            offer.Status = OfferStatus.Cancelled;

            await _context.SaveChangesAsync();

            return Ok(new { Message = "Trade cancelled and bounty reopened for new offers." });
        }
    }

    public class PendingTradeDto
    {
        public int Id { get; set; }

        public string BountyOwnerName { get; set; } = "";
        public string BountyOwnerFriendCode { get; set; } = "";

        public string OffererName { get; set; } = "";
        public string OffererFriendCode { get; set; } = "";

        public string RequestedCardName { get; set; } = "";
        public string RequestedCardImageUrl { get; set; } = "";

        public string OfferedCardName { get; set; } = "";
        public string OfferedCardImageUrl { get; set; } = "";

        public DateTime CreatedAt { get; set; }
    }
}
