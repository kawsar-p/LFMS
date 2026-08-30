using LFMS.Data;
using LFMS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ======================================================
// DATABASE
// ======================================================

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// ======================================================
// IDENTITY
// ======================================================

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;

    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// ======================================================
// COOKIE SETTINGS
// ======================================================

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

// ======================================================
// MVC
// ======================================================

builder.Services.AddControllersWithViews();

var app = builder.Build();

// ======================================================
// DATABASE INITIALIZATION
// ======================================================

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var db = services.GetRequiredService<ApplicationDbContext>();

    // IMPORTANT: Do not call EnsureCreatedAsync() on every startup when an
    // existing SQL Server database is already being used. EnsureCreated is
    // intended for a new database and can conflict with an existing schema.
    // Only create the full EF schema when the database is new / has no
    // AspNetUsers table. Existing databases are upgraded by the safe SQL
    // checks below.
    var canConnect = await db.Database.CanConnectAsync();
    if (!canConnect)
    {
        await db.Database.EnsureCreatedAsync();
    }
    else
    {
        var connection = db.Database.GetDbConnection();
        var wasOpen = connection.State == System.Data.ConnectionState.Open;
        if (!wasOpen)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT CASE WHEN OBJECT_ID(N'[dbo].[AspNetUsers]', N'U') IS NULL THEN 0 ELSE 1 END";
            var result = await command.ExecuteScalarAsync();
            var hasIdentityTables = Convert.ToInt32(result) == 1;

            if (!hasIdentityTables)
                await db.Database.EnsureCreatedAsync();
        }
        finally
        {
            if (!wasOpen)
                await connection.CloseAsync();
        }
    }

    // ==================================================
    // ASP.NET USER - IsActive
    // ==================================================

    await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[AspNetUsers]', N'U') IS NOT NULL
   AND COL_LENGTH(N'AspNetUsers', N'IsActive') IS NULL
BEGIN
    ALTER TABLE [AspNetUsers]
    ADD [IsActive] bit NOT NULL
        CONSTRAINT [DF_AspNetUsers_IsActive]
        DEFAULT (1);
END
");

    // ==================================================
    // ASP.NET USER - ProfileImagePath
    // ==================================================

    await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[AspNetUsers]', N'U') IS NOT NULL
   AND COL_LENGTH(N'AspNetUsers', N'ProfileImagePath') IS NULL
BEGIN
    ALTER TABLE [AspNetUsers]
    ADD [ProfileImagePath] nvarchar(300) NULL;
END
");

    // ==================================================
    // CHAT MESSAGES
    // ==================================================

    await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[ChatMessages]', N'U') IS NULL
   AND OBJECT_ID(N'[AspNetUsers]', N'U') IS NOT NULL
BEGIN
    CREATE TABLE [ChatMessages]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [SenderId] nvarchar(450) NOT NULL,
        [ReceiverId] nvarchar(450) NOT NULL,
        [Content] nvarchar(2000) NOT NULL,
        [CreatedAt] datetime2 NOT NULL
            CONSTRAINT [DF_ChatMessages_CreatedAt]
            DEFAULT (GETUTCDATE()),
        [IsRead] bit NOT NULL
            CONSTRAINT [DF_ChatMessages_IsRead]
            DEFAULT (0),

        CONSTRAINT [PK_ChatMessages]
            PRIMARY KEY ([Id]),

        CONSTRAINT [FK_ChatMessages_Sender]
            FOREIGN KEY ([SenderId])
            REFERENCES [AspNetUsers]([Id])
            ON DELETE NO ACTION,

        CONSTRAINT [FK_ChatMessages_Receiver]
            FOREIGN KEY ([ReceiverId])
            REFERENCES [AspNetUsers]([Id])
            ON DELETE NO ACTION
    );

    CREATE INDEX [IX_ChatMessages_Sender_Receiver_CreatedAt]
    ON [ChatMessages]([SenderId], [ReceiverId], [CreatedAt]);
END
");

    // ==================================================
    // NOTIFICATIONS
    // ==================================================

    await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[Notifications]', N'U') IS NULL
   AND OBJECT_ID(N'[AspNetUsers]', N'U') IS NOT NULL
BEGIN
    CREATE TABLE [Notifications]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [Message] nvarchar(500) NOT NULL,
        [Link] nvarchar(300) NULL,
        [IsRead] bit NOT NULL
            CONSTRAINT [DF_Notifications_IsRead]
            DEFAULT (0),
        [CreatedAt] datetime2 NOT NULL
            CONSTRAINT [DF_Notifications_CreatedAt]
            DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_Notifications]
            PRIMARY KEY ([Id]),

        CONSTRAINT [FK_Notifications_AspNetUsers_UserId]
            FOREIGN KEY ([UserId])
            REFERENCES [AspNetUsers]([Id])
            ON DELETE NO ACTION
    );

    CREATE INDEX [IX_Notifications_UserId_CreatedAt]
    ON [Notifications]([UserId], [CreatedAt]);
END
");

    // ==================================================
    // POSTS - REFERENCE CODE
    // ==================================================

    await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[Posts]', N'U') IS NOT NULL
   AND COL_LENGTH(N'Posts', N'ReferenceCode') IS NULL
BEGIN
    ALTER TABLE [Posts]
    ADD [ReferenceCode] nvarchar(30) NULL;
END
");

    await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[Posts]', N'U') IS NOT NULL
   AND COL_LENGTH(N'Posts', N'ReferenceCode') IS NOT NULL
BEGIN
    UPDATE [Posts]
    SET [ReferenceCode] =
        CONCAT(
            'LF-',
            RIGHT(
                '000000' + CAST([Id] AS varchar(6)),
                6
            )
        )
    WHERE [ReferenceCode] IS NULL
       OR LTRIM(RTRIM([ReferenceCode])) = '';
END
");

    // ==================================================
    // POSTS - STATUS
    // ==================================================

    await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[Posts]', N'U') IS NOT NULL
   AND COL_LENGTH(N'Posts', N'Status') IS NULL
BEGIN
    ALTER TABLE [Posts]
    ADD [Status] nvarchar(20) NULL;
END
");

    await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[Posts]', N'U') IS NOT NULL
   AND COL_LENGTH(N'Posts', N'Status') IS NOT NULL
BEGIN
    UPDATE [Posts]
    SET [Status] = N'Available'
    WHERE [Status] IS NULL
       OR LTRIM(RTRIM([Status])) = N'';
END
");

    // ==================================================
    // POSTS - PRIVATE CLAIM VERIFICATION DETAILS
    // ==================================================

    await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[Posts]', N'U') IS NOT NULL
   AND COL_LENGTH(N'Posts', N'PrivateVerificationDetails') IS NULL
BEGIN
    ALTER TABLE [Posts]
    ADD [PrivateVerificationDetails] nvarchar(2000) NULL;
END
");

    // ==================================================
    // POSTS - UNIQUE REFERENCE CODE INDEX
    // ==================================================

    await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[Posts]', N'U') IS NOT NULL
   AND COL_LENGTH(N'Posts', N'ReferenceCode') IS NOT NULL
   AND NOT EXISTS
   (
       SELECT 1
       FROM sys.indexes
       WHERE name = N'IX_Posts_ReferenceCode'
         AND object_id = OBJECT_ID(N'[Posts]')
   )
BEGIN
    CREATE UNIQUE INDEX [IX_Posts_ReferenceCode]
    ON [Posts]([ReferenceCode])
    WHERE [ReferenceCode] IS NOT NULL;
END
");

    // ==================================================
    // POST IMAGES
    // ==================================================

    await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[PostImages]', N'U') IS NULL
   AND OBJECT_ID(N'[Posts]', N'U') IS NOT NULL
BEGIN
    CREATE TABLE [PostImages]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [ImagePath] nvarchar(300) NOT NULL,
        [PostId] int NOT NULL,

        CONSTRAINT [PK_PostImages]
            PRIMARY KEY ([Id]),

        CONSTRAINT [FK_PostImages_Posts_PostId]
            FOREIGN KEY ([PostId])
            REFERENCES [Posts]([Id])
            ON DELETE CASCADE
    );

    CREATE INDEX [IX_PostImages_PostId]
    ON [PostImages]([PostId]);
END
");

    // ==================================================
    // LIKES
    // ==================================================

    await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[Likes]', N'U') IS NULL
   AND OBJECT_ID(N'[Posts]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[AspNetUsers]', N'U') IS NOT NULL
BEGIN
    CREATE TABLE [Likes]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [PostId] int NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [CreatedAt] datetime2 NOT NULL
            CONSTRAINT [DF_Likes_CreatedAt]
            DEFAULT (GETUTCDATE()),

        CONSTRAINT [PK_Likes]
            PRIMARY KEY ([Id]),

        CONSTRAINT [FK_Likes_Posts_PostId]
            FOREIGN KEY ([PostId])
            REFERENCES [Posts]([Id])
            ON DELETE CASCADE,

        CONSTRAINT [FK_Likes_AspNetUsers_UserId]
            FOREIGN KEY ([UserId])
            REFERENCES [AspNetUsers]([Id])
            ON DELETE NO ACTION
    );

    CREATE UNIQUE INDEX [IX_Likes_PostId_UserId]
    ON [Likes]([PostId], [UserId]);
END
");

    // ==================================================
    // COLLECTION / RECOVERY CONFIRMATION HISTORY
    // ==================================================

    await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[CollectionConfirmations]', N'U') IS NULL
   AND OBJECT_ID(N'[Posts]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[AspNetUsers]', N'U') IS NOT NULL
BEGIN
    CREATE TABLE [CollectionConfirmations]
    (
        [Id] int IDENTITY(1,1) NOT NULL,
        [PostId] int NOT NULL,
        [UserId] nvarchar(450) NOT NULL,
        [FullName] nvarchar(100) NOT NULL,
        [PhoneNumber] nvarchar(20) NOT NULL,
        [ConfirmationType] nvarchar(40) NOT NULL,
        [IdentificationDetails] nvarchar(2000) NOT NULL,
        [HandoverDetails] nvarchar(2000) NOT NULL,
        [HandoverDate] datetime2 NOT NULL,
        [Status] nvarchar(100) NOT NULL CONSTRAINT [DF_CollectionConfirmations_Status] DEFAULT (N'PendingAdminApproval'),
        [ConfirmedAt] datetime2 NOT NULL CONSTRAINT [DF_CollectionConfirmations_ConfirmedAt] DEFAULT (GETUTCDATE()),
        [ClaimantVerificationAnswer] nvarchar(2000) NULL,
        [VerificationReferenceAtSubmission] nvarchar(2000) NULL,
        [OwnerApprovalUserId] nvarchar(450) NULL,
        [OwnerApprovedAt] datetime2 NULL,
        [AdminApprovalUserId] nvarchar(450) NULL,
        [AdminApprovedAt] datetime2 NULL,
        [ReviewNotes] nvarchar(1000) NULL,

        CONSTRAINT [PK_CollectionConfirmations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CollectionConfirmations_Posts] FOREIGN KEY ([PostId]) REFERENCES [Posts]([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_CollectionConfirmations_AspNetUsers] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers]([Id]) ON DELETE NO ACTION
    );

    CREATE INDEX [IX_CollectionConfirmations_PostId_ConfirmedAt]
    ON [CollectionConfirmations]([PostId], [ConfirmedAt]);
END
");

    await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[CollectionConfirmations]', N'U') IS NOT NULL
BEGIN
    UPDATE [CollectionConfirmations] SET [Status] = N'PendingAdminApproval' WHERE [Status] = N'Submitted';
END
");

    await db.Database.ExecuteSqlRawAsync(@"
IF OBJECT_ID(N'[CollectionConfirmations]', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'CollectionConfirmations', N'ClaimantVerificationAnswer') IS NULL ALTER TABLE [CollectionConfirmations] ADD [ClaimantVerificationAnswer] nvarchar(2000) NULL;
    IF COL_LENGTH(N'CollectionConfirmations', N'VerificationReferenceAtSubmission') IS NULL ALTER TABLE [CollectionConfirmations] ADD [VerificationReferenceAtSubmission] nvarchar(2000) NULL;
    IF COL_LENGTH(N'CollectionConfirmations', N'OwnerApprovalUserId') IS NULL ALTER TABLE [CollectionConfirmations] ADD [OwnerApprovalUserId] nvarchar(450) NULL;
    IF COL_LENGTH(N'CollectionConfirmations', N'OwnerApprovedAt') IS NULL ALTER TABLE [CollectionConfirmations] ADD [OwnerApprovedAt] datetime2 NULL;
    IF COL_LENGTH(N'CollectionConfirmations', N'AdminApprovalUserId') IS NULL ALTER TABLE [CollectionConfirmations] ADD [AdminApprovalUserId] nvarchar(450) NULL;
    IF COL_LENGTH(N'CollectionConfirmations', N'AdminApprovedAt') IS NULL ALTER TABLE [CollectionConfirmations] ADD [AdminApprovedAt] datetime2 NULL;
    IF COL_LENGTH(N'CollectionConfirmations', N'ReviewNotes') IS NULL ALTER TABLE [CollectionConfirmations] ADD [ReviewNotes] nvarchar(1000) NULL;
END
");

    // ==================================================
    // ROLES
    // ==================================================

    var roleManager =
        services.GetRequiredService<RoleManager<IdentityRole>>();

    foreach (var role in new[] { "Admin", "User" })
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(
                new IdentityRole(role)
            );
        }
    }

    // ==================================================
    // ADMIN ACCOUNT
    // ==================================================

    var userManager =
        services.GetRequiredService<UserManager<ApplicationUser>>();

    const string adminEmail = "admin@lostfound.local";
    const string adminPassword = "Admin123";

    var admin =
        await userManager.FindByEmailAsync(adminEmail);

    if (admin == null)
    {
        admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FullName = "Administrator",
            EmailConfirmed = true
        };

        var result =
            await userManager.CreateAsync(
                admin,
                adminPassword
            );

        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(
                admin,
                "Admin"
            );
        }
    }
    else
    {
        if (!await userManager.IsInRoleAsync(admin, "Admin"))
        {
            await userManager.AddToRoleAsync(
                admin,
                "Admin"
            );
        }
    }

    // ==================================================
    // DEFAULT CATEGORIES
    // ==================================================

    if (!await db.Categories.AnyAsync())
    {
        db.Categories.AddRange(
            new Category { Name = "Electronics" },
            new Category { Name = "Wallet" },
            new Category { Name = "Keys" },
            new Category { Name = "Bag" },
            new Category { Name = "Books" },
            new Category { Name = "ID Card" },
            new Category { Name = "Documents" },
            new Category { Name = "Other" }
        );

        await db.SaveChangesAsync();
    }
}

// ======================================================
// HTTP PIPELINE
// ======================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Local development uses HTTP to avoid HTTPS certificate/setup issues.
// HTTPS can be enabled later for deployment.


// Static files
app.UseStaticFiles();

// Routing
app.UseRouting();

// Authentication
app.UseAuthentication();

// Authorization
app.UseAuthorization();

// MVC Route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

// Run
app.Run();