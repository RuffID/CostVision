namespace CostVision.Infrastructure.Models.Constants
{
    public static class HashSettingsConstants
    {
        public const ushort SALT_SIZE = 16;
        public const ushort KEY_SIZE = 32;
        public const int ITERATIONS = 100000;
        public const char SEPARATOR = ':';
        public const string ALGORITHM = "SHA256";
    }
}
