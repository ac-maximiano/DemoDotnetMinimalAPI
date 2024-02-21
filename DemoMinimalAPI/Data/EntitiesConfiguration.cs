using DemoMinimalAPI.Models;
using Microsoft.EntityFrameworkCore;

namespace DemoMinimalAPI.Data
{
    public static class EntitiesConfiguration
    {
        public static ModelBuilder ConfigureEntities(this ModelBuilder builder)
        {
            builder.Entity<Fornecedor>()
                .HasKey(f => f.Id);

            builder.Entity<Fornecedor>()
                .Property(p => p.Nome)
                .IsRequired()
                .HasColumnType("varchar(200)");

            builder.Entity<Fornecedor>()
                .Property(f => f.Documento)
                .IsRequired()
                .HasColumnType("varchar(14)");

            builder.Entity<Fornecedor>()
                .ToTable("Fornecedores");

            return builder;
        }
    }
}
