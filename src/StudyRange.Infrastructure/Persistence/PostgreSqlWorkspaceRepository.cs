using Npgsql;
using StudyRange.Application.Contracts;
using StudyRange.Domain.Entities;
using StudyRange.Domain.ValueObjects;

namespace StudyRange.Infrastructure.Persistence;

public sealed class PostgreSqlWorkspaceRepository : IWorkspaceRepository
{
    private readonly PostgreSqlConnectionFactory _connectionFactory;

    public PostgreSqlWorkspaceRepository(PostgreSqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task AddAsync(Workspace workspace, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await InsertWorkspaceAsync(connection, transaction, workspace, cancellationToken);
        await InsertExamRangesAsync(connection, transaction, workspace.Id, workspace.ExamRanges, cancellationToken);
        await InsertDocumentsAsync(connection, transaction, workspace.Documents, cancellationToken);
        await InsertGeneratedContentsAsync(connection, transaction, workspace.GeneratedContents, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateAsync(Workspace workspace, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await UpdateWorkspaceAsync(connection, transaction, workspace, cancellationToken);
        await ReplaceExamRangesAsync(connection, transaction, workspace, cancellationToken);
        await ReplaceDocumentsAsync(connection, transaction, workspace, cancellationToken);
        await ReplaceGeneratedContentsAsync(connection, transaction, workspace, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<Workspace?> GetByIdAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string workspaceSql = """
                                    SELECT id, name, created_at_utc
                                    FROM workspaces
                                    WHERE id = @id
                                    """;
        await using var workspaceCommand = new NpgsqlCommand(workspaceSql, connection);
        workspaceCommand.Parameters.AddWithValue("id", workspaceId);

        await using var reader = await workspaceCommand.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var workspace = new Workspace(
            id: reader.GetGuid(0),
            name: reader.GetString(1),
            createdAtUtc: reader.GetFieldValue<DateTimeOffset>(2));
        await reader.CloseAsync();

        await LoadExamRangesAsync(connection, workspace, cancellationToken);
        await LoadDocumentsAsync(connection, workspace, cancellationToken);
        await LoadGeneratedContentsAsync(connection, workspace, cancellationToken);
        return workspace;
    }

    public async Task<IReadOnlyList<Workspace>> ListAsync(CancellationToken cancellationToken)
    {
        var result = new List<Workspace>();

        await using var connection = _connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        const string sql = """
                           SELECT id, name, created_at_utc
                           FROM workspaces
                           ORDER BY created_at_utc DESC
                           """;
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new Workspace(
                id: reader.GetGuid(0),
                name: reader.GetString(1),
                createdAtUtc: reader.GetFieldValue<DateTimeOffset>(2)));
        }
        await reader.CloseAsync();

        foreach (var workspace in result)
        {
            await LoadExamRangesAsync(connection, workspace, cancellationToken);
            await LoadDocumentsAsync(connection, workspace, cancellationToken);
            await LoadGeneratedContentsAsync(connection, workspace, cancellationToken);
        }

        return result;
    }

    private static async Task InsertWorkspaceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Workspace workspace,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           INSERT INTO workspaces (id, name, created_at_utc)
                           VALUES (@id, @name, @created_at_utc)
                           """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Transaction = transaction;
        command.Parameters.AddWithValue("id", workspace.Id);
        command.Parameters.AddWithValue("name", workspace.Name);
        command.Parameters.AddWithValue("created_at_utc", workspace.CreatedAtUtc);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpdateWorkspaceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Workspace workspace,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           UPDATE workspaces
                           SET name = @name
                           WHERE id = @id
                           """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Transaction = transaction;
        command.Parameters.AddWithValue("id", workspace.Id);
        command.Parameters.AddWithValue("name", workspace.Name);
        _ = await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ReplaceExamRangesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Workspace workspace,
        CancellationToken cancellationToken)
    {
        const string deleteSql = "DELETE FROM exam_ranges WHERE workspace_id = @workspace_id";
        await using (var deleteCommand = new NpgsqlCommand(deleteSql, connection))
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.Parameters.AddWithValue("workspace_id", workspace.Id);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertExamRangesAsync(connection, transaction, workspace.Id, workspace.ExamRanges, cancellationToken);
    }

    private static async Task InsertExamRangesAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid workspaceId,
        IReadOnlyList<ExamRange> ranges,
        CancellationToken cancellationToken)
    {
        const string insertSql = """
                                 INSERT INTO exam_ranges (id, workspace_id, subject, start_page, end_page, created_at_utc)
                                 VALUES (@id, @workspace_id, @subject, @start_page, @end_page, @created_at_utc)
                                 """;

        foreach (var range in ranges)
        {
            await using var command = new NpgsqlCommand(insertSql, connection);
            command.Transaction = transaction;
            command.Parameters.AddWithValue("id", range.Id);
            command.Parameters.AddWithValue("workspace_id", workspaceId);
            command.Parameters.AddWithValue("subject", range.Subject);
            command.Parameters.AddWithValue("start_page", range.Range.StartPage);
            command.Parameters.AddWithValue("end_page", range.Range.EndPage);
            command.Parameters.AddWithValue("created_at_utc", range.CreatedAtUtc);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task ReplaceDocumentsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Workspace workspace,
        CancellationToken cancellationToken)
    {
        const string deleteSql = "DELETE FROM document_assets WHERE workspace_id = @workspace_id";
        await using (var deleteCommand = new NpgsqlCommand(deleteSql, connection))
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.Parameters.AddWithValue("workspace_id", workspace.Id);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertDocumentsAsync(connection, transaction, workspace.Documents, cancellationToken);
    }

    private static async Task InsertDocumentsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<DocumentAsset> documents,
        CancellationToken cancellationToken)
    {
        const string insertSql = """
                                 INSERT INTO document_assets (
                                     id, workspace_id, document_type, original_file_name, stored_path, size_in_bytes,
                                     uploaded_at_utc, processing_status, processing_summary)
                                 VALUES (
                                     @id, @workspace_id, @document_type, @original_file_name, @stored_path, @size_in_bytes,
                                     @uploaded_at_utc, @processing_status, @processing_summary)
                                 """;

        foreach (var document in documents)
        {
            await using var command = new NpgsqlCommand(insertSql, connection);
            command.Transaction = transaction;
            command.Parameters.AddWithValue("id", document.Id);
            command.Parameters.AddWithValue("workspace_id", document.WorkspaceId);
            command.Parameters.AddWithValue("document_type", (int)document.DocumentType);
            command.Parameters.AddWithValue("original_file_name", document.OriginalFileName);
            command.Parameters.AddWithValue("stored_path", document.StoredPath);
            command.Parameters.AddWithValue("size_in_bytes", document.SizeInBytes);
            command.Parameters.AddWithValue("uploaded_at_utc", document.UploadedAtUtc);
            command.Parameters.AddWithValue("processing_status", (int)document.ProcessingStatus);
            command.Parameters.AddWithValue("processing_summary", (object?)document.ProcessingSummary ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task LoadExamRangesAsync(
        NpgsqlConnection connection,
        Workspace workspace,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, subject, start_page, end_page, created_at_utc
                           FROM exam_ranges
                           WHERE workspace_id = @workspace_id
                           ORDER BY created_at_utc DESC
                           """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workspace_id", workspace.Id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var examRange = new ExamRange(
                id: reader.GetGuid(0),
                subject: reader.GetString(1),
                range: new PageRange(reader.GetInt32(2), reader.GetInt32(3)),
                createdAtUtc: reader.GetFieldValue<DateTimeOffset>(4));
            workspace.AttachExamRange(examRange);
        }
        await reader.CloseAsync();
    }

    private static async Task LoadDocumentsAsync(
        NpgsqlConnection connection,
        Workspace workspace,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, workspace_id, document_type, original_file_name, stored_path, size_in_bytes, uploaded_at_utc,
                                  processing_status, processing_summary
                           FROM document_assets
                           WHERE workspace_id = @workspace_id
                           ORDER BY uploaded_at_utc DESC
                           """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workspace_id", workspace.Id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var document = DocumentAsset.Rehydrate(
                id: reader.GetGuid(0),
                workspaceId: reader.GetGuid(1),
                documentType: (DocumentType)reader.GetInt32(2),
                originalFileName: reader.GetString(3),
                storedPath: reader.GetString(4),
                sizeInBytes: reader.GetInt64(5),
                uploadedAtUtc: reader.GetFieldValue<DateTimeOffset>(6),
                processingStatus: (ProcessingStatus)reader.GetInt32(7),
                processingSummary: reader.IsDBNull(8) ? null : reader.GetString(8));
            workspace.AttachDocument(document);
        }
        await reader.CloseAsync();
    }

    private static async Task ReplaceGeneratedContentsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Workspace workspace,
        CancellationToken cancellationToken)
    {
        const string deleteSql = "DELETE FROM generated_contents WHERE workspace_id = @workspace_id";
        await using (var deleteCommand = new NpgsqlCommand(deleteSql, connection))
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.Parameters.AddWithValue("workspace_id", workspace.Id);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await InsertGeneratedContentsAsync(connection, transaction, workspace.GeneratedContents, cancellationToken);
    }

    private static async Task InsertGeneratedContentsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<GeneratedContentArtifact> contents,
        CancellationToken cancellationToken)
    {
        const string insertSql = """
                                 INSERT INTO generated_contents (
                                     id, workspace_id, exam_range_id, subject, start_page, end_page, content_type, content, provider, model, generated_at_utc)
                                 VALUES (
                                     @id, @workspace_id, @exam_range_id, @subject, @start_page, @end_page, @content_type, @content, @provider, @model, @generated_at_utc)
                                 """;

        foreach (var item in contents)
        {
            await using var command = new NpgsqlCommand(insertSql, connection);
            command.Transaction = transaction;
            command.Parameters.AddWithValue("id", item.Id);
            command.Parameters.AddWithValue("workspace_id", item.WorkspaceId);
            command.Parameters.AddWithValue("exam_range_id", item.ExamRangeId);
            command.Parameters.AddWithValue("subject", item.Subject);
            command.Parameters.AddWithValue("start_page", item.StartPage);
            command.Parameters.AddWithValue("end_page", item.EndPage);
            command.Parameters.AddWithValue("content_type", item.ContentType);
            command.Parameters.AddWithValue("content", item.Content);
            command.Parameters.AddWithValue("provider", item.Provider);
            command.Parameters.AddWithValue("model", item.Model);
            command.Parameters.AddWithValue("generated_at_utc", item.GeneratedAtUtc);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task LoadGeneratedContentsAsync(
        NpgsqlConnection connection,
        Workspace workspace,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id, workspace_id, exam_range_id, subject, start_page, end_page, content_type, content, provider, model, generated_at_utc
                           FROM generated_contents
                           WHERE workspace_id = @workspace_id
                           ORDER BY generated_at_utc DESC
                           """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workspace_id", workspace.Id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var item = new GeneratedContentArtifact(
                id: reader.GetGuid(0),
                workspaceId: reader.GetGuid(1),
                examRangeId: reader.GetGuid(2),
                subject: reader.GetString(3),
                startPage: reader.GetInt32(4),
                endPage: reader.GetInt32(5),
                contentType: reader.GetString(6),
                content: reader.GetString(7),
                provider: reader.GetString(8),
                model: reader.GetString(9),
                generatedAtUtc: reader.GetFieldValue<DateTimeOffset>(10));
            workspace.AttachGeneratedContent(item);
        }
        await reader.CloseAsync();
    }
}
