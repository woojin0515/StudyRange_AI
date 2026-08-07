using Npgsql;

namespace StudyRange.Infrastructure.Persistence;

public sealed class PostgreSqlInfrastructureInitializer : IInfrastructureInitializer
{
    private readonly PostgreSqlConnectionFactory _connectionFactory;

    public PostgreSqlInfrastructureInitializer(PostgreSqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
                           CREATE TABLE IF NOT EXISTS workspaces (
                               id UUID PRIMARY KEY,
                               name TEXT NOT NULL,
                               created_at_utc TIMESTAMPTZ NOT NULL
                           );

                           CREATE TABLE IF NOT EXISTS exam_ranges (
                               id UUID PRIMARY KEY,
                               workspace_id UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
                               subject TEXT NOT NULL,
                               start_page INTEGER NOT NULL,
                               end_page INTEGER NOT NULL,
                               created_at_utc TIMESTAMPTZ NOT NULL
                           );

                           CREATE TABLE IF NOT EXISTS document_assets (
                               id UUID PRIMARY KEY,
                               workspace_id UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
                               document_type INTEGER NOT NULL,
                               original_file_name TEXT NOT NULL,
                               stored_path TEXT NOT NULL,
                               size_in_bytes BIGINT NOT NULL,
                               uploaded_at_utc TIMESTAMPTZ NOT NULL,
                               processing_status INTEGER NOT NULL,
                               processing_summary TEXT NULL
                           );

                           CREATE TABLE IF NOT EXISTS processing_jobs (
                               id UUID PRIMARY KEY,
                               workspace_id UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
                               document_id UUID NOT NULL,
                               status INTEGER NOT NULL,
                               error_message TEXT NULL,
                               created_at_utc TIMESTAMPTZ NOT NULL,
                               started_at_utc TIMESTAMPTZ NULL,
                               completed_at_utc TIMESTAMPTZ NULL
                           );

                           CREATE TABLE IF NOT EXISTS generated_contents (
                               id UUID PRIMARY KEY,
                               workspace_id UUID NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
                               exam_range_id UUID NOT NULL,
                               subject TEXT NOT NULL,
                               start_page INTEGER NOT NULL,
                               end_page INTEGER NOT NULL,
                               content_type TEXT NOT NULL,
                               content TEXT NOT NULL,
                               provider TEXT NOT NULL,
                               model TEXT NOT NULL,
                               generated_at_utc TIMESTAMPTZ NOT NULL
                           );

                           CREATE INDEX IF NOT EXISTS ix_workspaces_created_at_utc_desc ON workspaces(created_at_utc DESC);
                           CREATE INDEX IF NOT EXISTS ix_exam_ranges_workspace_id_created_at_utc_desc ON exam_ranges(workspace_id, created_at_utc DESC);
                           CREATE INDEX IF NOT EXISTS ix_document_assets_workspace_id_uploaded_at_utc_desc ON document_assets(workspace_id, uploaded_at_utc DESC);
                           CREATE INDEX IF NOT EXISTS ix_processing_jobs_workspace_id_created_at_utc_desc ON processing_jobs(workspace_id, created_at_utc DESC);
                           CREATE INDEX IF NOT EXISTS ix_generated_contents_workspace_id_generated_at_utc_desc ON generated_contents(workspace_id, generated_at_utc DESC);
                           """;

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
