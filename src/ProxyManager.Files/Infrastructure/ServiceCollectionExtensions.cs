using Amazon.Runtime;
using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using West94.ProxyManager.Files.Data;
using West94.ProxyManager.Files.Options;
using West94.ProxyManager.Files.Repositories;
using West94.ProxyManager.Files.Services;
using West94.ProxyManager.Files.Storage;
using West94.ProxyManager.Files.Validation;

namespace West94.ProxyManager.Files.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers the S3-backed object store and its supporting options against the configured RustFS endpoint.</summary>
    public static IServiceCollection AddFilesServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ObjectStorageOptions>(configuration.GetSection(ObjectStorageOptions.Section));

        services.AddSingleton<IAmazonS3>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<ObjectStorageOptions>>().Value;

            var config = new AmazonS3Config
            {
                ServiceURL = options.ServiceUrl,
                ForcePathStyle = options.ForcePathStyle,
                AuthenticationRegion = options.Region,
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            };

            var credentials = new BasicAWSCredentials(options.AccessKey, options.SecretKey);
            return new AmazonS3Client(credentials, config);
        });

        services.AddSingleton<IObjectStore, S3ObjectStore>();

        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.Section));
        services.AddDbContext<FilesDbContext>((sp, options) =>
        {
            var cs = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString;
            options.UseNpgsql(cs, o =>
            {
                o.MigrationsAssembly(typeof(FilesDbContext).Assembly.FullName);
                o.MigrationsHistoryTable("__ef_migrations_history", "files");
            });
        });

        services.AddScoped<IFileAssetRepository, PostgresFileAssetRepository>();

        services.Configure<UploadOptions>(configuration.GetSection(UploadOptions.Section));
        services.AddSingleton<UploadContentValidator>();
        services.AddScoped<IFileAssetService, FileAssetService>();

        return services;
    }
}
