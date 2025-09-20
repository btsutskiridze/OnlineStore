using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace ProductCatalog.Api.Resilience
{
    public static class ProductStockChangeRetryPipeline
    {
        public static void Configure(ResiliencePipelineBuilder builder)
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential,
                MaxDelay = TimeSpan.FromSeconds(2),
                ShouldHandle = new PredicateBuilder()
                        .Handle<SqlException>(ex => IsTransientSqlException(ex))
                        .Handle<DbUpdateException>(ex => IsTransientDbException(ex)),
                OnRetry = args =>
                {
                    Console.WriteLine($"Retry attempt {args.AttemptNumber} for database operation. Exception: {args.Outcome.Exception?.Message}");
                    return ValueTask.CompletedTask;
                }
            })
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(8)
            });
        }

        private static bool IsTransientSqlException(SqlException ex)
        {
            return ex.Number switch
            {
                1205 => true, // Deadlock
                1222 => true, // Lock timeout
                2 => true,    // Connection timeout
                53 => true,   // Network error
                -2 => true,   // Timeout
                _ => false
            };
        }

        private static bool IsTransientDbException(DbUpdateException ex)
        {
            return ex.InnerException is SqlException sqlEx && IsTransientSqlException(sqlEx);
        }
    }
}
