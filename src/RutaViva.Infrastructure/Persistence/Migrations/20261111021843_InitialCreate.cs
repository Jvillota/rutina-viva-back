using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RutaViva.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "daily_check_runs",
                columns: table => new
                {
                    business_date = table.Column<DateOnly>(type: "date", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    routines_changed = table.Column<int>(type: "integer", nullable: false),
                    orders_created = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_daily_check_runs", x => x.business_date);
                });

            migrationBuilder.CreateTable(
                name: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    outbox_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    message = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: true),
                    service_order_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notifications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "system_clock",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    offset_seconds = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_system_clock", x => x.id);
                    table.CheckConstraint("ck_system_clock_offset_forward", "offset_seconds >= 0");
                    table.CheckConstraint("ck_system_clock_singleton", "id = 1");
                });

            migrationBuilder.CreateTable(
                name: "workshops",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workshops", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vehicles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    plate = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    workshop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    odometer_km = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vehicles", x => x.id);
                    table.CheckConstraint("ck_vehicles_odometer_not_negative", "odometer_km >= 0");
                    table.ForeignKey(
                        name: "fk_vehicles_workshops_workshop_id",
                        column: x => x.workshop_id,
                        principalTable: "workshops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "maintenance_routines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    interval_km = table.Column<int>(type: "integer", nullable: true),
                    interval_months = table.Column<int>(type: "integer", nullable: true),
                    cycle_start_km = table.Column<int>(type: "integer", nullable: false),
                    cycle_start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    due_soon_notified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_maintenance_routines", x => x.id);
                    table.CheckConstraint("ck_routines_interval", "interval_km IS NOT NULL OR interval_months IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_maintenance_routines_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "odometer_readings",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    km = table.Column<int>(type: "integer", nullable: false),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_odometer_readings", x => x.id);
                    table.ForeignKey(
                        name: "fk_odometer_readings_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_id = table.Column<Guid>(type: "uuid", nullable: false),
                    routine_id = table.Column<Guid>(type: "uuid", nullable: false),
                    workshop_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vehicle_plate = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    routine_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quote_amount_cop = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    approval_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    service_km = table.Column<int>(type: "integer", nullable: true),
                    service_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_on = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_orders", x => x.id);
                    table.ForeignKey(
                        name: "fk_service_orders_maintenance_routines_routine_id",
                        column: x => x.routine_id,
                        principalTable: "maintenance_routines",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_orders_vehicles_vehicle_id",
                        column: x => x.vehicle_id,
                        principalTable: "vehicles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_service_orders_workshops_workshop_id",
                        column: x => x.workshop_id,
                        principalTable: "workshops",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_order_transitions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    from_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    to_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    service_order_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_order_transitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_service_order_transitions_service_orders_service_order_id",
                        column: x => x.service_order_id,
                        principalTable: "service_orders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_routines_vehicle_name",
                table: "maintenance_routines",
                columns: new[] { "vehicle_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_created_at",
                table: "notifications",
                column: "created_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ux_notifications_outbox",
                table: "notifications",
                column: "outbox_message_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_odometer_readings_vehicle",
                table: "odometer_readings",
                columns: new[] { "vehicle_id", "received_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_pending",
                table: "outbox_messages",
                column: "occurred_at",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_service_order_transitions_service_order_id",
                table: "service_order_transitions",
                column: "service_order_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_orders_routine_id",
                table: "service_orders",
                column: "routine_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_orders_status",
                table: "service_orders",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_service_orders_workshop_id",
                table: "service_orders",
                column: "workshop_id");

            migrationBuilder.CreateIndex(
                name: "ux_service_orders_open_per_routine",
                table: "service_orders",
                columns: new[] { "vehicle_id", "routine_id" },
                unique: true,
                filter: "status NOT IN ('Completed', 'Cancelled')");

            migrationBuilder.CreateIndex(
                name: "ix_vehicles_workshop_id",
                table: "vehicles",
                column: "workshop_id");

            migrationBuilder.CreateIndex(
                name: "ux_vehicles_plate",
                table: "vehicles",
                column: "plate",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_check_runs");

            migrationBuilder.DropTable(
                name: "notifications");

            migrationBuilder.DropTable(
                name: "odometer_readings");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "service_order_transitions");

            migrationBuilder.DropTable(
                name: "system_clock");

            migrationBuilder.DropTable(
                name: "service_orders");

            migrationBuilder.DropTable(
                name: "maintenance_routines");

            migrationBuilder.DropTable(
                name: "vehicles");

            migrationBuilder.DropTable(
                name: "workshops");
        }
    }
}
