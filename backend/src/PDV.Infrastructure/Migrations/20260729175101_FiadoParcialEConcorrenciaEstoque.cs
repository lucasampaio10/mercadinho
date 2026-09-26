using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PDV.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FiadoParcialEConcorrenciaEstoque : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTA: o scaffold gerou AddColumn<uint>("xmin") para produtos e clientes.
            // Foram removidos de propósito: xmin é coluna de sistema do PostgreSQL,
            // já existe em toda tabela, e o ALTER TABLE falharia com
            // "column name \"xmin\" conflicts with a system column name".
            // A propriedade continua no modelo (UseXminAsConcurrencyToken), que é o que
            // faz o EF incluí-la no WHERE dos UPDATEs — nada precisa ser criado no banco.

            migrationBuilder.AddColumn<decimal>(
                name: "ValorPago",
                table: "itens_fiado",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "pagamentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uuid", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pagamentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pagamentos_clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pagamentos_ClienteId",
                table: "pagamentos",
                column: "ClienteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pagamentos");

            migrationBuilder.DropColumn(
                name: "ValorPago",
                table: "itens_fiado");

            // Sem DropColumn de xmin: nunca foi criada (ver nota no Up).
        }
    }
}
