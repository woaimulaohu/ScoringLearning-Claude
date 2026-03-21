using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CCTaskScoring.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "tasks",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", nullable: false),
                    language = table.Column<string>(type: "TEXT", nullable: true),
                    framework = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    completed_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    artifacts = table.Column<string>(type: "TEXT", nullable: true),
                    logs = table.Column<string>(type: "TEXT", nullable: true),
                    test_results = table.Column<string>(type: "TEXT", nullable: true),
                    static_analysis = table.Column<string>(type: "TEXT", nullable: true),
                    metadata = table.Column<string>(type: "TEXT", nullable: true),
                    status = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "pending")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tasks", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rewards_punishments",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    task_id = table.Column<string>(type: "TEXT", nullable: false),
                    action_type = table.Column<string>(type: "TEXT", nullable: false),
                    reason = table.Column<string>(type: "TEXT", nullable: true),
                    applied_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    expiry = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rewards_punishments", x => x.id);
                    table.ForeignKey(
                        name: "FK_rewards_punishments_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scores",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    task_id = table.Column<string>(type: "TEXT", nullable: false),
                    completion_score = table.Column<double>(type: "REAL", nullable: false),
                    correctness_score = table.Column<double>(type: "REAL", nullable: false),
                    quality_score = table.Column<double>(type: "REAL", nullable: false),
                    efficiency_score = table.Column<double>(type: "REAL", nullable: false),
                    ux_score = table.Column<double>(type: "REAL", nullable: false),
                    total_score = table.Column<double>(type: "REAL", nullable: false),
                    auto_scored = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    reviewer_comments = table.Column<string>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scores", x => x.id);
                    table.ForeignKey(
                        name: "FK_scores_tasks_task_id",
                        column: x => x.task_id,
                        principalTable: "tasks",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rewards_punishments_task_id",
                table: "rewards_punishments",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "IX_scores_task_id",
                table: "scores",
                column: "task_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rewards_punishments");

            migrationBuilder.DropTable(
                name: "scores");

            migrationBuilder.DropTable(
                name: "tasks");
        }
    }
}
