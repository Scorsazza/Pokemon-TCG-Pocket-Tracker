using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BlazorApp3.Data;
using BlazorApp3.Data.Models;
using System.Security.Claims;

namespace BlazorApp3.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BountyBoardController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<BountyBoardController> _logger;

        public BountyBoardController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<BountyBoardController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        [HttpPost("create-request")]
        [IgnoreAntiforgeryToken]

        public async Task<ActionResult> CreateBountyRequest([FromBody] CreateBountyRequest request)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("You must be logged in to create a bounty.");

            var card = await _context.PokemonCards.FindAsync(request.CardId);
            if (card == null)
                return NotFound("Card not found.");

            var existing = await _context.BountyRequests
                .FirstOrDefaultAsync(br => br.UserId == userId && br.CardId == request.CardId && br.Status == BountyStatus.Active);

            if (existing != null)
                return BadRequest("You already have an active bounty for this card.");

            var bounty = new BountyRequest
            {
                UserId = userId,
                CardId = request.CardId,
                Description = request.Description,
                OfferedAmount = request.OfferedAmount,
                OfferedItem = request.OfferedItem,
                Status = BountyStatus.Active,
                CreatedAt = DateTime.UtcNow
            };

            _context.BountyRequests.Add(bounty);
            await _context.SaveChangesAsync();
            await _context.UpdateUserStatsAsync(userId);

            return Ok(new { Message = "Bounty request created successfully.", BountyId = bounty.Id });
        }

        [HttpGet("requests")]
        public async Task<ActionResult> GetBountyRequests(
            [FromQuery] string? cardName = null,
            [FromQuery] string? pack = null,
            [FromQuery] BountyStatus? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var query = _context.BountyRequests
                .Include(br => br.User)
                .Include(br => br.Card)
                .Include(br => br.Offers)
                .AsQueryable();

            if (!string.IsNullOrEmpty(cardName))
                query = query.Where(br => br.Card.Name.Contains(cardName));

            if (!string.IsNullOrEmpty(pack))
                query = query.Where(br => br.Card.Pack == pack);

            query = query.Where(br => br.Status == (status ?? BountyStatus.Active));

            var totalCount = await query.CountAsync();
            var bounties = await query
                .OrderByDescending(br => br.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(br => new BountyRequestDto
                {
                    Id = br.Id,
                    UserName = br.User.UserName ?? "Unknown",
                    CardName = br.Card.Name,
                    CardImageUrl = br.Card.ImageUrl,
                    Description = br.Description,
                    OfferedAmount = br.OfferedAmount,
                    OfferedItem = br.OfferedItem,
                    Status = br.Status,
                    CreatedAt = br.CreatedAt,
                    OfferCount = br.Offers.Count
                })
                .ToListAsync();

            return Ok(new
            {
                Bounties = bounties,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }
    }
}