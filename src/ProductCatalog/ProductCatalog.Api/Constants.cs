namespace ProductCatalog.Api
{
    public static class UserRoles
    {
        public const string User = "User";
        public const string Admin = "Admin";
        public const string AdminOrUser = $"{Admin},{User}";
    }

    public static class Policies
    {
        public const string InterServiceAccessOnly = "InterServiceAccessOnly";
    }

    public static class SqlErrorCodes
    {
        // Constraint violations
        public const int UniqueConstraintViolation = 2601;
        public const int UniqueIndexViolation = 2627;

        // Transient errors (retryable)
        public const int Deadlock = 1205;
        public const int LockTimeout = 1222;
        public const int ConnectionTimeout = 2;
        public const int NetworkError = 53;
        public const int Timeout = -2;
    }

    public static class ResiliencePipelines
    {
        public const string DatabaseOperations = "database-operations";
    }
}