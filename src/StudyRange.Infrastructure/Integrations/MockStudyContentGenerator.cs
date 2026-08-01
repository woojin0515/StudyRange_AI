using Microsoft.Extensions.Options;
using StudyRange.Application.Contracts;
using StudyRange.Application.UseCases;
using StudyRange.Domain.Entities;

namespace StudyRange.Infrastructure.Integrations;

public sealed class MockStudyContentGenerator : IStudyContentGenerator
{
    private readonly LlmOptions _options;

    public MockStudyContentGenerator(IOptions<LlmOptions> options)
    {
        _options = options.Value;
    }

    public Task<GeneratedStudyContentModel> GenerateAsync(
        Workspace workspace,
        ExamRange examRange,
        GeneratedContentType contentType,
        CancellationToken cancellationToken)
    {
        var completedDocuments = workspace.Documents.Count(d => d.ProcessingStatus == ProcessingStatus.Completed);
        var content = contentType switch
        {
            GeneratedContentType.Summary => $$"""
                [Mock Summary]
                Workspace: {{workspace.Name}}
                Subject: {{examRange.Subject}}
                Range: {{examRange.Range.StartPage}}~{{examRange.Range.EndPage}}
                Completed documents: {{completedDocuments}}

                1. 핵심 개념을 3개로 나누어 정리하세요.
                2. 페이지 범위를 20~30분 단위로 분할해 복습하세요.
                3. 모르는 용어를 별도 노트에 기록하세요.
                """,
            GeneratedContentType.Quiz => $$"""
                [Mock Quiz]
                Subject: {{examRange.Subject}} ({{examRange.Range.StartPage}}~{{examRange.Range.EndPage}})
                
                Q1. 핵심 개념 A를 한 문장으로 설명하세요.
                Q2. 핵심 개념 B와 C의 차이를 비교하세요.
                Q3. 해당 범위의 실수하기 쉬운 포인트를 2가지 쓰세요.
                """,
            _ => throw new InvalidOperationException("지원하지 않는 생성 유형입니다.")
        };

        return Task.FromResult(new GeneratedStudyContentModel(
            contentType,
            content,
            Provider: _options.Provider,
            Model: _options.Model,
            GeneratedAtUtc: DateTimeOffset.UtcNow));
    }
}
