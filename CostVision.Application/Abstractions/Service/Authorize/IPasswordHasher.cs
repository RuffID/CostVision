namespace CostVision.Application.Abstractions.Service.Authorize
{
    public interface IPasswordHasher
    {
        string Hash(string input);

        bool Verify(string input, string hash);
    }
}
