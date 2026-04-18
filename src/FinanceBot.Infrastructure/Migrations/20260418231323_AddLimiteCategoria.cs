using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FinanceBot.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLimiteCategoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "limite_categorias",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    categoria = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    valor = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_limite_categorias", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_limite_categorias_categoria",
                table: "limite_categorias",
                column: "categoria",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "limite_categorias");
        }
    }
}
