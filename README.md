# StudyRange AI

Blazor Server 기반 시험 범위 학습 코치 MVP입니다.

## 현재 구현 범위

- Workspace 생성/선택
- 시험 범위 등록
- 문서 업로드(파일 형식/시그니처/크기 검증)
- 비동기 문서 처리 큐/워커
  - PDF: 페이지 추정 + 텍스트 샘플 요약
  - 이미지: 처리 상태 및 기본 요약
- AI 요약/퀴즈 생성
  - OpenAI API 키만으로 기본 동작
  - Endpoint + Deployment 설정 시 Azure OpenAI 경로 사용
- 교육 메타데이터 통합 조회
  - NCIC(기본 개정 교육과정)
  - KOTRY(API 키 기반 교과서 조회, 미설정 시 Mock 데이터)
  - NEIS(학교 정보 조회)

## 기술 스택

- .NET 9 / ASP.NET Core Blazor Server
- MudBlazor
- PostgreSQL (선택) / InMemory (개발 기본)

## 실행 방법

1. `src/StudyRange.Web/appsettings.Development.json` 설정
2. 실행

```bash
dotnet run --project src/StudyRange.Web/StudyRange.Web.csproj
```

## 주요 설정

### LLM

```json
"Llm": {
  "Provider": "OpenAI",
  "Model": "gpt-5-mini",
  "Endpoint": "",
  "ApiKey": "",
  "Deployment": ""
}
```

- `ApiKey` 필수
- `Endpoint`, `Deployment`를 함께 넣으면 Azure OpenAI 방식으로 호출

### Persistence

```json
"Persistence": {
  "Provider": "InMemory",
  "PostgreSqlConnectionString": ""
}
```

- `Provider = PostgreSql`일 때 `PostgreSqlConnectionString` 필수

### Education APIs

```json
"EducationApis": {
  "Kotry": {
    "BaseUrl": "http://www.kotry.kr",
    "ApiKey": ""
  },
  "Ncic": {
    "ApiKey": ""
  },
  "Neis": {
    "BaseUrl": "https://open.neis.go.kr",
    "ApiKey": ""
  }
}
```