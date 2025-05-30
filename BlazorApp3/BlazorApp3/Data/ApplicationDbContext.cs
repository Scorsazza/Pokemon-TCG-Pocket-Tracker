﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlazorApp3.Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BlazorApp3.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Pokémon-related DbSets
        public DbSet<PokemonCard> PokemonCards { get; set; }
        public DbSet<UserCard> UserCards { get; set; }
        public DbSet<BountyRequest> BountyRequests { get; set; }
        public DbSet<BountyOffer> BountyOffers { get; set; }
        public DbSet<UserStats> UserStats { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure Identity entities for SQLite compatibility
            if (Database.IsSqlite())
            {
                ConfigureIdentityForSqlite(builder);
            }

            // PokemonCard configuration
            builder.Entity<PokemonCard>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Name);
                entity.HasIndex(e => e.Pack);
                entity.HasIndex(e => e.Rarity);
                entity.HasIndex(e => new { e.Pack, e.CardNumber }).IsUnique();
            });

            // UserCard configuration
            builder.Entity<UserCard>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.UserId, e.CardId }).IsUnique();
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.CardId);
                entity.HasIndex(e => e.IsForTrade);

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Card)
                      .WithMany(c => c.UserCards)
                      .HasForeignKey(e => e.CardId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // BountyRequest configuration
            builder.Entity<BountyRequest>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.CardId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.CreatedAt);

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Card)
                      .WithMany(c => c.BountyRequests)
                      .HasForeignKey(e => e.CardId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // BountyOffer configuration
            builder.Entity<BountyOffer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.BountyRequestId);
                entity.HasIndex(e => e.OffererId);
                entity.HasIndex(e => e.CardId);
                entity.HasIndex(e => e.Status);

                entity.HasOne(e => e.BountyRequest)
                      .WithMany(br => br.Offers)
                      .HasForeignKey(e => e.BountyRequestId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Offerer)
                      .WithMany()
                      .HasForeignKey(e => e.OffererId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Card)
                      .WithMany(c => c.BountyOffers)
                      .HasForeignKey(e => e.CardId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // UserStats configuration
            builder.Entity<UserStats>(entity =>
            {
                entity.HasKey(e => e.UserId);
                entity.HasIndex(e => e.UniqueCards);
                entity.HasIndex(e => e.CompletedSets);
                entity.HasIndex(e => e.BountiesCompleted);

                entity.HasOne(e => e.User)
                      .WithOne()
                      .HasForeignKey<UserStats>(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }

        private void ConfigureIdentityForSqlite(ModelBuilder builder)
        {
            // Configure Identity entities with SQLite-compatible column types and sizes
            builder.Entity<IdentityRole>(entity =>
            {
                entity.Property(e => e.Id).HasMaxLength(85);
                entity.Property(e => e.Name).HasMaxLength(256);
                entity.Property(e => e.NormalizedName).HasMaxLength(256);
                entity.Property(e => e.ConcurrencyStamp).HasColumnType("TEXT");
            });

            builder.Entity<ApplicationUser>(entity =>
            {
                entity.Property(e => e.Id).HasMaxLength(85);
                entity.Property(e => e.UserName).HasMaxLength(256);
                entity.Property(e => e.NormalizedUserName).HasMaxLength(256);
                entity.Property(e => e.Email).HasMaxLength(256);
                entity.Property(e => e.NormalizedEmail).HasMaxLength(256);
                entity.Property(e => e.PasswordHash).HasColumnType("TEXT");
                entity.Property(e => e.SecurityStamp).HasColumnType("TEXT");
                entity.Property(e => e.ConcurrencyStamp).HasColumnType("TEXT");
                entity.Property(e => e.PhoneNumber).HasMaxLength(50);
            });

            builder.Entity<IdentityUserClaim<string>>(entity =>
            {
                entity.Property(e => e.UserId).HasMaxLength(85);
                entity.Property(e => e.ClaimType).HasColumnType("TEXT");
                entity.Property(e => e.ClaimValue).HasColumnType("TEXT");
            });

            builder.Entity<IdentityUserLogin<string>>(entity =>
            {
                entity.Property(e => e.LoginProvider).HasMaxLength(85);
                entity.Property(e => e.ProviderKey).HasMaxLength(85);
                entity.Property(e => e.ProviderDisplayName).HasMaxLength(256);
                entity.Property(e => e.UserId).HasMaxLength(85);
            });

            builder.Entity<IdentityUserToken<string>>(entity =>
            {
                entity.Property(e => e.UserId).HasMaxLength(85);
                entity.Property(e => e.LoginProvider).HasMaxLength(85);
                entity.Property(e => e.Name).HasMaxLength(85);
                entity.Property(e => e.Value).HasColumnType("TEXT");
            });

            builder.Entity<IdentityUserRole<string>>(entity =>
            {
                entity.Property(e => e.UserId).HasMaxLength(85);
                entity.Property(e => e.RoleId).HasMaxLength(85);
            });

            builder.Entity<IdentityRoleClaim<string>>(entity =>
            {
                entity.Property(e => e.RoleId).HasMaxLength(85);
                entity.Property(e => e.ClaimType).HasColumnType("TEXT");
                entity.Property(e => e.ClaimValue).HasColumnType("TEXT");
            });

            // Additional fix for any remaining nvarchar(max) issues
            foreach (var entity in builder.Model.GetEntityTypes())
            {
                foreach (var property in entity.GetProperties())
                {
                    var columnType = property.GetColumnType();
                    if (!string.IsNullOrEmpty(columnType))
                    {
                        if (columnType.Contains("nvarchar(max)", StringComparison.OrdinalIgnoreCase) ||
                            columnType.Contains("varchar(max)", StringComparison.OrdinalIgnoreCase) ||
                            columnType.Contains("ntext", StringComparison.OrdinalIgnoreCase) ||
                            columnType.Contains("text", StringComparison.OrdinalIgnoreCase))
                        {
                            property.SetColumnType("TEXT");
                        }
                    }
                }
            }
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            foreach (var entry in ChangeTracker.Entries<PokemonCard>().Where(e => e.State == EntityState.Modified))
            {
                entry.Entity.UpdatedAt = now;
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }

    public static class DbContextExtensions
    {
        public static async Task<List<CardCollectionDto>> GetUserCollectionAsync(
            this ApplicationDbContext context,
            string userId,
            string? packFilter = null,
            string? rarityFilter = null,
            bool? isCollectedFilter = null)
        {
            var query = from uc in context.UserCards
                        where uc.UserId == userId
                        join card in context.PokemonCards on uc.CardId equals card.Id
                        select new CardCollectionDto
                        {
                            CardId = card.Id,
                            Name = card.Name,
                            Pack = card.Pack,
                            Rarity = card.Rarity,
                            Expansion = card.Expansion,
                            CardNumber = card.CardNumber,
                            ImageUrl = card.ImageUrl,
                            Type = card.Type,
                            Quantity = uc.Quantity,
                            IsCollected = true,
                            IsForTrade = uc.IsForTrade,
                            CollectedAt = uc.CollectedAt
                        };

            if (!string.IsNullOrEmpty(packFilter))
                query = query.Where(c => c.Pack == packFilter);

            if (!string.IsNullOrEmpty(rarityFilter))
                query = query.Where(c => c.Rarity == rarityFilter);

            return await query
                .OrderBy(c => c.Pack)
                .ThenBy(c => c.CardNumber)
                .ToListAsync();
        }

        public static async Task<UserStatsDto> GetUserStatsAsync(
            this ApplicationDbContext context,
            string userId)
        {
            var userCards = await context.UserCards
                .Where(uc => uc.UserId == userId)
                .ToListAsync();

            var totalCards = userCards.Sum(uc => uc.Quantity);
            var uniqueCards = userCards.Count;
            var totalPossible = await context.PokemonCards.CountAsync();
            var storedStats = await context.UserStats.FirstOrDefaultAsync(us => us.UserId == userId);
            var completionPct = totalPossible > 0
                                   ? Math.Round((decimal)uniqueCards / totalPossible * 100, 2)
                                   : 0;
            var rank = await context.UserStats
                                   .Where(us => us.UniqueCards > uniqueCards)
                                   .CountAsync()
                               + 1;

            return new UserStatsDto
            {
                TotalCards = totalCards,
                UniqueCards = uniqueCards,
                CompletedSets = storedStats?.CompletedSets ?? 0,
                CompletionPercentage = completionPct,
                BountiesCompleted = storedStats?.BountiesCompleted ?? 0,
                BountiesPosted = storedStats?.BountiesPosted ?? 0,
                TradesCompleted = storedStats?.TradesCompleted ?? 0,
                Rank = rank
            };
        }

        public static async Task UpdateUserStatsAsync(
            this ApplicationDbContext context,
            string userId)
        {
            var userCards = await context.UserCards
                .Where(uc => uc.UserId == userId)
                .ToListAsync();

            var totalCards = userCards.Sum(uc => uc.Quantity);
            var uniqueCards = userCards.Count;
            var bountiesDone = await context.BountyRequests
                .CountAsync(br => br.UserId == userId && br.Status == BountyStatus.Completed);
            var bountiesPosted = await context.BountyRequests
                .CountAsync(br => br.UserId == userId);

            var stats = await context.UserStats.FirstOrDefaultAsync(us => us.UserId == userId);
            if (stats == null)
            {
                stats = new UserStats
                {
                    UserId = userId,
                    TotalCards = totalCards,
                    UniqueCards = uniqueCards,
                    BountiesCompleted = bountiesDone,
                    BountiesPosted = bountiesPosted,
                    LastUpdated = DateTime.UtcNow
                };
                context.UserStats.Add(stats);
            }
            else
            {
                stats.TotalCards = totalCards;
                stats.UniqueCards = uniqueCards;
                stats.BountiesCompleted = bountiesDone;
                stats.BountiesPosted = bountiesPosted;
                stats.LastUpdated = DateTime.UtcNow;
            }

            await context.SaveChangesAsync();
        }
    }
}