using Microsoft.EntityFrameworkCore.Migrations;
using System;

namespace ThreeDPayment.Sample.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Banks",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(nullable: true),
                    SystemName = table.Column<string>(nullable: true),
                    BankCode = table.Column<int>(nullable: false),
                    LogoPath = table.Column<string>(nullable: true),
                    UseCommonPaymentPage = table.Column<bool>(nullable: false),
                    DefaultBank = table.Column<bool>(nullable: false),
                    Active = table.Column<bool>(nullable: false),
                    CreateDate = table.Column<DateTime>(nullable: false),
                    UpdateDate = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Banks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BankParameters",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BankId = table.Column<int>(nullable: false),
                    Key = table.Column<string>(nullable: true),
                    Value = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankParameters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankParameters_Banks_BankId",
                        column: x => x.BankId,
                        principalTable: "Banks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreditCards",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BankId = table.Column<int>(nullable: false),
                    Name = table.Column<string>(nullable: true),
                    Active = table.Column<bool>(nullable: false),
                    ManufacturerCard = table.Column<bool>(nullable: false),
                    CampaignCard = table.Column<bool>(nullable: false),
                    Deleted = table.Column<bool>(nullable: false),
                    CreateDate = table.Column<DateTime>(nullable: false),
                    UpdateDate = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditCards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditCards_Banks_BankId",
                        column: x => x.BankId,
                        principalTable: "Banks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PaymentTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrderNumber = table.Column<string>(nullable: true),
                    TransactionNumber = table.Column<string>(nullable: true),
                    ReferenceNumber = table.Column<string>(nullable: true),
                    UserIpAddress = table.Column<string>(nullable: true),
                    UserAgent = table.Column<string>(nullable: true),
                    BankId = table.Column<int>(nullable: false),
                    CardPrefix = table.Column<string>(nullable: true),
                    CardHolderName = table.Column<string>(nullable: true),
                    Installment = table.Column<int>(nullable: false),
                    ExtraInstallment = table.Column<int>(nullable: false),
                    TotalAmount = table.Column<decimal>(nullable: false),
                    BankErrorMessage = table.Column<string>(nullable: true),
                    PaidDate = table.Column<DateTime>(nullable: true),
                    CreateDate = table.Column<DateTime>(nullable: false),
                    StatusId = table.Column<int>(nullable: false),
                    Deleted = table.Column<bool>(nullable: false),
                    BankRequest = table.Column<string>(nullable: true),
                    BankResponse = table.Column<string>(nullable: true),
                    Status = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_Banks_BankId",
                        column: x => x.BankId,
                        principalTable: "Banks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreditCardInstallments",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreditCardId = table.Column<int>(nullable: false),
                    Installment = table.Column<int>(nullable: false),
                    InstallmentRate = table.Column<decimal>(nullable: false),
                    Active = table.Column<bool>(nullable: false),
                    Deleted = table.Column<bool>(nullable: false),
                    CreateDate = table.Column<DateTime>(nullable: false),
                    UpdateDate = table.Column<DateTime>(nullable: false),
                    BankId = table.Column<int>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditCardInstallments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditCardInstallments_Banks_BankId",
                        column: x => x.BankId,
                        principalTable: "Banks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditCardInstallments_CreditCards_CreditCardId",
                        column: x => x.CreditCardId,
                        principalTable: "CreditCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreditCardPrefixes",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CreditCardId = table.Column<int>(nullable: false),
                    Prefix = table.Column<string>(nullable: true),
                    Active = table.Column<bool>(nullable: false),
                    Deleted = table.Column<bool>(nullable: false),
                    CreateDate = table.Column<DateTime>(nullable: false),
                    UpdateDate = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditCardPrefixes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreditCardPrefixes_CreditCards_CreditCardId",
                        column: x => x.CreditCardId,
                        principalTable: "CreditCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankParameters_BankId",
                table: "BankParameters",
                column: "BankId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditCardInstallments_BankId",
                table: "CreditCardInstallments",
                column: "BankId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditCardInstallments_CreditCardId",
                table: "CreditCardInstallments",
                column: "CreditCardId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditCardPrefixes_CreditCardId",
                table: "CreditCardPrefixes",
                column: "CreditCardId");

            migrationBuilder.CreateIndex(
                name: "IX_CreditCards_BankId",
                table: "CreditCards",
                column: "BankId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_BankId",
                table: "PaymentTransactions",
                column: "BankId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankParameters");

            migrationBuilder.DropTable(
                name: "CreditCardInstallments");

            migrationBuilder.DropTable(
                name: "CreditCardPrefixes");

            migrationBuilder.DropTable(
                name: "PaymentTransactions");

            migrationBuilder.DropTable(
                name: "CreditCards");

            migrationBuilder.DropTable(
                name: "Banks");
        }
    }
}
