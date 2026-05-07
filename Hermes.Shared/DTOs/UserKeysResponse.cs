namespace Hermes.Shared.DTOs
{
    public class UserKeysResponse
    {
        public string PublicKey { get; set; } = string.Empty;
        public string WrappedPrivateKey { get; set; } = string.Empty;
        public string Salt { get; set; } = string.Empty;
    }
}
