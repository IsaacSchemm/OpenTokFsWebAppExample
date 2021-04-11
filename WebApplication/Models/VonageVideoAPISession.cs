using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication.Models {
    public class VonageVideoAPISession {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public string SessionId { get; set; }

        [Required]
        public VonageVideoAPIProjectCredential Project { get; set; }
    }
}
