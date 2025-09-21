using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace ProductCatalog.Api.Resilience
{
    public static class DatabaseRetryPipeline
    {
        public static void Configure(ResiliencePipelineBuilder builder)
        {
            builder
                .AddRetry(CreateRetryStrategyOptions())
                .AddTimeout(CreateTimeoutStrategyOptions());
        }

        private static RetryStrategyOptions CreateRetryStrategyOptions()
        {
            return new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(100),
                BackoffType = DelayBackoffType.Exponential,
                MaxDelay = TimeSpan.FromSeconds(2),
                ShouldHandle = new PredicateBuilder()
                    .Handle<SqlException>(IsTransientSqlException)
                    .Handle<DbUpdateException>(IsTransientDbUpdateException),
                OnRetry = args =>
                {
                    Console.WriteLine($"Retry attempt {args.AttemptNumber} for database operation. Exception: {args.Outcome.Exception?.Message}");
                    return ValueTask.CompletedTask;
                }
            };
        }

        private static TimeoutStrategyOptions CreateTimeoutStrategyOptions()
        {
            return new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(8)
            };
        }

        private static bool IsTransientSqlException(SqlException exception)
        {
            return exception.Number switch
            {
                SqlErrorCodes.Deadlock => true,
                SqlErrorCodes.LockTimeout => true,
                SqlErrorCodes.ConnectionTimeout => true,
                SqlErrorCodes.NetworkError => true,
                SqlErrorCodes.Timeout => true,
                _ => false
            };
        }

        private static bool IsTransientDbUpdateException(DbUpdateException exception)
        {
            return exception.InnerException is SqlException sqlException &&
                   IsTransientSqlException(sqlException);
        }
    }
}
