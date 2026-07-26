using Npgsql;
using StudyRange.Application.Contracts;
using StudyRange.Domain.Entities;

namespace StudyRange.Infrastructure.Persistence;

public sealed class PostgreSqlProcessingJobRepository : IProcessingJobRepository
{
    private readonly PostgreSqlConnectionFactory _connectionFactory;

    public PostgreSqlProcessingJobRepository(PostgreSqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task AddAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
                           INSERT INTO processing_jobs (
                               id, workspace_id, document_id, status, error_message, created_at_utc, started_at_utc, completed_at_utc)
                           VALUES (
                               @id, @workspace_id, @document_id, @status, @error_message, @created_at_utc, @started_at_utc, @completed_at_utc)
                           """;
        await using var command = new NpgsqlCommand(sql, connection);
        Bind(command, job);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
                           UPDATE processing_jobs
                           SET status = @status,
                               error_message = @error_message,
                               started_at_utc = @started_at_utc,
                               completed_at_utc = @completed_at_utc
                           WHERE id = @id
                           """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", job.Id);
        command.Parameters.AddWithValue("status", (int)job.Status);
        command.Parameters.AddWithValue("error_message", (object?)job.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("started_at_utc", (object?)job.StartedAtUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("completed_at_utc", (object?)job.CompletedAtUtc ?? DBNull.Value);
        _ = await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ProcessingJob?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
                           SELECT id, workspace_id, document_id, status, error_message, created_at_utc, started_at_utc, completed_at_utc
                           FROM processing_jobs
                           WHERE id = @id
                           """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", jobId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return Map(reader);
    }

    public async Task<IReadOnlyList<ProcessingJob>> ListByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var result = new List<ProcessingJob>();

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
                           SELECT id, workspace_id, document_id, status, error_message, created_at_utc, started_at_utc, completed_at_utc
                           FROM processing_jobs
                           WHERE workspace_id = @workspace_id
                           ORDER BY created_at_utc DESC
                           """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workspace_id", workspaceId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(Map(reader));
        }

        return result;
    }

    private static void Bind(NpgsqlCommand command, ProcessingJob job)
    {
        command.Parameters.AddWithValue("id", job.Id);
        command.Parameters.AddWithValue("workspace_id", job.WorkspaceId);
        command.Parameters.AddWithValue("document_id", job.DocumentId);
        command.Parameters.AddWithValue("status", (int)job.Status);
        command.Parameters.AddWithValue("error_message", (object?)job.ErrorMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("created_at_utc", job.CreatedAtUtc);
        command.Parameters.AddWithValue("started_at_utc", (object?)job.StartedAtUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("completed_at_utc", (object?)job.CompletedAtUtc ?? DBNull.Value);
    }

    private static ProcessingJob Map(NpgsqlDataReader reader)
    {
        return ProcessingJob.Rehydrate(
            id: reader.GetGuid(0),
            workspaceId: reader.GetGuid(1),
            documentId: reader.GetGuid(2),
            status: (ProcessingStatus)reader.GetInt32(3),
            errorMessage: reader.IsDBNull(4) ? null : reader.GetString(4),
            createdAtUtc: reader.GetFieldValue<DateTimeOffset>(5),
            startedAtUtc: reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6),
            completedAtUtc: reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTimeOffset>(7));
    }
}
