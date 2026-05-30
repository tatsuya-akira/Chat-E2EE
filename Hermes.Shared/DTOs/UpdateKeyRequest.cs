using System;
using System.Collections.Generic;
using System.Text;

namespace Hermes.Shared.DTOs
{
    public class UpdateKeyRequest
    {
        public string UserId { get; set; }
        public string PublicKey { get; set; }
        public string WrappedPrivateKey { get; set; }
        public string Salt { get; set; }
    }
}
