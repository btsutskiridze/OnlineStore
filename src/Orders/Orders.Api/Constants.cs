namespace Orders.Api
{
    public static class UserRoles
    {
        public const string User = "User";
        public const string Admin = "Admin";
        public const string AdminOrUser = $"{Admin},{User}";
    }

    public static class SqlErrorCodes
    {
        // Constraint violations
        public const int UniqueConstraintViolation = 2601;
        public const int UniqueIndexViolation = 2627;
    }

}
