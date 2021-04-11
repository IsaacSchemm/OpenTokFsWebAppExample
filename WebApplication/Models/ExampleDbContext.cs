using Microsoft.EntityFrameworkCore;

namespace WebApplication.Models {
    public class ExampleDbContext : DbContext {
        public ExampleDbContext(DbContextOptions<ExampleDbContext> options)
            : base(options) {

        }

        public DbSet<VonageVideoAPIProjectCredential> VonageVideoAPIProjectCredentials { get; set; }
        public DbSet<VonageVideoAPISession> VonageVideoAPISessions { get; set; }
    }
}
