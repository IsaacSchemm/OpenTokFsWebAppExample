using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication.Models {
    public class VonageVideoAPIProjectCredential : OpenTokFs.Credentials.IProjectCredentials {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int ApiKey { get; set; }

        [Required]
        public string ApiSecret { get; set; }
    }
}
