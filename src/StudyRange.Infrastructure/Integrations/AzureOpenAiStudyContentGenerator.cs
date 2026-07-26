using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using StudyRange.Application.Contracts;
using StudyRange.Application.UseCases;
using StudyRange.Domain.Entities;

namespace StudyRange.Infrastructure.Integrations;

public sealed class AzureOpenAiStudyContentGenerator : IStudyContentGenerator
{
    private const string ApiVersion = "2024-10-21";
    private readonly HttpClient _httpClient;
    private readonly LlmOptions _options;

    public AzureOpenAiStudyContentGenerator(HttpClient httpClient, LlmOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<GeneratedStudyContentModel> GenerateAsync(
        Workspace workspace,
        ExamRange examRange,
        GeneratedContentType contentType,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint) ||
            string.IsNullOrWhiteSpace(_options.ApiKey) ||
            string.IsNullOrWhiteSpace(_options.Deployment))
        {
            throw new InvalidOperationException("Azure OpenAI 설정이 누락되었습니다. Endpoint/ApiKey/Deployment를 확인하세요.");
        }

        var prompt = BuildPrompt(workspace, examRange, contentType);
        var request = new
        {
            messages = new object[]
            {
                new { role = "system", content = "당신은 한국어 학습 코치입니다. 학생이 바로 공부를 시작할 수 있도록 구조화해서 답변하세요." },
                new { role = "user", content = prompt }
            },
            temperature = 0.4,
            max_tokens = 1200
        };

        var endpoint = _options.Endpoint.TrimEnd('/');
        var uri = $"{endpoint}/openai/deployments/{_options.Deployment}/chat/completions?api-version={ApiVersion}";
        var json = JsonSerializer.Serialize(request);

        using var message = new HttpRequestMessage(HttpMethod.Post, uri);
        message.Headers.Add("api-key", _options.ApiKey);
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        message.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"LLM 생성 실패: {response.StatusCode} {responseBody}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("LLM 응답이 비어 있습니다.");
        }

        return new GeneratedStudyContentModel(
            contentType,
            content,
            Provider: _options.Provider,
            Model: _options.Model,
            GeneratedAtUtc: DateTimeOffset.UtcNow);
    }

    private static string BuildPrompt(Workspace workspace, ExamRange examRange, GeneratedContentType contentType)
    {
        var documents = workspace.Documents
            .Where(d => d.ProcessingStatus == ProcessingStatus.Completed)
            .Select(d => $"- {d.DocumentType}: {d.OriginalFileName} ({d.ProcessingSummary ?? "요약 없음"})")
            .ToList();

        var docs = documents.Count == 0
            ? "- 처리 완료된 문서 없음"
            : string.Join(Environment.NewLine, documents);

        return contentType switch
        {
            GeneratedContentType.Summary => $$"""
                다음 학습 범위를 한국어로 요약해줘.
                - Workspace: {{workspace.Name}}
                - 과목: {{examRange.Subject}}
                - 시험 범위: {{examRange.Range.StartPage}}~{{examRange.Range.EndPage}}
                - 문서 정보:
                {{docs}}

                출력 형식:
                1) 핵심 개념 5개
                2) 빈출 포인트 3개
                3) 오늘 바로 공부 시작용 체크리스트 5개
                """,
            GeneratedContentType.Quiz => $$"""
                다음 학습 범위를 기반으로 한국어 퀴즈를 만들어줘.
                - Workspace: {{workspace.Name}}
                - 과목: {{examRange.Subject}}
                - 시험 범위: {{examRange.Range.StartPage}}~{{examRange.Range.EndPage}}
                - 문서 정보:
                {{docs}}

                출력 형식:
                - 객관식 5문항 (정답/해설 포함)
                - 단답형 3문항 (모범답안 포함)
                """,
            _ => throw new InvalidOperationException("지원하지 않는 생성 유형입니다.")
        };
    }
}
