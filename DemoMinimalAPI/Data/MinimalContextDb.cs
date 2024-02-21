using DemoMinimalAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DemoMinimalAPI.Data
{
    public class MinimalContextDb : DbContext
    {
        public MinimalContextDb(DbContextOptions<MinimalContextDb> options) : base(options) { }

        public DbSet<Fornecedor> Fornecedores { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.ConfigureEntities();
            base.OnModelCreating(builder);
        }
    }
}
