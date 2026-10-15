using EVOLVE_VAPT.Data;
using EVOLVE_VAPT.Models;
using EVOLVE_VAPT.Scanning;
using EVOLVE_VAPT.Scanning.Adapters;
using EVOLVE_VAPT.Scanning.Configuration;
using EVOLVE_VAPT.Scanning.Contracts;
using EVOLVE_VAPT.Scanning.Execution;
using EVOLVE_VAPT.Scanning.Normalization;
using EVOLVE_VAPT.Security;
using EVOLVE_VAPT.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;



var builder = WebApplication.CreateBuilder(args);

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

//add the database context and identity services
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDbContext<EVOLVE_VAPTContext>(
    options =>
        options.UseSqlServer(connectionString));

// Configure Identity options
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;

    options.Lockout.MaxFailedAccessAttempts = 5;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(
        "CanManageAdministration",
        policy =>
            policy.RequireRole(
                AppRoles.Administrator));


    options.AddPolicy(
        "CanPerformAssessments",
        policy =>
            policy.RequireRole(
                AppRoles.Administrator,
                AppRoles.SecurityAnalyst));


    options.AddPolicy(
        "CanReviewFindings",
        policy =>
            policy.RequireRole(
                AppRoles.Administrator,
                AppRoles.SecurityAnalyst,
                AppRoles.Reviewer));


    options.AddPolicy(
        "CanViewReports",
        policy =>
            policy.RequireRole(
                AppRoles.Administrator,
                AppRoles.SecurityAnalyst,
                AppRoles.Reviewer,
                AppRoles.ReadOnlyClient));

    // Users who may view Targets & Scope information.
    options.AddPolicy(
        "CanViewTargets",
        policy =>
            policy.RequireRole(
                AppRoles.Administrator,
                AppRoles.SecurityAnalyst,
                AppRoles.Reviewer,
                AppRoles.ReadOnlyClient));


    // Users who may create/edit/deactivate assessment targets.
    options.AddPolicy(
        "CanManageTargets",
        policy =>
            policy.RequireRole(
                AppRoles.Administrator,
                AppRoles.SecurityAnalyst));


    // Target approval is intentionally separate from target creation.
    // This helps preserve separation between testing and approval.
    options.AddPolicy(
        "CanApproveTargets",
        policy =>
            policy.RequireRole(
                AppRoles.Administrator,
                AppRoles.Reviewer));

    options.AddPolicy(
    "CanViewScanProfiles",
    policy =>
        policy.RequireRole(
            AppRoles.Administrator,
            AppRoles.SecurityAnalyst,
            AppRoles.Reviewer,
            AppRoles.ReadOnlyClient
        )
);

    options.AddPolicy(
        "CanManageScanProfiles",
        policy =>
            policy.RequireRole(
                AppRoles.Administrator,
                AppRoles.SecurityAnalyst
            )
    );

    options.AddPolicy(
    "CanViewScanJobs",
    policy =>
        policy.RequireRole(
            AppRoles.Administrator,
            AppRoles.SecurityAnalyst,
            AppRoles.Reviewer,
            AppRoles.ReadOnlyClient
        )
);


    options.AddPolicy(
        "CanCreateScanJobs",
        policy =>
            policy.RequireRole(
                AppRoles.Administrator,
                AppRoles.SecurityAnalyst
            )
    );
   

    options.AddPolicy(
        "CanApproveScanJobs",
        policy =>
            policy.RequireRole(
                AppRoles.Administrator,
                AppRoles.Reviewer
            )
    );


    options.AddPolicy(
        "CanCancelScanJobs",
        policy =>
            policy.RequireRole(
                AppRoles.Administrator,
                AppRoles.SecurityAnalyst
            )
    );

    options.AddPolicy(
    "CanViewFindings",
    policy =>
        policy.RequireRole(
            AppRoles.Administrator,
            AppRoles.SecurityAnalyst,
            AppRoles.Reviewer,
            AppRoles.ReadOnlyClient));

    options.AddPolicy(
    "CanViewRemediation",
    policy =>
        policy.RequireRole(
            AppRoles.Administrator,
            AppRoles.SecurityAnalyst,
            AppRoles.Reviewer,
            AppRoles.ReadOnlyClient));

    options.AddPolicy(
    "CanManageRemediation",
    policy =>
        policy.RequireRole(
            AppRoles.Administrator,
            AppRoles.SecurityAnalyst,
            AppRoles.Reviewer));

    options.AddPolicy(
    "CanViewRetests",
    policy =>
        policy.RequireRole(
            AppRoles.Administrator,
            AppRoles.SecurityAnalyst,
            AppRoles.Reviewer,
            AppRoles.ReadOnlyClient));

    options.AddPolicy(
    "CanCreateRetests",
    policy =>
        policy.RequireRole(
            AppRoles.Administrator,
            AppRoles.SecurityAnalyst));

    options.AddPolicy(
    "CanReviewRetests",
    policy =>
        policy.RequireRole(
            AppRoles.Administrator,
            AppRoles.SecurityAnalyst,
            AppRoles.Reviewer));

    options.AddPolicy(
    "CanViewAuditTrail",
    policy =>
        policy.RequireRole(
            AppRoles.Administrator,
            AppRoles.Reviewer));

});

// Configure cookie settings for authentication 
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";

    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);

    options.SlidingExpiration = true;
});


builder.Services.AddScoped<
    AuditTrailQueryService>();

// Add services to the container.
builder.Services.AddControllersWithViews();
// Assessment module business logic.
builder.Services.AddScoped<AssessmentService>();

// Targets & Scope module business logic.
builder.Services.AddScoped<TargetService>();

// Central audit logging service.
builder.Services.AddScoped<AuditTrailService>();

// User management module business logic.
builder.Services.AddScoped<UserManagementService>();

// Target-specific URL/domain/IP/CIDR validation.
builder.Services.AddScoped<TargetValidationService>();

// Scan Profile module.
builder.Services.AddScoped<ScanProfileService>();

// Scan Job orchestration module.
builder.Services.AddScoped<ScanJobService>();

/*
 * Scanner adapters.
 *
 * No scanner is invoked by MVC controllers.
 * These implementations will be consumed by the
 * background execution worker.
 */

builder.Services.AddScoped<
    IScannerAdapter,
    MockScannerAdapter>();

builder.Services.AddScoped<
    IScannerAdapter,
    NmapScannerAdapter>();

builder.Services.AddScoped<
    IScannerAdapter,
    ZapScannerAdapter>();

builder.Services.AddScoped<
    ScannerAdapterResolver>();

builder.Services.AddScoped<
    ScanJobExecutionService>();

builder.Services.AddHostedService<
    ScanJobExecutionWorker>();

builder.Services.AddScoped<
    IScanResultNormalizer,
    MockScanResultNormalizer>();

builder.Services.AddScoped<
    IScanResultNormalizer,
    NmapResultNormalizer>();

builder.Services.AddScoped<
    IScanResultNormalizer,
    NucleiResultNormalizer>();
builder.Services.AddScoped<
    IScanResultNormalizer,
    ZapResultNormalizer>();

builder.Services.AddScoped<
    IScannerAdapter,
    NucleiScannerAdapter>();

builder.Services.AddScoped<
    ScanResultNormalizerResolver>();

builder.Services.AddScoped<
    FindingNormalizationService>();



builder.Services.AddScoped<FindingService>();

builder.Services.AddScoped<RemediationService>();

builder.Services.AddScoped<RetestService>();

builder.Services.AddScoped<
    RetestStatusSynchronizationService>();

builder.Services.AddScoped<DashboardService>();

builder.Services.AddScoped<ReportService>();


builder.Services
    .AddOptions<ScanExecutionOptions>()
    .Bind(
        builder.Configuration.GetSection(
            ScanExecutionOptions.SectionName))
    .Validate(
        x =>
            x.MaxConcurrentJobs >= 1 &&
            x.MaxConcurrentJobs <= 20,
        "MaxConcurrentJobs must be between 1 and 20.")
    .Validate(
        x =>
            x.HeartbeatSeconds >= 5 &&
            x.HeartbeatSeconds <= 300,
        "HeartbeatSeconds must be between 5 and 300.")
    .Validate(
        x =>
            x.StaleAfterMinutes >= 1 &&
            x.StaleAfterMinutes <= 60,
        "StaleAfterMinutes must be between 1 and 60.")
    .ValidateOnStart();


builder.Services.AddSingleton<
    IExternalProcessRunner,
    ExternalProcessRunner>();


builder.Services
    .AddOptions<NmapExecutionOptions>()
    .Bind(
        builder.Configuration.GetSection(
            NmapExecutionOptions.SectionName))
    .Validate(
        x =>
            !string.IsNullOrWhiteSpace(
                x.ExecutablePath),
        "Nmap executable path is required.")
    .Validate(
        x =>
            x.MaxStandardOutputCharacters >= 10_000,
        "Nmap output limit is too small.")
    .Validate(
        x =>
            x.MaxStandardErrorCharacters >= 1_000,
        "Nmap error-output limit is too small.")
    .Validate(
        x =>
            x.MaxTopPorts >= 1 &&
            x.MaxTopPorts <= 65535,
        "Nmap MaxTopPorts must be between 1 and 65535.")
    .Validate(
        x =>
            x.MinimumIpv4CidrPrefix >= 0 &&
            x.MinimumIpv4CidrPrefix <= 32,
        "Invalid IPv4 CIDR safety limit.")
    .Validate(
        x =>
            x.MinimumIpv6CidrPrefix >= 0 &&
            x.MinimumIpv6CidrPrefix <= 128,
        "Invalid IPv6 CIDR safety limit.")
    .ValidateOnStart();

builder.Services
    .AddOptions<NucleiExecutionOptions>()
    .Bind(
        builder.Configuration.GetSection(
            NucleiExecutionOptions.SectionName))
    .Validate(
        x =>
            !string.IsNullOrWhiteSpace(
                x.ExecutablePath),
        "Nuclei executable path is required.")
    .Validate(
        x =>
            !string.IsNullOrWhiteSpace(
                x.TemplatesPath),
        "Nuclei templates path is required.")
    .Validate(
        x =>
            x.MaxRequestsPerSecond >= 1 &&
            x.MaxRequestsPerSecond <= 500,
        "Nuclei request rate must be between 1 and 500.")
    .Validate(
        x =>
            x.MaxTemplateConcurrency >= 1 &&
            x.MaxTemplateConcurrency <= 100,
        "Nuclei template concurrency must be between 1 and 100.")
    .Validate(
        x =>
            x.RequestTimeoutSeconds >= 1 &&
            x.RequestTimeoutSeconds <= 120,
        "Nuclei request timeout must be between 1 and 120 seconds.")
    .Validate(
        x =>
            x.Retries >= 0 &&
            x.Retries <= 5,
        "Nuclei retries must be between 0 and 5.")
    .ValidateOnStart();

builder.Services
    .AddOptions<ZapExecutionOptions>()
    .Bind(
        builder.Configuration.GetSection(
            ZapExecutionOptions.SectionName))
    .Validate(
        x =>
            !string.IsNullOrWhiteSpace(
                x.JavaExecutablePath),
        "ZAP Java executable path is required.")
    .Validate(
        x =>
            Path.IsPathFullyQualified(
                x.JavaExecutablePath) &&
            File.Exists(
                x.JavaExecutablePath),
        "ZAP Java executable must be a fully qualified path to an existing file.")
    .Validate(
        x =>
            !string.IsNullOrWhiteSpace(
                x.InstallationDirectory),
        "ZAP installation directory is required.")
    .Validate(
        x =>
            Path.IsPathFullyQualified(
                x.InstallationDirectory) &&
            Directory.Exists(
                x.InstallationDirectory),
        "ZAP installation directory must be a fully qualified existing directory.")
    .Validate(
        x =>
            !string.IsNullOrWhiteSpace(
                x.JarPath),
        "ZAP JAR path is required.")
    .Validate(
        x =>
            Path.IsPathFullyQualified(
                x.JarPath) &&
            File.Exists(
                x.JarPath) &&
            string.Equals(
                Path.GetExtension(
                    x.JarPath),
                ".jar",
                StringComparison.OrdinalIgnoreCase),
        "ZAP JAR must be a fully qualified path to an existing .jar file.")
    .Validate(
        x =>
            x.MaxHeapMegabytes >= 256 &&
            x.MaxHeapMegabytes <= 4096,
        "ZAP maximum heap must be between 256 MB and 4096 MB.")
    .Validate(
        x =>
            x.MaxStandardOutputCharacters >=
            10_000,
        "ZAP output limit is too small.")
    .Validate(
        x =>
            x.MaxStandardErrorCharacters >=
            1_000,
        "ZAP error-output limit is too small.")
    .Validate(
        x =>
            x.RequestTimeoutSeconds >= 1 &&
            x.RequestTimeoutSeconds <= 120,
        "ZAP request timeout must be between 1 and 120 seconds.")
    .Validate(
        x =>
            x.MaxSpiderMinutes >= 1 &&
            x.MaxSpiderMinutes <= 60,
        "ZAP spider limit must be between 1 and 60 minutes.")
    .ValidateOnStart();




var app = builder.Build();

////check if the application is running in development mode and run a test external process
//if (app.Environment.IsDevelopment())
//{
//    var runner =
//        app.Services
//            .GetRequiredService<
//                IExternalProcessRunner>();


//    var systemDirectory =
//        Environment.GetFolderPath(
//            Environment.SpecialFolder.System);


//    var result =
//        await runner.RunAsync(
//            new ExternalProcessRequest
//            {
//                ExecutablePath =
//    Path.Combine(
//        Environment.GetFolderPath(
//            Environment.SpecialFolder.System),
//        "cmd.exe"),

//                Timeout =
//                    TimeSpan.FromSeconds(10),

//                MaxStandardOutputCharacters =
//                    10_000,

//                MaxStandardErrorCharacters =
//                    10_000
//            },
//            CancellationToken.None);


//    app.Logger.LogInformation(
//        "ExternalProcessRunner QA: Started={Started}, ExitCode={ExitCode}, Output={Output}",
//        result.Started,
//        result.ExitCode,
//        result.StandardOutput.Trim());
//}

//using (var scope = app.Services.CreateScope())
//{
//    await IdentitySeeder.SeedAdminAsync(
//        scope.ServiceProvider);
//}

//using (var scope =
//    app.Services.CreateScope())
//{
//    await IdentitySeeder.SeedAsync(
//        scope.ServiceProvider,
//        app.Configuration);
//}


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

//app.UseHttpsRedirection();
//app.UseRouting();

//app.UseAuthorization();

app.UseHttpsRedirection();

app.UseRouting(); 

app.UseAuthentication();

app.UseAuthorization();


app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
