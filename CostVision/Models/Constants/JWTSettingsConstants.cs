namespace CostVision.Models.Constants
{
    public static class JWTSettingsConstants
    {
        public const string ISSUER = "Server";
        public const string AUDIENCE = "Client";
        public const int ACCESS_TOKEN_LIFE_TIME_FROM_MINUTES = 120;
        public const int REFRESH_TOKEN_LIFE_TIME_FROM_DAYS = 14;
    }
}
