using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;

namespace Data
{
    public class BankDbContext : DbContext
    {
        public BankDbContext(DbContextOptions<BankDbContext> options) : base(options) { }

        public DbSet<Student> Students { get; set; } = null!;
        public DbSet<Course> Courses { get; set; } = null!;
        public DbSet<Teacher> Teachers { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<RevokedToken> RevokedTokens { get; set; } = null!;
        public DbSet<Permission> Permissions { get; set; } = null!;
        public DbSet<RolePermission> RolePermissions { get; set; } = null!;
        public DbSet<UserDeniedPermission> UserDeniedPermissions { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Teacher -> Courses (1-to-many)
            modelBuilder.Entity<Teacher>()
                .HasMany(t => t.Courses)
                .WithOne(c => c.Teacher)
                .HasForeignKey(c => c.TeacherId);

            // Student -> Courses (many-to-many)
            modelBuilder.Entity<Student>()
                .HasMany(s => s.Courses)
                .WithMany(c => c.Students)
                .UsingEntity(j => j.ToTable("StudentCourses"));

            // User -> Role (many-to-1)
            modelBuilder.Entity<User>()
                .HasOne(u => u.Role)
                .WithMany(r => r.Users)
                .HasForeignKey(u => u.RoleId);

            // RolePermission (many-to-many Role <-> Permission)
            modelBuilder.Entity<RolePermission>()
                .HasKey(rp => new { rp.RoleId, rp.PermissionId });
            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Role)
                .WithMany(r => r.RolePermissions)
                .HasForeignKey(rp => rp.RoleId);
            modelBuilder.Entity<RolePermission>()
                .HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId);

            // User-Permission (many-to-many User <-> Permission)
            modelBuilder.Entity<User>()
                .HasMany(u => u.Permissions)
                .WithMany()
                .UsingEntity(j => j.ToTable("UserPermissions"));

            // UserDeniedPermissions
            modelBuilder.Entity<UserDeniedPermission>()
                .HasKey(dp => new { dp.UserId, dp.PermissionId });
            modelBuilder.Entity<UserDeniedPermission>()
                .HasOne(dp => dp.User)
                .WithMany(u => u.DeniedPermissions)
                .HasForeignKey(dp => dp.UserId);
            modelBuilder.Entity<UserDeniedPermission>()
                .HasOne(dp => dp.Permission)
                .WithMany()
                .HasForeignKey(dp => dp.PermissionId);

            // Seed Roles
            modelBuilder.Entity<Role>().HasData(
                new Role { Id = 1, Name = "Admin" },
                new Role { Id = 2, Name = "Teacher" },
                new Role { Id = 3, Name = "Student" }
            );

            // Seed Admin User
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Username = "wael",
                    Email = "admin@example.com",
                    PasswordHash = "jGl25bVBBBW96Qi9Te4V37Fnqchz/Eu4qB9vKrRIqRg=", // SHA256("123456")
                    RoleId = 1,
                    CreatedAt = new DateTime(2025, 9, 20, 0, 0, 0)
                }
            );

            // Seed Permissions
            modelBuilder.Entity<Permission>().HasData(
                new Permission { Id = 1, Name = "course.read" },
                new Permission { Id = 2, Name = "course.create" },
                new Permission { Id = 3, Name = "course.update" },
                new Permission { Id = 4, Name = "course.delete" }
            );

            // Seed RolePermissions (Admin عنده كلشي)
            modelBuilder.Entity<RolePermission>().HasData(
                new RolePermission { RoleId = 1, PermissionId = 1 },
                new RolePermission { RoleId = 1, PermissionId = 2 },
                new RolePermission { RoleId = 1, PermissionId = 3 },
                new RolePermission { RoleId = 1, PermissionId = 4 }
            );
        }
    }
}
