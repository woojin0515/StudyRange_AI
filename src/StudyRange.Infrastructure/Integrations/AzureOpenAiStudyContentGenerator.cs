using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using StudyRange.Application.Contracts;
using StudyRange.Application.UseCases;
using StudyRange.Domain.Entities;
using UglyToad.PdfPig;

namespace StudyRange.Infrastructure.Integrations;

public sealed class AzureOpenAiStudyContentGenerator : IStudyContentGenerator
{
    private const string ApiVersion = "2024-10-21";
    private readonly HttpClient _httpClient;
    private readonly LlmOptions _options;

    public AzureOpenAiStudyContentGenerator(HttpClient httpClient, IOptions<LlmOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<GeneratedStudyContentModel> GenerateAsync(
        Workspace workspace,
        ExamRange examRange,
        GeneratedContentType contentType,
        GeneratedContentContextModel context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("LLM API 키가 설정되지 않았습니다. Llm:ApiKey 또는 OPENAI_API_KEY/AZURE_OPENAI_API_KEY를 확인하세요.");
        }

        if (string.IsNullOrWhiteSpace(_options.Model))
        {
            throw new InvalidOperationException("LLM 모델이 설정되지 않았습니다. Llm:Model을 확인하세요.");
        }

        var prompt = BuildPrompt(workspace, examRange, contentType, context);
        var request = CreateRequestPayload(prompt);
        var (uri, useAzureApiKeyHeader) = ResolveTarget();
        var json = JsonSerializer.Serialize(request);

        using var message = new HttpRequestMessage(HttpMethod.Post, uri);
        if (useAzureApiKeyHeader)
        {
            message.Headers.Add("api-key", _options.ApiKey);
        }
        else
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }

        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        message.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"LLM 생성 실패: {response.StatusCode} {responseBody}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var messageElement = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message");
        var content = ExtractMessageContent(messageElement);

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

    private (string Uri, bool UseAzureApiKeyHeader) ResolveTarget()
    {
        var endpoint = string.IsNullOrWhiteSpace(_options.Endpoint)
            ? "https://api.openai.com/v1"
            : _options.Endpoint.TrimEnd('/');

        if (!string.IsNullOrWhiteSpace(_options.Endpoint) && !string.IsNullOrWhiteSpace(_options.Deployment))
        {
            return ($"{endpoint}/openai/deployments/{_options.Deployment}/chat/completions?api-version={ApiVersion}", true);
        }

        return ($"{endpoint}/chat/completions", false);
    }

    private object CreateRequestPayload(string prompt)
    {
        var messages = new object[]
        {
            new { role = "system", content = "당신은 한국어 학습 코치입니다. 학생이 바로 공부를 시작할 수 있도록 구조화해서 답변하세요." },
            new { role = "user", content = prompt }
        };

        if (!string.IsNullOrWhiteSpace(_options.Endpoint) && !string.IsNullOrWhiteSpace(_options.Deployment))
        {
            return new
            {
                messages,
                max_completion_tokens = 1200
            };
        }

        return new
        {
            model = _options.Model,
            messages,
            max_completion_tokens = 1200
        };
    }

    private static string? ExtractMessageContent(JsonElement messageElement)
    {
        if (!messageElement.TryGetProperty("content", out var contentElement))
        {
            return null;
        }

        if (contentElement.ValueKind == JsonValueKind.String)
        {
            return contentElement.GetString();
        }

        if (contentElement.ValueKind == JsonValueKind.Array)
        {
            var parts = contentElement
                .EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.Object)
                .Where(x => x.TryGetProperty("type", out var typeElement) &&
                            typeElement.ValueKind == JsonValueKind.String &&
                            typeElement.GetString() == "text")
                .Select(x => x.TryGetProperty("text", out var textElement) && textElement.ValueKind == JsonValueKind.String
                    ? textElement.GetString()
                    : null)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (parts.Count > 0)
            {
                return string.Join(Environment.NewLine, parts);
            }
        }

        return null;
    }

    private static string BuildPrompt(
        Workspace workspace,
        ExamRange examRange,
        GeneratedContentType contentType,
        GeneratedContentContextModel context)
    {
        var source = BuildSourceForExamRange(workspace, examRange);
        if (source.Snippets.Count == 0)
        {
            throw new InvalidOperationException(source.ErrorMessage);
        }

        var sourceText = string.Join(Environment.NewLine + Environment.NewLine, source.Snippets);
        var schoolName = string.IsNullOrWhiteSpace(context.SchoolName) ? "미입력" : context.SchoolName.Trim();
        var birthYear = context.BirthYear?.ToString() ?? "미입력";
        var curriculum = string.IsNullOrWhiteSpace(context.CurriculumRevision) ? "미입력" : context.CurriculumRevision.Trim();
        var publisher = string.IsNullOrWhiteSpace(context.TextbookPublisher) ? "미입력" : context.TextbookPublisher.Trim();
        var schoolLevel = MapSchoolLevel(context.SchoolLevel);

        return contentType switch
        {
            GeneratedContentType.Summary => $$"""
                다음 학습 범위를 한국어로 요약해줘. 반드시 아래 [근거 텍스트]만 사용하고, 근거 없는 일반론을 추가하지 마.
                - Workspace: {{workspace.Name}}
                - 과목: {{context.Subject}}
                - 학교급/학년: {{schoolLevel}} {{context.Grade}}학년
                - 출생년도: {{birthYear}}
                - 학교명: {{schoolName}}
                - 교과서 출판사: {{publisher}}
                - 교육과정 개정: {{curriculum}}
                - 시험 범위: {{examRange.Range.StartPage}}~{{examRange.Range.EndPage}}
                - 근거 문서: {{source.DocumentSummary}}

                [근거 텍스트]
                {{sourceText}}

                출력 형식:
                1) 핵심 개념 5개
                2) 빈출 포인트 3개
                3) 오늘 바로 공부 시작용 체크리스트 5개

                제약:
                - 각 항목마다 최소 1개 이상 페이지 근거를 `[p.페이지번호]` 형태로 붙여.
                - [근거 텍스트]에 없는 정보는 쓰지 마.
                """,
            GeneratedContentType.Quiz => $$"""
                다음 학습 범위를 기반으로 한국어 퀴즈를 만들어줘. 반드시 아래 [근거 텍스트]만 사용하고, 근거 없는 일반론을 추가하지 마.
                - Workspace: {{workspace.Name}}
                - 과목: {{context.Subject}}
                - 학교급/학년: {{schoolLevel}} {{context.Grade}}학년
                - 출생년도: {{birthYear}}
                - 학교명: {{schoolName}}
                - 교과서 출판사: {{publisher}}
                - 교육과정 개정: {{curriculum}}
                - 시험 범위: {{examRange.Range.StartPage}}~{{examRange.Range.EndPage}}
                - 근거 문서: {{source.DocumentSummary}}

                [근거 텍스트]
                {{sourceText}}

                출력 형식:
                - 객관식 5문항 (정답/해설 포함)
                - 단답형 3문항 (모범답안 포함)

                제약:
                - 모든 문항/정답/해설에 `[p.페이지번호]` 근거를 붙여.
                - [근거 텍스트]에 없는 정보는 출제하지 마.
                """,
            _ => throw new InvalidOperationException("지원하지 않는 생성 유형입니다.")
        };
    }

    private static TextbookSourceResult BuildSourceForExamRange(Workspace workspace, ExamRange examRange)
    {
        var textbookDocuments = workspace.Documents
            .Where(x => x.DocumentType == DocumentType.TextbookPdf && File.Exists(x.StoredPath))
            .ToList();
        if (textbookDocuments.Count == 0)
        {
            return TextbookSourceResult.Fail("교과서 PDF가 없습니다. 문서 유형을 '교과서'로 업로드한 뒤 다시 시도하세요.");
        }

        var snippets = new List<string>();
        var documentSummaries = new List<string>();
        const int maxSnippetChars = 14000;
        var currentChars = 0;

        foreach (var document in textbookDocuments)
        {
            using var pdf = PdfDocument.Open(document.StoredPath);
            var start = Math.Max(1, examRange.Range.StartPage);
            var end = Math.Min(pdf.NumberOfPages, examRange.Range.EndPage);
            documentSummaries.Add($"{document.OriginalFileName}({pdf.NumberOfPages}p)");

            if (start > end)
            {
                continue;
            }

            for (var page = start; page <= end; page++)
            {
                var pageText = NormalizeSourceText(pdf.GetPage(page).Text);
                if (string.IsNullOrWhiteSpace(pageText))
                {
                    continue;
                }

                var clipped = pageText.Length > 1200 ? pageText[..1200] : pageText;
                var chunk = $"[p.{page}] {clipped}";
                if (currentChars + chunk.Length > maxSnippetChars)
                {
                    return TextbookSourceResult.Success(snippets, string.Join(", ", documentSummaries));
                }

                snippets.Add(chunk);
                currentChars += chunk.Length;
            }
        }

        if (snippets.Count == 0)
        {
            return TextbookSourceResult.Fail("선택한 시험 범위 페이지에서 추출된 교과서 본문이 없습니다. 범위를 확인하거나 교과서 PDF를 다시 업로드하세요.");
        }

        return TextbookSourceResult.Success(snippets, string.Join(", ", documentSummaries));
    }

    private static string NormalizeSourceText(string raw)
    {
        return string.Join(' ',
            raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string MapSchoolLevel(SchoolLevel schoolLevel)
    {
        return schoolLevel switch
        {
            SchoolLevel.Elementary => "초등학교",
            SchoolLevel.Middle => "중학교",
            SchoolLevel.High => "고등학교",
            _ => "중학교"
        };
    }

    private sealed record TextbookSourceResult(
        IReadOnlyList<string> Snippets,
        string DocumentSummary,
        string ErrorMessage)
    {
        public static TextbookSourceResult Success(IReadOnlyList<string> snippets, string documentSummary)
        => new(snippets, documentSummary, string.Empty);

        public static TextbookSourceResult Fail(string errorMessage)
        => new([], string.Empty, errorMessage);
    }
}
