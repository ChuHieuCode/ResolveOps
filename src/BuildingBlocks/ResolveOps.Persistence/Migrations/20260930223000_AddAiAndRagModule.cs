using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ResolveOps.Persistence.Migrations;

/// <inheritdoc />
public partial class AddAiAndRagModule : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ai_tasks",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                tenant_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                task_type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                source_entity_type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                source_entity_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                model_provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                model_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                prompt_version = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                input_hash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                output_json = table.Column<string>(type: "nvarchar(max)", nullable: true),
                confidence = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                failure_code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                completed_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                review_status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                reviewed_by = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                reviewed_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                concurrency_stamp = table.Column<string>(type: "nvarchar(36)", maxLength: 36, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ai_tasks", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "ai_feedback",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                tenant_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ai_task_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                field_path = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                original_value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                corrected_value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                feedback_type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                reviewer_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ai_feedback", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "ai_knowledge_embeddings",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                tenant_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                knowledge_type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                carrier_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                content_chunk = table.Column<string>(type: "nvarchar(max)", nullable: false),
                embedding_vector_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                metadata_json = table.Column<string>(type: "nvarchar(max)", nullable: false),
                created_at_utc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ai_knowledge_embeddings", x => x.id);
            });

        // Indexes for ai_tasks
        migrationBuilder.CreateIndex(
            name: "ix_ai_tasks_tenant_task_status",
            table: "ai_tasks",
            columns: new[] { "tenant_id", "task_type", "status" });

        migrationBuilder.CreateIndex(
            name: "ix_ai_tasks_tenant_source",
            table: "ai_tasks",
            columns: new[] { "tenant_id", "source_entity_type", "source_entity_id" });

        migrationBuilder.CreateIndex(
            name: "ix_ai_tasks_tenant_review_status",
            table: "ai_tasks",
            columns: new[] { "tenant_id", "review_status" });

        // Indexes for ai_feedback
        migrationBuilder.CreateIndex(
            name: "ix_ai_feedback_tenant_task",
            table: "ai_feedback",
            columns: new[] { "tenant_id", "ai_task_id" });

        // Indexes for ai_knowledge_embeddings
        migrationBuilder.CreateIndex(
            name: "ix_ai_knowledge_tenant_type_carrier",
            table: "ai_knowledge_embeddings",
            columns: new[] { "tenant_id", "knowledge_type", "carrier_id" });

        migrationBuilder.CreateIndex(
            name: "ix_ai_knowledge_tenant_carrier",
            table: "ai_knowledge_embeddings",
            columns: new[] { "tenant_id", "carrier_id" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ai_knowledge_embeddings");
        migrationBuilder.DropTable(name: "ai_feedback");
        migrationBuilder.DropTable(name: "ai_tasks");
    }
}
