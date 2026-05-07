using System.ComponentModel.DataAnnotations;

namespace Hermes.Shared.DTOs
{
    public class RegisterRequest
    {
        [Required]
        public string Id { get; set; } = string.Empty;
        
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
        
        [Required]
        public string FullName { get; set; } = string.Empty;
        
        [Required]
        public string PublicKey { get; set; } = string.Empty;
        
        [Required]
        public string WrappedPrivateKey { get; set; } = string.Empty;
        
        [Required]
        public string Salt { get; set; } = string.Empty;
    }
}
