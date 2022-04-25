using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Chirper.Models;
using Chirper.Data;
using Microsoft.Extensions.Options;

namespace Chirper.Data
{
    public partial class PostgresContext : DbContext
    {
        private IOptions<AppSettings> appSettings;

        public PostgresContext()
        {
        }

        public PostgresContext(DbContextOptions<PostgresContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Chirp> Chirps { get; set; } = null!;
        public virtual DbSet<Comment> Comments { get; set; } = null!;
        public virtual DbSet<Follower> Followers { get; set; } = null!;
        public virtual DbSet<ChirpTag> ChirpTags { get; set; } = null!;
        public virtual DbSet<TagList> TagLists { get; set; } = null!;
        public virtual DbSet<User> Users { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseNpgsql(appSettings.Value.PostgresRDS);
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("uuid-ossp");

            modelBuilder.Entity<Chirp>(entity =>
            {
                entity.ToTable("chirps");

                entity.Property(e => e.ChirpId)
                    .HasColumnName("chirp_id")
                    .HasDefaultValueSql("uuid_generate_v4()");

                entity.Property(e => e.ChirpBody)
                    .HasMaxLength(1000)
                    .HasColumnName("chirp_body");

                entity.Property(e => e.ChirpDislikes).HasColumnName("chirp_dislikes");

                entity.Property(e => e.ChirpLikes).HasColumnName("chirp_likes");

                entity.Property(e => e.ChirpTimestamp)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("chirp_timestamp")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.UserId).HasColumnName("user_id");
            });

            modelBuilder.Entity<Comment>(entity =>
            {
                entity.ToTable("comments");

                entity.Property(e => e.CommentId)
                    .HasColumnName("comment_id")
                    .HasDefaultValueSql("uuid_generate_v4()");

                entity.Property(e => e.ChirpId).HasColumnName("chirp_id");

                entity.Property(e => e.CommentBody)
                    .HasMaxLength(1000)
                    .HasColumnName("comment_body");

                entity.Property(e => e.CommentTimestamp)
                    .HasColumnType("timestamp without time zone")
                    .HasColumnName("comment_timestamp")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.UserId).HasColumnName("user_id");
            });

            modelBuilder.Entity<Follower>(entity =>
            {
                entity.HasKey(e => e.EntryId)
                    .HasName("followers_pk");

                entity.ToTable("followers");

                entity.Property(e => e.EntryId)
                    .HasColumnName("entry_id")
                    .HasDefaultValueSql("uuid_generate_v4()");

                entity.Property(e => e.FollowerId).HasColumnName("follower_id");

                entity.Property(e => e.UserId).HasColumnName("user_id");
            });

            modelBuilder.Entity<ChirpTag>(entity =>
            {
                entity.HasKey(e => e.EntryId)
                    .HasName("tags_pk");

                entity.ToTable("chirp_tags");

                entity.Property(e => e.EntryId)
                    .HasColumnName("entry_id")
                    .HasDefaultValueSql("uuid_generate_v4()");

                entity.Property(e => e.ChripId).HasColumnName("chrip_id");

                entity.Property(e => e.TagId).HasColumnName("tag_id");
            });

            modelBuilder.Entity<TagList>(entity =>
            {
                entity.HasKey(e => e.TagId)
                    .HasName("tag_list_pk");

                entity.ToTable("tag_list");

                entity.HasIndex(e => e.TagName, "tag_list_tag_name_uindex")
                    .IsUnique();

                entity.Property(e => e.TagId)
                    .HasColumnName("tag_id")
                    .HasDefaultValueSql("uuid_generate_v4()");

                entity.Property(e => e.TagName)
                    .HasMaxLength(128)
                    .HasColumnName("tag_name");
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");

                entity.HasIndex(e => e.Email, "users_email_uindex")
                    .IsUnique();

                entity.HasIndex(e => e.Username, "users_username_uindex")
                    .IsUnique();

                entity.Property(e => e.UserId)
                    .HasColumnName("user_id")
                    .HasDefaultValueSql("uuid_generate_v4()");

                entity.Property(e => e.Email)
                    .HasColumnType("character varying")
                    .HasColumnName("email");

                entity.Property(e => e.AccountActive).HasColumnName("account_active");

                entity.Property(e => e.FirstName)
                    .HasMaxLength(64)
                    .HasColumnName("first_name");

                entity.Property(e => e.JoinDate)
                    .HasColumnName("join_date")
                    .HasDefaultValueSql("now()");

                entity.Property(e => e.LastName)
                    .HasMaxLength(64)
                    .HasColumnName("last_name");

                entity.Property(e => e.PwdHash)
                    .HasColumnType("character varying")
                    .HasColumnName("pwd_hash");

                entity.Property(e => e.Username)
                    .HasMaxLength(32)
                    .HasColumnName("username");

                entity.Property(e => e.ActivationKey)
                    .HasColumnName("activation_key");

                entity.Property(e => e.PwdResetTimestamp)
                    .HasColumnName("pwd_reset_timestamp");
            });

            OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
    }
}
